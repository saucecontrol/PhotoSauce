using System;
using System.IO;
using System.Linq;
using System.Diagnostics;
using System.Collections.Generic;
using System.Runtime.InteropServices;

using PhotoSauce.Interop.Wic;

namespace PhotoSauce.MagicScaler
{
	internal class WicImageContainer : IImageContainer
	{
		private static readonly IDictionary<Guid, FileFormat> formatMap = new Dictionary<Guid, FileFormat> {
			[Consts.GUID_ContainerFormatBmp] = FileFormat.Bmp,
			[Consts.GUID_ContainerFormatGif] = FileFormat.Gif,
			[Consts.GUID_ContainerFormatJpeg] = FileFormat.Jpeg,
			[Consts.GUID_ContainerFormatPng] = FileFormat.Png,
			[Consts.GUID_ContainerFormatTiff] = FileFormat.Tiff
		};

		private readonly WicPipelineContext wicContext;

		public IWICBitmapDecoder WicDecoder { get; }
		public Guid WicContainerFormat { get; }
		public FileFormat ContainerFormat { get; }
		public int FrameCount { get; }

		public bool IsRawContainer => WicContainerFormat == Consts.GUID_ContainerFormatRaw || WicContainerFormat == Consts.GUID_ContainerFormatRaw2 || WicContainerFormat == Consts.GUID_ContainerFormatAdng;

		public IImageFrame GetFrame(int index) => new WicImageFrame(this, (uint)index);

		private WicImageContainer(IWICBitmapDecoder dec, WicPipelineContext ctx)
		{
			wicContext = ctx;
			WicDecoder = ctx.AddRef(dec);

			WicContainerFormat = dec.GetContainerFormat();
			ContainerFormat = formatMap.GetValueOrDefault(WicContainerFormat, FileFormat.Unknown);
			FrameCount = (int)dec.GetFrameCount();
		}

		private static IWICBitmapDecoder createDecoder(Func<IWICBitmapDecoder> factory)
		{
			try
			{
				return factory();
			}
			catch (COMException ex) when (ex.HResult == (int)WinCodecError.WINCODEC_ERR_COMPONENTNOTFOUND)
			{
				throw new InvalidDataException("Image format not supported.  Please ensure the input file is an image and that a WIC codec capable of reading the image is installed.", ex);
			}
		}

		public static WicImageContainer Create(string fileName, WicPipelineContext ctx)
		{
			var dec = createDecoder(() => Wic.Factory.CreateDecoderFromFilename(fileName, null, GenericAccessRights.GENERIC_READ, WICDecodeOptions.WICDecodeMetadataCacheOnDemand));
			return new WicImageContainer(dec, ctx);
		}

		public static WicImageContainer Create(Stream inFile, WicPipelineContext ctx)
		{
			var stm = ctx.AddRef(Wic.Factory.CreateStream());
			stm.InitializeFromIStream(inFile.AsIStream());

			var dec = createDecoder(() => Wic.Factory.CreateDecoderFromStream(stm, null, WICDecodeOptions.WICDecodeMetadataCacheOnDemand));
			return new WicImageContainer(dec, ctx);
		}

		unsafe public static WicImageContainer Create(ReadOnlySpan<byte> inBuffer, WicPipelineContext ctx)
		{
			fixed (byte* pbBuffer = inBuffer)
			{
				var stm = ctx.AddRef(Wic.Factory.CreateStream());
				stm.InitializeFromMemory((IntPtr)pbBuffer, (uint)inBuffer.Length);

				var dec = createDecoder(() => Wic.Factory.CreateDecoderFromStream(stm, null, WICDecodeOptions.WICDecodeMetadataCacheOnDemand));
				return new WicImageContainer(dec, ctx);
			}
		}
	}

	internal class WicEncoder
	{
		private static readonly IDictionary<FileFormat, Guid> formatMap = new Dictionary<FileFormat, Guid> {
			[FileFormat.Bmp] = Consts.GUID_ContainerFormatBmp,
			[FileFormat.Gif] = Consts.GUID_ContainerFormatGif,
			[FileFormat.Jpeg] = Consts.GUID_ContainerFormatJpeg,
			[FileFormat.Png] = Consts.GUID_ContainerFormatPng,
			[FileFormat.Png8] = Consts.GUID_ContainerFormatPng,
			[FileFormat.Tiff] = Consts.GUID_ContainerFormatTiff
		};

		public IWICBitmapEncoder Encoder { get; private set; }
		public IWICBitmapFrameEncode Frame { get; private set; }

		public WicEncoder(PipelineContext ctx, IStream stm)
		{
			var fmt = ctx.Settings.SaveFormat;
			Encoder = ctx.WicContext.AddRef(Wic.Factory.CreateEncoder(formatMap.GetValueOrDefault(fmt, Consts.GUID_ContainerFormatPng), null));
			Encoder.Initialize(stm, WICBitmapEncoderCacheOption.WICBitmapEncoderNoCache);

			var bag = default(IPropertyBag2);
			Encoder.CreateNewFrame(out var frame, ref bag);
			ctx.WicContext.AddRef(frame);
			ctx.WicContext.AddRef(bag);

			if (fmt == FileFormat.Jpeg)
				bag.Write("ImageQuality", ctx.Settings.JpegQuality / 100f);

			if (fmt == FileFormat.Jpeg && ctx.Settings.JpegSubsampleMode != ChromaSubsampleMode.Default)
				bag.Write("JpegYCrCbSubsampling", (byte)ctx.Settings.JpegSubsampleMode);

			if (fmt == FileFormat.Tiff)
				bag.Write("TiffCompressionMethod", (byte)WICTiffCompressionOption.WICTiffCompressionNone);

			if (fmt == FileFormat.Bmp && ctx.Source.Format.AlphaRepresentation != PixelAlphaRepresentation.None)
				bag.Write("EnableV5Header32bppBGRA", true);

			frame.Initialize(bag);
			frame.SetSize((uint)ctx.Source.Width, (uint)ctx.Source.Height);
			frame.SetResolution(ctx.Settings.DpiX > 0d ? ctx.Settings.DpiX : ctx.ImageFrame.DpiX, ctx.Settings.DpiY > 0d ? ctx.Settings.DpiY : ctx.ImageFrame.DpiY);

			if (frame.TryGetMetadataQueryWriter(out var metawriter))
			{
				ctx.WicContext.AddRef(metawriter);
				if (ctx.ImageFrame is WicImageFrame wicFrame && wicFrame.WicMetadataReader != null && ctx.Settings.MetadataNames != Enumerable.Empty<string>())
				{
					foreach (string prop in ctx.Settings.MetadataNames)
					{
						if (wicFrame.WicMetadataReader.TryGetMetadataByName(prop, out var pvar) && !(pvar.Value is null))
							metawriter.TrySetMetadataByName(prop, pvar);
					}
				}

				if (ctx.Settings.OrientationMode == OrientationMode.Preserve && ctx.ImageFrame.ExifOrientation != Orientation.Normal)
				{
					string orientationPath = ctx.Settings.SaveFormat == FileFormat.Jpeg ? Wic.Metadata.OrientationJpegPath : Wic.Metadata.OrientationExifPath;
					metawriter.TrySetMetadataByName(orientationPath, new PropVariant((ushort)ctx.ImageFrame.ExifOrientation));
				}
			}

			if (ctx.WicContext.DestColorContext != null && (ctx.Settings.ColorProfileMode == ColorProfileMode.NormalizeAndEmbed || ctx.Settings.ColorProfileMode == ColorProfileMode.Preserve))
			{
				var cc = ctx.WicContext.DestColorContext;
				if (ctx.DestColorProfile == ColorProfile.sRGB)
					cc = WicColorProfile.SrgbCompact.Value.WicColorContext;
				else if (ctx.DestColorProfile == ColorProfile.sGrey)
					cc = WicColorProfile.GreyCompact.Value.WicColorContext;

				frame.TrySetColorContexts(cc);
			}

			Frame = frame;
		}

		public void WriteSource(PipelineContext ctx)
		{
			if (ctx.PlanarContext != null)
			{
				var oformat = Consts.GUID_WICPixelFormat24bppBGR;
				Frame.SetPixelFormat(ref oformat);

				var planes = new[] { ctx.PlanarContext.SourceY.WicSource, ctx.PlanarContext.SourceCb.WicSource, ctx.PlanarContext.SourceCr.WicSource };
				((IWICPlanarBitmapFrameEncode)Frame).WriteSource(planes, (uint)planes.Length, WICRect.Null);
			}
			else
			{
				var oformat = ctx.Source.Format.FormatGuid;
				Frame.SetPixelFormat(ref oformat);
				if (oformat != ctx.Source.Format.FormatGuid)
				{
					var pal = default(IWICPalette);
					var ptt = WICBitmapPaletteType.WICBitmapPaletteTypeCustom;
					if (PixelFormat.FromGuid(oformat).NumericRepresentation == PixelNumericRepresentation.Indexed)
					{
						pal = ctx.WicContext.AddRef(Wic.Factory.CreatePalette());
						pal.InitializePredefined(WICBitmapPaletteType.WICBitmapPaletteTypeFixedGray256, false);
						ptt = WICBitmapPaletteType.WICBitmapPaletteTypeFixedGray256;

						Frame.SetPalette(pal);
					}

					var conv = ctx.WicContext.AddRef(Wic.Factory.CreateFormatConverter());
					conv.Initialize(ctx.Source.WicSource, oformat, WICBitmapDitherType.WICBitmapDitherTypeNone, pal, 0.0, ptt);
					ctx.Source = conv.AsPixelSource($"{nameof(IWICFormatConverter)}: {ctx.Source.Format.Name}->{PixelFormat.FromGuid(oformat).Name}", false);
				}
				else if (oformat == Consts.GUID_WICPixelFormat8bppIndexed)
				{
					Debug.Assert(ctx.WicContext.DestPalette != null);
					Frame.SetPalette(ctx.WicContext.DestPalette);
				}

				Frame.WriteSource(ctx.Source.WicSource, WICRect.Null);
			}

			Frame.Commit();
			Encoder.Commit();
		}
	}

	internal class WicColorProfile
	{
		public static readonly Lazy<WicColorProfile> Cmyk = new Lazy<WicColorProfile>(() => new WicColorProfile(getDefaultColorContext(Consts.GUID_WICPixelFormat32bppCMYK), null!));
		public static readonly Lazy<WicColorProfile> Srgb = new Lazy<WicColorProfile>(() => new WicColorProfile(CreateContextFromProfile(IccProfiles.sRgbV4.Value), ColorProfile.sRGB));
		public static readonly Lazy<WicColorProfile> Grey = new Lazy<WicColorProfile>(() => new WicColorProfile(CreateContextFromProfile(IccProfiles.sGreyV4.Value), ColorProfile.sGrey));
		public static readonly Lazy<WicColorProfile> SrgbCompact = new Lazy<WicColorProfile>(() => new WicColorProfile(CreateContextFromProfile(IccProfiles.sRgbCompact.Value), ColorProfile.sRGB));
		public static readonly Lazy<WicColorProfile> GreyCompact = new Lazy<WicColorProfile>(() => new WicColorProfile(CreateContextFromProfile(IccProfiles.sGreyCompact.Value), ColorProfile.sGrey));
		public static readonly Lazy<WicColorProfile> AdobeRgb = new Lazy<WicColorProfile>(() => new WicColorProfile(CreateContextFromProfile(IccProfiles.AdobeRgb.Value), ColorProfile.AdobeRgb));

		public static WicColorProfile GetDefaultFor(PixelFormat fmt) => fmt.ColorRepresentation switch {
			PixelColorRepresentation.Cmyk => Cmyk.Value,
			PixelColorRepresentation.Grey => Grey.Value,
			_ => Srgb.Value
		};

		public static IWICColorContext CreateContextFromProfile(byte[] profile)
		{
			var cc = Wic.Factory.CreateColorContext();
			cc.InitializeFromMemory(profile, (uint)profile.Length);
			return cc;
		}

		private static IWICColorContext getDefaultColorContext(Guid pixelFormat)
		{
			using var pfi = new ComHandle<IWICPixelFormatInfo>(Wic.Factory.CreateComponentInfo(pixelFormat));
			return pfi.ComObject.GetColorContext();
		}

		public IWICColorContext WicColorContext { get; }
		public ColorProfile ParsedProfile { get; }

		public WicColorProfile(IWICColorContext ctx, ColorProfile prof)
		{
			WicColorContext = ctx;
			ParsedProfile = prof;
		}
	}
}