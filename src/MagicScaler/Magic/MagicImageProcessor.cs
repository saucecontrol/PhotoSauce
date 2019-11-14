using System;
using System.IO;
using System.Numerics;
using System.Diagnostics;
using System.ComponentModel;

using PhotoSauce.Interop.Wic;

namespace PhotoSauce.MagicScaler
{
	/// <summary>Provides a set of methods for constructing or executing a MagicScaler processing pipeline.</summary>
	public static class MagicImageProcessor
	{
		/// <summary>True to allow <a href="https://en.wikipedia.org/wiki/YCbCr">Y'CbCr</a> images to be processed in their native planar format, false to force RGB conversion before processing.</summary>
		public static bool EnablePlanarPipeline { get; set; } = true;

		/// <summary>True to check for Orientation tag in Xmp metadata in addition to the default Exif metadata location, false to check Exif only.</summary>
		public static bool EnableXmpOrientation { get; set; } = false;

		/// <summary>True to enable internal <see cref="IPixelSource"/> instrumentation, false to disable.  When disabled, no <see cref="PixelSourceStats" /> will be collected for the pipeline stages.</summary>
		public static bool EnablePixelSourceStats { get; set; } = false;

		/// <summary>Overrides the default <a href="https://en.wikipedia.org/wiki/SIMD">SIMD</a> support detection to force floating point processing on or off.</summary>
		[EditorBrowsable(EditorBrowsableState.Never)]
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
			ctx.ImageContainer = WicImageContainer.Create(imgPath, ctx.WicContext);

			buildPipeline(ctx);
			return executePipeline(ctx, outStream);
		}

#pragma warning disable 1573 // not all params have docs

		/// <inheritdoc cref="ProcessImage(string, Stream, ProcessImageSettings)" />
		/// <param name="imgBuffer">A buffer containing a supported input image container.</param>
		public static ProcessImageResult ProcessImage(ReadOnlySpan<byte> imgBuffer, Stream outStream, ProcessImageSettings settings)
		{
			if (imgBuffer == default) throw new ArgumentNullException(nameof(imgBuffer));
			checkOutStream(outStream);

			using var ctx = new PipelineContext(settings);
			ctx.ImageContainer = WicImageContainer.Create(imgBuffer, ctx.WicContext);

			buildPipeline(ctx);
			return executePipeline(ctx, outStream);
		}

		/// <inheritdoc cref="ProcessImage(string, Stream, ProcessImageSettings)" />
		/// <param name="imgStream">A stream containing a supported input image container.</param>
		public static ProcessImageResult ProcessImage(Stream imgStream, Stream outStream, ProcessImageSettings settings)
		{
			checkInStream(imgStream);
			checkOutStream(outStream);

			using var ctx = new PipelineContext(settings);
			ctx.ImageContainer = WicImageContainer.Create(imgStream, ctx.WicContext);

			buildPipeline(ctx);
			return executePipeline(ctx, outStream);
		}

		/// <inheritdoc cref="ProcessImage(string, Stream, ProcessImageSettings)" />
		/// <param name="imgSource">A custom pixel source to use as input.</param>
		public static ProcessImageResult ProcessImage(IPixelSource imgSource, Stream outStream, ProcessImageSettings settings)
		{
			if (imgSource is null) throw new ArgumentNullException(nameof(imgSource));
			checkOutStream(outStream);

			using var ctx = new PipelineContext(settings) {
				ImageContainer = new PixelSourceContainer(imgSource),
				Source = imgSource.AsPixelSource()
			};

			buildPipeline(ctx);
			return executePipeline(ctx, outStream);
		}

		/// <inheritdoc cref="ProcessImage(string, Stream, ProcessImageSettings)" />
		/// <param name="imgContainer">A custom <see cref="IImageContainer"/> to use as input.</param>
		public static ProcessImageResult ProcessImage(IImageContainer imgContainer, Stream outStream, ProcessImageSettings settings)
		{
			if (imgContainer is null) throw new ArgumentNullException(nameof(imgContainer));
			checkOutStream(outStream);

			using var ctx = new PipelineContext(settings) {
				ImageContainer = imgContainer
			};

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
			ctx.ImageContainer = WicImageContainer.Create(imgPath, ctx.WicContext);

			buildPipeline(ctx, false);
			return new ProcessingPipeline(ctx);
		}

		/// <inheritdoc cref="BuildPipeline(string, ProcessImageSettings)" />
		/// <param name="imgBuffer">A buffer containing a supported input image container.</param>
		public static ProcessingPipeline BuildPipeline(ReadOnlySpan<byte> imgBuffer, ProcessImageSettings settings)
		{
			if (imgBuffer == default) throw new ArgumentNullException(nameof(imgBuffer));

			var ctx = new PipelineContext(settings);
			ctx.ImageContainer = WicImageContainer.Create(imgBuffer, ctx.WicContext);

			buildPipeline(ctx, false);
			return new ProcessingPipeline(ctx);
		}

		/// <inheritdoc cref="BuildPipeline(string, ProcessImageSettings)" />
		/// <param name="imgStream">A stream containing a supported input image container.</param>
		public static ProcessingPipeline BuildPipeline(Stream imgStream, ProcessImageSettings settings)
		{
			checkInStream(imgStream);

			var ctx = new PipelineContext(settings);
			ctx.ImageContainer = WicImageContainer.Create(imgStream, ctx.WicContext);

			buildPipeline(ctx, false);
			return new ProcessingPipeline(ctx);
		}

		/// <inheritdoc cref="BuildPipeline(string, ProcessImageSettings)" />
		/// <param name="imgSource">A custom pixel source to use as input.</param>
		public static ProcessingPipeline BuildPipeline(IPixelSource imgSource, ProcessImageSettings settings)
		{
			if (imgSource is null) throw new ArgumentNullException(nameof(imgSource));

			var ctx = new PipelineContext(settings) {
				ImageContainer = new PixelSourceContainer(imgSource),
				Source = imgSource.AsPixelSource()
			};

			buildPipeline(ctx, false);
			return new ProcessingPipeline(ctx);
		}

		/// <inheritdoc cref="BuildPipeline(string, ProcessImageSettings)" />
		/// <param name="imgContainer">A custom <see cref="IImageContainer"/> to use as input.</param>
		public static ProcessingPipeline BuildPipeline(IImageContainer imgContainer, ProcessImageSettings settings)
		{
			if (imgContainer is null) throw new ArgumentNullException(nameof(imgContainer));

			var ctx = new PipelineContext(settings) {
				ImageContainer = imgContainer
			};

			buildPipeline(ctx, false);
			return new ProcessingPipeline(ctx);
		}

#pragma warning restore 1573

		/// <summary>Completes processing of a <see cref="ProcessingPipeline" />, saving the output to <paramref name="outStream" />.</summary>
		/// <param name="pipeline">The processing pipeline attached to a pixel source.</param>
		/// <param name="outStream">The stream to which the output image will be written.</param>
		/// <returns>A <see cref="ProcessImageResult" /> containing the settings used and basic instrumentation for the pipeline.</returns>
		public static ProcessImageResult ExecutePipeline(this ProcessingPipeline pipeline, Stream outStream) =>
			executePipeline(pipeline.Context, outStream);

		private static void buildPipeline(PipelineContext ctx, bool outputPlanar = true)
		{
			ctx.ImageFrame = ctx.ImageContainer.GetFrame(ctx.Settings.FrameIndex);

			bool processPlanar = false;
			var wicFrame = ctx.ImageFrame as WicImageFrame;

			if (wicFrame != null)
			{
				processPlanar = EnablePlanarPipeline && wicFrame.SupportsPlanarProcessing && ctx.Settings.Interpolation.WeightingFunction.Support >= 0.5d;
				bool profilingPassThrough = processPlanar || (wicFrame.SupportsNativeScale && ctx.Settings.HybridScaleRatio > 1);
				ctx.Source = wicFrame.WicSource.AsPixelSource(nameof(IWICBitmapFrameDecode), !profilingPassThrough);
			}
			else if (ctx.ImageFrame is IYccImageFrame planarFrame)
			{
				processPlanar = true;
				ctx.PlanarContext = new PipelineContext.PlanarPipelineContext(planarFrame.PixelSource.AsPixelSource(), planarFrame.PixelSourceCb.AsPixelSource(), planarFrame.PixelSourceCr.AsPixelSource());
				ctx.Source = ctx.PlanarContext.SourceY;
			}

			WicTransforms.AddMetadataReader(ctx);

			ctx.FinalizeSettings();
			ctx.Settings.UnsharpMask = ctx.UsedSettings.UnsharpMask;
			ctx.Settings.JpegQuality = ctx.UsedSettings.JpegQuality;
			ctx.Settings.JpegSubsampleMode = ctx.UsedSettings.JpegSubsampleMode;

			var subsample = (WICJpegYCrCbSubsamplingOption)ctx.Settings.JpegSubsampleMode;

			if (processPlanar)
			{
				if (wicFrame != null && !ctx.Settings.AutoCrop && ctx.Settings.HybridScaleRatio == 1)
				{
					var orCrop = PixelArea.FromGdiRect(ctx.Settings.Crop).DeOrient(ctx.Orientation, ctx.Source.Width, ctx.Source.Height);

					if (wicFrame.ChromaSubsampling.IsSubsampledX() && ((orCrop.X & 1) != 0 || (orCrop.Width & 1) != 0))
						processPlanar = false;
					if (wicFrame.ChromaSubsampling.IsSubsampledY() && ((orCrop.Y & 1) != 0 || (orCrop.Height & 1) != 0))
						processPlanar = false;
				}

				if (ctx.Settings.SaveFormat == FileFormat.Jpeg && ctx.Orientation.SwapsDimensions())
				{
					if (subsample.IsSubsampledX() && (ctx.Settings.InnerRect.Width & 1) != 0)
						outputPlanar = false;
					if (subsample.IsSubsampledY() && (ctx.Settings.InnerRect.Height & 1) != 0)
						outputPlanar = false;
				}
			}

			if (processPlanar)
			{
				var orient = ctx.Orientation;
				bool savePlanar = outputPlanar
					&& ctx.Settings.SaveFormat == FileFormat.Jpeg
					&& ctx.Settings.InnerRect == ctx.Settings.OuterRect
					&& ctx.WicContext.SourceColorContext is null;

				if (wicFrame != null)
					WicTransforms.AddPlanarCache(ctx);

				Debug.Assert(ctx.PlanarContext != null);

				MagicTransforms.AddHighQualityScaler(ctx);
				MagicTransforms.AddUnsharpMask(ctx);
				MagicTransforms.AddExternalFormatConverter(ctx);
				MagicTransforms.AddExifFlipRotator(ctx);

				ctx.PlanarContext.SourceY = ctx.Source;
				ctx.Source = ctx.PlanarContext.SourceCb;

				ctx.Orientation = orient;
				ctx.Settings.Crop = ctx.Source.Area.ReOrient(orient, ctx.Source.Width, ctx.Source.Height).ToGdiRect();

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

				ctx.PlanarContext.SourceCb = ctx.Source;
				ctx.Source = ctx.PlanarContext.SourceCr;

				MagicTransforms.AddHighQualityScaler(ctx);
				MagicTransforms.AddExternalFormatConverter(ctx);
				MagicTransforms.AddExifFlipRotator(ctx);

				ctx.PlanarContext.SourceCr = ctx.Source;
				ctx.Source = ctx.PlanarContext.SourceY;

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

			return new ProcessImageResult(ctx.UsedSettings, ctx.Stats);
		}
	}
}
