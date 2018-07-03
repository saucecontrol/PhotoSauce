using System;
using System.IO;
using System.Numerics;

using PhotoSauce.MagicScaler.Interop;

namespace PhotoSauce.MagicScaler
{
	public static class MagicImageProcessor
	{
		public static bool EnablePlanarPipeline { get; set; } = true;
		public static bool EnableSimd { get; set; } = Vector.IsHardwareAccelerated && (Vector<float>.Count == 4 || Vector<float>.Count == 8);

		private static void checkInStream(Stream imgStream)
		{
			if (imgStream == null) throw new ArgumentNullException(nameof(imgStream));
			if (!imgStream.CanSeek || !imgStream.CanRead) throw new ArgumentException("Input Stream must allow Seek and Read", nameof(imgStream));
			if (imgStream.Length <= 0 || imgStream.Position >= imgStream.Length) throw new ArgumentException("Input Stream is empty or positioned at its end", nameof(imgStream));
		}

		private static void checkOutStream(Stream outStream)
		{
			if (outStream == null) throw new ArgumentNullException(nameof(outStream));
			if (!outStream.CanSeek || !outStream.CanWrite) throw new ArgumentException("Output Stream must allow Seek and Write", nameof(outStream));
		}

		public static ProcessImageResult ProcessImage(string imgPath, Stream outStream, ProcessImageSettings settings)
		{
			if (imgPath == null) throw new ArgumentNullException(nameof(imgPath));
			checkOutStream(outStream);

			using (var ctx = new WicProcessingContext(settings))
			{
				var dec = new WicDecoder(imgPath, ctx);
				buildPipeline(ctx);
				return executePipeline(ctx, outStream);
			}
		}

		public static ProcessImageResult ProcessImage(ArraySegment<byte> imgBuffer, Stream outStream, ProcessImageSettings settings)
		{
			if (imgBuffer == null) throw new ArgumentNullException(nameof(imgBuffer));
			checkOutStream(outStream);

			using (var ctx = new WicProcessingContext(settings))
			{
				var dec = new WicDecoder(imgBuffer, ctx);
				buildPipeline(ctx);
				return executePipeline(ctx, outStream);
			}
		}

		public static ProcessImageResult ProcessImage(Stream imgStream, Stream outStream, ProcessImageSettings settings)
		{
			checkInStream(imgStream);
			checkOutStream(outStream);

			using (var ctx = new WicProcessingContext(settings))
			{
				var dec = new WicDecoder(imgStream, ctx);
				buildPipeline(ctx);
				return executePipeline(ctx, outStream);
			}
		}

		public static ProcessImageResult ProcessImage(IPixelSource imgSource, Stream outStream, ProcessImageSettings settings)
		{
			if (imgSource == null) throw new ArgumentNullException(nameof(imgSource));
			checkOutStream(outStream);

			using (var ctx = new WicProcessingContext(settings))
			{
				var dec = new WicDecoder(imgSource, ctx);
				buildPipeline(ctx);
				return executePipeline(ctx, outStream);
			}
		}

		public static ProcessingPipeline BuildPipeline(string imgPath, ProcessImageSettings settings)
		{
			if (imgPath == null) throw new ArgumentNullException(nameof(imgPath));

			var ctx = new WicProcessingContext(settings);
			var dec = new WicDecoder(imgPath, ctx);
			buildPipeline(ctx, false);
			return new ProcessingPipeline(ctx);
		}

		public static ProcessingPipeline BuildPipeline(ArraySegment<byte> imgBuffer, ProcessImageSettings settings)
		{
			if (imgBuffer == null) throw new ArgumentNullException(nameof(imgBuffer));
			if (imgBuffer.Offset != 0) throw new ArgumentException($"{nameof(imgBuffer.Offset)} must be 0", nameof(imgBuffer));

			var ctx = new WicProcessingContext(settings);
			var dec = new WicDecoder(imgBuffer, ctx);
			buildPipeline(ctx, false);
			return new ProcessingPipeline(ctx);
		}

		public static ProcessingPipeline BuildPipeline(Stream imgStream, ProcessImageSettings settings)
		{
			checkInStream(imgStream);

			var ctx = new WicProcessingContext(settings);
			var dec = new WicDecoder(imgStream, ctx);
			buildPipeline(ctx, false);
			return new ProcessingPipeline(ctx);
		}

		public static ProcessingPipeline BuildPipeline(IPixelSource imgSource, ProcessImageSettings settings)
		{
			if (imgSource == null) throw new ArgumentNullException(nameof(imgSource));

			var ctx = new WicProcessingContext(settings);
			var dec = new WicDecoder(imgSource, ctx);
			buildPipeline(ctx, false);
			return new ProcessingPipeline(ctx);
		}

		public static ProcessImageResult ExecutePipeline(this ProcessingPipeline pipeline, Stream outStream)
		{
			return executePipeline(pipeline.Context, outStream);
		}

		private static void buildPipeline(WicProcessingContext ctx, bool outputPlanar = true)
		{
			var frm = new WicFrameReader(ctx, EnablePlanarPipeline);
			WicTransforms.AddMetadataReader(ctx);

			ctx.FinalizeSettings();
			ctx.Settings.UnsharpMask = ctx.UsedSettings.UnsharpMask;
			ctx.Settings.JpegQuality = ctx.UsedSettings.JpegQuality;
			ctx.Settings.JpegSubsampleMode = ctx.UsedSettings.JpegSubsampleMode;

			if (ctx.DecoderFrame.SupportsPlanarPipeline)
			{
				bool savePlanar = outputPlanar
					&& ctx.Settings.SaveFormat == FileFormat.Jpeg
					&& ctx.Settings.InnerRect == ctx.Settings.OuterRect
					&& ctx.SourceColorContext == null;

				WicTransforms.AddExifRotator(ctx);
				WicTransforms.AddPlanarCache(ctx);

				MagicTransforms.AddHighQualityScaler(ctx);
				MagicTransforms.AddUnsharpMask(ctx);
				MagicTransforms.AddExternalFormatConverter(ctx);

				ctx.SwitchPlanarSource(WicPlane.Chroma);

				if (savePlanar)
				{
					var subsample = ctx.Settings.JpegSubsampleMode;
					if (subsample == ChromaSubsampleMode.Subsample420)
						ctx.Settings.InnerRect.Height = (int)Math.Ceiling(ctx.Settings.InnerRect.Height / 2d);

					if (subsample == ChromaSubsampleMode.Subsample420 || subsample == ChromaSubsampleMode.Subsample422)
						ctx.Settings.InnerRect.Width = (int)Math.Ceiling(ctx.Settings.InnerRect.Width / 2d);
				}

				MagicTransforms.AddHighQualityScaler(ctx);
				MagicTransforms.AddExternalFormatConverter(ctx);

				ctx.SwitchPlanarSource(WicPlane.Luma);

				if (!savePlanar)
				{
					WicTransforms.AddPlanarConverter(ctx);
					MagicTransforms.AddColorspaceConverter(ctx);
					MagicTransforms.AddExternalFormatConverter(ctx);
					MagicTransforms.AddPad(ctx);
				}
			}
			else
			{
				WicTransforms.AddNativeScaler(ctx);
				WicTransforms.AddExifRotator(ctx);
				WicTransforms.AddConditionalCache(ctx);
				WicTransforms.AddCropper(ctx);
				MagicTransforms.AddHighQualityScaler(ctx, true);
				WicTransforms.AddPixelFormatConverter(ctx);
				WicTransforms.AddScaler(ctx, true);
				MagicTransforms.AddHighQualityScaler(ctx);
				MagicTransforms.AddColorspaceConverter(ctx);
				MagicTransforms.AddMatte(ctx);
				MagicTransforms.AddUnsharpMask(ctx);
				MagicTransforms.AddExternalFormatConverter(ctx);
				MagicTransforms.AddPad(ctx);
			}
		}

		private static ProcessImageResult executePipeline(WicProcessingContext ctx, Stream ostm)
		{
			WicTransforms.AddIndexedColorConverter(ctx);

			var enc = new WicEncoder(ctx, ostm.AsIStream());
			enc.WriteSource(ctx);

			return new ProcessImageResult { Settings = ctx.UsedSettings, Stats = ctx.Stats };
		}
	}
}
