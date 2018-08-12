using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.ComTypes;

using PhotoSauce.MagicScaler.Interop;

namespace PhotoSauce.MagicScaler
{
	internal class WicDecoder
	{
		private static readonly IDictionary<Guid, FileFormat> formatMap = new Dictionary<Guid, FileFormat> {
			[Consts.GUID_ContainerFormatBmp] = FileFormat.Bmp,
			[Consts.GUID_ContainerFormatGif] = FileFormat.Gif,
			[Consts.GUID_ContainerFormatJpeg] = FileFormat.Jpeg,
			[Consts.GUID_ContainerFormatPng] = FileFormat.Png,
			[Consts.GUID_ContainerFormatTiff] = FileFormat.Tiff
		};

		public IWICBitmapDecoder Decoder { get; private set; }
		public Guid WicContainerFormat { get; private set; }
		public FileFormat ContainerFormat { get; private set; }
		public uint FrameCount { get; private set; }

		private void init(IWICBitmapDecoder dec, WicProcessingContext ctx)
		{
			ctx.Decoder = this;

			if (dec == null)
				return;

			Decoder = ctx.AddRef(dec);

			WicContainerFormat = dec.GetContainerFormat();
			ContainerFormat = formatMap.GetValueOrDefault(WicContainerFormat, () => FileFormat.Unknown);
			FrameCount = dec.GetFrameCount();
		}

		private IWICBitmapDecoder checkDecoder(Func<IWICBitmapDecoder> factory)
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

		public WicDecoder(string fileName, WicProcessingContext ctx)
		{
			init(checkDecoder(() => Wic.Factory.CreateDecoderFromFilename(fileName, null, GenericAccessRights.GENERIC_READ, WICDecodeOptions.WICDecodeMetadataCacheOnDemand)), ctx);
		}

		public WicDecoder(Stream inFile, WicProcessingContext ctx)
		{
			var stm = ctx.AddRef(Wic.Factory.CreateStream());
			stm.InitializeFromIStream(inFile.AsIStream());
			init(checkDecoder(() => Wic.Factory.CreateDecoderFromStream(stm, null, WICDecodeOptions.WICDecodeMetadataCacheOnDemand)), ctx);
		}

		unsafe public WicDecoder(ReadOnlySpan<byte> inBuffer, WicProcessingContext ctx)
		{
			fixed (byte* pbBuffer = &MemoryMarshal.GetReference(inBuffer))
			{
				var stm = ctx.AddRef(Wic.Factory.CreateStream());
				stm.InitializeFromMemory((IntPtr)pbBuffer, (uint)inBuffer.Length);
				init(checkDecoder(() => Wic.Factory.CreateDecoderFromStream(stm, null, WICDecodeOptions.WICDecodeMetadataCacheOnDemand)), ctx);
			}
		}

		public WicDecoder(IPixelSource imgSource, WicProcessingContext ctx)
		{
			init(null, ctx);
			ContainerFormat = FileFormat.Unknown;
			FrameCount = 1;
			ctx.Source = imgSource.AsPixelSource();
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

		public WicEncoder(WicProcessingContext ctx, IStream stm)
		{
			Encoder = ctx.AddRef(Wic.Factory.CreateEncoder(formatMap.GetValueOrDefault(ctx.Settings.SaveFormat, () => Consts.GUID_ContainerFormatPng), null));
			Encoder.Initialize(stm, WICBitmapEncoderCacheOption.WICBitmapEncoderNoCache);

			var props = new Dictionary<string, object>();
			var bag = default(IPropertyBag2);
			Encoder.CreateNewFrame(out var frame, ref bag);
			ctx.AddRef(frame);
			ctx.AddRef(bag);

			if (ctx.Settings.SaveFormat == FileFormat.Jpeg)
				props.Add("ImageQuality", ctx.Settings.JpegQuality / 100f);

			if (ctx.Settings.SaveFormat == FileFormat.Jpeg && ctx.Settings.JpegSubsampleMode != ChromaSubsampleMode.Default)
				props.Add("JpegYCrCbSubsampling", (byte)ctx.Settings.JpegSubsampleMode);

			if (ctx.Settings.SaveFormat == FileFormat.Tiff)
				props.Add("TiffCompressionMethod", (byte)WICTiffCompressionOption.WICTiffCompressionNone);

			if (ctx.Settings.SaveFormat == FileFormat.Bmp && ctx.Source.Format.AlphaRepresentation != PixelAlphaRepresentation.None)
				props.Add("EnableV5Header32bppBGRA", true);

			if (props.Count > 0)
				bag.Write((uint)props.Count, props.Keys.Select(k => new PROPBAG2 { pstrName = k }).ToArray(), props.Values.ToArray());

			frame.Initialize(bag);
			frame.SetSize(ctx.Source.Width, ctx.Source.Height);
			frame.SetResolution(ctx.Settings.DpiX > 0d ? ctx.Settings.DpiX : ctx.DecoderFrame.DpiX, ctx.Settings.DpiY > 0d ? ctx.Settings.DpiY : ctx.DecoderFrame.DpiY);

			if (ctx.DecoderFrame.Metadata?.Count > 0 && frame.TryGetMetadataQueryWriter(out var metawriter))
			{
				ctx.AddRef(metawriter);
				foreach (var nv in ctx.DecoderFrame.Metadata)
					metawriter.TrySetMetadataByName(nv.Key, nv.Value);
			}

			if (!(ctx.DestColorContext is null) && (ctx.Settings.ColorProfileMode == ColorProfileMode.NormalizeAndEmbed || ctx.Settings.ColorProfileMode == ColorProfileMode.Preserve))
			{
				var cc = ctx.DestColorContext;
				if (ctx.DestColorProfile == ColorProfile.sRGB)
					cc = Wic.SrgbCompactContext.Value;
				else if (ctx.DestColorProfile == ColorProfile.sGrey)
					cc = Wic.GreyCompactContext.Value;

				frame.TrySetColorContexts(cc);
			}

			Frame = frame;
		}

		public void WriteSource(WicProcessingContext ctx)
		{
			if (ctx.PlanarLumaSource != null)
			{
				var oformat = Consts.GUID_WICPixelFormat24bppBGR;
				Frame.SetPixelFormat(ref oformat);

				var penc = Frame as IWICPlanarBitmapFrameEncode;
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
					if (PixelFormat.Cache[oformat].NumericRepresentation == PixelNumericRepresentation.Indexed)
					{
						pal = ctx.AddRef(Wic.Factory.CreatePalette());
						pal.InitializePredefined(WICBitmapPaletteType.WICBitmapPaletteTypeFixedGray256, false);
						ptt = WICBitmapPaletteType.WICBitmapPaletteTypeFixedGray256;

						Frame.SetPalette(pal);
					}

					var conv = ctx.AddRef(Wic.Factory.CreateFormatConverter());
					conv.Initialize(ctx.Source.WicSource, oformat, WICBitmapDitherType.WICBitmapDitherTypeNone, pal, 0.0, ptt);
					ctx.Source = conv.AsPixelSource($"{nameof(IWICFormatConverter)}: {ctx.Source.Format.Name}->{PixelFormat.Cache[oformat].Name}", false);
				}
				else if (oformat == Consts.GUID_WICPixelFormat8bppIndexed)
				{
					Frame.SetPalette(ctx.DestPalette);
				}

				Frame.WriteSource(ctx.Source.WicSource, null);
			}

			Frame.Commit();
			Encoder.Commit();
		}
	}
}