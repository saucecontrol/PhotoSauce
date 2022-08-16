// Copyright © Clinton Ingram and Contributors.  Licensed under the MIT License.

using System;

using TerraFX.Interop.Windows;
using static TerraFX.Interop.Windows.GUID;
using static TerraFX.Interop.Windows.Windows;

using PhotoSauce.Interop.Wic;
using PhotoSauce.MagicScaler.Transforms;

namespace PhotoSauce.MagicScaler;

internal static unsafe class WicTransforms
{
	public static void AddColorProfileReader(PipelineContext ctx)
	{
		var mode = ctx.Settings.ColorProfileMode;
		var frame = ctx.ImageFrame is WicPlanarCache pframe ? pframe.Frame : ctx.ImageFrame as WicImageFrame;
		if (frame is null || mode == ColorProfileMode.Ignore)
			return;

		var srcProfile = WicColorProfile.GetSourceProfile(frame.ColorProfileSource, mode);
		var dstProfile = WicColorProfile.GetDestProfile(srcProfile, mode);
		ctx.WicContext.SourceColorContext = new ComPtr<IWICColorContext>(srcProfile.WicColorContext).Detach();
		ctx.WicContext.DestColorContext = new ComPtr<IWICColorContext>(dstProfile.WicColorContext).Detach();
		ctx.SourceColorProfile = srcProfile.ParsedProfile;
		ctx.DestColorProfile = dstProfile.ParsedProfile;
	}

	public static void AddColorspaceConverter(PipelineContext ctx)
	{
		if (ctx.WicContext.SourceColorContext is null || ctx.WicContext.DestColorContext is null || ctx.WicContext.SourceColorContext == ctx.WicContext.DestColorContext)
			return;

		var guid = ctx.Source.Format.FormatGuid;
		using var trans = default(ComPtr<IWICColorTransform>);
		using var src = new ComPtr<IWICBitmapSource>(ctx.Source.AsIWICBitmapSource());
		HRESULT.Check(Wic.Factory->CreateColorTransformer(trans.GetAddressOf()));
		if (SUCCEEDED(trans.Get()->Initialize(src, ctx.WicContext.SourceColorContext, ctx.WicContext.DestColorContext, &guid)))
			ctx.Source = ctx.AddProfiler(((IWICBitmapSource*)trans.Detach())->AsPixelSource(ctx.Source, nameof(IWICColorTransform)));
	}

	public static void AddPixelFormatConverter(PipelineContext ctx, bool allowPbgra = true)
	{
		var curFormat = ctx.Source.Format;
		if (curFormat.ColorRepresentation == PixelColorRepresentation.Cmyk)
		{
			var rgbColorContext = ctx.Settings.ColorProfileMode == ColorProfileMode.ConvertToSrgb ? WicColorProfile.Srgb.Value : WicColorProfile.AdobeRgb.Value;
			if (ctx.WicContext.SourceColorContext is null)
				ctx.WicContext.SourceColorContext = new ComPtr<IWICColorContext>(WicColorProfile.Cmyk.Value.WicColorContext).Detach();

			// TODO WIC doesn't support proper CMYKA conversion with color profile
			if (curFormat.AlphaRepresentation == PixelAlphaRepresentation.None)
			{
				// WIC doesn't support 16bpc CMYK conversion with color profile
				if (curFormat.BitsPerPixel == 64)
					ctx.Source = ctx.AddProfiler(new ConversionTransform(ctx.Source, PixelFormat.Cmyk32));

				var guid = GUID_WICPixelFormat24bppBGR;
				using var trans = default(ComPtr<IWICColorTransform>);
				using var src = new ComPtr<IWICBitmapSource>(ctx.Source.AsIWICBitmapSource());
				HRESULT.Check(Wic.Factory->CreateColorTransformer(trans.GetAddressOf()));
				if (SUCCEEDED(trans.Get()->Initialize(src, ctx.WicContext.SourceColorContext, rgbColorContext.WicColorContext, &guid)))
				{
					ctx.Source = ctx.AddProfiler(((IWICBitmapSource*)trans.Detach())->AsPixelSource(ctx.Source, nameof(IWICColorTransform)));
					curFormat = ctx.Source.Format;
				}
			}

			ctx.DestColorProfile = ctx.SourceColorProfile = rgbColorContext.ParsedProfile;
			ctx.WicContext.Dispose();
		}

		if (curFormat == PixelFormat.Y8 || curFormat == PixelFormat.Cb8 || curFormat == PixelFormat.Cr8)
			return;

		var newFormat = PixelFormat.Bgr24;
		if (allowPbgra && curFormat.AlphaRepresentation == PixelAlphaRepresentation.Associated && ctx.Settings.BlendingMode != GammaMode.Linear && ctx.Settings.MatteColor.IsEmpty)
			newFormat = PixelFormat.Pbgra32;
		else if (curFormat.AlphaRepresentation != PixelAlphaRepresentation.None)
			newFormat = PixelFormat.Bgra32;
		else if (curFormat.ColorRepresentation == PixelColorRepresentation.Grey)
			newFormat = PixelFormat.Grey8;

		if (curFormat == newFormat)
			return;

		using var conv = default(ComPtr<IWICFormatConverter>);
		HRESULT.Check(Wic.Factory->CreateFormatConverter(conv.GetAddressOf()));

		BOOL bval;
		var cfmt = curFormat.FormatGuid;
		var nfmt = newFormat.FormatGuid;
		if (FAILED(conv.Get()->CanConvert(&cfmt, &nfmt, &bval)) || !bval)
			throw new NotSupportedException("Can't convert to destination pixel format");

		HRESULT.Check(conv.Get()->Initialize(ctx.Source.AsIWICBitmapSource(), &nfmt, WICBitmapDitherType.WICBitmapDitherTypeNone, null, 0.0, WICBitmapPaletteType.WICBitmapPaletteTypeCustom));
		ctx.Source = ctx.AddProfiler(((IWICBitmapSource*)conv.Detach())->AsPixelSource(ctx.Source, $"{nameof(IWICFormatConverter)}: {curFormat.Name}->{newFormat.Name}"));
	}

#if WICPROCESSOR
	public static void AddIndexedColorConverter(PipelineContext ctx)
	{
		var encinfo = ctx.Settings.EncoderInfo!;
		if (!encinfo.SupportsPixelFormat(PixelFormat.Indexed8.FormatGuid))
			return;

		var curFormat = ctx.Source.Format;
		var indexedOptions = ctx.Settings.EncoderOptions as IIndexedEncoderOptions;
		if (indexedOptions is null && encinfo.SupportsPixelFormat(curFormat.FormatGuid))
			return;

		indexedOptions ??= encinfo.DefaultOptions as IIndexedEncoderOptions;
		bool autoPalette256 = indexedOptions is null || (indexedOptions.MaxPaletteSize >= 256 && indexedOptions.PredefinedPalette is null);
		bool greyToIndexed = curFormat.ColorRepresentation == PixelColorRepresentation.Grey && autoPalette256;

		if (greyToIndexed || (curFormat.NumericRepresentation != PixelNumericRepresentation.Indexed && indexedOptions is not null))
		{
			using var conv = default(ComPtr<IWICFormatConverter>);
			HRESULT.Check(Wic.Factory->CreateFormatConverter(conv.GetAddressOf()));

			BOOL bval;
			var cfmt = curFormat.FormatGuid;
			var nfmt = PixelFormat.Indexed8.FormatGuid;
			if (FAILED(conv.Get()->CanConvert(&cfmt, &nfmt, &bval)) || !bval)
				throw new NotSupportedException("Can't convert to destination pixel format");

			var palType = greyToIndexed ? WICBitmapPaletteType.WICBitmapPaletteTypeFixedGray256 : WICBitmapPaletteType.WICBitmapPaletteTypeCustom;
			var ditType = greyToIndexed || indexedOptions!.Dither == DitherMode.None ? WICBitmapDitherType.WICBitmapDitherTypeNone : WICBitmapDitherType.WICBitmapDitherTypeErrorDiffusion;
			using var pal = default(ComPtr<IWICPalette>);
			HRESULT.Check(Wic.Factory->CreatePalette(pal.GetAddressOf()));

			if (greyToIndexed)
			{
				HRESULT.Check(pal.Get()->InitializePredefined(WICBitmapPaletteType.WICBitmapPaletteTypeFixedGray256, 0));
			}
			else if (indexedOptions!.PredefinedPalette is int[] cpal)
			{
				fixed (int* ppal = cpal)
					HRESULT.Check(pal.Get()->InitializeCustom((uint*)ppal, (uint)cpal.Length));
			}
			else
			{
				using var bmp = default(ComPtr<IWICBitmap>);
				HRESULT.Check(Wic.Factory->CreateBitmapFromSource(ctx.Source.AsIWICBitmapSource(), WICBitmapCreateCacheOption.WICBitmapCacheOnDemand, bmp.GetAddressOf()));
				ctx.Source = ctx.AddProfiler(((IWICBitmapSource*)bmp.Detach())->AsPixelSource(ctx.Source, nameof(IWICBitmap)));

				var pp = ctx.AddProfiler($"{nameof(IWICPalette)}.{nameof(IWICPalette.InitializeFromBitmap)}");
				pp.ResumeTiming(ctx.Source.Area);
				HRESULT.Check(pal.Get()->InitializeFromBitmap(ctx.Source.AsIWICBitmapSource(), (uint)indexedOptions.MaxPaletteSize, curFormat.AlphaRepresentation == PixelAlphaRepresentation.None ? -1 : -1));
				pp.PauseTiming();
			}

			HRESULT.Check(conv.Get()->Initialize(ctx.Source.AsIWICBitmapSource(), &nfmt, ditType, pal, 33.33, palType));
			ctx.Source = ctx.AddProfiler(((IWICBitmapSource*)conv.Detach())->AsPixelSource(ctx.Source, $"{nameof(IWICFormatConverter)}: {curFormat.Name}->{PixelFormat.Indexed8.Name}"));
		}
	}

	public static void AddExifFlipRotator(PipelineContext ctx)
	{
		if (ctx.Orientation == Orientation.Normal)
			return;

		using var rotator = default(ComPtr<IWICBitmapFlipRotator>);
		HRESULT.Check(Wic.Factory->CreateBitmapFlipRotator(rotator.GetAddressOf()));
		HRESULT.Check(rotator.Get()->Initialize(ctx.Source.AsIWICBitmapSource(), ctx.Orientation.ToWicTransformOptions()));
		ctx.Source = ctx.AddProfiler(((IWICBitmapSource*)rotator.Detach())->AsPixelSource(ctx.Source, nameof(IWICBitmapFlipRotator)));

		if (ctx.Orientation.RequiresCache())
		{
			var crop = ctx.Settings.Crop;

			using var bmp = default(ComPtr<IWICBitmap>);
			HRESULT.Check(Wic.Factory->CreateBitmapFromSourceRect(ctx.Source.AsIWICBitmapSource(), (uint)crop.X, (uint)crop.Y, (uint)crop.Width, (uint)crop.Height, bmp.GetAddressOf()));

			ctx.Source = ctx.AddProfiler(((IWICBitmapSource*)bmp.Detach())->AsPixelSource(ctx.Source, nameof(IWICBitmap)));
			ctx.Settings.Crop = ctx.Source.Area;
		}

		ctx.Orientation = Orientation.Normal;
	}

	public static void AddCropper(PipelineContext ctx)
	{
		if ((PixelArea)ctx.Settings.Crop == ctx.Source.Area)
			return;

		var rect = (WICRect)ctx.Settings.Crop;
		using var cropper = default(ComPtr<IWICBitmapClipper>);
		HRESULT.Check(Wic.Factory->CreateBitmapClipper(cropper.GetAddressOf()));
		HRESULT.Check(cropper.Get()->Initialize(ctx.Source.AsIWICBitmapSource(), &rect));

		ctx.Source = ctx.AddProfiler(((IWICBitmapSource*)cropper.Detach())->AsPixelSource(ctx.Source, nameof(IWICBitmapClipper)));
		ctx.Settings.Crop = ctx.Source.Area;
	}

	public static void AddScaler(PipelineContext ctx)
	{
		bool swap = ctx.Orientation.SwapsDimensions();
		var tsize = ctx.Settings.InnerSize;

		int width = swap ? tsize.Height : tsize.Width, height = swap ? tsize.Width : tsize.Height;
		if (ctx.Source.Width == width && ctx.Source.Height == height)
			return;

		var mode =
			ctx.Settings.Interpolation.WeightingFunction.Support < 0.1 ? WICBitmapInterpolationMode.WICBitmapInterpolationModeNearestNeighbor :
			ctx.Settings.Interpolation.WeightingFunction.Support < 1.0 ? ctx.Settings.ScaleRatio > 1.0 ? WICBitmapInterpolationMode.WICBitmapInterpolationModeFant : WICBitmapInterpolationMode.WICBitmapInterpolationModeNearestNeighbor :
			ctx.Settings.Interpolation.WeightingFunction.Support > 1.0 ? ctx.Settings.ScaleRatio > 1.0 ? WICBitmapInterpolationMode.WICBitmapInterpolationModeHighQualityCubic :WICBitmapInterpolationMode.WICBitmapInterpolationModeCubic :
			ctx.Settings.ScaleRatio > 1.0 ? WICBitmapInterpolationMode.WICBitmapInterpolationModeFant :
			WICBitmapInterpolationMode.WICBitmapInterpolationModeLinear;

		using var scaler = default(ComPtr<IWICBitmapScaler>);
		HRESULT.Check(Wic.Factory->CreateBitmapScaler(scaler.GetAddressOf()));
		HRESULT.Check(scaler.Get()->Initialize(ctx.Source.AsIWICBitmapSource(), (uint)width, (uint)height, mode));

		ctx.Source = ctx.AddProfiler(((IWICBitmapSource*)scaler.Detach())->AsPixelSource(ctx.Source, nameof(IWICBitmapScaler)));
	}

	public static void AddHybridScaler(PipelineContext ctx)
	{
		int ratio = ctx.Settings.HybridScaleRatio;
		if (ratio == 1 || ctx.Settings.Interpolation.IsPointSampler)
			return;

		uint width = (uint)MathUtil.DivCeiling(ctx.Source.Width, ratio);
		uint height = (uint)MathUtil.DivCeiling(ctx.Source.Height, ratio);

		using var transform = default(ComPtr<IWICBitmapSourceTransform>);
		if (ctx.Source is WicPixelSource wsrc && SUCCEEDED(wsrc.WicSource->QueryInterface(__uuidof<IWICBitmapSourceTransform>(), (void**)transform.GetAddressOf())))
			ctx.Source = ctx.AddProfiler(new ComPtr<IWICBitmapSource>(wsrc.WicSource).Detach()->AsPixelSource(ctx.Source, nameof(IWICBitmapFrameDecode)));

		using var scaler = default(ComPtr<IWICBitmapScaler>);
		HRESULT.Check(Wic.Factory->CreateBitmapScaler(scaler.GetAddressOf()));
		HRESULT.Check(scaler.Get()->Initialize(ctx.Source.AsIWICBitmapSource(), width, height, WICBitmapInterpolationMode.WICBitmapInterpolationModeFant));

		ctx.Source = ctx.AddProfiler(((IWICBitmapSource*)scaler.Detach())->AsPixelSource(ctx.Source, $"{nameof(IWICBitmapScaler)} (hybrid)"));
		ctx.Settings.HybridMode = HybridScaleMode.Off;
	}
#endif
}