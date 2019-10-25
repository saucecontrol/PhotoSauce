using System;

using PhotoSauce.Interop.Wic;

namespace PhotoSauce.MagicScaler
{
	internal static class MagicTransforms
	{
		public static void AddInternalFormatConverter(PipelineContext ctx, bool allow96bppFloat = false, bool forceLinear = false)
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
				else if (ifmt == Consts.GUID_WICPixelFormat8bppCb)
					ofmt = PixelFormat.Cb32BppFloat.FormatGuid;
				else if (ifmt == Consts.GUID_WICPixelFormat8bppCr)
					ofmt = PixelFormat.Cr32BppFloat.FormatGuid;
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

		public static void AddExternalFormatConverter(PipelineContext ctx)
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
			else if (ifmt == PixelFormat.Cb32BppFloat.FormatGuid)
				ofmt = Consts.GUID_WICPixelFormat8bppCb;
			else if (ifmt == PixelFormat.Cr32BppFloat.FormatGuid)
				ofmt = Consts.GUID_WICPixelFormat8bppCr;

			if (ofmt == ifmt)
				return;

			bool forceSrgb = (ifmt == PixelFormat.Y32BppLinearFloat.FormatGuid || ifmt == PixelFormat.Y16BppLinearUQ15.FormatGuid) && ctx.SourceColorProfile != ColorProfile.sRGB;

			ctx.Source = ctx.AddDispose(new FormatConversionTransformInternal(ctx.Source, forceSrgb ? ColorProfile.sRGB : ctx.SourceColorProfile, forceSrgb ? ColorProfile.sRGB : ctx.DestColorProfile, ofmt));
		}

		public static void AddHighQualityScaler(PipelineContext ctx, bool hybrid = false)
		{
			bool swap = ctx.DecoderFrame.ExifOrientation.SwapsDimensions();
			var srect = ctx.Settings.InnerRect;

			int width = swap ? srect.Height : srect.Width, height = swap ? srect.Width : srect.Height;
			int ratio = (int)ctx.Settings.HybridScaleRatio;

			if ((ctx.Source.Width == width && ctx.Source.Height == height) || (hybrid && ratio == 1))
				return;

			if (hybrid)
			{
				if (ctx.Source.Format.FormatGuid != Consts.GUID_WICPixelFormat32bppCMYK)
					return;

				width = MathUtil.DivCeiling((int)ctx.Source.Width, ratio);
				height = MathUtil.DivCeiling((int)ctx.Source.Height, ratio);
				ctx.Settings.HybridMode = HybridScaleMode.Off;
			}

			AddInternalFormatConverter(ctx, allow96bppFloat: true);

			var fmt = ctx.Source.Format;
			var interpolatorx = width == ctx.Source.Width ? InterpolationSettings.NearestNeighbor : hybrid ? InterpolationSettings.Average : ctx.Settings.Interpolation;
			var interpolatory = height == ctx.Source.Height ? InterpolationSettings.NearestNeighbor : hybrid ? InterpolationSettings.Average : ctx.Settings.Interpolation;

			if (fmt.NumericRepresentation == PixelNumericRepresentation.Float)
				ctx.Source = ctx.AddDispose(ConvolutionTransform<float, float>.CreateResize(ctx.Source, (uint)width, (uint)height, interpolatorx, interpolatory));
			else if (fmt.NumericRepresentation == PixelNumericRepresentation.Fixed)
				ctx.Source = ctx.AddDispose(ConvolutionTransform<ushort, int>.CreateResize(ctx.Source, (uint)width, (uint)height, interpolatorx, interpolatory));
			else
				ctx.Source = ctx.AddDispose(ConvolutionTransform<byte, int>.CreateResize(ctx.Source, (uint)width, (uint)height, interpolatorx, interpolatory));

			ctx.Settings.Crop = ctx.Source.Area.ReOrient(ctx.DecoderFrame.ExifOrientation, ctx.Source.Width, ctx.Source.Height).ToGdiRect();
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
			if (ctx.Settings.MatteColor.IsEmpty || ctx.Source.Format.ColorRepresentation != PixelColorRepresentation.Bgr || ctx.Source.Format.AlphaRepresentation == PixelAlphaRepresentation.None)
				return;

			if (ctx.Source.Format == PixelFormat.Pbgra128BppFloat)
				ctx.Source = ctx.AddDispose(new FormatConversionTransformInternal(ctx.Source, null, null, Consts.GUID_WICPixelFormat32bppBGRA));

			ctx.Source = new MatteTransform(ctx.Source, ctx.Settings.MatteColor);

			if (ctx.Source.Format.AlphaRepresentation != PixelAlphaRepresentation.None && ctx.Settings.MatteColor.A == byte.MaxValue)
			{
				var oldFmt = ctx.Source.Format;
				var newFmt = oldFmt == PixelFormat.Pbgra64BppLinearUQ15 ? PixelFormat.Bgr48BppLinearUQ15
					: oldFmt.FormatGuid == Consts.GUID_WICPixelFormat32bppBGRA ? PixelFormat.FromGuid(Consts.GUID_WICPixelFormat24bppBGR)
					: throw new NotSupportedException("Unsupported pixel format");

				ctx.Source = ctx.AddDispose(new FormatConversionTransformInternal(ctx.Source, null, null, newFmt.FormatGuid));
			}
		}

		public static void AddPad(PipelineContext ctx)
		{
			if (ctx.Settings.InnerRect == ctx.Settings.OuterRect)
				return;

			AddExternalFormatConverter(ctx);

			ctx.Source = new PadTransformInternal(ctx.Source, ctx.Settings.MatteColor, ctx.Settings.InnerRect, ctx.Settings.OuterRect);
		}

		public static void AddCropper(PipelineContext ctx)
		{
			var crop = PixelArea.FromGdiRect(ctx.Settings.Crop).DeOrient(ctx.DecoderFrame.ExifOrientation, ctx.Source.Width, ctx.Source.Height);
			if (crop == ctx.Source.Area)
				return;

			ctx.Source = new CropTransform(ctx.Source, crop);
			ctx.Settings.Crop = ctx.Source.Area.ReOrient(ctx.DecoderFrame.ExifOrientation, ctx.Source.Width, ctx.Source.Height).ToGdiRect();
		}

		public static void AddFlipRotator(PipelineContext ctx, Orientation orientation)
		{
			if (orientation == Orientation.Normal)
				return;

			AddExternalFormatConverter(ctx);

			ctx.Source = new OrientationTransformInternal(ctx.Source, orientation, PixelArea.FromGdiRect(ctx.Settings.Crop));
			ctx.Settings.Crop = ctx.Source.Area.ToGdiRect();
			ctx.DecoderFrame.ExifOrientation = Orientation.Normal;
		}

		public static void AddExifFlipRotator(PipelineContext ctx) => AddFlipRotator(ctx, ctx.DecoderFrame.ExifOrientation);

		public static void AddColorspaceConverter(PipelineContext ctx)
		{
			if (ctx.SourceColorProfile == ctx.DestColorProfile)
			{
				if ((ctx.SourceColorProfile == ColorProfile.sRGB || ctx.SourceColorProfile == ColorProfile.sGrey) && ctx.WicContext.SourceColorContext != null)
				{
					AddExternalFormatConverter(ctx);
					WicTransforms.AddColorspaceConverter(ctx);
				}

				return;
			}

			if (ctx.Source.Format.NumericRepresentation == PixelNumericRepresentation.Float && ctx.Source.Format.Colorspace != PixelColorspace.LinearRgb)
				AddExternalFormatConverter(ctx);

			AddInternalFormatConverter(ctx, forceLinear: true);

			if (ctx.Source.Format.ColorRepresentation == PixelColorRepresentation.Bgr && ctx.SourceColorProfile is MatrixProfile srcProf && ctx.DestColorProfile is MatrixProfile dstProf)
			{
				var matrix = srcProf.Matrix * dstProf.InverseMatrix;
				if (matrix != default && !matrix.IsIdentity)
					ctx.Source = new ColorMatrixTransformInternal(ctx.Source, matrix);
			}
		}
	}
}
