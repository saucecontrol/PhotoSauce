using System;
using System.IO;
using System.Linq;
using System.Diagnostics;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.ComTypes;

using PhotoSauce.MagicScaler.Interop;

namespace PhotoSauce.MagicScaler
{
	internal class WicFrame : IImageFrame
	{
		private readonly Lazy<IPixelSource> iSource;

		public IWICBitmapFrameDecode Frame { get; }
		public PixelSource Source { get; }

		public IPixelSource PixelSource => iSource.Value;

		public WicFrame(IWICBitmapFrameDecode frame)
		{
			Frame = frame;
			Source = frame.AsPixelSource(nameof(IWICBitmapFrameDecode), false);
			iSource = new Lazy<IPixelSource>(() => Source.AsIPixelSource());
		}
	}

	internal class WicDecoder : IImageContainer
	{
		private static readonly IDictionary<Guid, FileFormat> formatMap = new Dictionary<Guid, FileFormat> {
			[Consts.GUID_ContainerFormatBmp] = FileFormat.Bmp,
			[Consts.GUID_ContainerFormatGif] = FileFormat.Gif,
			[Consts.GUID_ContainerFormatJpeg] = FileFormat.Jpeg,
			[Consts.GUID_ContainerFormatPng] = FileFormat.Png,
			[Consts.GUID_ContainerFormatTiff] = FileFormat.Tiff
		};

		public IWICBitmapDecoder Decoder { get; }
		public Guid WicContainerFormat { get; }
		public FileFormat ContainerFormat { get; }
		public int FrameCount { get; }

		public bool IsRawContainer => WicContainerFormat == Consts.GUID_ContainerFormatRaw || WicContainerFormat == Consts.GUID_ContainerFormatRaw2 || WicContainerFormat == Consts.GUID_ContainerFormatAdng;

		public IImageFrame GetFrame(int index) => new WicFrame(Decoder.GetFrame((uint)index));

		private WicDecoder(IWICBitmapDecoder dec, WicPipelineContext ctx)
		{
			Decoder = ctx.AddRef(dec);

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

		public static WicDecoder Create(string fileName, WicPipelineContext ctx)
		{
			var dec = createDecoder(() => Wic.Factory.CreateDecoderFromFilename(fileName, null, GenericAccessRights.GENERIC_READ, WICDecodeOptions.WICDecodeMetadataCacheOnDemand));
			return new WicDecoder(dec, ctx);
		}

		public static WicDecoder Create(Stream inFile, WicPipelineContext ctx)
		{
			var stm = ctx.AddRef(Wic.Factory.CreateStream());
			stm.InitializeFromIStream(inFile.AsIStream());

			var dec = createDecoder(() => Wic.Factory.CreateDecoderFromStream(stm, null, WICDecodeOptions.WICDecodeMetadataCacheOnDemand));
			return new WicDecoder(dec, ctx);
		}

		unsafe public static WicDecoder Create(ReadOnlySpan<byte> inBuffer, WicPipelineContext ctx)
		{
			fixed (byte* pbBuffer = inBuffer)
			{
				var stm = ctx.AddRef(Wic.Factory.CreateStream());
				stm.InitializeFromMemory((IntPtr)pbBuffer, (uint)inBuffer.Length);

				var dec = createDecoder(() => Wic.Factory.CreateDecoderFromStream(stm, null, WICDecodeOptions.WICDecodeMetadataCacheOnDemand));
				return new WicDecoder(dec, ctx);
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

			var props = new Dictionary<string, object>();
			var bag = default(IPropertyBag2);
			Encoder.CreateNewFrame(out var frame, ref bag);
			ctx.WicContext.AddRef(frame);
			ctx.WicContext.AddRef(bag);

			if (fmt == FileFormat.Jpeg)
				props.Add("ImageQuality", ctx.Settings.JpegQuality / 100f);

			if (fmt == FileFormat.Jpeg && ctx.Settings.JpegSubsampleMode != ChromaSubsampleMode.Default)
				props.Add("JpegYCrCbSubsampling", (byte)ctx.Settings.JpegSubsampleMode);

			if (fmt == FileFormat.Tiff)
				props.Add("TiffCompressionMethod", (byte)WICTiffCompressionOption.WICTiffCompressionNone);

			if (fmt == FileFormat.Bmp && ctx.Source.Format.AlphaRepresentation != PixelAlphaRepresentation.None)
				props.Add("EnableV5Header32bppBGRA", true);

			if (props.Count > 0)
				bag.Write((uint)props.Count, props.Keys.Select(k => new PROPBAG2 { pstrName = k }).ToArray(), props.Values.ToArray());

			frame.Initialize(bag);
			frame.SetSize(ctx.Source.Width, ctx.Source.Height);
			frame.SetResolution(ctx.Settings.DpiX > 0d ? ctx.Settings.DpiX : ctx.DecoderFrame.DpiX, ctx.Settings.DpiY > 0d ? ctx.Settings.DpiY : ctx.DecoderFrame.DpiY);

			if (ctx.DecoderFrame.Metadata?.Count > 0 && frame.TryGetMetadataQueryWriter(out var metawriter))
			{
				ctx.WicContext.AddRef(metawriter);
				foreach (var nv in ctx.DecoderFrame.Metadata)
					metawriter.TrySetMetadataByName(nv.Key, nv.Value);
			}

			if (!(ctx.WicContext.DestColorContext is null) && (ctx.Settings.ColorProfileMode == ColorProfileMode.NormalizeAndEmbed || ctx.Settings.ColorProfileMode == ColorProfileMode.Preserve))
			{
				var cc = ctx.WicContext.DestColorContext;
				if (ctx.DestColorProfile == ColorProfile.sRGB)
					cc = Wic.SrgbCompactContext.Value;
				else if (ctx.DestColorProfile == ColorProfile.sGrey)
					cc = Wic.GreyCompactContext.Value;

				frame.TrySetColorContexts(cc);
			}

			Frame = frame;
		}

		public void WriteSource(PipelineContext ctx)
		{
			if (ctx.PlanarLumaSource != null && ctx.PlanarChromaSource != null)
			{
				var oformat = Consts.GUID_WICPixelFormat24bppBGR;
				Frame.SetPixelFormat(ref oformat);

				var penc = (IWICPlanarBitmapFrameEncode)Frame;
				penc.WriteSource(new[] { ctx.PlanarLumaSource.WicSource, ctx.PlanarChromaSource.WicSource }, 2, null);
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

				Frame.WriteSource(ctx.Source.WicSource, null);
			}

			Frame.Commit();
			Encoder.Commit();
		}
	}
}