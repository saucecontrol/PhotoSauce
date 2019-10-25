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
			ctx.ImageContainer = WicDecoder.Create(imgPath, ctx.WicContext);

			return processImage(ctx, outStream);
		}

		public static ProcessImageResult ProcessImage(ReadOnlySpan<byte> imgBuffer, Stream outStream, ProcessImageSettings settings)
		{
			using var ctx = new PipelineContext(settings);
			ctx.ImageContainer = WicDecoder.Create(imgBuffer, ctx.WicContext);

			return processImage(ctx, outStream);
		}

		public static ProcessImageResult ProcessImage(Stream imgStream, Stream outStream, ProcessImageSettings settings)
		{
			using var ctx = new PipelineContext(settings);
			ctx.ImageContainer = WicDecoder.Create(imgStream, ctx.WicContext);

			return processImage(ctx, outStream);
		}

		private static ProcessImageResult processImage(PipelineContext ctx, Stream ostm)
		{
			ctx.DecoderFrame = new WicFrameReader(ctx.ImageContainer, ctx.Settings, ctx.WicContext);
			ctx.Source = ctx.DecoderFrame.Source!.AsPixelSource(nameof(IWICBitmapFrameDecode));

			WicTransforms.AddMetadataReader(ctx);

			ctx.FinalizeSettings();

			WicTransforms.AddNativeScaler(ctx);
			WicTransforms.AddExifFlipRotator(ctx);
			WicTransforms.AddCropper(ctx);
			WicTransforms.AddPixelFormatConverter(ctx);
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