// Copyright © Clinton Ingram and Contributors.  Licensed under the MIT License.

using System;
using System.Drawing;
using System.Diagnostics;
using System.Collections.Generic;
using System.Runtime.InteropServices;

namespace PhotoSauce.MagicScaler.Transforms
{
	internal static class MagicTransforms
	{
		private static readonly Dictionary<PixelFormat, PixelFormat> externalFormatMap = new() {
			[PixelFormat.Grey32Float] = PixelFormat.Grey8,
			[PixelFormat.Grey32FloatLinear] = PixelFormat.Grey8,
			[PixelFormat.Grey16UQ15Linear] = PixelFormat.Grey8,
			[PixelFormat.Y32Float] = PixelFormat.Y8,
			[PixelFormat.Y32FloatLinear] = PixelFormat.Y8,
			[PixelFormat.Y16UQ15Linear] = PixelFormat.Y8,
			[PixelFormat.Bgrx128Float] = PixelFormat.Bgr24,
			[PixelFormat.Bgrx128FloatLinear] = PixelFormat.Bgr24,
			[PixelFormat.Bgr96Float] = PixelFormat.Bgr24,
			[PixelFormat.Bgr96FloatLinear] = PixelFormat.Bgr24,
			[PixelFormat.Bgr48UQ15Linear] = PixelFormat.Bgr24,
			[PixelFormat.Pbgra128Float] = PixelFormat.Bgra32,
			[PixelFormat.Pbgra128FloatLinear] = PixelFormat.Bgra32,
			[PixelFormat.Pbgra64UQ15Linear] = PixelFormat.Bgra32,
			[PixelFormat.Cb32Float] = PixelFormat.Cb8,
			[PixelFormat.Cr32Float] = PixelFormat.Cr8
		};

		private static readonly Dictionary<PixelFormat, PixelFormat> internalFormatMapSimd = new() {
			[PixelFormat.Grey8] = PixelFormat.Grey32Float,
			[PixelFormat.Y8] = PixelFormat.Y32Float,
			[PixelFormat.Bgr24] = PixelFormat.Bgrx128Float,
			[PixelFormat.Bgra32] = PixelFormat.Pbgra128Float,
			[PixelFormat.Pbgra32] = PixelFormat.Pbgra128Float,
			[PixelFormat.Grey32FloatLinear] = PixelFormat.Grey32Float,
			[PixelFormat.Y32FloatLinear] = PixelFormat.Y32Float,
			[PixelFormat.Bgrx128FloatLinear] = PixelFormat.Bgrx128Float,
			[PixelFormat.Pbgra128FloatLinear] = PixelFormat.Pbgra128Float,
			[PixelFormat.Cb8] = PixelFormat.Cb32Float,
			[PixelFormat.Cr8] = PixelFormat.Cr32Float
		};

		private static readonly Dictionary<PixelFormat, PixelFormat> internalFormatMapLinear = new() {
			[PixelFormat.Grey8] = PixelFormat.Grey16UQ15Linear,
			[PixelFormat.Y8] = PixelFormat.Y16UQ15Linear,
			[PixelFormat.Bgr24] = PixelFormat.Bgr48UQ15Linear,
			[PixelFormat.Bgra32] = PixelFormat.Pbgra64UQ15Linear
		};

		private static readonly Dictionary<PixelFormat, PixelFormat> internalFormatMapLinearSimd = new() {
			[PixelFormat.Grey8] = PixelFormat.Grey32FloatLinear,
			[PixelFormat.Y8] = PixelFormat.Y32FloatLinear,
			[PixelFormat.Bgr24] = PixelFormat.Bgrx128FloatLinear,
			[PixelFormat.Bgra32] = PixelFormat.Pbgra128FloatLinear,
			[PixelFormat.Grey32Float] = PixelFormat.Grey32FloatLinear,
			[PixelFormat.Y32Float] = PixelFormat.Y32FloatLinear,
			[PixelFormat.Bgrx128Float] = PixelFormat.Bgrx128FloatLinear,
			[PixelFormat.Pbgra128Float] = PixelFormat.Pbgra128FloatLinear,
			[PixelFormat.Grey32FloatLinear] = PixelFormat.Grey32FloatLinear,
			[PixelFormat.Y32FloatLinear] = PixelFormat.Y32FloatLinear,
			[PixelFormat.Bgrx128FloatLinear] = PixelFormat.Bgrx128FloatLinear,
			[PixelFormat.Pbgra128FloatLinear] = PixelFormat.Pbgra128FloatLinear,
		};

		public static void AddInternalFormatConverter(PipelineContext ctx, PixelValueEncoding enc = PixelValueEncoding.Unspecified, bool allow96bppFloat = false)
		{
			var ifmt = ctx.Source.Format;
			var ofmt = ifmt;
			bool linear = enc == PixelValueEncoding.Unspecified ? ctx.Settings.BlendingMode == GammaMode.Linear : enc == PixelValueEncoding.Linear;

			if (allow96bppFloat && MagicImageProcessor.EnableSimd && ifmt == PixelFormat.Bgr24)
				ofmt = linear ? PixelFormat.Bgr96FloatLinear : PixelFormat.Bgr96Float;
			else if (linear && (MagicImageProcessor.EnableSimd ? internalFormatMapLinearSimd : internalFormatMapLinear).TryGetValue(ifmt, out var ofmtl))
				ofmt = ofmtl;
			else if (MagicImageProcessor.EnableSimd && internalFormatMapSimd.TryGetValue(ifmt, out var ofmts))
				ofmt = ofmts;

			if (ctx.Source is PlanarPixelSource plsrc)
			{
				if (ofmt != ifmt || plsrc.VideoLumaLevels)
				{
					bool forceSrgb = (ofmt == PixelFormat.Y32FloatLinear || ofmt == PixelFormat.Y16UQ15Linear) && ctx.SourceColorProfile != ColorProfile.sRGB;
					plsrc.SourceY = ctx.AddProfiler(new ConversionTransform(plsrc.SourceY, ofmt, forceSrgb ? ColorProfile.sRGB : ctx.SourceColorProfile, ctx.DestColorProfile, plsrc.VideoLumaLevels));
					plsrc.VideoLumaLevels = false;
				}

				if (MagicImageProcessor.EnableSimd && internalFormatMapSimd.TryGetValue(plsrc.SourceCb.Format, out var ofmtb) && internalFormatMapSimd.TryGetValue(plsrc.SourceCr.Format, out var ofmtc))
				{
					plsrc.SourceCb = ctx.AddProfiler(new ConversionTransform(plsrc.SourceCb, ofmtb, videoLevels: plsrc.VideoChromaLevels));
					plsrc.SourceCr = ctx.AddProfiler(new ConversionTransform(plsrc.SourceCr, ofmtc, videoLevels: plsrc.VideoChromaLevels));
					plsrc.VideoChromaLevels = false;
				}

				return;
			}

			if (ofmt != ifmt)
				ctx.Source = ctx.AddProfiler(new ConversionTransform(ctx.Source, ofmt, ctx.SourceColorProfile, ctx.DestColorProfile));
		}

		public static void AddExternalFormatConverter(PipelineContext ctx, bool lastChance = false)
		{
			var ifmt = ctx.Source.Format;
			var ofmt = ifmt;
			if (externalFormatMap.TryGetValue(ifmt, out var ofmtm))
				ofmt = ofmtm;

			if (ctx.Source is PlanarPixelSource plsrc)
			{
				if (ofmt != ifmt || plsrc.VideoLumaLevels)
				{
					bool forceSrgb = (ifmt == PixelFormat.Y32FloatLinear || ifmt == PixelFormat.Y16UQ15Linear) && ctx.SourceColorProfile != ColorProfile.sRGB;
					plsrc.SourceY = ctx.AddProfiler(new ConversionTransform(plsrc.SourceY, ofmt, ctx.SourceColorProfile, forceSrgb ? ColorProfile.sRGB : ctx.DestColorProfile, plsrc.VideoLumaLevels));
					plsrc.VideoLumaLevels = false;
				}

				if (externalFormatMap.TryGetValue(plsrc.SourceCb.Format, out var ofmtb) && externalFormatMap.TryGetValue(plsrc.SourceCr.Format, out var ofmtc))
				{
					plsrc.SourceCb = ctx.AddProfiler(new ConversionTransform(plsrc.SourceCb, ofmtb));
					plsrc.SourceCr = ctx.AddProfiler(new ConversionTransform(plsrc.SourceCr, ofmtc));
				}
				else if (lastChance && plsrc.VideoChromaLevels)
				{
					plsrc.SourceCb = ctx.AddProfiler(new ConversionTransform(plsrc.SourceCb, plsrc.SourceCb.Format, videoLevels: plsrc.VideoChromaLevels));
					plsrc.SourceCr = ctx.AddProfiler(new ConversionTransform(plsrc.SourceCr, plsrc.SourceCr.Format, videoLevels: plsrc.VideoChromaLevels));
					plsrc.VideoChromaLevels = false;
				}

				return;
			}

			if (ofmt != ifmt)
				ctx.Source = ctx.AddProfiler(new ConversionTransform(ctx.Source, ofmt, ctx.SourceColorProfile, ctx.DestColorProfile));
			else if (lastChance && ifmt == PixelFormat.Bgrx32 && ctx.Settings.EncoderInfo!.SupportsPixelFormat(PixelFormat.Bgr24.FormatGuid))
				ctx.Source = ctx.AddProfiler(new ConversionTransform(ctx.Source, PixelFormat.Bgr24));
		}

		public static void AddNormalizingFormatConverter(PipelineContext ctx, bool lastChance = false)
		{
			var curFormat = ctx.Source.Format;
			if (curFormat.ColorRepresentation == PixelColorRepresentation.Cmyk)
			{
				if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
					throw new PlatformNotSupportedException("CMYK conversion is not yet supported on this platform.");

				WicTransforms.AddPixelFormatConverter(ctx);
				curFormat = ctx.Source.Format;
			}

			if (curFormat == PixelFormat.Y8 || curFormat == PixelFormat.Cb8 || curFormat == PixelFormat.Cr8)
				return;

			var newFormat = PixelFormat.Bgr24;
			if (!lastChance && curFormat.AlphaRepresentation == PixelAlphaRepresentation.Associated && ctx.Settings.BlendingMode != GammaMode.Linear && ctx.Settings.MatteColor.IsEmpty)
				newFormat = PixelFormat.Pbgra32;
			else if (curFormat.AlphaRepresentation != PixelAlphaRepresentation.None)
				newFormat = PixelFormat.Bgra32;
			else if (curFormat.ColorRepresentation == PixelColorRepresentation.Grey)
				newFormat = PixelFormat.Grey8;

			if (curFormat == newFormat)
				return;

			if ((curFormat == PixelFormat.Rgb24 && newFormat == PixelFormat.Bgr24) || (curFormat == PixelFormat.Rgba32 && curFormat == PixelFormat.Bgra32))
			{
				ctx.Source = ctx.AddProfiler(new ConversionTransform(ctx.Source, newFormat));
				return;
			}

			if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
				throw new PlatformNotSupportedException($"[{curFormat.Name}]->[{newFormat.Name}] conversion is not yet implemented on this platform.");

			WicTransforms.AddPixelFormatConverter(ctx, !lastChance);
		}

		public static void AddHighQualityScaler(PipelineContext ctx, ChromaSubsampleMode subsample = ChromaSubsampleMode.Subsample444)
		{
			bool swap = ctx.Orientation.SwapsDimensions();
			var tsize = ctx.Settings.InnerSize;

			int width = swap ? tsize.Height : tsize.Width, height = swap ? tsize.Width : tsize.Height;
			if (ctx.Source.Width == width && ctx.Source.Height == height && ctx.Source is not PlanarPixelSource)
				return;

			var interpolatorx = width == ctx.Source.Width ? InterpolationSettings.NearestNeighbor : ctx.Settings.Interpolation;
			var interpolatory = height == ctx.Source.Height ? InterpolationSettings.NearestNeighbor : ctx.Settings.Interpolation;
			if (interpolatorx.WeightingFunction.Support >= 0.1 || interpolatory.WeightingFunction.Support >= 0.1)
				AddInternalFormatConverter(ctx, allow96bppFloat: true);

			if (ctx.Source is PlanarPixelSource plsrc)
			{
				if (plsrc.SourceY.Width != width || plsrc.SourceY.Height != height)
					plsrc.SourceY = ctx.AddProfiler(ConvolutionTransform.CreateResample(plsrc.SourceY, width, height, interpolatorx, interpolatory));

				if (swap ? subsample.IsSubsampledY() : subsample.IsSubsampledX())
					width = MathUtil.DivCeiling(width, 2);
				if (swap ? subsample.IsSubsampledX() : subsample.IsSubsampledY())
					height = MathUtil.DivCeiling(height, 2);

				if (plsrc.SourceCb.Width == width && plsrc.SourceCb.Height == height)
					return;

				interpolatorx = width == plsrc.SourceCb.Width ? InterpolationSettings.NearestNeighbor : ctx.Settings.Interpolation;
				interpolatory = height == plsrc.SourceCb.Height ? InterpolationSettings.NearestNeighbor : ctx.Settings.Interpolation;

				bool offsetX = false, offsetY = false;
				if (ctx.ImageFrame is IYccImageFrame frame)
				{
					offsetX = frame.ChromaPosition.HasFlag(ChromaPosition.CositedHorizontal) && plsrc.ChromaSubsampling.IsSubsampledX();
					offsetY = frame.ChromaPosition.HasFlag(ChromaPosition.CositedVertical) && plsrc.ChromaSubsampling.IsSubsampledY();
				}

				plsrc.SourceCb = ctx.AddProfiler(ConvolutionTransform.CreateResample(plsrc.SourceCb, width, height, interpolatorx, interpolatory, offsetX, offsetY));
				plsrc.SourceCr = ctx.AddProfiler(ConvolutionTransform.CreateResample(plsrc.SourceCr, width, height, interpolatorx, interpolatory, offsetX, offsetY));
				plsrc.ChromaSubsampling = subsample;

				return;
			}

			ctx.Source = ctx.AddProfiler(ConvolutionTransform.CreateResample(ctx.Source, width, height, interpolatorx, interpolatory));
		}

		public static void AddHybridScaler(PipelineContext ctx)
		{
			var ratio = ctx.Settings.HybridScaleRatio;
			if (ratio == 1 || ctx.Settings.Interpolation.WeightingFunction.Support < 0.1 || ctx.Source.Format.BitsPerPixel / ctx.Source.Format.ChannelCount != 8)
				return;

			if (ctx.Source is PlanarPixelSource plsrc)
			{
				int ratioX = (plsrc.SourceY.Width + 1) / plsrc.SourceCb.Width;
				int ratioY = (plsrc.SourceY.Height + 1) / plsrc.SourceCb.Height;
				int ratioC = ratio / Math.Min(ratioX, ratioY);

				plsrc.SourceY = ctx.AddProfiler(new HybridScaleTransform(plsrc.SourceY, ratio));
				ctx.Settings.HybridMode = HybridScaleMode.Off;

				if (ratioC != 1)
				{
					plsrc.SourceCb = ctx.AddProfiler(new HybridScaleTransform(plsrc.SourceCb, ratioC));
					plsrc.SourceCr = ctx.AddProfiler(new HybridScaleTransform(plsrc.SourceCr, ratioC));
				}

				return;
			}

			ctx.Source = ctx.AddProfiler(new HybridScaleTransform(ctx.Source, ratio));
			ctx.Settings.HybridMode = HybridScaleMode.Off;
		}

		public static void AddUnsharpMask(PipelineContext ctx)
		{
			var ss = ctx.Settings.UnsharpMask;
			if (!ctx.Settings.Sharpen || ss.Radius <= 0d || ss.Amount <= 0)
				return;

			if (ctx.Source is PlanarPixelSource plsrc)
				plsrc.SourceY = ctx.AddProfiler(ConvolutionTransform.CreateSharpen(plsrc.SourceY, ss));
			else
				ctx.Source = ctx.AddProfiler(ConvolutionTransform.CreateSharpen(ctx.Source, ss));
		}

		public static void AddMatte(PipelineContext ctx)
		{
			var fmt = ctx.Source.Format;
			if (ctx.Settings.MatteColor.IsEmpty || fmt.ColorRepresentation != PixelColorRepresentation.Bgr || fmt.AlphaRepresentation == PixelAlphaRepresentation.None)
				return;

			if (fmt.NumericRepresentation == PixelNumericRepresentation.Float && fmt.Encoding == PixelValueEncoding.Companded)
				AddInternalFormatConverter(ctx, PixelValueEncoding.Linear);

			bool discardAlpha = !ctx.IsAnimationPipeline && !ctx.Settings.MatteColor.IsTransparent();
			ctx.Source = ctx.AddProfiler(new MatteTransform(ctx.Source, ctx.Settings.MatteColor, discardAlpha));

			if (discardAlpha && ctx.Source.Format.AlphaRepresentation != PixelAlphaRepresentation.None)
			{
				var oldFmt = ctx.Source.Format;
				var newFmt = oldFmt == PixelFormat.Pbgra64UQ15Linear ? PixelFormat.Bgr48UQ15Linear
					: oldFmt == PixelFormat.Bgra32 ? PixelFormat.Bgr24
					: throw new NotSupportedException("Unsupported pixel format");

				ctx.Source = ctx.AddProfiler(new ConversionTransform(ctx.Source, newFmt));
			}
		}

		public static void AddPad(PipelineContext ctx)
		{
			if (ctx.Settings.InnerSize == ctx.Settings.OuterSize)
				return;

			AddExternalFormatConverter(ctx);

			var fmt = ctx.Source.Format;
			if (fmt.AlphaRepresentation == PixelAlphaRepresentation.None && ctx.Settings.MatteColor.IsTransparent())
				ctx.Source = ctx.AddProfiler(new ConversionTransform(ctx.Source, PixelFormat.Bgra32));
			else if (fmt.ColorRepresentation == PixelColorRepresentation.Grey && !ctx.Settings.MatteColor.IsGrey())
				ctx.Source = ctx.AddProfiler(new ConversionTransform(ctx.Source, PixelFormat.Bgr24));

			ctx.Source = ctx.AddProfiler(new PadTransformInternal(ctx.Source, ctx.Settings.MatteColor, ctx.Settings.InnerRect, ctx.Settings.OuterSize));
		}

		public static void AddCropper(PipelineContext ctx)
		{
			var crop = ((PixelArea)ctx.Settings.Crop).DeOrient(ctx.Orientation, ctx.Source.Width, ctx.Source.Height);
			if (crop == ctx.Source.Area)
				return;

			if (ctx.Source is PlanarPixelSource plsrc)
			{
				int yw = plsrc.SourceY.Width;
				int yh = plsrc.SourceY.Height;
				int cw = plsrc.SourceCb.Width;
				int ch = plsrc.SourceCb.Height;

				int ratioX = (yw + 1) / cw;
				int ratioY = (yh + 1) / ch;

				int scropX = MathUtil.PowerOfTwoFloor(crop.X, ratioX);
				int scropY = MathUtil.PowerOfTwoFloor(crop.Y, ratioY);
				int scropW = Math.Min(MathUtil.PowerOfTwoCeiling(crop.Width, ratioX), yw - scropX);
				int scropH = Math.Min(MathUtil.PowerOfTwoCeiling(crop.Height, ratioY), yh - scropY);
				var scrop = new PixelArea(scropX, scropY, scropW, scropH);

				plsrc.SourceY = ctx.AddProfiler(new CropTransform(plsrc.SourceY, scrop));

				cw = Math.Min(MathUtil.DivCeiling(scrop.Width, ratioX), cw);
				ch = Math.Min(MathUtil.DivCeiling(scrop.Height, ratioY), ch);
				scrop = new PixelArea(scrop.X / ratioX, scrop.Y / ratioY, cw, ch);

				plsrc.SourceCb = ctx.AddProfiler(new CropTransform(plsrc.SourceCb, scrop));
				plsrc.SourceCr = ctx.AddProfiler(new CropTransform(plsrc.SourceCr, scrop));

				return;
			}

			ctx.Source = ctx.AddProfiler(new CropTransform(ctx.Source, crop));
		}

		public static void AddFlipRotator(PipelineContext ctx, Orientation orientation)
		{
			if (orientation == Orientation.Normal)
				return;

			AddExternalFormatConverter(ctx);

			ctx.Source = ctx.AddProfiler(new OrientationTransformInternal(ctx.Source, orientation));
			ctx.Orientation = Orientation.Normal;
		}

		public static void AddExifFlipRotator(PipelineContext ctx)
		{
			var orientation = ctx.Orientation;
			if (orientation == Orientation.Normal)
				return;

			if (ctx.Source is PlanarPixelSource plsrc)
			{
				plsrc.SourceY = ctx.AddProfiler(new OrientationTransformInternal(plsrc.SourceY, orientation));
				plsrc.SourceCb = ctx.AddProfiler(new OrientationTransformInternal(plsrc.SourceCb, orientation));
				plsrc.SourceCr = ctx.AddProfiler(new OrientationTransformInternal(plsrc.SourceCr, orientation));
				ctx.Orientation = Orientation.Normal;

				return;
			}

			AddFlipRotator(ctx, orientation);
		}

		public static unsafe void AddColorProfileReader(PipelineContext ctx)
		{
			var mode = ctx.Settings.ColorProfileMode;
			if (mode == ColorProfileMode.Ignore)
				return;

			if (ctx.ImageFrame is WicImageFrame)
			{
				WicTransforms.AddColorProfileReader(ctx);
			}
			else
			{
				var fmt = ctx.ImageFrame is IYccImageFrame ? PixelFormat.Bgr24 : ctx.Source.Format;
				var profile = ColorProfile.GetDefaultFor(fmt);

				if (ctx.ImageFrame is IMetadataSource meta && meta.TryGetMetadata<IIccProfileSource>(out var icc))
				{
					using var buff = BufferPool.RentLocal<byte>(icc.ProfileLength);
					icc.CopyProfile(buff.Span);

					var prof = ColorProfile.Cache.GetOrAdd(buff.Span);
					if (prof.IsValid && prof.IsCompatibleWith(fmt))
						profile = ColorProfile.GetSourceProfile(prof, mode);
				}

				ctx.SourceColorProfile = profile;
				ctx.DestColorProfile = ColorProfile.GetDestProfile(ctx.SourceColorProfile, mode);
			}
		}

		public static unsafe void AddColorspaceConverter(PipelineContext ctx)
		{
			if (ctx.SourceColorProfile is null || ctx.DestColorProfile is null || ctx.SourceColorProfile == ctx.DestColorProfile)
				return;

			if (ctx.SourceColorProfile.ProfileType > ColorProfileType.Matrix || ctx.DestColorProfile.ProfileType > ColorProfileType.Matrix)
			{
				if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
				{
					if (ctx.WicContext.SourceColorContext is null)
						ctx.WicContext.SourceColorContext = WicColorProfile.CreateContextFromProfile(ctx.SourceColorProfile.ProfileBytes);

					if (ctx.WicContext.DestColorContext is null)
						ctx.WicContext.DestColorContext = WicColorProfile.CreateContextFromProfile(ctx.DestColorProfile.ProfileBytes);

					AddExternalFormatConverter(ctx);
					WicTransforms.AddColorspaceConverter(ctx);
				}

				return;
			}

			AddInternalFormatConverter(ctx, PixelValueEncoding.Linear);

			if (ctx.Source.Format.ColorRepresentation == PixelColorRepresentation.Bgr && ctx.SourceColorProfile is MatrixProfile srcProf && ctx.DestColorProfile is MatrixProfile dstProf)
			{
				var matrix = srcProf.Matrix * dstProf.InverseMatrix;
				if (matrix != default && !matrix.IsIdentity)
					ctx.Source = ctx.AddProfiler(new ColorMatrixTransformInternal(ctx.Source, matrix));
			}
		}

		public static void AddPlanarConverter(PipelineContext ctx)
		{
			if (ctx.Source is not PlanarPixelSource plsrc)
				throw new NotSupportedException("Must be a planar pixel source.");

			var matrix = YccMatrix.Rec601;
			if (ctx.ImageFrame is IYccImageFrame yccFrame)
			{
				matrix = yccFrame.RgbYccMatrix;

				if (plsrc.VideoLumaLevels)
					AddInternalFormatConverter(ctx, PixelValueEncoding.Companded);
			}

			if (plsrc.SourceY.Format.Encoding == PixelValueEncoding.Linear || plsrc.SourceCb.Format.NumericRepresentation != plsrc.SourceY.Format.NumericRepresentation)
			{
				if (plsrc.SourceY.Format.NumericRepresentation == PixelNumericRepresentation.Float && plsrc.SourceCb.Format.NumericRepresentation == PixelNumericRepresentation.Float)
					AddInternalFormatConverter(ctx, PixelValueEncoding.Companded);
				else
					AddExternalFormatConverter(ctx);
			}

			ctx.Source = ctx.AddProfiler(new PlanarConversionTransform(plsrc.SourceY, plsrc.SourceCb, plsrc.SourceCr, matrix, plsrc.VideoChromaLevels));
		}

		public static unsafe void AddIndexedColorConverter(PipelineContext ctx)
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

			if (curFormat.ColorRepresentation == PixelColorRepresentation.Grey && autoPalette256)
			{
				ctx.Source = ctx.AddProfiler(new IndexedColorTransform(ctx.Source));
			}
			else if (curFormat.NumericRepresentation != PixelNumericRepresentation.Indexed && indexedOptions is not null)
			{
				var newfmt = curFormat.AlphaRepresentation != PixelAlphaRepresentation.None ? PixelFormat.Bgra32 : PixelFormat.Bgrx32;
				if (curFormat != newfmt)
					ctx.Source = ctx.AddProfiler(new ConversionTransform(ctx.Source, newfmt));

				var iconv = default(IndexedColorTransform);
				if (indexedOptions.PredefinedPalette is not null)
				{
					iconv = new IndexedColorTransform(ctx.Source);
					iconv.SetPalette(MemoryMarshal.Cast<int, uint>(indexedOptions.PredefinedPalette.AsSpan()), indexedOptions.Dither == DitherMode.None);
				}
				else
				{
					var buffC = new FrameBufferSource(ctx.Source.Width, ctx.Source.Height, ctx.Source.Format);
					fixed (byte* pbuff = buffC.Span)
						ctx.Source.CopyPixels(ctx.Source.Area, buffC.Stride, buffC.Span.Length, pbuff);

					ctx.Source.Dispose();

					using var quant = ctx.AddProfiler(new OctreeQuantizer());
					bool isExact = quant.CreatePalette(indexedOptions.MaxPaletteSize, buffC.Span, buffC.Width, buffC.Height, buffC.Stride);

					iconv = new IndexedColorTransform(buffC);
					iconv.SetPalette(quant.Palette, isExact || indexedOptions.Dither == DitherMode.None);
				}

				ctx.Source = ctx.AddProfiler(iconv);
			}
		}

		public static unsafe void AddAnimationFrameBuffer(PipelineContext ctx, bool replay = true)
		{
			var anicnt = default(AnimationContainer);
			var anifrm = default(AnimationFrame);

			bool nocntmeta = ctx.ImageContainer is not IMetadataSource cmsrc || !cmsrc.TryGetMetadata(out anicnt);
			bool nofrmmeta = ctx.ImageFrame is not IMetadataSource fmsrc || !fmsrc.TryGetMetadata(out anifrm);
			if (nocntmeta && nofrmmeta)
				return;

			if (nocntmeta || anicnt.ScreenWidth is 0 || anicnt.ScreenHeight is 0)
				anicnt = new(ctx.Source.Width, ctx.Source.Height);
			if (nofrmmeta)
				anifrm = AnimationFrame.Default;

			if (replay && ctx.ImageContainer is WicGifContainer gif && gif.IsAnimation)
			{
				if (gif.FrameOffset != 0)
					gif.ReplayAnimation(ctx, gif.FrameOffset);

				replay = false;
			}

			var disposal = anifrm.Disposal;
			var ldisp = ctx.AnimationContext?.LastDisposal ?? FrameDisposalMethod.RestoreBackground;
			var innerArea = new PixelArea(anifrm.OffsetLeft, anifrm.OffsetTop, ctx.Source.Width, ctx.Source.Height);
			bool useBuffer = !replay && disposal == FrameDisposalMethod.Preserve;

			if (!replay)
			{
				var anictx = ctx.AnimationContext ??= new();

				if (anicnt.RequiresScreenBuffer && disposal == FrameDisposalMethod.Preserve)
					anictx.UpdateFrameBuffer(ctx.ImageFrame, anicnt, anifrm);

				ldisp = anictx.LastDisposal = disposal;
			}

			if (!anicnt.RequiresScreenBuffer)
				return;

			var frmsrc = ctx.Source;
			if (ctx.AnimationContext?.FrameBufferSource is not null && ldisp != FrameDisposalMethod.RestoreBackground)
				ctx.Source = ctx.AnimationContext.FrameBufferSource;

			bool fullScreen = innerArea.Width >= anicnt.ScreenWidth && innerArea.Height >= anicnt.ScreenHeight;
			if (!fullScreen && ldisp == FrameDisposalMethod.RestoreBackground && !useBuffer)
			{
				var outerArea = new PixelArea(0, 0, anicnt.ScreenWidth, anicnt.ScreenHeight);
				var bgColor = anifrm.HasAlpha ? Color.Empty : Color.FromArgb(anicnt.BackgroundColor);

				ctx.Source = new PadTransformInternal(frmsrc, bgColor, innerArea, outerArea, true);
			}
			else if (ldisp != FrameDisposalMethod.RestoreBackground && !useBuffer)
			{
				Debug.Assert(ctx.AnimationContext?.FrameBufferSource is not null);

				var anictx = ctx.AnimationContext;
				var fbuff = anictx.FrameBufferSource;

				ctx.Source = new OverlayTransform(fbuff, frmsrc, anifrm.OffsetLeft, anifrm.OffsetTop, anifrm.HasAlpha);
			}
			else if (ctx.Source != frmsrc)
			{
				frmsrc.Dispose();
			}

			if (ctx.Source.Width > anicnt.ScreenWidth || ctx.Source.Height > anicnt.ScreenHeight)
				ctx.Source = new CropTransform(ctx.Source, new PixelArea(0, 0, anicnt.ScreenWidth, anicnt.ScreenHeight), true);
		}
	}
}
