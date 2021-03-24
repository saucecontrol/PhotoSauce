// Copyright © Clinton Ingram and Contributors.  Licensed under the MIT License.

using System;
using System.Diagnostics;

using TerraFX.Interop;
using static TerraFX.Interop.Windows;

using PhotoSauce.Interop.Wic;
using PhotoSauce.MagicScaler.Transforms;

namespace PhotoSauce.MagicScaler
{
	internal static unsafe class WicTransforms
	{
		public static readonly Guid[] PlanarPixelFormats = new[] { GUID_WICPixelFormat8bppY, GUID_WICPixelFormat8bppCb, GUID_WICPixelFormat8bppCr };

		public static void AddColorProfileReader(PipelineContext ctx)
		{
			if (ctx.ImageFrame is not WicImageFrame wicFrame || ctx.Settings.ColorProfileMode == ColorProfileMode.Ignore)
				return;

			ctx.WicContext.SourceColorContext = WicColorProfile.GetSourceProfile(wicFrame.ColorProfileSource, ctx.Settings.ColorProfileMode).WicColorContext;
			ctx.WicContext.DestColorContext = WicColorProfile.GetDestProfile(wicFrame.ColorProfileSource, ctx.Settings.ColorProfileMode).WicColorContext;
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
				ctx.Source = ctx.AddDispose(new ComPtr<IWICBitmapSource>((IWICBitmapSource*)trans.Get()).AsPixelSource(nameof(IWICColorTransform)));
		}

		public static void AddPixelFormatConverter(PipelineContext ctx, bool allowPbgra = true)
		{
			var curFormat = ctx.Source.Format;
			if (curFormat.ColorRepresentation == PixelColorRepresentation.Cmyk)
			{
				var rgbColorContext = ctx.Settings.ColorProfileMode == ColorProfileMode.ConvertToSrgb ? WicColorProfile.Srgb.Value : WicColorProfile.AdobeRgb.Value;
				if (ctx.WicContext.SourceColorContext is null)
					ctx.WicContext.SourceColorContext = WicColorProfile.Cmyk.Value.WicColorContext;

				// TODO WIC doesn't support proper CMYKA conversion with color profile
				if (curFormat.AlphaRepresentation == PixelAlphaRepresentation.None)
				{
					// WIC doesn't support 16bpc CMYK conversion with color profile
					if (curFormat.BitsPerPixel == 64)
						ctx.Source = ctx.AddDispose(new ConversionTransform(ctx.Source, null, null, PixelFormat.Cmyk32Bpp));

					var guid = GUID_WICPixelFormat24bppBGR;
					using var trans = default(ComPtr<IWICColorTransform>);
					using var src = new ComPtr<IWICBitmapSource>(ctx.Source.AsIWICBitmapSource());
					HRESULT.Check(Wic.Factory->CreateColorTransformer(trans.GetAddressOf()));
					if (SUCCEEDED(trans.Get()->Initialize(src, ctx.WicContext.SourceColorContext, rgbColorContext.WicColorContext, &guid)))
					{
						ctx.Source = ctx.AddDispose(new ComPtr<IWICBitmapSource>((IWICBitmapSource*)trans.Get()).AsPixelSource(nameof(IWICColorTransform)));
						curFormat = ctx.Source.Format;
					}
				}

				ctx.WicContext.DestColorContext = ctx.WicContext.SourceColorContext = rgbColorContext.WicColorContext;
				ctx.DestColorProfile = ctx.SourceColorProfile = rgbColorContext.ParsedProfile;
			}

			if (curFormat == PixelFormat.Y8Bpp || curFormat == PixelFormat.Cb8Bpp || curFormat == PixelFormat.Cr8Bpp)
				return;

			var newFormat = PixelFormat.Bgr24Bpp;
			if (allowPbgra && curFormat.AlphaRepresentation == PixelAlphaRepresentation.Associated && ctx.Settings.BlendingMode != GammaMode.Linear && ctx.Settings.MatteColor.IsEmpty)
				newFormat = PixelFormat.Pbgra32Bpp;
			else if (curFormat.AlphaRepresentation != PixelAlphaRepresentation.None)
				newFormat = PixelFormat.Bgra32Bpp;
			else if (curFormat.ColorRepresentation == PixelColorRepresentation.Grey)
				newFormat = PixelFormat.Grey8Bpp;

			if (curFormat == newFormat)
				return;

			using var conv = default(ComPtr<IWICFormatConverter>);
			HRESULT.Check(Wic.Factory->CreateFormatConverter(conv.GetAddressOf()));

			int bval;
			var cfmt = curFormat.FormatGuid;
			var nfmt = newFormat.FormatGuid;
			if (FAILED(conv.Get()->CanConvert(&cfmt, &nfmt, &bval)) || bval == 0)
				throw new NotSupportedException("Can't convert to destination pixel format");

			HRESULT.Check(conv.Get()->Initialize(ctx.Source.AsIWICBitmapSource(), &nfmt, WICBitmapDitherType.WICBitmapDitherTypeNone, null, 0.0, WICBitmapPaletteType.WICBitmapPaletteTypeCustom));
			ctx.Source = ctx.AddDispose(new ComPtr<IWICBitmapSource>((IWICBitmapSource*)conv.Get()).AsPixelSource($"{nameof(IWICFormatConverter)}: {curFormat.Name}->{newFormat.Name}"));
		}

		public static void AddIndexedColorConverter(PipelineContext ctx)
		{
			var curFormat = ctx.Source.Format;
			var newFormat = PixelFormat.Indexed8Bpp;

			if (!ctx.Settings.IndexedColor || curFormat.NumericRepresentation == PixelNumericRepresentation.Indexed || curFormat.ColorRepresentation == PixelColorRepresentation.Grey)
				return;

			using var conv = default(ComPtr<IWICFormatConverter>);
			HRESULT.Check(Wic.Factory->CreateFormatConverter(conv.GetAddressOf()));

			int bval;
			var cfmt = curFormat.FormatGuid;
			var nfmt = newFormat.FormatGuid;
			if (FAILED(conv.Get()->CanConvert(&cfmt, &nfmt, &bval)) || bval == 0)
				throw new NotSupportedException("Can't convert to destination pixel format");

			using var bmp = default(ComPtr<IWICBitmap>);
			HRESULT.Check(Wic.Factory->CreateBitmapFromSource(ctx.Source.AsIWICBitmapSource(), WICBitmapCreateCacheOption.WICBitmapCacheOnDemand, bmp.GetAddressOf()));
			ctx.Source = ctx.AddDispose(new ComPtr<IWICBitmapSource>((IWICBitmapSource*)bmp.Get()).AsPixelSource(nameof(IWICBitmap)));

			int tcolor = curFormat.AlphaRepresentation == PixelAlphaRepresentation.None ? 0 : -1;
			using var pal = default(ComPtr<IWICPalette>);
			HRESULT.Check(Wic.Factory->CreatePalette(pal.GetAddressOf()));

			var pp = MagicImageProcessor.EnablePixelSourceStats ? ctx.AddProfiler(new ProcessingProfiler(nameof(IWICPalette))) : NoopProfiler.Instance;
			pp.ResumeTiming(ctx.Source.Area);
			HRESULT.Check(pal.Get()->InitializeFromBitmap(ctx.Source.AsIWICBitmapSource(), 256u, tcolor));
			ctx.WicContext.DestPalette = pal.Detach();
			pp.PauseTiming();

			HRESULT.Check(conv.Get()->Initialize(ctx.Source.AsIWICBitmapSource(), &nfmt, WICBitmapDitherType.WICBitmapDitherTypeErrorDiffusion, ctx.WicContext.DestPalette, 33.33, WICBitmapPaletteType.WICBitmapPaletteTypeCustom));
			ctx.Source = ctx.AddDispose(new ComPtr<IWICBitmapSource>((IWICBitmapSource*)conv.Get()).AsPixelSource($"{nameof(IWICFormatConverter)}: {curFormat.Name}->{newFormat.Name}"));
		}

		public static void AddExifFlipRotator(PipelineContext ctx)
		{
			if (ctx.Orientation == Orientation.Normal)
				return;

			using var rotator = default(ComPtr<IWICBitmapFlipRotator>);
			HRESULT.Check(Wic.Factory->CreateBitmapFlipRotator(rotator.GetAddressOf()));
			HRESULT.Check(rotator.Get()->Initialize(ctx.Source.AsIWICBitmapSource(), ctx.Orientation.ToWicTransformOptions()));
			ctx.Source = ctx.AddDispose(new ComPtr<IWICBitmapSource>((IWICBitmapSource*)rotator.Get()).AsPixelSource(nameof(IWICBitmapFlipRotator)));

			if (ctx.Orientation.RequiresCache())
			{
				var crop = ctx.Settings.Crop;

				using var bmp = default(ComPtr<IWICBitmap>);
				HRESULT.Check(Wic.Factory->CreateBitmapFromSourceRect(ctx.Source.AsIWICBitmapSource(), (uint)crop.X, (uint)crop.Y, (uint)crop.Width, (uint)crop.Height, bmp.GetAddressOf()));

				ctx.Source = ctx.AddDispose(new ComPtr<IWICBitmapSource>((IWICBitmapSource*)bmp.Get()).AsPixelSource(nameof(IWICBitmap)));
				ctx.Settings.Crop = ctx.Source.Area.ToGdiRect();
			}

			ctx.Orientation = Orientation.Normal;
		}

		public static void AddCropper(PipelineContext ctx)
		{
			var crop = PixelArea.FromGdiRect(ctx.Settings.Crop);
			if (crop == ctx.Source.Area)
				return;

			var rect = crop.ToWicRect();
			using var cropper = default(ComPtr<IWICBitmapClipper>);
			HRESULT.Check(Wic.Factory->CreateBitmapClipper(cropper.GetAddressOf()));
			HRESULT.Check(cropper.Get()->Initialize(ctx.Source.AsIWICBitmapSource(), &rect));

			ctx.Source = ctx.AddDispose(new ComPtr<IWICBitmapSource>((IWICBitmapSource*)cropper.Get()).AsPixelSource(nameof(IWICBitmapClipper)));
			ctx.Settings.Crop = ctx.Source.Area.ToGdiRect();
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

			ctx.Source = ctx.AddDispose(new ComPtr<IWICBitmapSource>((IWICBitmapSource*)scaler.Get()).AsPixelSource(nameof(IWICBitmapScaler)));
		}

		public static void AddHybridScaler(PipelineContext ctx, int ratio = default)
		{
			ratio = ratio == default ? ctx.Settings.HybridScaleRatio : ratio;
			if (ratio == 1 || ctx.Settings.Interpolation.WeightingFunction.Support < 0.1)
				return;

			uint width = (uint)MathUtil.DivCeiling(ctx.Source.Width, ratio);
			uint height = (uint)MathUtil.DivCeiling(ctx.Source.Height, ratio);

			using var transform = default(ComPtr<IWICBitmapSourceTransform>);
			if (ctx.Source is WicPixelSource wsrc && SUCCEEDED(wsrc.WicSource->QueryInterface(__uuidof<IWICBitmapSourceTransform>(), (void**)transform.GetAddressOf())))
				ctx.Source = ctx.AddDispose(new ComPtr<IWICBitmapSource>(wsrc.WicSource).AsPixelSource(nameof(IWICBitmapFrameDecode)));

			using var scaler = default(ComPtr<IWICBitmapScaler>);
			HRESULT.Check(Wic.Factory->CreateBitmapScaler(scaler.GetAddressOf()));
			HRESULT.Check(scaler.Get()->Initialize(ctx.Source.AsIWICBitmapSource(), width, height, WICBitmapInterpolationMode.WICBitmapInterpolationModeFant));

			ctx.Source = ctx.AddDispose(new ComPtr<IWICBitmapSource>((IWICBitmapSource*)scaler.Get()).AsPixelSource(nameof(IWICBitmapScaler) + " (hybrid)"));
			ctx.Settings.HybridMode = HybridScaleMode.Off;
		}

		public static void AddNativeScaler(PipelineContext ctx)
		{
			int ratio = ctx.Settings.HybridScaleRatio;
			if (ratio == 1 || ctx.ImageFrame is not WicImageFrame wicFrame || !wicFrame.SupportsNativeScale || ctx.Source is not WicPixelSource wsrc)
				return;

			using var transform = default(ComPtr<IWICBitmapSourceTransform>);
			if (FAILED(wsrc.WicSource->QueryInterface(__uuidof<IWICBitmapSourceTransform>(), (void**)transform.GetAddressOf())))
				return;

			uint ow = (uint)ctx.Source.Width, oh = (uint)ctx.Source.Height;
			uint cw = (uint)MathUtil.DivCeiling((int)ow, ratio), ch = (uint)MathUtil.DivCeiling((int)oh, ratio);
			HRESULT.Check(transform.Get()->GetClosestSize(&cw, &ch));

			if (cw == ow && ch == oh)
				return;

			var orient = ctx.Orientation;

			using var scaler = default(ComPtr<IWICBitmapScaler>);
			HRESULT.Check(Wic.Factory->CreateBitmapScaler(scaler.GetAddressOf()));
			HRESULT.Check(scaler.Get()->Initialize(ctx.Source.AsIWICBitmapSource(), cw, ch, WICBitmapInterpolationMode.WICBitmapInterpolationModeFant));

			ctx.Source = ctx.AddDispose(new ComPtr<IWICBitmapSource>((IWICBitmapSource*)scaler.Get()).AsPixelSource(nameof(IWICBitmapSourceTransform)));
			ctx.Settings.Crop = PixelArea.FromGdiRect(ctx.Settings.Crop).DeOrient(orient, (int)ow, (int)oh).ProportionalScale((int)ow, (int)oh, (int)cw, (int)ch).ReOrient(orient, (int)cw, (int)ch).ToGdiRect();
			ctx.Settings.HybridMode = HybridScaleMode.Off;
		}

		public static void AddPlanarCache(PipelineContext ctx)
		{
			using var transform = default(ComPtr<IWICPlanarBitmapSourceTransform>);
			if (ctx.Source is not WicPixelSource wsrc || FAILED(wsrc.WicSource->QueryInterface(__uuidof<IWICPlanarBitmapSourceTransform>(), (void**)transform.GetAddressOf())))
				throw new NotSupportedException("Transform chain doesn't support planar mode.  Only JPEG Decoder, Rotator, Scaler, and PixelFormatConverter are allowed");

			int ratio = ctx.Settings.HybridScaleRatio.Clamp(1, 8);
			uint ow = (uint)ctx.Source.Width, oh = (uint)ctx.Source.Height;
			uint cw = (uint)MathUtil.DivCeiling((int)ow, ratio), ch = (uint)MathUtil.DivCeiling((int)oh, ratio);

			var desc = stackalloc WICBitmapPlaneDescription[PlanarPixelFormats.Length];
			fixed (Guid* pfmt = PlanarPixelFormats)
			{
				int bval;
				HRESULT.Check(transform.Get()->DoesSupportTransform(&cw, &ch, WICBitmapTransformOptions.WICBitmapTransformRotate0, WICPlanarOptions.WICPlanarOptionsDefault, pfmt, desc, (uint)PlanarPixelFormats.Length, &bval));
				if (bval == 0)
					throw new NotSupportedException("Requested planar transform not supported");
			}

			var crop = PixelArea.FromGdiRect(ctx.Settings.Crop).DeOrient(ctx.Orientation, (int)ow, (int)oh).ProportionalScale((int)ow, (int)oh, (int)cw, (int)ch);
			var cache = ctx.AddDispose(new WicPlanarCache(transform.Detach(), new Span<WICBitmapPlaneDescription>(desc, PlanarPixelFormats.Length), WICBitmapTransformOptions.WICBitmapTransformRotate0, cw, ch, crop));

			ctx.PlanarContext = new PipelineContext.PlanarPipelineContext(cache.SourceY, cache.SourceCb, cache.SourceCr);
			ctx.Source = ctx.PlanarContext.SourceY;
			ctx.Settings.Crop = ctx.Source.Area.ReOrient(ctx.Orientation, ctx.Source.Width, ctx.Source.Height).ToGdiRect();
			ctx.Settings.HybridMode = HybridScaleMode.Off;
		}
	}
}