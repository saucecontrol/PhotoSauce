// Copyright © Clinton Ingram and Contributors.  Licensed under the MIT License.

using System;
using System.Diagnostics.CodeAnalysis;

using TerraFX.Interop.Windows;
using static TerraFX.Interop.Windows.Windows;

using PhotoSauce.Interop.Wic;

namespace PhotoSauce.MagicScaler;

internal unsafe class WicImageFrame : IScaledDecoder, IMetadataSource
{
	private WicFramePixelSource? pixsrc;
	private WicColorProfile? colorProfile;

	public readonly WicImageContainer Container;

	public IWICBitmapFrameDecode* WicFrame { get; private set; }
	public IWICBitmapSource* WicSource { get; private set; }
	public IWICMetadataQueryReader* WicMetadataReader { get; private set; }

	public IPixelSource PixelSource => pixsrc ??= new WicFramePixelSource(this);

	public WicColorProfile ColorProfileSource => colorProfile ??= getColorProfile();

	public WicImageFrame(WicImageContainer decoder, uint index)
	{
		Container = decoder;

		using var frame = default(ComPtr<IWICBitmapFrameDecode>);
		HRESULT.Check(decoder.WicDecoder->GetFrame(index, frame.GetAddressOf()));

		using var source = new ComPtr<IWICBitmapSource>((IWICBitmapSource*)frame.Get());

		uint frameWidth, frameHeight;
		HRESULT.Check(frame.Get()->GetSize(&frameWidth, &frameHeight));

		using var metareader = default(ComPtr<IWICMetadataQueryReader>);
		if (SUCCEEDED(frame.Get()->GetMetadataQueryReader(metareader.GetAddressOf())))
			WicMetadataReader = metareader.Detach();

		if (index == 0 && Container.Options is CameraRawDecoderOptions ropt && ropt.UsePreview != RawPreviewMode.Never)
		{
			using var preview = default(ComPtr<IWICBitmapSource>);
			if (SUCCEEDED(decoder.WicDecoder->GetPreview(preview.GetAddressOf())))
			{
				uint pw, ph;
				HRESULT.Check(preview.Get()->GetSize(&pw, &ph));

				if (ropt.UsePreview == RawPreviewMode.Always || (pw == frameWidth && ph == frameHeight))
				{
					(frameWidth, frameHeight) = (pw, ph);
					source.Attach(preview.Detach());
				}
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

				BOOL bval;
				if (SUCCEEDED(pal.Get()->HasAlpha(&bval)) && bval)
					newFormat = PixelFormat.Bgra32;
				else if ((SUCCEEDED(pal.Get()->IsGrayscale(&bval)) && bval) || (SUCCEEDED(pal.Get()->IsBlackWhite(&bval)) && bval))
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

	public virtual bool TryGetMetadata<T>([NotNullWhen(true)] out T? metadata) where T : IMetadata
	{
		if (typeof(T) == typeof(ResolutionMetadata))
		{
			double dpix, dpiy;
			HRESULT.Check(WicFrame->GetResolution(&dpix, &dpiy));

			metadata = (T)(object)(new ResolutionMetadata(dpix.ToRational(), dpiy.ToRational(), ResolutionUnit.Inch));
			return true;
		}

		if (typeof(T) == typeof(OrientationMetadata))
		{
			var orient = Orientation.Normal;
			if (WicMetadataReader is not null)
			{
				string orientationPath =
					MagicImageProcessor.EnableXmpOrientation ? Wic.Metadata.OrientationWindowsPolicy :
					Container.MimeType == ImageMimeTypes.Jpeg ? Wic.Metadata.OrientationJpeg :
					Container.MimeType == ImageMimeTypes.Heic ? Wic.Metadata.OrientationHeif :
					Wic.Metadata.OrientationExif;

				orient = ((Orientation)WicMetadataReader->GetValueOrDefault<ushort>(orientationPath)).Clamp();
			}

			metadata = (T)(object)(new OrientationMetadata(orient));
			return true;
		}

		metadata = default;
		return false;
	}

	public (int width, int height) SetDecodeScale(int ratio)
	{
		int ow = PixelSource.Width, oh = PixelSource.Height;

		using var transform = default(ComPtr<IWICBitmapSourceTransform>);
		if (FAILED(WicSource->QueryInterface(__uuidof<IWICBitmapSourceTransform>(), (void**)transform.GetAddressOf())))
			return (ow, oh);

		// WIC HEIF decoder will report any size as valid but then fail on CopyPixels if the scale ratio is greater than 8:1
		if (Container.MimeType == ImageMimeTypes.Heic)
			ratio = ratio.Clamp(1, 8);

		uint cw = (uint)MathUtil.DivCeiling(ow, ratio), ch = (uint)MathUtil.DivCeiling(oh, ratio);
		HRESULT.Check(transform.Get()->GetClosestSize(&cw, &ch));

		if (cw != (uint)ow && ch != (uint)oh)
		{
			using var scaler = default(ComPtr<IWICBitmapScaler>);
			HRESULT.Check(Wic.Factory->CreateBitmapScaler(scaler.GetAddressOf()));
			HRESULT.Check(scaler.Get()->Initialize(WicSource, cw, ch, WICBitmapInterpolationMode.WICBitmapInterpolationModeFant));

			WicSource->Release();
			WicSource = (IWICBitmapSource*)scaler.Detach();

			pixsrc!.UpdateSize();
		}

		return ((int)cw, (int)ch);
	}

	protected virtual void Dispose(bool disposing)
	{
		if (WicFrame is null)
			return;

		if (disposing)
		{
			colorProfile?.Dispose();
			pixsrc?.Dispose();
			GC.SuppressFinalize(this);
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

	public void Dispose() => Dispose(true);

	~WicImageFrame()
	{
		ThrowHelper.ThrowIfFinalizerExceptionsEnabled(nameof(WicImageFrame));

		Dispose(false);
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
					if (WicMetadataReader->GetValueOrDefault(Container.MimeType == ImageMimeTypes.Jpeg ? Wic.Metadata.InteropIndexJpeg : Wic.Metadata.InteropIndexExif, buff).SequenceEqual("R03"u8))
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

	public WicGifFrame(WicGifContainer cont, uint index) : base(cont, index)
	{
		using var meta = new ComPtr<IWICMetadataQueryReader>(WicMetadataReader);

		uint width, height;
		HRESULT.Check(WicFrame->GetSize(&width, &height));

		int left = 0, top = 0;
		if (width < cont.AnimationMetadata.ScreenWidth || height < cont.AnimationMetadata.ScreenHeight)
		{
			left = meta.Get()->GetValueOrDefault<ushort>(Wic.Metadata.Gif.FrameLeft);
			top = meta.Get()->GetValueOrDefault<ushort>(Wic.Metadata.Gif.FrameTop);
		}

		var duration = new Rational(meta.Get()->GetValueOrDefault<ushort>(Wic.Metadata.Gif.FrameDelay), 100);
		var disposal = ((FrameDisposalMethod)meta.Get()->GetValueOrDefault<byte>(Wic.Metadata.Gif.FrameDisposal)).Clamp();
		var hasAlpha = meta.Get()->GetValueOrDefault<bool>(Wic.Metadata.Gif.TransparencyFlag);

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

internal sealed unsafe class WicPlanarFrame : WicImageFrame, IPlanarDecoder
{
	public IWICPlanarBitmapSourceTransform* WicPlanarTransform { get; private set; }

	public WicPlanarFrame(WicImageContainer cont, uint index) : base(cont, index)
	{
		void* ptrans;
		if (SUCCEEDED(WicSource->QueryInterface(__uuidof<IWICPlanarBitmapSourceTransform>(), &ptrans)))
			WicPlanarTransform = (IWICPlanarBitmapSourceTransform*)ptrans;
	}

	public bool TryGetYccFrame([NotNullWhen(true)] out IYccImageFrame? frame)
	{
		if (WicPlanarTransform is not null)
		{
			var fmts = WicPlanarCache.PlanarPixelFormats;
			var desc = stackalloc WICBitmapPlaneDescription[fmts.Length];
			fixed (Guid* pfmt = fmts)
			{
				BOOL bval;
				uint ow, oh;
				HRESULT.Check(WicSource->GetSize(&ow, &oh));
				HRESULT.Check(WicPlanarTransform->DoesSupportTransform(
					&ow, &oh, WICBitmapTransformOptions.WICBitmapTransformRotate0, WICPlanarOptions.WICPlanarOptionsDefault,
					pfmt, desc, (uint)fmts.Length, &bval
				));

				if (bval)
				{
					frame = new WicPlanarCache(this, new ReadOnlySpan<WICBitmapPlaneDescription>(desc, fmts.Length));
					return true;
				}
			}
		}

		frame = null;
		return false;
	}

	protected override void Dispose(bool disposing)
	{
		if (WicPlanarTransform is not null)
		{
			WicPlanarTransform->Release();
			WicPlanarTransform = null;
		}

		base.Dispose(disposing);
	}
}