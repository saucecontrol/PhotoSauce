using System;
using System.IO;
using System.Numerics;

using PhotoSauce.MagicScaler.Interop;

namespace PhotoSauce.MagicScaler
{
	/// <summary>Provides a set of methods for constructing or executing a MagicScaler processing pipeline.</summary>
	public static class MagicImageProcessor
	{
		/// <summary>True to allow <a href="https://en.wikipedia.org/wiki/YCbCr">Y'CbCr</a> images to be processed in their native planar format, false to force RGB conversion before processing.</summary>
		public static bool EnablePlanarPipeline { get; set; } = true;

		/// <summary>True to check for Orientation tag in Xmp metadata in addition to the default Exif metadata location, false to check Exif only.</summary>
		public static bool EnableXmpOrientation { get; set; } = false;

		/// <summary>Overrides the default <a href="https://en.wikipedia.org/wiki/SIMD">SIMD</a> support detection to force floating point processing on or off.</summary>
		public static bool EnableSimd { get; set; } = Vector.IsHardwareAccelerated && (Vector<float>.Count == 4 || Vector<float>.Count == 8);

		private static void checkInStream(Stream imgStream)
		{
			if (imgStream is null) throw new ArgumentNullException(nameof(imgStream));
			if (!imgStream.CanSeek || !imgStream.CanRead) throw new ArgumentException("Input Stream must allow Seek and Read", nameof(imgStream));
			if (imgStream.Length <= 0 || imgStream.Position >= imgStream.Length) throw new ArgumentException("Input Stream is empty or positioned at its end", nameof(imgStream));
		}

		private static void checkOutStream(Stream outStream)
		{
			if (outStream is null) throw new ArgumentNullException(nameof(outStream));
			if (!outStream.CanSeek || !outStream.CanWrite) throw new ArgumentException("Output Stream must allow Seek and Write", nameof(outStream));
		}

		/// <summary>All-in-one processing of an image according to the specified <paramref name="settings" />.</summary>
		/// <param name="imgPath">The path to a file containing the input image.</param>
		/// <param name="outStream">The stream to which the output image will be written.</param>
		/// <param name="settings">The settings for this processing operation.</param>
		/// <returns>A <see cref="ProcessImageResult" /> containing the settings used and basic instrumentation for the pipeline.</returns>
		public static ProcessImageResult ProcessImage(string imgPath, Stream outStream, ProcessImageSettings settings)
		{
			if (imgPath is null) throw new ArgumentNullException(nameof(imgPath));
			checkOutStream(outStream);

			using var ctx = new PipelineContext(settings);
			var dec = new WicDecoder(imgPath, ctx);

			buildPipeline(ctx);
			return executePipeline(ctx, outStream);
		}

		/// <summary>All-in-one processing of an image according to the specified <paramref name="settings" />.</summary>
		/// <param name="imgBuffer">A buffer containing a supported input image container.</param>
		/// <param name="outStream">The stream to which the output image will be written.</param>
		/// <param name="settings">The settings for this processing operation.</param>
		/// <returns>A <see cref="ProcessImageResult" /> containing the settings used and basic instrumentation for the pipeline.</returns>
		public static ProcessImageResult ProcessImage(ReadOnlySpan<byte> imgBuffer, Stream outStream, ProcessImageSettings settings)
		{
			if (imgBuffer == default) throw new ArgumentNullException(nameof(imgBuffer));
			checkOutStream(outStream);

			using var ctx = new PipelineContext(settings);
			var dec = new WicDecoder(imgBuffer, ctx);

			buildPipeline(ctx);
			return executePipeline(ctx, outStream);
		}

		/// <summary>All-in-one processing of an image according to the specified <paramref name="settings" />.</summary>
		/// <param name="imgStream">A stream containing a supported input image container.</param>
		/// <param name="outStream">The stream to which the output image will be written.</param>
		/// <param name="settings">The settings for this processing operation.</param>
		/// <returns>A <see cref="ProcessImageResult" /> containing the settings used and basic instrumentation for the pipeline.</returns>
		public static ProcessImageResult ProcessImage(Stream imgStream, Stream outStream, ProcessImageSettings settings)
		{
			checkInStream(imgStream);
			checkOutStream(outStream);

			using var ctx = new PipelineContext(settings);
			var dec = new WicDecoder(imgStream, ctx);

			buildPipeline(ctx);
			return executePipeline(ctx, outStream);
		}

		/// <summary>All-in-one processing of an image according to the specified <paramref name="settings" />.</summary>
		/// <param name="imgSource">A custom pixel source to use as input.</param>
		/// <param name="outStream">The stream to which the output image will be written.</param>
		/// <param name="settings">The settings for this processing operation.</param>
		/// <returns>A <see cref="ProcessImageResult" /> containing the settings used and basic instrumentation for the pipeline.</returns>
		public static ProcessImageResult ProcessImage(IPixelSource imgSource, Stream outStream, ProcessImageSettings settings)
		{
			if (imgSource is null) throw new ArgumentNullException(nameof(imgSource));
			checkOutStream(outStream);

			using var ctx = new PipelineContext(settings);
			var dec = new WicDecoder(imgSource, ctx);

			buildPipeline(ctx);
			return executePipeline(ctx, outStream);
		}

		/// <summary>Constructs a new processing pipeline from which pixels can be retrieved.</summary>
		/// <param name="imgPath">The path to a file containing the input image.</param>
		/// <param name="settings">The settings for this processing operation.</param>
		/// <returns>A <see cref="ProcessingPipeline" /> containing the <see cref="IPixelSource" />, settings used, and basic instrumentation for the pipeline.</returns>
		public static ProcessingPipeline BuildPipeline(string imgPath, ProcessImageSettings settings)
		{
			if (imgPath is null) throw new ArgumentNullException(nameof(imgPath));

			var ctx = new PipelineContext(settings);
			var dec = new WicDecoder(imgPath, ctx);
			buildPipeline(ctx, false);
			return new ProcessingPipeline(ctx);
		}

		/// <summary>Constructs a new processing pipeline from which pixels can be retrieved.</summary>
		/// <param name="imgBuffer">A buffer containing a supported input image container.</param>
		/// <param name="settings">The settings for this processing operation.</param>
		/// <returns>A <see cref="ProcessingPipeline" /> containing the <see cref="IPixelSource" />, settings used, and basic instrumentation for the pipeline.</returns>
		public static ProcessingPipeline BuildPipeline(ReadOnlySpan<byte> imgBuffer, ProcessImageSettings settings)
		{
			if (imgBuffer == default) throw new ArgumentNullException(nameof(imgBuffer));

			var ctx = new PipelineContext(settings);
			var dec = new WicDecoder(imgBuffer, ctx);
			buildPipeline(ctx, false);
			return new ProcessingPipeline(ctx);
		}

		/// <summary>Constructs a new processing pipeline from which pixels can be retrieved.</summary>
		/// <param name="imgStream">A stream containing a supported input image container.</param>
		/// <param name="settings">The settings for this processing operation.</param>
		/// <returns>A <see cref="ProcessingPipeline" /> containing the <see cref="IPixelSource" />, settings used, and basic instrumentation for the pipeline.</returns>
		public static ProcessingPipeline BuildPipeline(Stream imgStream, ProcessImageSettings settings)
		{
			checkInStream(imgStream);

			var ctx = new PipelineContext(settings);
			var dec = new WicDecoder(imgStream, ctx);
			buildPipeline(ctx, false);
			return new ProcessingPipeline(ctx);
		}

		/// <summary>Constructs a new processing pipeline from which pixels can be retrieved.</summary>
		/// <param name="imgSource">A custom pixel source to use as input.</param>
		/// <param name="settings">The settings for this processing operation.</param>
		/// <returns>A <see cref="ProcessingPipeline" /> containing the <see cref="IPixelSource" />, settings used, and basic instrumentation for the pipeline.</returns>
		public static ProcessingPipeline BuildPipeline(IPixelSource imgSource, ProcessImageSettings settings)
		{
			if (imgSource is null) throw new ArgumentNullException(nameof(imgSource));

			var ctx = new PipelineContext(settings);
			var dec = new WicDecoder(imgSource, ctx);
			buildPipeline(ctx, false);
			return new ProcessingPipeline(ctx);
		}

		/// <summary>Completes processing of a <see cref="ProcessingPipeline" />, saving the output to <paramref name="outStream" />.</summary>
		/// <param name="pipeline">The processing pipeline attached to a pixel source.</param>
		/// <param name="outStream">The stream to which the output image will be written.</param>
		/// <returns>A <see cref="ProcessImageResult" /> containing the settings used and basic instrumentation for the pipeline.</returns>
		public static ProcessImageResult ExecutePipeline(this ProcessingPipeline pipeline, Stream outStream) =>
			executePipeline(pipeline.Context, outStream);

		private static void buildPipeline(PipelineContext ctx, bool outputPlanar = true)
		{
			var frm = new WicFrameReader(ctx, EnablePlanarPipeline);
			WicTransforms.AddMetadataReader(ctx);

			ctx.FinalizeSettings();
			ctx.Settings.UnsharpMask = ctx.UsedSettings.UnsharpMask;
			ctx.Settings.JpegQuality = ctx.UsedSettings.JpegQuality;
			ctx.Settings.JpegSubsampleMode = ctx.UsedSettings.JpegSubsampleMode;

			bool processPlanar = ctx.SupportsPlanarProcessing;
			var subsample = (WICJpegYCrCbSubsamplingOption)ctx.Settings.JpegSubsampleMode;

			if (processPlanar)
			{
				if (!ctx.Settings.AutoCrop && (int)ctx.Settings.HybridScaleRatio == 1)
				{
					if (ctx.DecoderFrame.ChromaSubsampling.IsSubsampledX() && ((ctx.Settings.Crop.X & 1) != 0 || (ctx.Settings.Crop.Width & 1) != 0))
						processPlanar = false;
					if (ctx.DecoderFrame.ChromaSubsampling.IsSubsampledY() && ((ctx.Settings.Crop.Y & 1) != 0 || (ctx.Settings.Crop.Height & 1) != 0))
						processPlanar = false;
				}

				if (ctx.Settings.SaveFormat == FileFormat.Jpeg && ctx.DecoderFrame.ExifOrientation.SwapsDimensions())
				{
					if (subsample.IsSubsampledX() && (ctx.Settings.InnerRect.Width & 1) != 0)
						outputPlanar = false;
					if (subsample.IsSubsampledY() && (ctx.Settings.InnerRect.Height & 1) != 0)
						outputPlanar = false;
				}
			}

			if (processPlanar)
			{
				var orient = ctx.DecoderFrame.ExifOrientation;
				bool savePlanar = outputPlanar
					&& ctx.Settings.SaveFormat == FileFormat.Jpeg
					&& ctx.Settings.InnerRect == ctx.Settings.OuterRect
					&& ctx.WicContext.SourceColorContext is null;

				WicTransforms.AddPlanarCache(ctx);

				MagicTransforms.AddHighQualityScaler(ctx);
				MagicTransforms.AddUnsharpMask(ctx);
				MagicTransforms.AddExternalFormatConverter(ctx);
				MagicTransforms.AddExifFlipRotator(ctx);

				ctx.PlanarLumaSource = ctx.Source;
				ctx.Source = ctx.WicContext.PlanarCache.GetPlane(WicPlane.Chroma);
				ctx.DecoderFrame.ExifOrientation = orient;
				ctx.Settings.Crop = ctx.Source.Area.UnOrient(orient, ctx.Source.Width, ctx.Source.Height).ToGdiRect();

				if (savePlanar)
				{
					if (subsample.IsSubsampledX())
						ctx.Settings.InnerRect.Width = MathUtil.DivCeiling(ctx.Settings.InnerRect.Width, 2);
					if (subsample.IsSubsampledY())
						ctx.Settings.InnerRect.Height = MathUtil.DivCeiling(ctx.Settings.InnerRect.Height, 2);
				}

				MagicTransforms.AddHighQualityScaler(ctx);
				MagicTransforms.AddExternalFormatConverter(ctx);
				MagicTransforms.AddExifFlipRotator(ctx);

				ctx.PlanarChromaSource = ctx.Source;
				ctx.Source = ctx.PlanarLumaSource;

				if (!savePlanar)
				{
					WicTransforms.AddPlanarConverter(ctx);
					MagicTransforms.AddColorspaceConverter(ctx);
					MagicTransforms.AddPad(ctx);
				}
			}
			else
			{
				WicTransforms.AddNativeScaler(ctx);
				MagicTransforms.AddCropper(ctx);
				MagicTransforms.AddHighQualityScaler(ctx, true);
				WicTransforms.AddPixelFormatConverter(ctx);
				WicTransforms.AddScaler(ctx, true);
				MagicTransforms.AddHighQualityScaler(ctx);
				MagicTransforms.AddColorspaceConverter(ctx);
				MagicTransforms.AddMatte(ctx);
				MagicTransforms.AddUnsharpMask(ctx);
				MagicTransforms.AddExifFlipRotator(ctx);
				MagicTransforms.AddPad(ctx);
			}
		}

		private static ProcessImageResult executePipeline(PipelineContext ctx, Stream ostm)
		{
			MagicTransforms.AddExternalFormatConverter(ctx);
			WicTransforms.AddIndexedColorConverter(ctx);

			var enc = new WicEncoder(ctx, ostm.AsIStream());
			enc.WriteSource(ctx);

			return new ProcessImageResult { Settings = ctx.UsedSettings, Stats = ctx.Stats };
		}
	}
}
