using System;
using System.IO;
using System.Numerics;

using PhotoSauce.MagicScaler.Interop;

namespace PhotoSauce.MagicScaler
{
	public static class MagicImageProcessor
	{
		public static bool EnablePlanarPipeline { get; set; } = true;
		public static bool EnableSimd { get; set; } = Vector.IsHardwareAccelerated;

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
				return processImage(ctx, outStream);
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
				return processImage(ctx, outStream);
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
				return processImage(ctx, outStream);
			}
		}

		private static void buildPipeline(WicProcessingContext ctx)
		{
			var frm = new WicFrameReader(ctx, EnablePlanarPipeline);
			WicTransforms.AddMetadataReader(ctx);

			ctx.FinalizeSettings();

			if (ctx.DecoderFrame.SupportsPlanarPipeline)
			{
				bool savePlanar = ctx.Settings.SaveFormat == FileFormat.Jpeg && ctx.SourceColorContext == null;

				WicTransforms.AddExifRotator(ctx);
				WicTransforms.AddPlanarCache(ctx);

				MagicTransforms.AddInternalFormatConverter(ctx);
				MagicTransforms.AddHighQualityScaler(ctx);
				MagicTransforms.AddExternalFormatConverter(ctx);
				MagicTransforms.AddUnsharpMask(ctx);

				ctx.SwitchPlanarSource(WicPlane.Chroma);

				if (savePlanar)
				{
					var subsample = ctx.Settings.JpegSubsampleMode;
					if (subsample == ChromaSubsampleMode.Subsample420)
						ctx.Settings.Height = (int)Math.Ceiling(ctx.Settings.Height / 2d);

					if (subsample == ChromaSubsampleMode.Subsample420 || subsample == ChromaSubsampleMode.Subsample422)
						ctx.Settings.Width = (int)Math.Ceiling(ctx.Settings.Width / 2d);
				}

				MagicTransforms.AddInternalFormatConverter(ctx);
				MagicTransforms.AddHighQualityScaler(ctx);
				MagicTransforms.AddExternalFormatConverter(ctx);

				ctx.SwitchPlanarSource(WicPlane.Luma);

				if (!savePlanar)
				{
					WicTransforms.AddPlanarConverter(ctx);
					WicTransforms.AddColorspaceConverter(ctx);
				}
			}
			else
			{
				WicTransforms.AddNativeScaler(ctx);
				WicTransforms.AddExifRotator(ctx);
				WicTransforms.AddConditionalCache(ctx);
				WicTransforms.AddCropper(ctx);
				WicTransforms.AddPixelFormatConverter(ctx);
				WicTransforms.AddScaler(ctx, true);
				MagicTransforms.AddInternalFormatConverter(ctx);
				MagicTransforms.AddHighQualityScaler(ctx);
				MagicTransforms.AddMatte(ctx);
				MagicTransforms.AddExternalFormatConverter(ctx);
				MagicTransforms.AddUnsharpMask(ctx);
				WicTransforms.AddColorspaceConverter(ctx);
			}
		}

		private static ProcessImageResult processImage(WicProcessingContext ctx, Stream ostm)
		{
			WicTransforms.AddIndexedColorConverter(ctx);

			var enc = new WicEncoder(ctx, ostm.AsIStream());
			enc.WriteSource(ctx);

			return new ProcessImageResult { Settings = ctx.UsedSettings, Stats = ctx.Stats };
		}
	}
}
