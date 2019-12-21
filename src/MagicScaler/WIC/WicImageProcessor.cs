#pragma warning disable CS1591 // XML Comments

using System;
using System.IO;
using System.ComponentModel;

using PhotoSauce.Interop.Wic;

namespace PhotoSauce.MagicScaler
{
	[Obsolete("This class is meant only for testing/benchmarking and will be removed in a future version"), EditorBrowsable(EditorBrowsableState.Never)]
	public static class WicImageProcessor
	{
		public static ProcessImageResult ProcessImage(string imgPath, Stream outStream, ProcessImageSettings settings)
		{
			using var ctx = new PipelineContext(settings);
			ctx.ImageContainer = WicImageContainer.Create(imgPath, ctx.WicContext);

			return processImage(ctx, outStream);
		}

		public static ProcessImageResult ProcessImage(ReadOnlySpan<byte> imgBuffer, Stream outStream, ProcessImageSettings settings)
		{
			using var ctx = new PipelineContext(settings);
			ctx.ImageContainer = WicImageContainer.Create(imgBuffer, ctx.WicContext);

			return processImage(ctx, outStream);
		}

		public static ProcessImageResult ProcessImage(Stream imgStream, Stream outStream, ProcessImageSettings settings)
		{
			using var ctx = new PipelineContext(settings);
			ctx.ImageContainer = WicImageContainer.Create(imgStream, ctx.WicContext);

			return processImage(ctx, outStream);
		}

		private static ProcessImageResult processImage(PipelineContext ctx, Stream ostm)
		{
			var frame = (WicImageFrame)ctx.ImageContainer.GetFrame(ctx.Settings.FrameIndex);

			ctx.ImageFrame = frame;
			ctx.Source = frame.Source;
			ctx.FinalizeSettings();

			WicTransforms.AddColorProfileReader(ctx);
			WicTransforms.AddNativeScaler(ctx);
			WicTransforms.AddExifFlipRotator(ctx);
			WicTransforms.AddCropper(ctx);
			WicTransforms.AddPixelFormatConverter(ctx);
			WicTransforms.AddHybridScaler(ctx);
			WicTransforms.AddScaler(ctx);
			WicTransforms.AddColorspaceConverter(ctx);
			MagicTransforms.AddMatte(ctx);
			MagicTransforms.AddPad(ctx);
			WicTransforms.AddIndexedColorConverter(ctx);

			var enc = new WicEncoder(ctx, ostm.AsIStream());
			enc.WriteSource(ctx);

			return new ProcessImageResult(ctx.UsedSettings, ctx.Stats);
		}
	}
}