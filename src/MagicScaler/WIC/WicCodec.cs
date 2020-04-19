using System;
using System.IO;
using System.Linq;
using System.Buffers;
using System.Diagnostics;
using System.Collections.Generic;
using System.Runtime.InteropServices;

using PhotoSauce.Interop.Wic;
using PhotoSauce.MagicScaler.Transforms;

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

	internal class WicImageEncoder : IDisposable
	{
		private static readonly IReadOnlyDictionary<FileFormat, Guid> formatMap = new Dictionary<FileFormat, Guid> {
			[FileFormat.Bmp] = Consts.GUID_ContainerFormatBmp,
			[FileFormat.Gif] = Consts.GUID_ContainerFormatGif,
			[FileFormat.Jpeg] = Consts.GUID_ContainerFormatJpeg,
			[FileFormat.Png] = Consts.GUID_ContainerFormatPng,
			[FileFormat.Png8] = Consts.GUID_ContainerFormatPng,
			[FileFormat.Tiff] = Consts.GUID_ContainerFormatTiff
		};

		private readonly ComHandle.ComDisposer<IWICBitmapEncoder> encoder;

		public IWICBitmapEncoder WicEncoder => encoder.ComObject;

		public WicImageEncoder(FileFormat format, IStream stm)
		{
			encoder = ComHandle.Wrap(Wic.Factory.CreateEncoder(formatMap.GetValueOrDefault(format, Consts.GUID_ContainerFormatPng), null));
			encoder.ComObject.Initialize(stm, WICBitmapEncoderCacheOption.WICBitmapEncoderNoCache);
		}

		unsafe public void WriteAnimatedGif(PipelineContext ctx)
		{
			var cnt = ctx.ImageContainer as WicGifContainer ?? throw new NotSupportedException("Source must be a GIF");

			using (var decmeta = ComHandle.Wrap(cnt.WicDecoder.GetMetadataQueryReader()))
			using (var encmeta = ComHandle.Wrap(WicEncoder.GetMetadataQueryWriter()))
			{
				if (decmeta.ComObject.TryGetMetadataByName(Wic.Metadata.Gif.AppExtension, out var appext))
					encmeta.ComObject.SetMetadataByName(Wic.Metadata.Gif.AppExtension, appext);
				if (decmeta.ComObject.TryGetMetadataByName(Wic.Metadata.Gif.AppExtensionData, out var appdata))
					encmeta.ComObject.SetMetadataByName(Wic.Metadata.Gif.AppExtensionData, appdata);
				if (decmeta.ComObject.TryGetMetadataByName(Wic.Metadata.Gif.PixelAspectRatio, out var aspect))
					encmeta.ComObject.SetMetadataByName(Wic.Metadata.Gif.PixelAspectRatio, aspect);
			}

			using var buffer = new FrameBufferSource(ctx.Source.Width, ctx.Source.Height, ctx.Source.Format);
			var bspan = buffer.Span;
			var lastSource = ctx.Source;
			var wicBuffer = buffer.AsIWICBitmapSource();

			var anictx = cnt.AnimationContext ??= new GifAnimationContext();

			for (int i = 0; i < ctx.ImageContainer.FrameCount; i++)
			{
				if (i > 0)
				{
					ctx.Settings.FrameIndex = i;

					ctx.ImageFrame.Dispose();
					ctx.ImageFrame = ctx.ImageContainer.GetFrame(ctx.Settings.FrameIndex);

					if (ctx.ImageFrame is WicImageFrame wicFrame)
						ctx.Source = wicFrame.WicSource.AsPixelSource(nameof(IWICBitmapFrameDecode), true);
					else
						ctx.Source = ctx.ImageFrame.PixelSource.AsPixelSource();

					MagicTransforms.AddGifFrameBuffer(ctx, false);

					if (lastSource is ChainedPixelSource chain)
					{
						chain.ReInit(ctx.Source);
						ctx.Source = chain;
					}
				}

				fixed (byte* pbuff = bspan)
				{
					ctx.Source.CopyPixels(ctx.Source.Area, buffer.Stride, bspan.Length, (IntPtr)pbuff);

					var curFormat = ctx.Source.Format;
					var newFormat = PixelFormat.Indexed8Bpp;
					bool alpha = curFormat.AlphaRepresentation != PixelAlphaRepresentation.None;

					using var pal = ComHandle.Wrap(Wic.Factory.CreatePalette());
					pal.ComObject.InitializeFromBitmap(wicBuffer, 256u, alpha);
					ctx.WicContext.DestPalette = pal.ComObject;

					using var conv = ComHandle.Wrap(Wic.Factory.CreateFormatConverter());
					conv.ComObject.Initialize(wicBuffer, newFormat.FormatGuid, WICBitmapDitherType.WICBitmapDitherTypeErrorDiffusion, pal.ComObject, 10.0, WICBitmapPaletteType.WICBitmapPaletteTypeCustom);
					ctx.Source = conv.ComObject.AsPixelSource($"{nameof(IWICFormatConverter)}: {curFormat.Name}->{newFormat.Name}", false);

					using var frm = new WicImageEncoderFrame(ctx, this);

					var srcmeta = ((WicImageFrame)ctx.ImageFrame).WicMetadataReader!;
					using (var frmmeta = ComHandle.Wrap(frm.WicEncoderFrame.GetMetadataQueryWriter()))
					{
						var disp = srcmeta.TryGetMetadataByName(Wic.Metadata.Gif.FrameDisposal, out var fdisp) && (byte)fdisp.Value! == (byte)GifDisposalMethod.RestoreBackground ? GifDisposalMethod.RestoreBackground : GifDisposalMethod.Undefined;

						frmmeta.ComObject.SetMetadataByName(Wic.Metadata.Gif.FrameDisposal, new PropVariant((byte)disp));
						if (srcmeta.TryGetMetadataByName(Wic.Metadata.Gif.FrameDelay, out var delay))
							frmmeta.ComObject.SetMetadataByName(Wic.Metadata.Gif.FrameDelay, delay);
						if (alpha)
						{
							frmmeta.ComObject.SetMetadataByName(Wic.Metadata.Gif.TransparencyFlag, new PropVariant(true));
							frmmeta.ComObject.SetMetadataByName(Wic.Metadata.Gif.TransparentColorIndex, new PropVariant((byte)(pal.ComObject.GetColorCount() - 1)));
						}
					}

					frm.WriteSource(ctx);
				}
			}
		}

		public void Dispose() => encoder.Dispose();
	}

	internal class WicImageEncoderFrame : IDisposable
	{
		private readonly ComHandle.ComDisposer<IWICBitmapFrameEncode> encoderFrame;

		public IWICBitmapFrameEncode WicEncoderFrame => encoderFrame.ComObject;

		public WicImageEncoderFrame(PipelineContext ctx, WicImageEncoder encoder)
		{
			var fmt = ctx.Settings.SaveFormat;

			var bag = default(IPropertyBag2);
			encoder.WicEncoder.CreateNewFrame(out var frame, ref bag);
			encoderFrame = ComHandle.Wrap(frame);

			using (var cbag = ComHandle.Wrap(bag))
			{
				if (fmt == FileFormat.Jpeg)
					bag.Write("ImageQuality", ctx.Settings.JpegQuality / 100f);

				if (fmt == FileFormat.Jpeg && ctx.Settings.JpegSubsampleMode != ChromaSubsampleMode.Default)
					bag.Write("JpegYCrCbSubsampling", (byte)ctx.Settings.JpegSubsampleMode);

				if (fmt == FileFormat.Tiff)
					bag.Write("TiffCompressionMethod", (byte)WICTiffCompressionOption.WICTiffCompressionNone);

				if (fmt == FileFormat.Bmp && ctx.Source.Format.AlphaRepresentation != PixelAlphaRepresentation.None)
					bag.Write("EnableV5Header32bppBGRA", true);

				frame.Initialize(bag);
			}

			frame.SetSize((uint)ctx.Source.Width, (uint)ctx.Source.Height);
			frame.SetResolution(ctx.Settings.DpiX > 0d ? ctx.Settings.DpiX : ctx.ImageFrame.DpiX, ctx.Settings.DpiY > 0d ? ctx.Settings.DpiY : ctx.ImageFrame.DpiY);

			bool copySourceMetadata = ctx.ImageFrame is WicImageFrame srcFrame && srcFrame.WicMetadataReader != null && ctx.Settings.MetadataNames != Enumerable.Empty<string>();
			bool writeOrientation = ctx.Settings.OrientationMode == OrientationMode.Preserve && ctx.ImageFrame.ExifOrientation != Orientation.Normal;
			bool writeColorContext = ctx.Settings.ColorProfileMode == ColorProfileMode.NormalizeAndEmbed || ctx.Settings.ColorProfileMode == ColorProfileMode.Preserve;

			if ((copySourceMetadata || writeOrientation) && frame.TryGetMetadataQueryWriter(out var metawriter))
			{
				using var cmeta = ComHandle.Wrap(metawriter);
				if (copySourceMetadata)
				{
					var wicFrame = (WicImageFrame)ctx.ImageFrame;
					foreach (string prop in ctx.Settings.MetadataNames)
					{
						if (wicFrame.WicMetadataReader!.TryGetMetadataByName(prop, out var pvar) && !(pvar.Value is null))
							metawriter.TrySetMetadataByName(prop, pvar);
					}
				}

				if (writeOrientation)
				{
					string orientationPath = ctx.Settings.SaveFormat == FileFormat.Jpeg ? Wic.Metadata.OrientationJpeg : Wic.Metadata.OrientationExif;
					metawriter.TrySetMetadataByName(orientationPath, new PropVariant((ushort)ctx.ImageFrame.ExifOrientation));
				}
			}

			if (writeColorContext)
			{
				Debug.Assert(ctx.WicContext.DestColorContext != null || ctx.DestColorProfile != null);

				var cc = ctx.WicContext.DestColorContext ?? ctx.WicContext.AddRef(WicColorProfile.CreateContextFromProfile(ctx.DestColorProfile!.ProfileBytes));
				if (ctx.DestColorProfile == ColorProfile.sRGB)
					cc = WicColorProfile.SrgbCompact.Value.WicColorContext;
				else if (ctx.DestColorProfile == ColorProfile.sGrey)
					cc = WicColorProfile.GreyCompact.Value.WicColorContext;

				frame.TrySetColorContexts(cc);
			}
		}

		public void WriteSource(PipelineContext ctx)
		{
			var wicFrame = WicEncoderFrame;

			if (ctx.PlanarContext != null)
			{
				var oformat = Consts.GUID_WICPixelFormat24bppBGR;
				wicFrame.SetPixelFormat(ref oformat);

				var planes = ArrayPool<IWICBitmapSource>.Shared.Rent(3);

				planes[0] = ctx.PlanarContext.SourceY.AsIWICBitmapSource();
				planes[1] = ctx.PlanarContext.SourceCb.AsIWICBitmapSource();
				planes[2] = ctx.PlanarContext.SourceCr.AsIWICBitmapSource();
				((IWICPlanarBitmapFrameEncode)wicFrame).WriteSource(planes, 3, WICRect.Null);

				ArrayPool<IWICBitmapSource>.Shared.Return(planes);
			}
			else
			{
				var oformat = ctx.Source.Format.FormatGuid;
				wicFrame.SetPixelFormat(ref oformat);
				if (oformat != ctx.Source.Format.FormatGuid)
				{
					var pal = default(IWICPalette);
					var ptt = WICBitmapPaletteType.WICBitmapPaletteTypeCustom;
					if (PixelFormat.FromGuid(oformat).NumericRepresentation == PixelNumericRepresentation.Indexed)
					{
						pal = ctx.WicContext.AddRef(Wic.Factory.CreatePalette());
						pal.InitializePredefined(WICBitmapPaletteType.WICBitmapPaletteTypeFixedGray256, false);
						ptt = WICBitmapPaletteType.WICBitmapPaletteTypeFixedGray256;

						wicFrame.SetPalette(pal);
					}

					var conv = ctx.WicContext.AddRef(Wic.Factory.CreateFormatConverter());
					conv.Initialize(ctx.Source.AsIWICBitmapSource(), oformat, WICBitmapDitherType.WICBitmapDitherTypeNone, pal, 0.0, ptt);
					ctx.Source = conv.AsPixelSource($"{nameof(IWICFormatConverter)}: {ctx.Source.Format.Name}->{PixelFormat.FromGuid(oformat).Name}", false);
				}
				else if (oformat == PixelFormat.Indexed8Bpp.FormatGuid)
				{
					Debug.Assert(ctx.WicContext.DestPalette != null);

					wicFrame.SetPalette(ctx.WicContext.DestPalette);
				}

				wicFrame.WriteSource(ctx.Source.AsIWICBitmapSource(), WICRect.Null);
			}

			wicFrame.Commit();
		}

		public void Dispose() => encoderFrame.Dispose();
	}

	internal sealed class WicColorProfile
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