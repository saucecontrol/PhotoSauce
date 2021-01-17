// Copyright © Clinton Ingram and Contributors.  Licensed under the MIT License.

using System;
using System.Diagnostics;
using System.Runtime.InteropServices;

using TerraFX.Interop;
using static TerraFX.Interop.Windows;

using PhotoSauce.Interop.Wic;
using PhotoSauce.MagicScaler.Transforms;

namespace PhotoSauce.MagicScaler
{
	internal unsafe class WicImageFrame : IImageFrame
	{
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

		public IWICBitmapFrameDecode* WicFrame { get; private set; }
		public IWICBitmapSource* WicSource { get; private set; }
		public IWICMetadataQueryReader* WicMetadataReader { get; private set; }

		public PixelSource Source => source ??= new ComPtr<IWICBitmapSource>(WicSource).AsPixelSource(null, nameof(IWICBitmapFrameDecode), false);

		public IPixelSource PixelSource => isource ??= Source.AsIPixelSource();

		public WicColorProfile ColorProfileSource => colorProfile ??= getColorProfile();

		public WicImageFrame(WicImageContainer decoder, uint index)
		{
			Container = decoder;

			using var frame = default(ComPtr<IWICBitmapFrameDecode>);
			HRESULT.Check(decoder.WicDecoder->GetFrame(index, frame.GetAddressOf()));

			using var source = new ComPtr<IWICBitmapSource>((IWICBitmapSource*)frame.Get());

			double dpix, dpiy;
			HRESULT.Check(frame.Get()->GetResolution(&dpix, &dpiy));
			(DpiX, DpiY) = (dpix, dpiy);

			uint frameWidth, frameHeight;
			HRESULT.Check(frame.Get()->GetSize(&frameWidth, &frameHeight));

			using var metareader = default(ComPtr<IWICMetadataQueryReader>);
			if (SUCCEEDED(frame.Get()->GetMetadataQueryReader(metareader.GetAddressOf())))
			{
				string orientationPath =
					MagicImageProcessor.EnableXmpOrientation ? Wic.Metadata.OrientationWindowsPolicy :
					Container.ContainerFormat == FileFormat.Jpeg ? Wic.Metadata.OrientationJpeg :
					Wic.Metadata.OrientationExif;

				ExifOrientation = ((Orientation)metareader.GetValueOrDefault<ushort>(orientationPath)).Clamp();
				WicMetadataReader = metareader.Detach();
			}

			using var preview = default(ComPtr<IWICBitmapSource>);
			if (decoder.IsRawContainer && index == 0 && SUCCEEDED(decoder.WicDecoder->GetPreview(preview.GetAddressOf())))
			{
				uint pw, ph;
				HRESULT.Check(preview.Get()->GetSize(&pw, &ph));

				if (pw == frameWidth && ph == frameHeight)
					source.Attach(preview.Detach());
			}

			using var transform = default(ComPtr<IWICBitmapSourceTransform>);
			if (SUCCEEDED(source.Get()->QueryInterface(__uuidof<IWICBitmapSourceTransform>(), (void**)transform.GetAddressOf())))
			{
				uint tw = 1, th = 1;
				HRESULT.Check(transform.Get()->GetClosestSize(&tw, &th));

				SupportsNativeScale = tw < frameWidth || th < frameHeight;
			}

			using var ptransform = default(ComPtr<IWICPlanarBitmapSourceTransform>);
			if (SUCCEEDED(source.Get()->QueryInterface(__uuidof<IWICPlanarBitmapSourceTransform>(), (void**)ptransform.GetAddressOf())))
			{
				var fmts = WicTransforms.PlanarPixelFormats;
				var desc = stackalloc WICBitmapPlaneDescription[fmts.Length];
				fixed (Guid* pfmt = fmts)
				{
					uint tw = frameWidth, th = frameHeight, st = 0;
					HRESULT.Check(ptransform.Get()->DoesSupportTransform(
						&tw, &th,
						WICBitmapTransformOptions.WICBitmapTransformRotate0, WICPlanarOptions.WICPlanarOptionsDefault,
						pfmt, desc, (uint)fmts.Length, (int*)&st
					));

					SupportsPlanarProcessing = st != 0;
				}

				ChromaSubsampling =
					desc[1].Width < desc[0].Width && desc[1].Height < desc[0].Height ? WICJpegYCrCbSubsamplingOption.WICJpegYCrCbSubsampling420 :
					desc[1].Width < desc[0].Width ? WICJpegYCrCbSubsamplingOption.WICJpegYCrCbSubsampling422 :
					desc[1].Height < desc[0].Height ? WICJpegYCrCbSubsamplingOption.WICJpegYCrCbSubsampling440 :
					WICJpegYCrCbSubsamplingOption.WICJpegYCrCbSubsampling444;
			}

			var guid = default(Guid);
			HRESULT.Check(source.Get()->GetPixelFormat(&guid));
			if (PixelFormat.FromGuid(guid).NumericRepresentation == PixelNumericRepresentation.Indexed)
			{
				var newFormat = PixelFormat.Bgr24Bpp;
				if (Container.ContainerFormat == FileFormat.Gif && Container.FrameCount > 1)
				{
					newFormat = PixelFormat.Bgra32Bpp;
				}
				else
				{
					using var pal = default(ComPtr<IWICPalette>);
					HRESULT.Check(Wic.Factory->CreatePalette(pal.GetAddressOf()));
					HRESULT.Check(source.Get()->CopyPalette(pal));

					int bval;
					if (SUCCEEDED(pal.Get()->HasAlpha(&bval)) && bval != 0)
						newFormat = PixelFormat.Bgra32Bpp;
					else if ((SUCCEEDED(pal.Get()->IsGrayscale(&bval)) && bval != 0) || (SUCCEEDED(pal.Get()->IsBlackWhite(&bval)) && bval != 0))
						newFormat = PixelFormat.Grey8Bpp;
				}

				var nfmt = newFormat.FormatGuid;
				using var conv = default(ComPtr<IWICFormatConverter>);
				HRESULT.Check(Wic.Factory->CreateFormatConverter(conv.GetAddressOf()));
				HRESULT.Check(conv.Get()->Initialize(source, &nfmt, WICBitmapDitherType.WICBitmapDitherTypeNone, null, 0.0, WICBitmapPaletteType.WICBitmapPaletteTypeCustom));

				source.Attach((IWICBitmapSource*)conv.Detach());
			}

			WicFrame = frame.Detach();
			WicSource = source.Detach();
		}

		public void Dispose()
		{
			if (WicFrame is null)
				return;

			colorProfile?.Dispose();

			if (WicMetadataReader is not null)
			{
				WicMetadataReader->Release();
				WicMetadataReader = null;
			}

			WicSource->Release();
			WicSource = null;

			WicFrame->Release();
			WicFrame = null;
		}

		public static void ReplayGifAnimationContext(WicGifContainer cont, int playTo)
		{
			var anictx = cont.AnimationContext ??= new GifAnimationContext();
			if (anictx.LastFrame > playTo)
				anictx.LastFrame = -1;

			for (; anictx.LastFrame < playTo; anictx.LastFrame++)
			{
				using var frame = default(ComPtr<IWICBitmapFrameDecode>);
				using var meta = default(ComPtr<IWICMetadataQueryReader>);
				HRESULT.Check(cont.WicDecoder->GetFrame((uint)(anictx.LastFrame + 1), frame.GetAddressOf()));
				HRESULT.Check(frame.Get()->GetMetadataQueryReader(meta.GetAddressOf()));

				var disp = ((GifDisposalMethod)meta.GetValueOrDefault<byte>(Wic.Metadata.Gif.FrameDisposal)).Clamp();
				if (disp == GifDisposalMethod.Preserve)
				{
					var nfmt = GUID_WICPixelFormat32bppBGRA;
					using var conv = default(ComPtr<IWICFormatConverter>);
					HRESULT.Check(Wic.Factory->CreateFormatConverter(conv.GetAddressOf()));
					HRESULT.Check(conv.Get()->Initialize((IWICBitmapSource*)frame.Get(), &nfmt, WICBitmapDitherType.WICBitmapDitherTypeNone, null, 0.0, WICBitmapPaletteType.WICBitmapPaletteTypeCustom));

					UpdateGifAnimationContext(cont, conv.Cast<IWICBitmapSource>(), meta);
				}

				anictx.LastDisposal = disp;
			}
		}

		public static void UpdateGifAnimationContext(WicGifContainer cont, ComPtr<IWICBitmapSource> src, ComPtr<IWICMetadataQueryReader> meta)
		{
			Debug.Assert(cont.AnimationContext is not null);
			var anictx = cont.AnimationContext;

			const int bytesPerPixel = 4;

			var finfo = GetGifFrameInfo(cont, src, meta);
			var fbuff = anictx.FrameBufferSource ??= new FrameBufferSource(cont.ScreenWidth, cont.ScreenHeight, PixelFormat.Bgra32Bpp);
			var bspan = fbuff.Span;

			fbuff.ResumeTiming();

			// Most GIF viewers clear the background to transparent instead of the background color when the next frame has transparency
			bool ftrans = meta.GetValueOrDefault<bool>(Wic.Metadata.Gif.TransparencyFlag);
			if (!finfo.FullScreen && anictx.LastDisposal == GifDisposalMethod.RestoreBackground)
				MemoryMarshal.Cast<byte, uint>(bspan).Fill(ftrans ? 0 : cont.BackgroundColor);

			// Similarly, they overwrite a background color with transparent pixels but overlay instead when the previous frame is preserved
			var fspan = bspan.Slice(finfo.Top * fbuff.Stride + finfo.Left * bytesPerPixel);
			fixed (byte* buff = fspan)
			{
				if (!ftrans || anictx.LastDisposal == GifDisposalMethod.RestoreBackground)
				{
					var rect = new WICRect { Width = finfo.Width, Height = finfo.Height };
					HRESULT.Check(src.Get()->CopyPixels(&rect, (uint)fbuff.Stride, (uint)fspan.Length, buff));
				}
				else
				{
					using var overlay = new OverlayTransform(fbuff, src.AsPixelSource(null, nameof(IWICBitmapFrameDecode), false), finfo.Left, finfo.Top, true, true);
					overlay.CopyPixels(new PixelArea(finfo.Left, finfo.Top, finfo.Width, finfo.Height), fbuff.Stride, fspan.Length, (IntPtr)buff);
				}
			}

			fbuff.PauseTiming();
		}

		public static (int Left, int Top, int Width, int Height, bool Alpha, bool FullScreen, GifDisposalMethod Disposal) GetGifFrameInfo(WicGifContainer cont, ComPtr<IWICBitmapSource> frame, ComPtr<IWICMetadataQueryReader> meta)
		{
			uint width, height;
			HRESULT.Check(frame.Get()->GetSize(&width, &height));

			int left = 0, top = 0;
			var disp = ((GifDisposalMethod)meta.GetValueOrDefault<byte>(Wic.Metadata.Gif.FrameDisposal)).Clamp();
			bool trans = meta.GetValueOrDefault<bool>(Wic.Metadata.Gif.TransparencyFlag);
			bool full = width >= cont.ScreenWidth && height >= cont.ScreenHeight;
			if (!full)
			{
				left = meta.GetValueOrDefault<ushort>(Wic.Metadata.Gif.FrameLeft);
				top = meta.GetValueOrDefault<ushort>(Wic.Metadata.Gif.FrameTop);
			}

			width = Math.Min(width, cont.ScreenWidth - (uint)left);
			height = Math.Min(height, cont.ScreenHeight - (uint)top);

			return (left, top, (int)width, (int)height, trans, full, disp);
		}

		private WicColorProfile getColorProfile()
		{
			var guid = default(Guid);
			HRESULT.Check(WicSource->GetPixelFormat(&guid));
			var fmt = PixelFormat.FromGuid(guid);

			uint ccc;
			if (FAILED(WicFrame->GetColorContexts(0, null, &ccc)) || ccc == 0)
				return WicColorProfile.GetDefaultFor(fmt);

			var profiles = stackalloc IWICColorContext*[(int)ccc];
			for (int i = 0; i < (int)ccc; i++)
				HRESULT.Check(Wic.Factory->CreateColorContext(&profiles[i]));

			HRESULT.Check(WicFrame->GetColorContexts(ccc, profiles, &ccc));
			var match = matchProfile(new Span<IntPtr>(profiles, (int)ccc), fmt);

			for (int i = 0; i < (int)ccc; i++)
				profiles[i]->Release();

			return match;
		}

		private WicColorProfile matchProfile(ReadOnlySpan<IntPtr> profiles, PixelFormat fmt)
		{
			var buff = (Span<byte>)stackalloc byte[8];
			foreach (var pcc in profiles)
			{
				var cc = (IWICColorContext*)pcc;

				var cct = default(WICColorContextType);
				HRESULT.Check(cc->GetType(&cct));

				if (cct == WICColorContextType.WICColorContextProfile)
				{
					uint cb;
					HRESULT.Check(cc->GetProfileBytes(0, null, &cb));

					// Don't try to read giant profiles. 4MiB should be enough, and more might indicate corrupt metadata.
					if (cb > 1024 * 1024 * 4)
						continue;

					var cpi = WicColorProfile.GetProfileFromContext(cc, cb);
					if (cpi.IsValid && cpi.IsCompatibleWith(fmt))
						return new WicColorProfile(new ComPtr<IWICColorContext>(cc), cpi, true);
				}
				else if (cct == WICColorContextType.WICColorContextExifColorSpace && WicMetadataReader is not null)
				{
					// Although WIC defines the non-standard AdobeRGB ExifColorSpace value, most software (including Adobe's) only supports the Uncalibrated/InteropIndex=R03 convention.
					// http://ninedegreesbelow.com/photography/embedded-color-space-information.html
					uint ecs;
					HRESULT.Check(cc->GetExifColorSpace(&ecs));
					if (ecs == (uint)ExifColorSpace.AdobeRGB)
						return WicColorProfile.AdobeRgb.Value;

					if (ecs == (uint)ExifColorSpace.Uncalibrated)
					{
						var r03 = (ReadOnlySpan<byte>)(new[] { (byte)'R', (byte)'0', (byte)'3' });
						var meta = ComPtr<IWICMetadataQueryReader>.Wrap(WicMetadataReader);
						if (meta.GetValueOrDefault(Container.ContainerFormat == FileFormat.Jpeg ? Wic.Metadata.InteropIndexJpeg : Wic.Metadata.InteropIndexExif, buff).SequenceEqual(r03))
							return WicColorProfile.AdobeRgb.Value;
					}
				}
			}

			return WicColorProfile.GetDefaultFor(fmt);
		}
	}
}