using System;
using System.Buffers;
using System.Runtime.InteropServices;

using PhotoSauce.Interop.Wic;
using PhotoSauce.MagicScaler.Transforms;

namespace PhotoSauce.MagicScaler
{
	internal class WicImageFrame : IImageFrame
	{
		private readonly ComHandleCollection comHandles = new ComHandleCollection(4);

		private PixelSource? source;
		private IPixelSource? isource;
		private WicColorProfile? colorProfile;

		public readonly WicImageContainer Container;

		public double DpiX { get; }
		public double DpiY { get; }
		public Orientation ExifOrientation { get; } = Orientation.Normal;
		public ReadOnlySpan<byte> IccProfile => ColorProfileSource.ParsedProfile.ProfileBytes;

		public bool SupportsNativeScale { get; }
		public bool SupportsPlanarProcessing { get; }

		public WICJpegYCrCbSubsamplingOption ChromaSubsampling { get; }

		public IWICBitmapFrameDecode WicFrame { get; }
		public IWICBitmapSource WicSource { get; }
		public IWICMetadataQueryReader? WicMetadataReader { get; }

		public PixelSource Source => source ??= WicSource.AsPixelSource(nameof(IWICBitmapFrameDecode), false);

		public IPixelSource PixelSource => isource ??= Source.AsIPixelSource();

		public WicColorProfile ColorProfileSource => colorProfile ??= getColorProfile();

		public WicImageFrame(WicImageContainer decoder, uint index)
		{
			WicFrame = comHandles.AddRef(decoder.WicDecoder.GetFrame(index));
			WicSource = WicFrame;
			Container = decoder;

			WicFrame.GetResolution(out double dpix, out double dpiy);
			DpiX = dpix;
			DpiY = dpiy;

			WicFrame.GetSize(out uint frameWidth, out uint frameHeight);

			if (WicFrame.TryGetMetadataQueryReader(out var metareader))
			{
				WicMetadataReader = comHandles.AddRef(metareader);

				string orientationPath =
					MagicImageProcessor.EnableXmpOrientation ? Wic.Metadata.OrientationWindowsPolicy :
					Container.ContainerFormat == FileFormat.Jpeg ? Wic.Metadata.OrientationJpeg :
					Wic.Metadata.OrientationExif;

				ExifOrientation = ((Orientation)metareader.GetValueOrDefault<ushort>(orientationPath)).Clamp();
			}

			if (decoder.IsRawContainer && index == 0 && decoder.WicDecoder.TryGetPreview(out var preview))
			{
				using var pvwSource = ComHandle.Wrap(preview);
				preview.GetSize(out uint pw, out uint ph);

				if (pw == frameWidth && ph == frameHeight)
					WicSource = comHandles.AddOwnRef(preview);
			}

			if (WicSource is IWICBitmapSourceTransform trans)
			{
				uint pw = 1, ph = 1;
				trans.GetClosestSize(ref pw, ref ph);

				SupportsNativeScale = pw < frameWidth || ph < frameHeight;
			}

			if (WicSource is IWICPlanarBitmapSourceTransform ptrans)
			{
				var desc = ArrayPool<WICBitmapPlaneDescription>.Shared.Rent(WicTransforms.PlanarPixelFormats.Length);

				uint twidth = frameWidth, theight = frameHeight;
				SupportsPlanarProcessing = ptrans.DoesSupportTransform(
					ref twidth, ref theight,
					WICBitmapTransformOptions.WICBitmapTransformRotate0, WICPlanarOptions.WICPlanarOptionsDefault,
					WicTransforms.PlanarPixelFormats, desc, (uint)WicTransforms.PlanarPixelFormats.Length
				);

				ChromaSubsampling =
					desc[1].Width < desc[0].Width && desc[1].Height < desc[0].Height ? WICJpegYCrCbSubsamplingOption.WICJpegYCrCbSubsampling420 :
					desc[1].Width < desc[0].Width ? WICJpegYCrCbSubsamplingOption.WICJpegYCrCbSubsampling422 :
					desc[1].Height < desc[0].Height ? WICJpegYCrCbSubsamplingOption.WICJpegYCrCbSubsampling440 :
					WICJpegYCrCbSubsamplingOption.WICJpegYCrCbSubsampling444;

				ArrayPool<WICBitmapPlaneDescription>.Shared.Return(desc);
			}

			if (PixelFormat.FromGuid(WicSource.GetPixelFormat()).NumericRepresentation == PixelNumericRepresentation.Indexed)
			{
				var newFormat = PixelFormat.Bgr24Bpp;
				if (Container.ContainerFormat == FileFormat.Gif && Container.FrameCount > 1)
				{
					newFormat = PixelFormat.Bgra32Bpp;
				}
				else
				{
					using var wicpal = ComHandle.Wrap(Wic.Factory.CreatePalette());
					var pal = wicpal.ComObject;
					WicSource.CopyPalette(pal);

					if (pal.HasAlpha())
						newFormat = PixelFormat.Bgra32Bpp;
					else if (pal.IsGrayscale() || pal.IsBlackWhite())
						newFormat = PixelFormat.Grey8Bpp;
				}

				var conv = comHandles.AddRef(Wic.Factory.CreateFormatConverter());
				conv.Initialize(WicSource, newFormat.FormatGuid, WICBitmapDitherType.WICBitmapDitherTypeNone, null, 0.0, WICBitmapPaletteType.WICBitmapPaletteTypeCustom);
				WicSource = conv;
			}
		}

		public void Dispose() => comHandles.Dispose();

		unsafe public static void SetupGifAnimationContext(WicGifContainer cont, int playTo)
		{
			const int bytesPerPixel = 4;

			var anictx = cont.AnimationContext ??= new GifAnimationContext();
			if (anictx.LastFrame > playTo)
				anictx.LastFrame = -1;

			for (; anictx.LastFrame < playTo; anictx.LastFrame++)
			{
				using var frame = ComHandle.Wrap(cont.WicDecoder.GetFrame((uint)(anictx.LastFrame + 1)));
				using var meta = ComHandle.Wrap(frame.ComObject.GetMetadataQueryReader());

				var ldisp = anictx.LastDisposal;
				anictx.LastDisposal = ((GifDisposalMethod)meta.ComObject.GetValueOrDefault<byte>(Wic.Metadata.Gif.FrameDisposal)).Clamp();

				if (anictx.LastDisposal != GifDisposalMethod.Preserve)
					continue;

				using var conv = ComHandle.Wrap(Wic.Factory.CreateFormatConverter());
				conv.ComObject.Initialize(frame.ComObject, Consts.GUID_WICPixelFormat32bppBGRA, WICBitmapDitherType.WICBitmapDitherTypeNone, null, 0.0, WICBitmapPaletteType.WICBitmapPaletteTypeCustom);

				var finfo = GetGifFrameInfo(cont, conv.ComObject, meta.ComObject);
				var fbuff = anictx.FrameBufferSource ??= new FrameBufferSource(cont.ScreenWidth, cont.ScreenHeight);
				var bspan = fbuff.Span;

				fbuff.ResumeTiming();

				// Most GIF viewers clear the background to transparent instead of the background color when the next frame has transparency
				bool ftrans = meta.ComObject.GetValueOrDefault<bool>(Wic.Metadata.Gif.TransparencyFlag);
				if (!finfo.FullScreen && ldisp == GifDisposalMethod.RestoreBackground)
					MemoryMarshal.Cast<byte, uint>(bspan).Fill(ftrans ? 0 : cont.BackgroundColor);

				// Similarly, they overwrite a background color with transparent pixels but overlay instead when the previous frame is preserved
				var fspan = bspan.Slice(finfo.Top * fbuff.Stride + finfo.Left * bytesPerPixel);
				fixed (byte* buff = fspan)
				{
					if (!ftrans || ldisp == GifDisposalMethod.RestoreBackground)
					{
						conv.ComObject.CopyPixels(WICRect.Null, (uint)fbuff.Stride, (uint)fspan.Length, (IntPtr)buff);
					}
					else
					{
						using var overlay = new OverlayTransform(fbuff, conv.ComObject.AsPixelSource(nameof(IWICBitmapFrameDecode)), finfo.Left, finfo.Top, true, true);
						overlay.CopyPixels(new PixelArea(finfo.Left, finfo.Top, finfo.Width, finfo.Height), fbuff.Stride, fspan.Length, (IntPtr)buff);
					}
				}

				fbuff.PauseTiming();
			}
		}

		public static (int Left, int Top, int Width, int Height, bool Alpha, bool FullScreen) GetGifFrameInfo(WicGifContainer cont, IWICBitmapSource frame, IWICMetadataQueryReader meta)
		{
			frame.GetSize(out uint width, out uint height);

			int left = 0, top = 0;
			bool trans = meta.GetValueOrDefault<bool>(Wic.Metadata.Gif.TransparencyFlag);
			bool full = width == cont.ScreenWidth && height == cont.ScreenHeight;
			if (!full)
			{
				left = meta.GetValueOrDefault<ushort>(Wic.Metadata.Gif.FrameLeft);
				top = meta.GetValueOrDefault<ushort>(Wic.Metadata.Gif.FrameTop);
			}

			return (left, top, (int)width, (int)height, trans, full);
		}

		private WicColorProfile getColorProfile()
		{
			var fmt = PixelFormat.FromGuid(WicSource.GetPixelFormat());
			uint ccc = WicFrame.GetColorContextCount();
			if (ccc == 0)
				return WicColorProfile.GetDefaultFor(fmt);

			var profiles = ArrayPool<IWICColorContext>.Shared.Rent((int)ccc);

			for (int i = 0; i < (int)ccc; i++)
				profiles[i] = comHandles.AddRef(Wic.Factory.CreateColorContext());

			WicFrame.GetColorContexts(ccc, profiles);
			var match = matchProfile(profiles.AsSpan(0, (int)ccc), fmt);

			ArrayPool<IWICColorContext>.Shared.Return(profiles);

			return match ?? WicColorProfile.GetDefaultFor(fmt);
		}

		private WicColorProfile? matchProfile(ReadOnlySpan<IWICColorContext> profiles, PixelFormat fmt)
		{
			foreach (var cc in profiles)
			{
				var cct = cc.GetType();
				if (cct == WICColorContextType.WICColorContextProfile)
				{
					uint cb = cc.GetProfileBytes(0, null);

					// Don't try to read giant profiles. 4MiB should be enough, and more might indicate corrupt metadata.
					if (cb > 1024 * 1024 * 4)
						continue;

					var buff = ArrayPool<byte>.Shared.Rent((int)cb);

					cc.GetProfileBytes(cb, buff);
					var cpi = ColorProfile.Cache.GetOrAdd(new ReadOnlySpan<byte>(buff, 0, (int)cb));

					ArrayPool<byte>.Shared.Return(buff);

					// Use the profile only if it matches the frame's pixel format.  Ignore embedded sRGB-compatible profiles -- they will be upgraded to the internal sRGB/sGrey definintion.
					if (cpi.IsValid && cpi.IsCompatibleWith(fmt) && !cpi.IsSrgb)
						return new WicColorProfile(cc, cpi);
				}
				else if (cct == WICColorContextType.WICColorContextExifColorSpace && WicMetadataReader != null)
				{
					// Although WIC defines the non-standard AdobeRGB ExifColorSpace value, most software (including Adobe's) only supports the Uncalibrated/InteropIndex=R03 convention.
					// http://ninedegreesbelow.com/photography/embedded-color-space-information.html
					var ecs = cc.GetExifColorSpace();
					if (
						ecs == ExifColorSpace.AdobeRGB || (
						ecs == ExifColorSpace.Uncalibrated
						&& WicMetadataReader.GetValueOrDefault<string>(Container.ContainerFormat == FileFormat.Jpeg ? Wic.Metadata.InteropIndexJpeg : Wic.Metadata.InteropIndexExif) == "R03")
					) return WicColorProfile.AdobeRgb.Value;
				}
			}

			return null;
		}
	}
}