using System;
using System.IO;
using System.Linq;
using System.Buffers;
using System.Threading;
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
			if (Thread.CurrentThread.GetApartmentState() == ApartmentState.STA)
				throw new NotSupportedException($"WIC integration is not supported on an STA thread, such as the UI thread in a WinForms or WPF application. Use a background thread (e.g. using Task.Run()) instead.");

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

		public void Dispose() => encoder.Dispose();
	}

	internal class WicImageEncoderFrame : IDisposable
	{
		private readonly ComHandle.ComDisposer<IWICBitmapFrameEncode> encoderFrame;

		public IWICBitmapFrameEncode WicEncoderFrame => encoderFrame.ComObject;

		public WicImageEncoderFrame(PipelineContext ctx, WicImageEncoder encoder, PixelArea area = default)
		{
			var fmt = ctx.Settings.SaveFormat;
			var encArea = area.IsEmpty ? ctx.Source.Area : area;
			var colorMode = ctx.Settings.ColorProfileMode;

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

			frame.SetSize((uint)encArea.Width, (uint)encArea.Height);
			frame.SetResolution(ctx.Settings.DpiX > 0d ? ctx.Settings.DpiX : ctx.ImageFrame.DpiX, ctx.Settings.DpiY > 0d ? ctx.Settings.DpiY : ctx.ImageFrame.DpiY);

			bool copySourceMetadata = ctx.ImageFrame is WicImageFrame srcFrame && srcFrame.WicMetadataReader is not null && ctx.Settings.MetadataNames != Enumerable.Empty<string>();
			bool writeOrientation = ctx.Settings.OrientationMode == OrientationMode.Preserve && ctx.ImageFrame.ExifOrientation != Orientation.Normal;
			bool writeColorContext = colorMode == ColorProfileMode.NormalizeAndEmbed || colorMode == ColorProfileMode.Preserve || (colorMode == ColorProfileMode.Normalize && ctx.DestColorProfile != ColorProfile.sRGB && ctx.DestColorProfile != ColorProfile.sGrey);

			if ((copySourceMetadata || writeOrientation) && frame.TryGetMetadataQueryWriter(out var metawriter))
			{
				using var cmeta = ComHandle.Wrap(metawriter);
				if (copySourceMetadata)
				{
					var wicFrame = (WicImageFrame)ctx.ImageFrame;
					foreach (string prop in ctx.Settings.MetadataNames)
					{
						if (wicFrame.WicMetadataReader!.TryGetMetadataByName(prop, out var pvar) && pvar.Value is not null)
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
				Debug.Assert(ctx.WicContext.DestColorContext is not null || ctx.DestColorProfile is not null);

				var cc = ctx.WicContext.DestColorContext;
				if (ctx.DestColorProfile == ColorProfile.sRGB)
					cc = WicColorProfile.SrgbCompact.Value.WicColorContext;
				else if (ctx.DestColorProfile == ColorProfile.sGrey)
					cc = WicColorProfile.GreyCompact.Value.WicColorContext;
				else if (ctx.DestColorProfile == ColorProfile.AdobeRgb)
					cc = WicColorProfile.AdobeRgb.Value.WicColorContext;
				else if (ctx.DestColorProfile == ColorProfile.DisplayP3)
					cc = WicColorProfile.DisplayP3Compact.Value.WicColorContext;

				frame.TrySetColorContexts(cc ?? ctx.WicContext.AddRef(WicColorProfile.CreateContextFromProfile(ctx.DestColorProfile!.ProfileBytes)));
			}
		}

		public void WriteSource(PipelineContext ctx, PixelArea area = default)
		{
			var wicFrame = WicEncoderFrame;
			var wicRect = area.ToWicRect();

			if (ctx.PlanarContext is not null)
			{
				var oformat = Consts.GUID_WICPixelFormat24bppBGR;
				wicFrame.SetPixelFormat(ref oformat);

				var planes = ArrayPool<IWICBitmapSource>.Shared.Rent(3);

				planes[0] = ctx.PlanarContext.SourceY.AsIWICBitmapSource();
				planes[1] = ctx.PlanarContext.SourceCb.AsIWICBitmapSource();
				planes[2] = ctx.PlanarContext.SourceCr.AsIWICBitmapSource();
				((IWICPlanarBitmapFrameEncode)wicFrame).WriteSource(planes, 3, area.IsEmpty ? ref WICRect.Null : ref wicRect);

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
					Debug.Assert(ctx.WicContext.DestPalette is not null);

					wicFrame.SetPalette(ctx.WicContext.DestPalette);
				}

				wicFrame.WriteSource(ctx.Source.AsIWICBitmapSource(), area.IsEmpty ? ref WICRect.Null : ref wicRect);
			}

			wicFrame.Commit();
		}

		public void Dispose() => encoderFrame.Dispose();
	}

	internal sealed class WicColorProfile
	{
		public static readonly Lazy<WicColorProfile> Cmyk = new Lazy<WicColorProfile>(() => new WicColorProfile(getDefaultColorContext(PixelFormat.Cmyk32Bpp.FormatGuid), null));
		public static readonly Lazy<WicColorProfile> Srgb = new Lazy<WicColorProfile>(() => new WicColorProfile(CreateContextFromProfile(IccProfiles.sRgbV4.Value), ColorProfile.sRGB));
		public static readonly Lazy<WicColorProfile> Grey = new Lazy<WicColorProfile>(() => new WicColorProfile(CreateContextFromProfile(IccProfiles.sGreyV4.Value), ColorProfile.sGrey));
		public static readonly Lazy<WicColorProfile> DisplayP3 = new Lazy<WicColorProfile>(() => new WicColorProfile(CreateContextFromProfile(IccProfiles.DisplayP3V4.Value), ColorProfile.sGrey));
		public static readonly Lazy<WicColorProfile> SrgbCompact = new Lazy<WicColorProfile>(() => new WicColorProfile(CreateContextFromProfile(IccProfiles.sRgbCompact.Value), ColorProfile.sRGB));
		public static readonly Lazy<WicColorProfile> GreyCompact = new Lazy<WicColorProfile>(() => new WicColorProfile(CreateContextFromProfile(IccProfiles.sGreyCompact.Value), ColorProfile.sGrey));
		public static readonly Lazy<WicColorProfile> AdobeRgb = new Lazy<WicColorProfile>(() => new WicColorProfile(CreateContextFromProfile(IccProfiles.AdobeRgb.Value), ColorProfile.AdobeRgb));
		public static readonly Lazy<WicColorProfile> DisplayP3Compact = new Lazy<WicColorProfile>(() => new WicColorProfile(CreateContextFromProfile(IccProfiles.DisplayP3Compact.Value), ColorProfile.AdobeRgb));

		public static WicColorProfile GetDefaultFor(PixelFormat fmt) => fmt.ColorRepresentation switch {
			PixelColorRepresentation.Cmyk => Cmyk.Value,
			PixelColorRepresentation.Grey => Grey.Value,
			_ => Srgb.Value
		};

		public static WicColorProfile GetSourceProfile(WicColorProfile wicprof, ColorProfileMode mode)
		{
			var prof = ColorProfile.GetSourceProfile(wicprof.ParsedProfile, mode);
			return MapKnownProfile(prof) ?? wicprof;
		}

		public static WicColorProfile GetDestProfile(WicColorProfile wicprof, ColorProfileMode mode)
		{
			var prof = ColorProfile.GetDestProfile(wicprof.ParsedProfile, mode);
			return MapKnownProfile(prof) ?? wicprof;
		}

		public static WicColorProfile? MapKnownProfile(ColorProfile prof)
		{
			if (prof == ColorProfile.sGrey)
				return Grey.Value;
			if (prof == ColorProfile.sRGB)
				return Srgb.Value;
			if (prof == ColorProfile.AdobeRgb)
				return AdobeRgb.Value;
			if (prof == ColorProfile.DisplayP3)
				return DisplayP3.Value;

			return null;
		}

		public static IWICColorContext CreateContextFromProfile(byte[] profile)
		{
			var cc = Wic.Factory.CreateColorContext();
			cc.InitializeFromMemory(profile, (uint)profile.Length);
			return cc;
		}

		public static ColorProfile GetProfileFromContext(IWICColorContext cc, uint cb)
		{
			if (cb == 0u)
				cb = cc.GetProfileBytes(0, null);

			var buff = ArrayPool<byte>.Shared.Rent((int)cb);

			cc.GetProfileBytes(cb, buff);
			var cpi = ColorProfile.Cache.GetOrAdd(new ReadOnlySpan<byte>(buff, 0, (int)cb));

			ArrayPool<byte>.Shared.Return(buff);

			return cpi;
		}

		private static IWICColorContext getDefaultColorContext(Guid pixelFormat)
		{
			using var pfi = ComHandle.QueryInterface<IWICPixelFormatInfo>(Wic.Factory.CreateComponentInfo(pixelFormat));
			return pfi.ComObject.GetColorContext();
		}

		public IWICColorContext WicColorContext { get; }
		public ColorProfile ParsedProfile { get; }

		public WicColorProfile(IWICColorContext cc, ColorProfile? prof)
		{
			WicColorContext = cc;
			ParsedProfile = prof ?? GetProfileFromContext(cc, 0);
		}
	}

	internal sealed class WicAnimatedGifEncoder
	{
		public class BufferFrame
		{
			public readonly FrameBufferSource Source;
			public PixelArea Area;
			public GifDisposalMethod Disposal;
			public int Delay;
			public bool Trans;

			public BufferFrame(int width, int height, PixelFormat format) =>
				Source = new FrameBufferSource(width, height, format);
		}

		private readonly PipelineContext ctx;
		private readonly WicImageEncoder encoder;
		private readonly PixelSource lastSource;
		private readonly BufferFrame[] frames = new BufferFrame[3];
		private readonly BufferFrame encodeFrame;
		private readonly IWICBitmapSource wicSource;
		private readonly int lastFrame;

		private int currentFrame = 0;

		public BufferFrame EncodeFrame => encodeFrame;
		public BufferFrame Current => frames[currentFrame % 3];
		public BufferFrame? Previous => currentFrame == 0 ? null : frames[(currentFrame - 1) % 3];
		public BufferFrame? Next => currentFrame == lastFrame ? null : frames[(currentFrame + 1) % 3];

		public WicAnimatedGifEncoder(PipelineContext ctx, WicImageEncoder enc)
		{
			this.ctx = ctx;
			encoder = enc;

			lastSource = ctx.Source;
			lastFrame = ctx.ImageContainer.FrameCount - 1;

			encodeFrame = new BufferFrame(lastSource.Width, lastSource.Height, lastSource.Format);
			for (int i = 0; i < frames.Length; i++)
				frames[i] = new BufferFrame(lastSource.Width, lastSource.Height, lastSource.Format);

			loadFrame(Current);
			Current.Source.Span.CopyTo(encodeFrame.Source.Span);
			wicSource = encodeFrame.Source.AsIWICBitmapSource();

			moveToFrame(1);
			loadFrame(Next!);
		}

		public void WriteGlobalMetadata()
		{
			var cnt = ctx.ImageContainer as WicGifContainer ?? throw new InvalidOperationException("Source must be a GIF");

			using var decmeta = ComHandle.Wrap(cnt.WicDecoder.GetMetadataQueryReader());
			using var encmeta = ComHandle.Wrap(encoder.WicEncoder.GetMetadataQueryWriter());
			var dm = decmeta.ComObject;
			var em = encmeta.ComObject;

			if (dm.TryGetMetadataByName(Wic.Metadata.Gif.AppExtension, out var appext))
				em.SetMetadataByName(Wic.Metadata.Gif.AppExtension, appext);
			if (dm.TryGetMetadataByName(Wic.Metadata.Gif.AppExtensionData, out var appdata))
				em.SetMetadataByName(Wic.Metadata.Gif.AppExtensionData, appdata);
			if (dm.TryGetMetadataByName(Wic.Metadata.Gif.PixelAspectRatio, out var aspect))
				em.SetMetadataByName(Wic.Metadata.Gif.PixelAspectRatio, aspect);

			// TODO WIC ignores these and sets the logical screen descriptor dimensions from the first frame
			//em.SetMetadataByName(Wic.Metadata.Gif.LogicalScreenWidth, new PropVariant((ushort)ctx.Source.Width));
			//em.SetMetadataByName(Wic.Metadata.Gif.LogicalScreenHeight, new PropVariant((ushort)ctx.Source.Height));

			if (dm.GetValueOrDefault<bool>(Wic.Metadata.Gif.GlobalPaletteFlag))
			{
				using var pal = ComHandle.Wrap(Wic.Factory.CreatePalette());
				cnt.WicDecoder.CopyPalette(pal.ComObject);
				encoder.WicEncoder.SetPalette(pal.ComObject);

				if (dm.TryGetMetadataByName(Wic.Metadata.Gif.PixelAspectRatio, out var bgidx))
					em.SetMetadataByName(Wic.Metadata.Gif.BackgroundColorIndex, bgidx);
			}
		}

		public void WriteFrames()
		{
			uint bgColor = ((WicGifContainer)ctx.ImageContainer).BackgroundColor;

			writeFrame(Current);

			while (moveNext())
			{
				TemporalFilters.Dedupe(this, bgColor);

				writeFrame(Current);
			}
		}

		private bool moveNext()
		{
			if (currentFrame == lastFrame)
				return false;

			if (++currentFrame != lastFrame)
			{
				moveToFrame(currentFrame + 1);
				loadFrame(Next!);
			}

			return true;
		}

		private void moveToFrame(int index)
		{
			ctx.Settings.FrameIndex = index;

			ctx.ImageFrame.Dispose();
			ctx.ImageFrame = ctx.ImageContainer.GetFrame(index);

			if (ctx.ImageFrame is WicImageFrame wicFrame)
				ctx.Source = wicFrame.WicSource.AsPixelSource(nameof(IWICBitmapFrameDecode), true);
			else
				ctx.Source = ctx.ImageFrame.PixelSource.AsPixelSource();

			MagicTransforms.AddGifFrameBuffer(ctx, false);

			if (lastSource is ChainedPixelSource chain && chain.Passthrough)
			{
				chain.ReInit(ctx.Source);
				ctx.Source = chain;
			}
		}

		unsafe private void loadFrame(BufferFrame frame)
		{
			var srcmeta = ((WicImageFrame)ctx.ImageFrame).WicMetadataReader!;

			frame.Disposal = (GifDisposalMethod)srcmeta.GetValueOrDefault<byte>(Wic.Metadata.Gif.FrameDisposal) == GifDisposalMethod.RestoreBackground ? GifDisposalMethod.RestoreBackground : GifDisposalMethod.Preserve;
			frame.Delay = srcmeta.GetValueOrDefault<ushort>(Wic.Metadata.Gif.FrameDelay);
			frame.Trans = srcmeta.GetValueOrDefault<bool>(Wic.Metadata.Gif.TransparencyFlag);
			frame.Area = ctx.Source.Area;

			var buff = frame.Source;
			fixed (byte* pbuff = buff.Span)
				ctx.Source.CopyPixels(frame.Area, buff.Stride, buff.Span.Length, (IntPtr)pbuff);
		}

		private void writeFrame(BufferFrame src)
		{
			using var quant = new OctreeQuantizer();
			using var buffI = new FrameBufferSource(ctx.Source.Width, ctx.Source.Height, PixelFormat.Indexed8Bpp);
			var buffC = encodeFrame.Source;

			quant.CreateHistorgram(buffC.Span, buffC.Width, buffC.Height, buffC.Stride);
			quant.Quantize(buffC.Span, buffI.Span, buffC.Width, buffC.Height, buffC.Stride, buffI.Stride);
			var palette = quant.Palette;

			var palarray = ArrayPool<uint>.Shared.Rent(palette.Length);
			palette.CopyTo(palarray);

			using var wicpal = ComHandle.Wrap(Wic.Factory.CreatePalette());
			wicpal.ComObject.InitializeCustom(palarray, (uint)palette.Length);

			ArrayPool<uint>.Shared.Return(palarray);

			ctx.WicContext.DestPalette = wicpal.ComObject;
			ctx.Source = buffI;

			using var frm = new WicImageEncoderFrame(ctx, encoder, src.Area);

			using var frmmeta = ComHandle.Wrap(frm.WicEncoderFrame.GetMetadataQueryWriter());
			var fm = frmmeta.ComObject;
			fm.SetMetadataByName(Wic.Metadata.Gif.FrameDisposal, new PropVariant((byte)src.Disposal));
			fm.SetMetadataByName(Wic.Metadata.Gif.FrameDelay, new PropVariant((ushort)src.Delay));

			if (src.Area.X != 0)
				fm.SetMetadataByName(Wic.Metadata.Gif.FrameLeft, new PropVariant((ushort)src.Area.X));
			if (src.Area.Y != 0)
				fm.SetMetadataByName(Wic.Metadata.Gif.FrameTop, new PropVariant((ushort)src.Area.Y));

			fm.SetMetadataByName(Wic.Metadata.Gif.TransparencyFlag, new PropVariant(true));
			fm.SetMetadataByName(Wic.Metadata.Gif.TransparentColorIndex, new PropVariant((byte)(palette.Length - 1)));

			frm.WriteSource(ctx, src.Area);
		}
	}
}