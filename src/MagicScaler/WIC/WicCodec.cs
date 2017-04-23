using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.ComTypes;

using PhotoSauce.MagicScaler.Interop;

namespace PhotoSauce.MagicScaler
{
	internal class WicDecoder : WicBase
	{
		public IWICBitmapDecoder Decoder { get; private set; }

		private void init(IWICBitmapDecoder dec, WicProcessingContext ctx)
		{
			Decoder = AddRef(dec);

			ctx.ContainerFormat = dec.GetContainerFormat();
			ctx.ContainerFrameCount = dec.GetFrameCount();
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
			init(checkDecoder(() => Wic.CreateDecoderFromFilename(fileName, null, GenericAccessRights.GENERIC_READ, WICDecodeOptions.WICDecodeMetadataCacheOnDemand)), ctx);
		}

		public WicDecoder(Stream inFile, WicProcessingContext ctx)
		{
			var stm = AddRef(Wic.CreateStream());
			stm.InitializeFromIStream(inFile.AsIStream());
			init(checkDecoder(() => Wic.CreateDecoderFromStream(stm, null, WICDecodeOptions.WICDecodeMetadataCacheOnDemand)), ctx);
		}

		public WicDecoder(byte[] inBuffer, WicProcessingContext ctx)
		{
			var stm = AddRef(Wic.CreateStream());
			stm.InitializeFromMemory(inBuffer, (uint)inBuffer.Length);
			init(checkDecoder(() => Wic.CreateDecoderFromStream(stm, null, WICDecodeOptions.WICDecodeMetadataCacheOnDemand)), ctx);
		}
	}

	internal class WicEncoder : WicBase
	{
		public IWICBitmapEncoder Encoder { get; private set; }
		public IWICBitmapFrameEncode Frame { get; private set; }

		public WicEncoder(IStream stm, WicProcessingContext ctx)
		{
			var frame = (IWICBitmapFrameEncode)null;
			if (ctx.Settings.SaveFormat == FileFormat.Jpeg)
			{
				Encoder = AddRef(Wic.CreateEncoder(Consts.GUID_ContainerFormatJpeg, null));
				Encoder.Initialize(stm, WICBitmapEncoderCacheOption.WICBitmapEncoderNoCache);

				var bag = (IPropertyBag2)null;
				Encoder.CreateNewFrame(out frame, ref bag);
				AddRef(frame);
				AddRef(bag);

				if (ctx.Settings.JpegSubsampleMode != ChromaSubsampleMode.Default)
				{
					var props = new PROPBAG2[] { new PROPBAG2 { pstrName = "ImageQuality" }, new PROPBAG2 { pstrName = "JpegYCrCbSubsampling" } };
					bag.Write(2, props, new object[] { ctx.Settings.JpegQuality / 100f, (byte)ctx.Settings.JpegSubsampleMode });
				}
				else
				{
					var props = new PROPBAG2[] { new PROPBAG2 { pstrName = "ImageQuality" } };
					bag.Write(1, props, new object[] { ctx.Settings.JpegQuality / 100f });
				}

				frame.Initialize(bag);
			}
			else if (ctx.Settings.SaveFormat == FileFormat.Gif)
			{
				Encoder = AddRef(Wic.CreateEncoder(Consts.GUID_ContainerFormatGif, null));
				Encoder.Initialize(stm, WICBitmapEncoderCacheOption.WICBitmapEncoderNoCache);
				Encoder.SetPalette(ctx.DestPalette);

				Encoder.CreateNewFrame(out frame, null);
				AddRef(frame);

				frame.Initialize(null);
			}
			else if (ctx.Settings.SaveFormat == FileFormat.Bmp)
			{
				Encoder = AddRef(Wic.CreateEncoder(Consts.GUID_ContainerFormatBmp, null));
				Encoder.Initialize(stm, WICBitmapEncoderCacheOption.WICBitmapEncoderNoCache);

				var bag = (IPropertyBag2)null;
				Encoder.CreateNewFrame(out frame, ref bag);
				AddRef(frame);
				AddRef(bag);

				var props = new PROPBAG2[] { new PROPBAG2 { pstrName = "EnableV5Header32bppBGRA" } };
				bag.Write(1, props, new object[] { ctx.PixelFormat == Consts.GUID_WICPixelFormat32bppBGRA });

				frame.Initialize(bag);
			}
			else if (ctx.Settings.SaveFormat == FileFormat.Tiff)
			{
				Encoder = AddRef(Wic.CreateEncoder(Consts.GUID_ContainerFormatTiff, null));
				Encoder.Initialize(stm, WICBitmapEncoderCacheOption.WICBitmapEncoderNoCache);

				var bag = (IPropertyBag2)null;
				Encoder.CreateNewFrame(out frame, ref bag);
				AddRef(frame);
				AddRef(bag);

				var props = new PROPBAG2[] { new PROPBAG2 { pstrName = "TiffCompressionMethod" } };
				bag.Write(1, props, new object[] { (byte)WICTiffCompressionOption.WICTiffCompressionNone });

				frame.Initialize(bag);
			}
			else
			{
				Encoder = AddRef(Wic.CreateEncoder(Consts.GUID_ContainerFormatPng, null));
				Encoder.Initialize(stm, WICBitmapEncoderCacheOption.WICBitmapEncoderNoCache);

				Encoder.CreateNewFrame(out frame, null);
				AddRef(frame);

				frame.Initialize(null);
			}

			frame.SetResolution(96d, 96d);
			frame.SetSize(ctx.Width, ctx.Height);

			if (ctx.Settings.IndexedColor && ctx.PixelFormat == Consts.GUID_WICPixelFormat8bppIndexed)
				frame.SetPalette(ctx.DestPalette);

#if NET46
			if (ctx.Metadata?.Count > 0 && frame.TryGetMetadataQueryWriter(out var metawriter))
			{
				AddRef(metawriter);
				foreach (var nv in ctx.Metadata)
					metawriter.TrySetMetadataByName(nv.Key, nv.Value);
			}
#endif

			// TODO setting
			//if (ctx.DestColorContext != null)
			//	frame.SetColorContexts(1, new IWICColorContext[] { ctx.DestColorContext });

			Frame = frame;
		}

		public void WriteSource(WicTransform prev)
		{
			var src = prev.Source;

			var iformat = src.GetPixelFormat();
			var oformat = iformat;

			Frame.SetPixelFormat(ref oformat);
			if (oformat != iformat)
			{
				// TODO grey -> indexed support
				//var pal = AddRef(Wic.CreatePalette());
				//pal.InitializePredefined(WICBitmapPaletteType.WICBitmapPaletteTypeFixedGray256, false);
				var conv = AddRef(Wic.CreateFormatConverter());
				conv.Initialize(src, oformat, WICBitmapDitherType.WICBitmapDitherTypeNone, null, 0.0, WICBitmapPaletteType.WICBitmapPaletteTypeCustom);
				src = conv;
			}

			Frame.WriteSource(src, null);

			Frame.Commit();
			Encoder.Commit();
		}
	}
}