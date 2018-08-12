using System;

using PhotoSauce.MagicScaler.Interop;

namespace PhotoSauce.MagicScaler
{
	internal static class MagicTransforms
	{
		public static void AddInternalFormatConverter(WicProcessingContext ctx, bool allow96bppFloat = false, bool forceLinear = false)
		{
			var ifmt = ctx.Source.Format.FormatGuid;
			var ofmt = ifmt;
			bool linear = forceLinear || ctx.Settings.BlendingMode == GammaMode.Linear;

			if (MagicImageProcessor.EnableSimd)
			{
				if (ifmt == Consts.GUID_WICPixelFormat8bppGray)
					ofmt = linear ? PixelFormat.Grey32BppLinearFloat.FormatGuid : PixelFormat.Grey32BppFloat.FormatGuid;
				else if (ifmt == Consts.GUID_WICPixelFormat8bppY)
					ofmt = linear ? PixelFormat.Y32BppLinearFloat.FormatGuid : PixelFormat.Y32BppFloat.FormatGuid;
				else if (ifmt == Consts.GUID_WICPixelFormat24bppBGR && allow96bppFloat)
					ofmt = linear ? PixelFormat.Bgr96BppLinearFloat.FormatGuid : PixelFormat.Bgr96BppFloat.FormatGuid;
				else if (ifmt == Consts.GUID_WICPixelFormat24bppBGR)
					ofmt = linear ? PixelFormat.Bgrx128BppLinearFloat.FormatGuid : PixelFormat.Bgrx128BppFloat.FormatGuid;
				else if (ifmt == Consts.GUID_WICPixelFormat32bppBGRA)
					ofmt = linear ? PixelFormat.Pbgra128BppLinearFloat.FormatGuid : PixelFormat.Pbgra128BppFloat.FormatGuid;
				else if (ifmt == Consts.GUID_WICPixelFormat32bppPBGRA)
					ofmt = PixelFormat.Pbgra128BppFloat.FormatGuid;
				else if (ifmt == Consts.GUID_WICPixelFormat16bppCbCr)
					ofmt = PixelFormat.CbCr64BppFloat.FormatGuid;
			}
			else if (linear)
			{
				if (ifmt == Consts.GUID_WICPixelFormat8bppGray)
					ofmt = PixelFormat.Grey16BppLinearUQ15.FormatGuid;
				else if (ifmt == Consts.GUID_WICPixelFormat8bppY)
					ofmt = PixelFormat.Y16BppLinearUQ15.FormatGuid;
				else if (ifmt == Consts.GUID_WICPixelFormat24bppBGR)
					ofmt = PixelFormat.Bgr48BppLinearUQ15.FormatGuid;
				else if (ifmt == Consts.GUID_WICPixelFormat32bppBGRA)
					ofmt = PixelFormat.Pbgra64BppLinearUQ15.FormatGuid;
			}

			if (ofmt == ifmt)
				return;

			bool forceSrgb = (ofmt == PixelFormat.Y32BppLinearFloat.FormatGuid || ofmt == PixelFormat.Y16BppLinearUQ15.FormatGuid) && ctx.SourceColorProfile != ColorProfile.sRGB;

			ctx.Source = ctx.AddDispose(new FormatConversionTransformInternal(ctx.Source, forceSrgb ? ColorProfile.sRGB : ctx.SourceColorProfile, forceSrgb ? ColorProfile.sRGB : ctx.DestColorProfile, ofmt));
		}

		public static void AddExternalFormatConverter(WicProcessingContext ctx)
		{
			var ifmt = ctx.Source.Format.FormatGuid;
			var ofmt = ifmt;

			if (ifmt == PixelFormat.Grey32BppFloat.FormatGuid || ifmt == PixelFormat.Grey32BppLinearFloat.FormatGuid || ifmt == PixelFormat.Grey16BppLinearUQ15.FormatGuid)
				ofmt = Consts.GUID_WICPixelFormat8bppGray;
			else if (ifmt == PixelFormat.Y32BppFloat.FormatGuid || ifmt == PixelFormat.Y32BppLinearFloat.FormatGuid || ifmt == PixelFormat.Y16BppLinearUQ15.FormatGuid)
				ofmt = Consts.GUID_WICPixelFormat8bppY;
			else if (ifmt == PixelFormat.Bgrx128BppFloat.FormatGuid || ifmt == PixelFormat.Bgrx128BppLinearFloat.FormatGuid || ifmt == PixelFormat.Bgr96BppFloat.FormatGuid || ifmt == PixelFormat.Bgr96BppLinearFloat.FormatGuid || ifmt == PixelFormat.Bgr48BppLinearUQ15.FormatGuid)
				ofmt = Consts.GUID_WICPixelFormat24bppBGR;
			else if (ifmt == PixelFormat.Pbgra128BppFloat.FormatGuid || ifmt == PixelFormat.Pbgra128BppLinearFloat.FormatGuid || ifmt == PixelFormat.Pbgra64BppLinearUQ15.FormatGuid)
				ofmt = Consts.GUID_WICPixelFormat32bppBGRA;
			else if (ifmt == PixelFormat.CbCr64BppFloat.FormatGuid)
				ofmt = Consts.GUID_WICPixelFormat16bppCbCr;

			if (ofmt == ifmt)
				return;

			bool forceSrgb = (ifmt == PixelFormat.Y32BppLinearFloat.FormatGuid || ifmt == PixelFormat.Y16BppLinearUQ15.FormatGuid) && ctx.SourceColorProfile != ColorProfile.sRGB;

			ctx.Source = ctx.AddDispose(new FormatConversionTransformInternal(ctx.Source, forceSrgb ? ColorProfile.sRGB : ctx.SourceColorProfile, forceSrgb ? ColorProfile.sRGB : ctx.DestColorProfile, ofmt));
		}

		public static void AddHighQualityScaler(WicProcessingContext ctx, bool hybrid = false)
		{
			uint width = (uint)ctx.Settings.InnerRect.Width, height = (uint)ctx.Settings.InnerRect.Height;
			double rat = ctx.Settings.HybridScaleRatio;

			if ((ctx.Source.Width == width && ctx.Source.Height == height) || (hybrid && rat == 1d))
				return;

			if (hybrid)
			{
				if (ctx.Source.Format.FormatGuid != Consts.GUID_WICPixelFormat32bppCMYK)
					return;

				width = (uint)Math.Ceiling(ctx.Source.Width / rat);
				height = (uint)Math.Ceiling(ctx.Source.Height / rat);
				ctx.Settings.HybridMode = HybridScaleMode.Off;
			}

			AddInternalFormatConverter(ctx, allow96bppFloat: true);

			var fmt = ctx.Source.Format;
			var interpolator = ctx.Settings.Interpolation.WeightingFunction.Support > 1d && fmt.ColorRepresentation == PixelColorRepresentation.Unspecified ? InterpolationSettings.Hermite : ctx.Settings.Interpolation;
			var interpolatorx = width == ctx.Source.Width ? InterpolationSettings.NearestNeighbor : hybrid ? InterpolationSettings.Average : interpolator;
			var interpolatory = height == ctx.Source.Height ? InterpolationSettings.NearestNeighbor : hybrid ? InterpolationSettings.Average : interpolator;

			if (fmt.NumericRepresentation == PixelNumericRepresentation.Float)
			{
				var mx = ctx.AddDispose(KernelMap<float>.MakeScaleMap(ctx.Source.Width, width, fmt.ColorChannelCount, fmt.AlphaRepresentation != PixelAlphaRepresentation.None, true, interpolatorx));
				var my = ctx.AddDispose(KernelMap<float>.MakeScaleMap(ctx.Source.Height, height, fmt.ChannelCount == 3 ? 4 : fmt.ColorChannelCount, fmt.AlphaRepresentation != PixelAlphaRepresentation.None, true, interpolatory));

				ctx.Source = ctx.AddDispose(new ConvolutionTransform<float, float>(ctx.Source, mx, my));
			}
			else
			{
				var mx = ctx.AddDispose(KernelMap<int>.MakeScaleMap(ctx.Source.Width, width, 1, false, false, interpolatorx));
				var my = ctx.AddDispose(KernelMap<int>.MakeScaleMap(ctx.Source.Height, height, 1, false, false, interpolatory));

				if (fmt.NumericRepresentation == PixelNumericRepresentation.Fixed)
					ctx.Source = ctx.AddDispose(new ConvolutionTransform<ushort, int>(ctx.Source, mx, my));
				else
					ctx.Source = ctx.AddDispose(new ConvolutionTransform<byte, int>(ctx.Source, mx, my));
			}
		}

		public static void AddUnsharpMask(WicProcessingContext ctx)
		{
			var ss = ctx.Settings.UnsharpMask;
			if (!ctx.Settings.Sharpen || ss.Radius <= 0d || ss.Amount <= 0)
				return;

			var fmt = ctx.Source.Format;
			if (fmt.NumericRepresentation == PixelNumericRepresentation.Float)
			{
				var mapx = ctx.AddDispose(KernelMap<float>.MakeBlurMap(ctx.Source.Width, ss.Radius, 1, false, true));
				var mapy = ctx.AddDispose(KernelMap<float>.MakeBlurMap(ctx.Source.Height, ss.Radius, 1, false, true));

				ctx.Source = ctx.AddDispose(new UnsharpMaskTransform<float, float>(ctx.Source, mapx, mapy, ss));
			}
			else
			{
				var mapx = ctx.AddDispose(KernelMap<int>.MakeBlurMap(ctx.Source.Width, ss.Radius, 1, false, false));
				var mapy = ctx.AddDispose(KernelMap<int>.MakeBlurMap(ctx.Source.Height, ss.Radius, 1, false, false));

				if (fmt.NumericRepresentation == PixelNumericRepresentation.Fixed)
					ctx.Source = ctx.AddDispose(new UnsharpMaskTransform<ushort, int>(ctx.Source, mapx, mapy, ss));
				else
					ctx.Source = ctx.AddDispose(new UnsharpMaskTransform<byte, int>(ctx.Source, mapx, mapy, ss));
			}
		}

		public static void AddMatte(WicProcessingContext ctx)
		{
			if (ctx.Settings.MatteColor.IsEmpty || ctx.Source.Format.ColorRepresentation != PixelColorRepresentation.Bgr || ctx.Source.Format.AlphaRepresentation == PixelAlphaRepresentation.None)
				return;

			if (ctx.Source.Format == PixelFormat.Pbgra128BppFloat)
				ctx.Source = ctx.AddDispose(new FormatConversionTransformInternal(ctx.Source, null, null, Consts.GUID_WICPixelFormat32bppBGRA));

			ctx.Source = new MatteTransform(ctx.Source, ctx.Settings.MatteColor);

			if (ctx.Source.Format.AlphaRepresentation != PixelAlphaRepresentation.None && ctx.Settings.MatteColor.A == byte.MaxValue)
			{
				var oldFmt = ctx.Source.Format;
				var newFmt = oldFmt == PixelFormat.Pbgra64BppLinearUQ15 ? PixelFormat.Bgr48BppLinearUQ15
					: oldFmt.FormatGuid == Consts.GUID_WICPixelFormat32bppBGRA ? PixelFormat.Cache[Consts.GUID_WICPixelFormat24bppBGR]
					: throw new NotSupportedException("Unsupported pixel format");

				ctx.Source = ctx.AddDispose(new FormatConversionTransformInternal(ctx.Source, null, null, newFmt.FormatGuid));
			}
		}

		public static void AddPad(WicProcessingContext ctx)
		{
			if (ctx.Settings.InnerRect == ctx.Settings.OuterRect)
				return;

			AddExternalFormatConverter(ctx);

			ctx.Source = new PadTransform(ctx.Source, ctx.Settings.MatteColor, ctx.Settings.InnerRect, ctx.Settings.OuterRect);
		}

		public static void AddColorspaceConverter(WicProcessingContext ctx)
		{
			if (ctx.SourceColorProfile == ctx.DestColorProfile)
			{
				if ((ctx.SourceColorProfile == ColorProfile.sRGB || ctx.SourceColorProfile == ColorProfile.sGrey) && ctx.SourceColorContext != null)
				{
					AddExternalFormatConverter(ctx);
					WicTransforms.AddColorspaceConverter(ctx);
				}

				return;
			}

			if (ctx.Source.Format.NumericRepresentation == PixelNumericRepresentation.Float && ctx.Source.Format.Colorspace != PixelColorspace.LinearRgb)
				AddExternalFormatConverter(ctx);

			AddInternalFormatConverter(ctx, forceLinear: true);

			if (ctx.Source.Format.ColorRepresentation != PixelColorRepresentation.Bgr)
				return;

			var matrix = ctx.SourceColorProfile.Matrix * ctx.DestColorProfile.InverseMatrix;
			if (matrix == default || matrix.IsIdentity)
				return;

			ctx.Source = new ColorMatrixTransformInternal(ctx.Source, matrix);
		}
	}
}
