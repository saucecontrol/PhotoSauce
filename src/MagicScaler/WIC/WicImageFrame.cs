// Copyright © Clinton Ingram and Contributors.  Licensed under the MIT License.

using System;
using System.Drawing;
using System.Diagnostics.CodeAnalysis;

using TerraFX.Interop;
using static TerraFX.Interop.Windows;

using PhotoSauce.Interop.Wic;

namespace PhotoSauce.MagicScaler
{
	internal unsafe class WicImageFrame : IImageFrame, IMetadataSource
	{
		private WicPixelSource? psource;
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

		public PixelSource Source => psource ??= new ComPtr<IWICBitmapSource>(WicSource).AsPixelSource(nameof(IWICBitmapFrameDecode), false);

		public IPixelSource PixelSource => Source;

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

			if (MagicImageProcessor.EnablePlanarPipeline && (Container.Options is not IPlanarDecoderOptions opt || opt.AllowPlanar))
			{
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
			}

			var guid = default(Guid);
			HRESULT.Check(source.Get()->GetPixelFormat(&guid));
			if (PixelFormat.FromGuid(guid).NumericRepresentation == PixelNumericRepresentation.Indexed)
			{
				var newFormat = PixelFormat.Bgr24;
				if (Container is WicGifContainer gif && gif.IsAnimation)
				{
					newFormat = PixelFormat.Bgra32;
				}
				else
				{
					using var pal = default(ComPtr<IWICPalette>);
					HRESULT.Check(Wic.Factory->CreatePalette(pal.GetAddressOf()));
					HRESULT.Check(source.Get()->CopyPalette(pal));

					int bval;
					if (SUCCEEDED(pal.Get()->HasAlpha(&bval)) && bval != 0)
						newFormat = PixelFormat.Bgra32;
					else if ((SUCCEEDED(pal.Get()->IsGrayscale(&bval)) && bval != 0) || (SUCCEEDED(pal.Get()->IsBlackWhite(&bval)) && bval != 0))
						newFormat = PixelFormat.Grey8;
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

		public virtual bool TryGetMetadata<T>([NotNullWhen(true)] out T? metadata) where T : IMetadata => Container.TryGetMetadata(out metadata);

		public void Dispose()
		{
			dispose(true);
			GC.SuppressFinalize(this);
		}

		private void dispose(bool disposing)
		{
			if (WicFrame is null)
				return;

			if (disposing)
			{
				colorProfile?.Dispose();
				psource?.Dispose();
			}

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

		~WicImageFrame() => dispose(false);

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
				using var cc = new ComPtr<IWICColorContext>((IWICColorContext*)pcc);
				var cct = default(WICColorContextType);
				HRESULT.Check(cc.Get()->GetType(&cct));

				if (cct == WICColorContextType.WICColorContextProfile)
				{
					uint cb;
					HRESULT.Check(cc.Get()->GetProfileBytes(0, null, &cb));

					// Don't try to read giant profiles. 4MiB should be enough, and more might indicate corrupt metadata.
					if (cb > 1024 * 1024 * 4)
						continue;

					var cpi = WicColorProfile.GetProfileFromContext(cc, cb);
					if (cpi.IsValid && cpi.IsCompatibleWith(fmt))
						return new WicColorProfile(cc.Detach(), cpi, true);
				}
				else if (cct == WICColorContextType.WICColorContextExifColorSpace && WicMetadataReader is not null)
				{
					// Although WIC defines the non-standard AdobeRGB ExifColorSpace value, most software (including Adobe's) only supports the
					// Uncalibrated/InteropIndex=R03 convention. See http://ninedegreesbelow.com/photography/embedded-color-space-information.html
					uint ecs;
					HRESULT.Check(cc.Get()->GetExifColorSpace(&ecs));
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

	internal sealed unsafe class WicGifFrame : WicImageFrame
	{
		public readonly AnimationFrame AnimationMetadata;
		public readonly Size Size;

		public WicGifFrame(WicGifContainer cont, uint index) : base(cont, index)
		{
			using var meta = new ComPtr<IWICMetadataQueryReader>(WicMetadataReader);

			uint width, height;
			HRESULT.Check(WicFrame->GetSize(&width, &height));

			int left = 0, top = 0;
			bool full = width >= cont.AnimationMetadata.ScreenWidth && height >= cont.AnimationMetadata.ScreenHeight;
			if (!full)
			{
				left = meta.GetValueOrDefault<ushort>(Wic.Metadata.Gif.FrameLeft);
				top = meta.GetValueOrDefault<ushort>(Wic.Metadata.Gif.FrameTop);
			}

			width = (uint)Math.Min(width, cont.AnimationMetadata.ScreenWidth - left);
			height = (uint)Math.Min(height, cont.AnimationMetadata.ScreenHeight - top);
			Size = new((int)width, (int)height);

			var duration = new Rational(meta.GetValueOrDefault<ushort>(Wic.Metadata.Gif.FrameDelay), 100);
			var disposal = ((FrameDisposalMethod)meta.GetValueOrDefault<byte>(Wic.Metadata.Gif.FrameDisposal)).Clamp();
			var hasAlpha = meta.GetValueOrDefault<bool>(Wic.Metadata.Gif.TransparencyFlag);

			if (index == 0 && disposal == FrameDisposalMethod.RestorePrevious)
				disposal = FrameDisposalMethod.Preserve;

			AnimationMetadata = new(left, top, duration, disposal, hasAlpha);
		}

		public override bool TryGetMetadata<T>(out T metadata)
		{
			if (typeof(T) == typeof(AnimationFrame))
			{
				metadata = (T)(object)AnimationMetadata;
				return true;
			}

			return base.TryGetMetadata(out metadata!);
		}
	}
}