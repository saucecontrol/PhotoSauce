// Copyright © Clinton Ingram and Contributors.  Licensed under the MIT License.

#if WICPROCESSOR
#pragma warning disable CS1591
using System;
using System.IO;

using PhotoSauce.MagicScaler.Transforms;

namespace PhotoSauce.MagicScaler;

public static class WicImageProcessor
{
	public static ProcessImageResult ProcessImage(string imgPath, Stream outStream, ProcessImageSettings settings)
	{
		using var ctx = new PipelineContext(settings, WicImageDecoder.Load(imgPath, settings.DecoderOptions));

		return processImage(ctx, outStream);
	}

	public static unsafe ProcessImageResult ProcessImage(ReadOnlySpan<byte> imgBuffer, Stream outStream, ProcessImageSettings settings)
	{
		fixed (byte* pbBuffer = imgBuffer)
		{
			using var ctx = new PipelineContext(settings, WicImageDecoder.Load(pbBuffer, imgBuffer.Length, settings.DecoderOptions));

			return processImage(ctx, outStream);
		}
	}

	public static ProcessImageResult ProcessImage(Stream imgStream, Stream outStream, ProcessImageSettings settings)
	{
		using var ctx = new PipelineContext(settings, WicImageDecoder.Load(imgStream, settings.DecoderOptions));

		return processImage(ctx, outStream);
	}

	private static ProcessImageResult processImage(PipelineContext ctx, Stream ostm)
	{
		ctx.ImageFrame = ctx.ImageContainer.GetFrame(0);
		ctx.Source = ctx.ImageFrame.PixelSource.AsPixelSource();
		ctx.Metadata = new MagicMetadataFilter(ctx);

		MagicTransforms.AddAnimationFrameBuffer(ctx);

		ctx.FinalizeSettings();
		ctx.Settings.EncoderOptions = ctx.UsedSettings.EncoderOptions;

		MagicTransforms.AddNativeScaler(ctx);
		ctx.AddProfiler(ctx.Source);

		WicTransforms.AddColorProfileReader(ctx);
		WicTransforms.AddExifFlipRotator(ctx);
		WicTransforms.AddCropper(ctx);
		WicTransforms.AddPixelFormatConverter(ctx);
		WicTransforms.AddHybridScaler(ctx);
		WicTransforms.AddScaler(ctx);
		WicTransforms.AddColorspaceConverter(ctx);
		MagicTransforms.AddMatte(ctx);
		MagicTransforms.AddPad(ctx);
		WicTransforms.AddIndexedColorConverter(ctx);
		MagicTransforms.AddExternalFormatConverter(ctx, true);

		using var enc = ctx.Settings.EncoderInfo!.Factory(ostm, ctx.Settings.EncoderOptions);
		enc.WriteFrame(ctx.Source, ctx.Metadata, PixelArea.Default);
		enc.Commit();

		return new ProcessImageResult(ctx.UsedSettings, ctx.Stats);
	}
}
#endif