using System;
using System.Drawing;
using System.Diagnostics;
using System.Collections.Generic;

namespace PhotoSauce.MagicScaler.Transforms
{
	internal static class MagicTransforms
	{
		private static readonly IReadOnlyDictionary<PixelFormat, PixelFormat> externalFormatMap = new Dictionary<PixelFormat, PixelFormat> {
			[PixelFormat.Grey32BppFloat] = PixelFormat.Grey8Bpp,
			[PixelFormat.Grey32BppLinearFloat] = PixelFormat.Grey8Bpp,
			[PixelFormat.Grey16BppLinearUQ15] = PixelFormat.Grey8Bpp,
			[PixelFormat.Y32BppFloat] = PixelFormat.Y8Bpp,
			[PixelFormat.Y32BppLinearFloat] = PixelFormat.Y8Bpp,
			[PixelFormat.Y16BppLinearUQ15] = PixelFormat.Y8Bpp,
			[PixelFormat.Bgrx128BppFloat] = PixelFormat.Bgr24Bpp,
			[PixelFormat.Bgrx128BppLinearFloat] = PixelFormat.Bgr24Bpp,
			[PixelFormat.Bgr96BppFloat] = PixelFormat.Bgr24Bpp,
			[PixelFormat.Bgr96BppLinearFloat] = PixelFormat.Bgr24Bpp,
			[PixelFormat.Bgr48BppLinearUQ15] = PixelFormat.Bgr24Bpp,
			[PixelFormat.Pbgra128BppFloat] = PixelFormat.Bgra32Bpp,
			[PixelFormat.Pbgra128BppLinearFloat] = PixelFormat.Bgra32Bpp,
			[PixelFormat.Pbgra64BppLinearUQ15] = PixelFormat.Bgra32Bpp,
			[PixelFormat.Cb32BppFloat] = PixelFormat.Cb8Bpp,
			[PixelFormat.Cr32BppFloat] = PixelFormat.Cr8Bpp
		};

		private static readonly IReadOnlyDictionary<PixelFormat, PixelFormat> internalFormatMapSimd = new Dictionary<PixelFormat, PixelFormat> {
			[PixelFormat.Grey8Bpp] = PixelFormat.Grey32BppFloat,
			[PixelFormat.Y8Bpp] = PixelFormat.Y32BppFloat,
			[PixelFormat.Bgr24Bpp] = PixelFormat.Bgrx128BppFloat,
			[PixelFormat.Bgra32Bpp] = PixelFormat.Pbgra128BppFloat,
			[PixelFormat.Pbgra32Bpp] = PixelFormat.Pbgra128BppFloat,
			[PixelFormat.Grey32BppLinearFloat] = PixelFormat.Grey32BppFloat,
			[PixelFormat.Y32BppLinearFloat] = PixelFormat.Y32BppFloat,
			[PixelFormat.Bgrx128BppLinearFloat] = PixelFormat.Bgrx128BppFloat,
			[PixelFormat.Pbgra128BppLinearFloat] = PixelFormat.Pbgra128BppFloat,
			[PixelFormat.Cb8Bpp] = PixelFormat.Cb32BppFloat,
			[PixelFormat.Cr8Bpp] = PixelFormat.Cr32BppFloat
		};

		private static readonly IReadOnlyDictionary<PixelFormat, PixelFormat> internalFormatMapLinear = new Dictionary<PixelFormat, PixelFormat> {
			[PixelFormat.Grey8Bpp] = PixelFormat.Grey16BppLinearUQ15,
			[PixelFormat.Y8Bpp] = PixelFormat.Y16BppLinearUQ15,
			[PixelFormat.Bgr24Bpp] = PixelFormat.Bgr48BppLinearUQ15,
			[PixelFormat.Bgra32Bpp] = PixelFormat.Pbgra64BppLinearUQ15
		};

		private static readonly IReadOnlyDictionary<PixelFormat, PixelFormat> internalFormatMapLinearSimd = new Dictionary<PixelFormat, PixelFormat> {
			[PixelFormat.Grey8Bpp] = PixelFormat.Grey32BppLinearFloat,
			[PixelFormat.Y8Bpp] = PixelFormat.Y32BppLinearFloat,
			[PixelFormat.Bgr24Bpp] = PixelFormat.Bgrx128BppLinearFloat,
			[PixelFormat.Bgra32Bpp] = PixelFormat.Pbgra128BppLinearFloat,
			[PixelFormat.Grey32BppFloat] = PixelFormat.Grey32BppLinearFloat,
			[PixelFormat.Y32BppFloat] = PixelFormat.Y32BppLinearFloat,
			[PixelFormat.Bgrx128BppFloat] = PixelFormat.Bgrx128BppLinearFloat,
			[PixelFormat.Pbgra128BppFloat] = PixelFormat.Pbgra128BppLinearFloat,
			[PixelFormat.Grey32BppLinearFloat] = PixelFormat.Grey32BppLinearFloat,
			[PixelFormat.Y32BppLinearFloat] = PixelFormat.Y32BppLinearFloat,
			[PixelFormat.Bgrx128BppLinearFloat] = PixelFormat.Bgrx128BppLinearFloat,
			[PixelFormat.Pbgra128BppLinearFloat] = PixelFormat.Pbgra128BppLinearFloat,
		};

		public static void AddInternalFormatConverter(PipelineContext ctx, PixelValueEncoding enc = PixelValueEncoding.Unspecified, bool allow96bppFloat = false)
		{
			var ifmt = ctx.Source.Format;
			var ofmt = ifmt;
			bool linear = enc == PixelValueEncoding.Unspecified ? ctx.Settings.BlendingMode == GammaMode.Linear : enc == PixelValueEncoding.Linear;

			if (allow96bppFloat && MagicImageProcessor.EnableSimd && ifmt == PixelFormat.Bgr24Bpp)
				ofmt = linear ? PixelFormat.Bgr96BppLinearFloat : PixelFormat.Bgr96BppFloat;
			else if (linear && (MagicImageProcessor.EnableSimd ? internalFormatMapLinearSimd : internalFormatMapLinear).TryGetValue(ifmt, out var ofmtl))
				ofmt = ofmtl;
			else if (MagicImageProcessor.EnableSimd && internalFormatMapSimd.TryGetValue(ifmt, out var ofmts))
				ofmt = ofmts;

			bool videoLevels = ifmt == PixelFormat.Y8Bpp && ctx.ImageFrame is IYccImageFrame frame && !frame.IsFullRange;

			if (ofmt == ifmt && !videoLevels)
				return;

			bool forceSrgb = (ofmt == PixelFormat.Y32BppLinearFloat || ofmt == PixelFormat.Y16BppLinearUQ15) && ctx.SourceColorProfile != ColorProfile.sRGB;

			ctx.Source = ctx.AddDispose(new ConversionTransform(ctx.Source, forceSrgb ? ColorProfile.sRGB : ctx.SourceColorProfile, forceSrgb ? ColorProfile.sRGB : ctx.DestColorProfile, ofmt, videoLevels));
		}

		public static void AddExternalFormatConverter(PipelineContext ctx)
		{
			var ifmt = ctx.Source.Format;
			if (!externalFormatMap.TryGetValue(ifmt, out var ofmt) || ofmt == ifmt)
				return;

			bool forceSrgb = (ifmt == PixelFormat.Y32BppLinearFloat || ifmt == PixelFormat.Y16BppLinearUQ15) && ctx.SourceColorProfile != ColorProfile.sRGB;

			ctx.Source = ctx.AddDispose(new ConversionTransform(ctx.Source, forceSrgb ? ColorProfile.sRGB : ctx.SourceColorProfile, forceSrgb ? ColorProfile.sRGB : ctx.DestColorProfile, ofmt));
		}

		public static void AddPlanarExternalFormatConverter(PipelineContext ctx)
		{
			Debug.Assert(ctx.PlanarContext is not null);

			AddExternalFormatConverter(ctx);
			ctx.PlanarContext.SourceY = ctx.Source;
			ctx.Source = ctx.PlanarContext.SourceCb;

			AddExternalFormatConverter(ctx);
			ctx.PlanarContext.SourceCb = ctx.Source;
			ctx.Source = ctx.PlanarContext.SourceCr;

			AddExternalFormatConverter(ctx);
			ctx.PlanarContext.SourceCr = ctx.Source;
			ctx.Source = ctx.PlanarContext.SourceY;
		}

		public static void AddHighQualityScaler(PipelineContext ctx)
		{
			bool swap = ctx.Orientation.SwapsDimensions();
			var tsize = ctx.Settings.InnerSize;

			int width = swap ? tsize.Height : tsize.Width, height = swap ? tsize.Width : tsize.Height;
			if (ctx.Source.Width == width && ctx.Source.Height == height)
				return;

			var interpolatorx = width == ctx.Source.Width ? InterpolationSettings.NearestNeighbor : ctx.Settings.Interpolation;
			var interpolatory = height == ctx.Source.Height ? InterpolationSettings.NearestNeighbor : ctx.Settings.Interpolation;
			if (interpolatorx.WeightingFunction.Support >= 0.1 || interpolatory.WeightingFunction.Support >= 0.1)
				AddInternalFormatConverter(ctx, allow96bppFloat: true);

			var fmt = ctx.Source.Format;
			bool offsetX = false, offsetY = false;
			if (ctx.ImageFrame is IYccImageFrame frame && ctx.PlanarContext is not null && fmt.Encoding == PixelValueEncoding.Unspecified)
			{
				offsetX = frame.ChromaPosition.HasFlag(ChromaPosition.CositedHorizontal) && ctx.PlanarContext.ChromaSubsampling.IsSubsampledX();
				offsetY = frame.ChromaPosition.HasFlag(ChromaPosition.CositedVertical) && ctx.PlanarContext.ChromaSubsampling.IsSubsampledY();
			}

			if (fmt.NumericRepresentation == PixelNumericRepresentation.Float)
				ctx.Source = ctx.AddDispose(ConvolutionTransform<float, float>.CreateResize(ctx.Source, width, height, interpolatorx, interpolatory, offsetX, offsetY));
			else if (fmt.NumericRepresentation == PixelNumericRepresentation.Fixed)
				ctx.Source = ctx.AddDispose(ConvolutionTransform<ushort, int>.CreateResize(ctx.Source, width, height, interpolatorx, interpolatory, offsetX, offsetY));
			else
				ctx.Source = ctx.AddDispose(ConvolutionTransform<byte, int>.CreateResize(ctx.Source, width, height, interpolatorx, interpolatory, offsetX, offsetY));
		}

		public static void AddPlanarHighQualityScaler(PipelineContext ctx, ChromaSubsampleMode subsample)
		{
			Debug.Assert(ctx.PlanarContext is not null);

			AddHighQualityScaler(ctx);
			ctx.PlanarContext.SourceY = ctx.Source;
			ctx.Source = ctx.PlanarContext.SourceCb;

			if (subsample.IsSubsampledX())
				ctx.Settings.InnerSize.Width = MathUtil.DivCeiling(ctx.Settings.InnerSize.Width, 2);
			if (subsample.IsSubsampledY())
				ctx.Settings.InnerSize.Height = MathUtil.DivCeiling(ctx.Settings.InnerSize.Height, 2);

			AddHighQualityScaler(ctx);
			ctx.PlanarContext.SourceCb = ctx.Source;
			ctx.Source = ctx.PlanarContext.SourceCr;

			AddHighQualityScaler(ctx);
			ctx.PlanarContext.SourceCr = ctx.Source;
			ctx.Source = ctx.PlanarContext.SourceY;
		}

		public static void AddHybridScaler(PipelineContext ctx, int ratio = default)
		{
			ratio = ratio == default ? ctx.Settings.HybridScaleRatio : ratio;
			if (ratio == 1 || ctx.Settings.Interpolation.WeightingFunction.Support < 0.1 || ctx.Source.Format.BitsPerPixel / ctx.Source.Format.ChannelCount != 8)
				return;

			ctx.Source = ctx.AddDispose(new HybridScaleTransform(ctx.Source, ratio));
			ctx.Settings.HybridMode = HybridScaleMode.Off;
		}

		public static void AddPlanarHybridScaler(PipelineContext ctx)
		{
			Debug.Assert(ctx.PlanarContext is not null);

			int ratio = ctx.Settings.HybridScaleRatio;
			if (ratio == 1)
				return;

			int ratioX = (ctx.Source.Width + 1) / ctx.PlanarContext.SourceCb.Width;
			int ratioY = (ctx.Source.Height + 1) / ctx.PlanarContext.SourceCb.Height;
			int ratioC = ratio / Math.Min(ratioX, ratioY);

			AddHybridScaler(ctx, ratio);
			ctx.PlanarContext.SourceY = ctx.Source;
			ctx.Source = ctx.PlanarContext.SourceCb;

			AddHybridScaler(ctx, ratioC);
			ctx.PlanarContext.SourceCb = ctx.Source;
			ctx.Source = ctx.PlanarContext.SourceCr;

			AddHybridScaler(ctx, ratioC);
			ctx.PlanarContext.SourceCr = ctx.Source;
			ctx.Source = ctx.PlanarContext.SourceY;
		}

		public static void AddUnsharpMask(PipelineContext ctx)
		{
			var ss = ctx.Settings.UnsharpMask;
			if (!ctx.Settings.Sharpen || ss.Radius <= 0d || ss.Amount <= 0)
				return;

			var fmt = ctx.Source.Format;
			if (fmt.NumericRepresentation == PixelNumericRepresentation.Float)
				ctx.Source = ctx.AddDispose(UnsharpMaskTransform<float, float>.CreateSharpen(ctx.Source, ss));
			else if (fmt.NumericRepresentation == PixelNumericRepresentation.Fixed)
				ctx.Source = ctx.AddDispose(UnsharpMaskTransform<ushort, int>.CreateSharpen(ctx.Source, ss));
			else
				ctx.Source = ctx.AddDispose(UnsharpMaskTransform<byte, int>.CreateSharpen(ctx.Source, ss));
		}

		public static void AddMatte(PipelineContext ctx)
		{
			var fmt = ctx.Source.Format;
			if (ctx.Settings.MatteColor.IsEmpty || fmt.ColorRepresentation != PixelColorRepresentation.Bgr || fmt.AlphaRepresentation == PixelAlphaRepresentation.None)
				return;

			if (fmt.NumericRepresentation == PixelNumericRepresentation.Float && fmt.Encoding == PixelValueEncoding.Companded)
				AddInternalFormatConverter(ctx, PixelValueEncoding.Linear);

			ctx.Source = new MatteTransform(ctx.Source, ctx.Settings.MatteColor, !ctx.IsAnimatedGifPipeline);

			if (!ctx.IsAnimatedGifPipeline && ctx.Source.Format.AlphaRepresentation != PixelAlphaRepresentation.None && !ctx.Settings.MatteColor.IsTransparent())
			{
				var oldFmt = ctx.Source.Format;
				var newFmt = oldFmt == PixelFormat.Pbgra64BppLinearUQ15 ? PixelFormat.Bgr48BppLinearUQ15
					: oldFmt == PixelFormat.Bgra32Bpp ? PixelFormat.Bgr24Bpp
					: throw new NotSupportedException("Unsupported pixel format");

				ctx.Source = ctx.AddDispose(new ConversionTransform(ctx.Source, null, null, newFmt));
			}
		}

		public static void AddPad(PipelineContext ctx)
		{
			if (ctx.Settings.InnerSize == ctx.Settings.OuterSize)
				return;

			AddExternalFormatConverter(ctx);

			var fmt = ctx.Source.Format;
			if (fmt.AlphaRepresentation == PixelAlphaRepresentation.None && ctx.Settings.MatteColor.IsTransparent())
				ctx.Source = ctx.AddDispose(new ConversionTransform(ctx.Source, null, null, PixelFormat.Bgra32Bpp));
			else if (fmt.ColorRepresentation == PixelColorRepresentation.Grey && !ctx.Settings.MatteColor.IsGrey())
				ctx.Source = ctx.AddDispose(new ConversionTransform(ctx.Source, null, null, PixelFormat.Bgr24Bpp));

			ctx.Source = new PadTransformInternal(ctx.Source, ctx.Settings.MatteColor, PixelArea.FromGdiRect(ctx.Settings.InnerRect), PixelArea.FromGdiSize(ctx.Settings.OuterSize));
		}

		public static void AddCropper(PipelineContext ctx, PixelArea area = default)
		{
			var crop = area.IsEmpty ? PixelArea.FromGdiRect(ctx.Settings.Crop).DeOrient(ctx.Orientation, ctx.Source.Width, ctx.Source.Height) : area;
			if (crop == ctx.Source.Area)
				return;

			ctx.Source = new CropTransform(ctx.Source, crop);
		}

		public static void AddPlanarCropper(PipelineContext ctx)
		{
			Debug.Assert(ctx.PlanarContext is not null);

			var crop = PixelArea.FromGdiRect(ctx.Settings.Crop).DeOrient(ctx.Orientation, ctx.Source.Width, ctx.Source.Height);
			if (crop == ctx.Source.Area)
				return;

			int yw = ctx.Source.Width;
			int yh = ctx.Source.Height;
			int cw = ctx.PlanarContext.SourceCb.Width;
			int ch = ctx.PlanarContext.SourceCb.Height;

			int ratioX = (yw + 1) / cw;
			int ratioY = (yh + 1) / ch;

			int scropX = MathUtil.PowerOfTwoFloor(crop.X, ratioX);
			int scropY = MathUtil.PowerOfTwoFloor(crop.Y, ratioY);
			int scropW = Math.Min(MathUtil.PowerOfTwoCeiling(crop.Width, ratioX), yw - scropX);
			int scropH = Math.Min(MathUtil.PowerOfTwoCeiling(crop.Height, ratioY), yh - scropY);
			var scrop = new PixelArea(scropX, scropY, scropW, scropH);

			AddCropper(ctx, scrop);
			ctx.PlanarContext.SourceY = ctx.Source;
			ctx.Source = ctx.PlanarContext.SourceCb;

			cw = Math.Min(MathUtil.DivCeiling(scrop.Width, ratioX), cw);
			ch = Math.Min(MathUtil.DivCeiling(scrop.Height, ratioY), ch);
			scrop = new PixelArea(scrop.X / ratioX, scrop.Y / ratioY, cw, ch);

			AddCropper(ctx, scrop);
			ctx.PlanarContext.SourceCb = ctx.Source;
			ctx.Source = ctx.PlanarContext.SourceCr;

			AddCropper(ctx, scrop);
			ctx.PlanarContext.SourceCr = ctx.Source;
			ctx.Source = ctx.PlanarContext.SourceY;
		}

		public static void AddFlipRotator(PipelineContext ctx, Orientation orientation)
		{
			if (orientation == Orientation.Normal)
				return;

			AddExternalFormatConverter(ctx);

			ctx.Source = new OrientationTransformInternal(ctx.Source, orientation);
			ctx.Orientation = Orientation.Normal;
		}

		public static void AddExifFlipRotator(PipelineContext ctx) => AddFlipRotator(ctx, ctx.Orientation);

		public static void AddPlanarExifFlipRotator(PipelineContext ctx)
		{
			Debug.Assert(ctx.PlanarContext is not null);

			var orientation = ctx.Orientation;
			if (orientation == Orientation.Normal)
				return;

			AddFlipRotator(ctx, orientation);
			ctx.PlanarContext.SourceY = ctx.Source;
			ctx.Source = ctx.PlanarContext.SourceCb;

			AddFlipRotator(ctx, orientation);
			ctx.PlanarContext.SourceCb = ctx.Source;
			ctx.Source = ctx.PlanarContext.SourceCr;

			AddFlipRotator(ctx, orientation);
			ctx.PlanarContext.SourceCr = ctx.Source;
			ctx.Source = ctx.PlanarContext.SourceY;
		}

		public static void AddColorProfileReader(PipelineContext ctx)
		{
			var mode = ctx.Settings.ColorProfileMode;
			if (mode == ColorProfileMode.Ignore)
				return;

			var fmt = ctx.ImageFrame is IYccImageFrame ? PixelFormat.Bgr24Bpp : ctx.Source.Format;

			if (ctx.ImageFrame is WicImageFrame wicFrame)
			{
				var profile = WicColorProfile.GetSourceProfile(wicFrame.ColorProfileSource, mode);
				ctx.SourceColorProfile = profile.ParsedProfile;
				ctx.WicContext.SourceColorContext = profile.WicColorContext;
				ctx.WicContext.DestColorContext = WicColorProfile.GetDestProfile(wicFrame.ColorProfileSource, mode).WicColorContext;
			}
			else
			{
				var profile = ColorProfile.Cache.GetOrAdd(ctx.ImageFrame.IccProfile);
				ctx.SourceColorProfile = profile.IsValid && profile.IsCompatibleWith(fmt) ? ColorProfile.GetSourceProfile(profile, mode) : ColorProfile.GetDefaultFor(fmt);
			}

			ctx.DestColorProfile = ColorProfile.GetDestProfile(ctx.SourceColorProfile, mode);
		}

		public static void AddColorspaceConverter(PipelineContext ctx)
		{
			if (ctx.SourceColorProfile is null || ctx.DestColorProfile is null || ctx.SourceColorProfile == ctx.DestColorProfile)
				return;

			if (ctx.SourceColorProfile.ProfileType > ColorProfileType.Matrix || ctx.DestColorProfile.ProfileType > ColorProfileType.Matrix)
			{
				AddExternalFormatConverter(ctx);

				ctx.WicContext.SourceColorContext ??= ctx.WicContext.AddRef(WicColorProfile.CreateContextFromProfile(ctx.SourceColorProfile.ProfileBytes));
				ctx.WicContext.DestColorContext ??= ctx.WicContext.AddRef(WicColorProfile.CreateContextFromProfile(ctx.DestColorProfile.ProfileBytes));

				WicTransforms.AddColorspaceConverter(ctx);

				return;
			}

			AddInternalFormatConverter(ctx, PixelValueEncoding.Linear);

			if (ctx.Source.Format.ColorRepresentation == PixelColorRepresentation.Bgr && ctx.SourceColorProfile is MatrixProfile srcProf && ctx.DestColorProfile is MatrixProfile dstProf)
			{
				var matrix = srcProf.Matrix * dstProf.InverseMatrix;
				if (matrix != default && !matrix.IsIdentity)
					ctx.Source = new ColorMatrixTransformInternal(ctx.Source, matrix);
			}
		}

		public static void AddPlanarConverter(PipelineContext ctx)
		{
			Debug.Assert(ctx.PlanarContext is not null);

			if (ctx.Source.Format.Encoding == PixelValueEncoding.Linear || ctx.PlanarContext.SourceCb.Format.NumericRepresentation != ctx.Source.Format.NumericRepresentation)
			{
				if (ctx.Source.Format.NumericRepresentation == PixelNumericRepresentation.Float && ctx.PlanarContext.SourceCb.Format.NumericRepresentation == ctx.Source.Format.NumericRepresentation)
					AddInternalFormatConverter(ctx, PixelValueEncoding.Companded);
				else
					AddPlanarExternalFormatConverter(ctx);
			}

			var matrix = YccMatrix.Rec601;
			bool videoLevels = false;

			if (ctx.ImageFrame is IYccImageFrame frame)
			{
				matrix = frame.RgbYccMatrix;
				videoLevels = !frame.IsFullRange;
			}

			ctx.Source = ctx.AddDispose(new PlanarConversionTransform(ctx.Source, ctx.PlanarContext.SourceCb, ctx.PlanarContext.SourceCr, matrix, videoLevels));
			ctx.PlanarContext = null;
		}

		public static void AddGifFrameBuffer(PipelineContext ctx, bool replay = true)
		{
			if (ctx.ImageFrame is not WicImageFrame wicFrame || wicFrame.Container is not WicGifContainer gif)
				return;

			Debug.Assert(wicFrame.WicMetadataReader is not null);

			if (replay && ctx.Settings.FrameIndex > 0)
				WicImageFrame.ReplayGifAnimationContext(gif, ctx.Settings.FrameIndex - 1);

			var finfo = WicImageFrame.GetGifFrameInfo(gif, wicFrame.WicSource, wicFrame.WicMetadataReader);
			if (finfo.Disposal == GifDisposalMethod.RestorePrevious && ctx.Settings.FrameIndex == 0)
				finfo.Disposal = GifDisposalMethod.Preserve;

			var ldisp = gif.AnimationContext?.LastDisposal ?? GifDisposalMethod.RestoreBackground;

			bool useBuffer = !replay && finfo.Disposal == GifDisposalMethod.Preserve;
			if (!replay)
			{
				var anictx = gif.AnimationContext ??= new GifAnimationContext();

				if (finfo.Disposal == GifDisposalMethod.Preserve)
					WicImageFrame.UpdateGifAnimationContext(gif, wicFrame.WicSource, wicFrame.WicMetadataReader);

				anictx.LastDisposal = finfo.Disposal;
				anictx.LastFrame = ctx.Settings.FrameIndex;

				ldisp = finfo.Disposal;
			}

			if (gif.AnimationContext is not null && gif.AnimationContext.FrameBufferSource is not null && ldisp != GifDisposalMethod.RestoreBackground)
				ctx.Source = gif.AnimationContext.FrameBufferSource;

			if (!finfo.FullScreen && ldisp == GifDisposalMethod.RestoreBackground && !useBuffer)
			{
				var innerArea = new PixelArea(finfo.Left, finfo.Top, wicFrame.Source.Width, wicFrame.Source.Height);
				var outerArea = new PixelArea(0, 0, gif.ScreenWidth, gif.ScreenHeight);
				var bgColor = finfo.Alpha ? Color.Empty : Color.FromArgb((int)gif.BackgroundColor);

				ctx.Source = new PadTransformInternal(wicFrame.Source, bgColor, innerArea, outerArea, true);
			}
			else if (ldisp != GifDisposalMethod.RestoreBackground && !useBuffer)
			{
				Debug.Assert(gif.AnimationContext?.FrameBufferSource is not null);

				var ani = gif.AnimationContext;
				var fbuff = ani.FrameBufferSource;

				ani.FrameOverlay?.Dispose();
				ani.FrameOverlay = new OverlayTransform(fbuff, wicFrame.Source, finfo.Left, finfo.Top, finfo.Alpha);

				ctx.Source = ani.FrameOverlay;
			}
		}
	}
}
