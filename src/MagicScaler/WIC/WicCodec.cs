using System;
using System.IO;
using System.Linq;
using System.Diagnostics;
using System.Collections.Generic;
using System.Runtime.InteropServices;

using PhotoSauce.Interop.Wic;

namespace PhotoSauce.MagicScaler
{
	internal static class WicImageDecoder
	{
		public static readonly IReadOnlyDictionary<Guid, FileFormat> FormatMap = new Dictionary<Guid, FileFormat> {
			[Consts.GUID_ContainerFormatBmp] = FileFormat.Bmp,
			[Consts.GUID_ContainerFormatGif] = FileFormat.Gif,
			[Consts.GUID_ContainerFormatJpeg] = FileFormat.Jpeg,
			[Consts.GUID_ContainerFormatPng] = FileFormat.Png,
			[Consts.GUID_ContainerFormatTiff] = FileFormat.Tiff
		};

		private static IWICBitmapDecoder createDecoder<T>(Func<T, IWICBitmapDecoder> factory, T arg)
		{
			try
			{
				return factory(arg);
			}
			catch (COMException ex) when (ex.HResult == (int)WinCodecError.WINCODEC_ERR_COMPONENTNOTFOUND)
			{
				throw new InvalidDataException("Image format not supported.  Please ensure the input file is an image and that a WIC codec capable of reading the image is installed.", ex);
			}
		}

		public static WicImageContainer Load(string fileName, PipelineContext ctx)
		{
			var dec = createDecoder(fn => Wic.Factory.CreateDecoderFromFilename(fn, null, GenericAccessRights.GENERIC_READ, WICDecodeOptions.WICDecodeMetadataCacheOnDemand), fileName);
			return WicImageContainer.Create(dec, ctx);
		}

		public static WicImageContainer Load(Stream inStream, PipelineContext ctx)
		{
			var dec = createDecoder(stm => Wic.Factory.CreateDecoderFromStream(stm, null, WICDecodeOptions.WICDecodeMetadataCacheOnDemand), inStream.AsIStream());
			return WicImageContainer.Create(dec, ctx);
		}

		unsafe public static WicImageContainer Load(byte* pbBuffer, int cbBuffer, PipelineContext ctx, bool ownCopy = false)
		{
			var istm = ctx.WicContext.AddRef(Wic.Factory.CreateStream());
			var ptr = (IntPtr)pbBuffer;

			if (ownCopy)
			{
				ptr = ctx.WicContext.AddUnmanagedMemory(cbBuffer).DangerousGetHandle();
				Buffer.MemoryCopy(pbBuffer, ptr.ToPointer(), cbBuffer, cbBuffer);
			}

			istm.InitializeFromMemory(ptr, (uint)cbBuffer);

			var dec = createDecoder(stm => Wic.Factory.CreateDecoderFromStream(stm, null, WICDecodeOptions.WICDecodeMetadataCacheOnDemand), istm);
			return WicImageContainer.Create(dec, ctx);
		}
	}

	internal class WicImageEncoder
	{
		private static readonly IReadOnlyDictionary<FileFormat, Guid> formatMap = new Dictionary<FileFormat, Guid> {
			[FileFormat.Bmp] = Consts.GUID_ContainerFormatBmp,
			[FileFormat.Gif] = Consts.GUID_ContainerFormatGif,
			[FileFormat.Jpeg] = Consts.GUID_ContainerFormatJpeg,
			[FileFormat.Png] = Consts.GUID_ContainerFormatPng,
			[FileFormat.Png8] = Consts.GUID_ContainerFormatPng,
			[FileFormat.Tiff] = Consts.GUID_ContainerFormatTiff
		};

		public IWICBitmapEncoder WicEncoder { get; private set; }
		public IWICBitmapFrameEncode WicEncoderFrame { get; private set; }

		public WicImageEncoder(PipelineContext ctx, IStream stm)
		{
			var fmt = ctx.Settings.SaveFormat;
			WicEncoder = ctx.WicContext.AddRef(Wic.Factory.CreateEncoder(formatMap.GetValueOrDefault(fmt, Consts.GUID_ContainerFormatPng), null));
			WicEncoder.Initialize(stm, WICBitmapEncoderCacheOption.WICBitmapEncoderNoCache);

			var bag = default(IPropertyBag2);
			WicEncoder.CreateNewFrame(out var frame, ref bag);
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
					string orientationPath = ctx.Settings.SaveFormat == FileFormat.Jpeg ? Wic.Metadata.OrientationJpeg : Wic.Metadata.OrientationExif;
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

			WicEncoderFrame = frame;
		}

		public void WriteSource(PipelineContext ctx)
		{
			if (ctx.PlanarContext != null)
			{
				var oformat = Consts.GUID_WICPixelFormat24bppBGR;
				WicEncoderFrame.SetPixelFormat(ref oformat);

				var planes = new[] { ctx.PlanarContext.SourceY.AsIWICBitmapSource(), ctx.PlanarContext.SourceCb.AsIWICBitmapSource(), ctx.PlanarContext.SourceCr.AsIWICBitmapSource() };
				((IWICPlanarBitmapFrameEncode)WicEncoderFrame).WriteSource(planes, (uint)planes.Length, WICRect.Null);
			}
			else
			{
				var oformat = ctx.Source.Format.FormatGuid;
				WicEncoderFrame.SetPixelFormat(ref oformat);
				if (oformat != ctx.Source.Format.FormatGuid)
				{
					var pal = default(IWICPalette);
					var ptt = WICBitmapPaletteType.WICBitmapPaletteTypeCustom;
					if (PixelFormat.FromGuid(oformat).NumericRepresentation == PixelNumericRepresentation.Indexed)
					{
						pal = ctx.WicContext.AddRef(Wic.Factory.CreatePalette());
						pal.InitializePredefined(WICBitmapPaletteType.WICBitmapPaletteTypeFixedGray256, false);
						ptt = WICBitmapPaletteType.WICBitmapPaletteTypeFixedGray256;

						WicEncoderFrame.SetPalette(pal);
					}

					var conv = ctx.WicContext.AddRef(Wic.Factory.CreateFormatConverter());
					conv.Initialize(ctx.Source.AsIWICBitmapSource(), oformat, WICBitmapDitherType.WICBitmapDitherTypeNone, pal, 0.0, ptt);
					ctx.Source = conv.AsPixelSource($"{nameof(IWICFormatConverter)}: {ctx.Source.Format.Name}->{PixelFormat.FromGuid(oformat).Name}", false);
				}
				else if (oformat == PixelFormat.Indexed8Bpp.FormatGuid)
				{
					Debug.Assert(ctx.WicContext.DestPalette != null);
					WicEncoderFrame.SetPalette(ctx.WicContext.DestPalette);
				}

				WicEncoderFrame.WriteSource(ctx.Source.AsIWICBitmapSource(), WICRect.Null);
			}

			WicEncoderFrame.Commit();
			WicEncoder.Commit();
		}
	}

	internal class WicColorProfile
	{
		public static readonly Lazy<WicColorProfile> Cmyk = new Lazy<WicColorProfile>(() => new WicColorProfile(getDefaultColorContext(PixelFormat.Cmyk32Bpp.FormatGuid), null!));
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
			using var pfi = ComHandle.QueryInterface<IWICPixelFormatInfo>(Wic.Factory.CreateComponentInfo(pixelFormat));
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