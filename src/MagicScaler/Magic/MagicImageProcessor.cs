// Copyright © Clinton Ingram and Contributors
// SPDX-License-Identifier: MIT

using System;
using System.IO;
using System.Numerics;
using System.ComponentModel;

using PhotoSauce.MagicScaler.Transforms;

namespace PhotoSauce.MagicScaler;

/// <summary>Provides a set of methods for constructing a MagicScaler processing pipeline or for all-at-once processing of an image.</summary>
public static class MagicImageProcessor
{
	/// <summary>Overrides the default <a href="https://en.wikipedia.org/wiki/SIMD">SIMD</a> support detection to force floating point processing on or off.</summary>
	/// <value>Default value: <see langword="true" /> if the runtime/JIT and hardware support hardware-accelerated <see cref="Vector{T}" />, otherwise <see langword="false" /></value>
	[Obsolete($"This feature will be removed in a future version."), EditorBrowsable(EditorBrowsableState.Never)]
	public static bool EnableSimd { get; set; } = Vector.IsHardwareAccelerated && (Vector<float>.Count is 4 or 8);

	/// <summary>All-in-one processing of an image according to the specified <paramref name="settings" />.</summary>
	/// <param name="imgPath">The path to a file containing the input image.</param>
	/// <param name="outStream">The stream to which the output image will be written. The stream must allow Seek and Write.</param>
	/// <param name="settings">The settings for this processing operation.</param>
	/// <returns>A <see cref="ProcessImageResult" /> containing the settings used and basic instrumentation for the pipeline.</returns>
	public static ProcessImageResult ProcessImage(string imgPath, Stream outStream, ProcessImageSettings settings)
	{
		ThrowHelper.ThrowIfNotValidForOutput(outStream);
		ThrowHelper.ThrowIfNullOrEmpty(imgPath);
		ThrowHelper.ThrowIfNull(settings);

		using var fs = new FileStream(imgPath, FileMode.Open, FileAccess.Read, FileShare.Read, 1);
		using var bfs = new PoolBufferedStream(fs);
		using var ctx = new PipelineContext(settings, CodecManager.GetDecoderForStream(bfs, settings.DecoderOptions));
		buildPipeline(ctx);

		return WriteOutput(ctx, outStream);
	}

	/// <inheritdoc cref="ProcessImage(string, Stream, ProcessImageSettings)" />
	/// <param name="outPath">The path to which the output image will be written.</param>
	/// <remarks>If <paramref name="outPath" /> already exists, it will be overwritten.</remarks>
	public static ProcessImageResult ProcessImage(string imgPath, string outPath, ProcessImageSettings settings)
	{
		ThrowHelper.ThrowIfNullOrEmpty(outPath);
		ThrowHelper.ThrowIfNullOrEmpty(imgPath);
		ThrowHelper.ThrowIfNull(settings);

		using var fsi = new FileStream(imgPath, FileMode.Open, FileAccess.Read, FileShare.Read, 1);
		using var bfs = new PoolBufferedStream(fsi);
		using var ctx = new PipelineContext(settings, CodecManager.GetDecoderForStream(bfs, settings.DecoderOptions));
		ctx.Settings.EncoderInfo ??= getEncoderFromPath(outPath);
		buildPipeline(ctx);

		using var fso = new FileStream(outPath, FileMode.Create, FileAccess.Write, FileShare.Read, 1);
		return WriteOutput(ctx, fso);
	}

	/// <inheritdoc cref="ProcessImage(string, Stream, ProcessImageSettings)" />
	/// <param name="imgBuffer">A buffer containing a supported input image container.</param>
	public static unsafe ProcessImageResult ProcessImage(ReadOnlySpan<byte> imgBuffer, Stream outStream, ProcessImageSettings settings)
	{
		ThrowHelper.ThrowIfNotValidForOutput(outStream);
		ThrowHelper.ThrowIfEmpty(imgBuffer);
		ThrowHelper.ThrowIfNull(settings);

		fixed (byte* pbBuffer = imgBuffer)
		{
			using var ums = new UnmanagedMemoryStream(pbBuffer, imgBuffer.Length);
			using var ctx = new PipelineContext(settings, CodecManager.GetDecoderForStream(ums, settings.DecoderOptions));
			buildPipeline(ctx);

			return WriteOutput(ctx, outStream);
		}
	}

	/// <inheritdoc cref="ProcessImage(string, Stream, ProcessImageSettings)" />
	/// <param name="imgBuffer">A buffer containing a supported input image container.</param>
	/// <param name="outPath">The path to which the output image will be written.</param>
	/// <remarks>If <paramref name="outPath" /> already exists, it will be overwritten.</remarks>
	public static unsafe ProcessImageResult ProcessImage(ReadOnlySpan<byte> imgBuffer, string outPath, ProcessImageSettings settings)
	{
		ThrowHelper.ThrowIfNullOrEmpty(outPath);
		ThrowHelper.ThrowIfEmpty(imgBuffer);
		ThrowHelper.ThrowIfNull(settings);

		fixed (byte* pbBuffer = imgBuffer)
		{
			using var ums = new UnmanagedMemoryStream(pbBuffer, imgBuffer.Length);
			using var ctx = new PipelineContext(settings, CodecManager.GetDecoderForStream(ums, settings.DecoderOptions));
			ctx.Settings.EncoderInfo ??= getEncoderFromPath(outPath);
			buildPipeline(ctx);

			using var fso = new FileStream(outPath, FileMode.Create, FileAccess.Write, FileShare.Read, 1);
			return WriteOutput(ctx, fso);
		}
	}

	/// <inheritdoc cref="ProcessImage(string, Stream, ProcessImageSettings)" />
	/// <param name="imgStream">A stream containing a supported input image container. The stream must allow Seek and Read.</param>
	public static ProcessImageResult ProcessImage(Stream imgStream, Stream outStream, ProcessImageSettings settings)
	{
		ThrowHelper.ThrowIfNotValidForOutput(outStream);
		ThrowHelper.ThrowIfNotValidForInput(imgStream);
		ThrowHelper.ThrowIfNull(settings);

		using var bfs = PoolBufferedStream.WrapIfFile(imgStream);
		using var ctx = new PipelineContext(settings, CodecManager.GetDecoderForStream(bfs ?? imgStream, settings.DecoderOptions));
		buildPipeline(ctx);

		return WriteOutput(ctx, outStream);
	}

	/// <inheritdoc cref="ProcessImage(string, string, ProcessImageSettings)" />
	/// <param name="imgStream">A stream containing a supported input image container. The stream must allow Seek and Read.</param>
	public static unsafe ProcessImageResult ProcessImage(Stream imgStream, string outPath, ProcessImageSettings settings)
	{
		ThrowHelper.ThrowIfNullOrEmpty(outPath);
		ThrowHelper.ThrowIfNotValidForInput(imgStream);
		ThrowHelper.ThrowIfNull(settings);

		using var bfs = PoolBufferedStream.WrapIfFile(imgStream);
		using var ctx = new PipelineContext(settings, CodecManager.GetDecoderForStream(bfs ?? imgStream, settings.DecoderOptions));
		ctx.Settings.EncoderInfo ??= getEncoderFromPath(outPath);
		buildPipeline(ctx);

		using var fso = new FileStream(outPath, FileMode.Create, FileAccess.Write, FileShare.Read, 1);
		return WriteOutput(ctx, fso);
	}

	/// <inheritdoc cref="ProcessImage(string, Stream, ProcessImageSettings)" />
	/// <param name="imgSource">A custom pixel source to use as input.</param>
	public static ProcessImageResult ProcessImage(IPixelSource imgSource, Stream outStream, ProcessImageSettings settings)
	{
		ThrowHelper.ThrowIfNotValidForOutput(outStream);
		ThrowHelper.ThrowIfNull(imgSource);
		ThrowHelper.ThrowIfNull(settings);

		using var ctx = new PipelineContext(settings, new PixelSourceContainer(imgSource));
		buildPipeline(ctx);

		return WriteOutput(ctx, outStream);
	}

	/// <inheritdoc cref="ProcessImage(string, string, ProcessImageSettings)" />
	/// <param name="imgSource">A custom pixel source to use as input.</param>
	public static unsafe ProcessImageResult ProcessImage(IPixelSource imgSource, string outPath, ProcessImageSettings settings)
	{
		ThrowHelper.ThrowIfNullOrEmpty(outPath);
		ThrowHelper.ThrowIfNull(imgSource);
		ThrowHelper.ThrowIfNull(settings);

		using var ctx = new PipelineContext(settings, new PixelSourceContainer(imgSource));
		ctx.Settings.EncoderInfo ??= getEncoderFromPath(outPath);
		buildPipeline(ctx);

		using var fso = new FileStream(outPath, FileMode.Create, FileAccess.Write, FileShare.Read, 1);
		return WriteOutput(ctx, fso);
	}

	/// <inheritdoc cref="ProcessImage(string, Stream, ProcessImageSettings)" />
	/// <param name="imgContainer">A custom <see cref="IImageContainer" /> to use as input.</param>
	public static ProcessImageResult ProcessImage(IImageContainer imgContainer, Stream outStream, ProcessImageSettings settings)
	{
		ThrowHelper.ThrowIfNotValidForOutput(outStream);
		ThrowHelper.ThrowIfNull(imgContainer);
		ThrowHelper.ThrowIfNull(settings);

		using var ctx = new PipelineContext(settings, imgContainer, false);
		buildPipeline(ctx);

		return WriteOutput(ctx, outStream);
	}

	/// <inheritdoc cref="ProcessImage(string, string, ProcessImageSettings)" />
	/// <param name="imgContainer">A custom <see cref="IImageContainer" /> to use as input.</param>
	public static unsafe ProcessImageResult ProcessImage(IImageContainer imgContainer, string outPath, ProcessImageSettings settings)
	{
		ThrowHelper.ThrowIfNullOrEmpty(outPath);
		ThrowHelper.ThrowIfNull(imgContainer);
		ThrowHelper.ThrowIfNull(settings);

		using var ctx = new PipelineContext(settings, imgContainer, false);
		ctx.Settings.EncoderInfo ??= getEncoderFromPath(outPath);
		buildPipeline(ctx);

		using var fso = new FileStream(outPath, FileMode.Create, FileAccess.Write, FileShare.Read, 1);
		return WriteOutput(ctx, fso);
	}

	/// <summary>Constructs a new processing pipeline from which pixels can be retrieved.</summary>
	/// <param name="imgPath">The path to a file containing the input image.</param>
	/// <param name="settings">The settings for this processing operation.</param>
	/// <returns>A <see cref="ProcessingPipeline" /> containing the <see cref="IPixelSource" />, settings used, and basic instrumentation for the pipeline.</returns>
	public static ProcessingPipeline BuildPipeline(string imgPath, ProcessImageSettings settings)
	{
		ThrowHelper.ThrowIfNullOrEmpty(imgPath);
		ThrowHelper.ThrowIfNull(settings);

		var fs = new FileStream(imgPath, FileMode.Open, FileAccess.Read, FileShare.Read, 1);
		var bfs = new PoolBufferedStream(fs, true);
		var ctx = new PipelineContext(settings, CodecManager.GetDecoderForStream(bfs, settings.DecoderOptions));
		ctx.AddDispose(bfs);
		buildPipeline(ctx, false);

		return new ProcessingPipeline(ctx);
	}

	/// <inheritdoc cref="BuildPipeline(string, ProcessImageSettings)" />
	/// <param name="imgStream">A stream containing a supported input image container. The stream must allow Seek and Read.</param>
	public static ProcessingPipeline BuildPipeline(Stream imgStream, ProcessImageSettings settings)
	{
		ThrowHelper.ThrowIfNotValidForInput(imgStream);
		ThrowHelper.ThrowIfNull(settings);

		var bfs = PoolBufferedStream.WrapIfFile(imgStream);
		var ctx = new PipelineContext(settings, CodecManager.GetDecoderForStream(bfs ?? imgStream, settings.DecoderOptions));
		if (bfs is not null)
			ctx.AddDispose(bfs);

		buildPipeline(ctx, false);

		return new ProcessingPipeline(ctx);
	}

	/// <inheritdoc cref="BuildPipeline(string, ProcessImageSettings)" />
	/// <param name="imgSource">A custom pixel source to use as input.</param>
	public static ProcessingPipeline BuildPipeline(IPixelSource imgSource, ProcessImageSettings settings)
	{
		ThrowHelper.ThrowIfNull(imgSource);
		ThrowHelper.ThrowIfNull(settings);

		var ctx = new PipelineContext(settings, new PixelSourceContainer(imgSource));
		buildPipeline(ctx, false);

		return new ProcessingPipeline(ctx);
	}

	/// <inheritdoc cref="BuildPipeline(string, ProcessImageSettings)" />
	/// <param name="imgContainer">A custom <see cref="IImageContainer" /> to use as input.</param>
	public static ProcessingPipeline BuildPipeline(IImageContainer imgContainer, ProcessImageSettings settings)
	{
		ThrowHelper.ThrowIfNull(imgContainer);
		ThrowHelper.ThrowIfNull(settings);

		var ctx = new PipelineContext(settings, imgContainer, false);
		buildPipeline(ctx, false);

		return new ProcessingPipeline(ctx);
	}

	internal static unsafe ProcessImageResult WriteOutput(PipelineContext ctx, Stream ostm)
	{
		MagicTransforms.AddExternalFormatConverter(ctx);

		using var bfs = PoolBufferedStream.WrapIfFile(ostm);
		using var enc = ctx.Settings.EncoderInfo!.Factory(bfs ?? ostm, ctx.Settings.EncoderOptions);

		if (ctx.IsAnimationPipeline && enc is IAnimatedImageEncoder aenc)
		{
			using var anienc = new AnimationEncoder(ctx, aenc);
			anienc.WriteGlobalMetadata();
			anienc.WriteFrames();
		}
		else
		{
			MagicTransforms.AddIndexedColorConverter(ctx);
			MagicTransforms.AddExternalFormatConverter(ctx, true);
			enc.WriteFrame(ctx.Source, ctx.Metadata, PixelArea.Default);
		}

		enc.Commit();

		return new ProcessImageResult(ctx.UsedSettings, ctx.Stats);
	}

	private static IImageEncoderInfo getEncoderFromPath(string path)
	{
		string extension = Path.GetExtension(path);
		if (!CodecManager.TryGetEncoderForFileExtension(extension, out var info))
			throw new NotSupportedException($"An encoder for file extension '{extension}' could not be found.");

		return info;
	}

	private static unsafe void buildPipeline(PipelineContext ctx, bool closedPipeline = true)
	{
		if (!closedPipeline && ctx.Settings.ColorProfileMode <= ColorProfileMode.NormalizeAndEmbed)
			ctx.Settings.ColorProfileMode = ColorProfileMode.ConvertToSrgb;

		ctx.ImageFrame = ctx.ImageContainer.GetFrame(0);
		ctx.Source = ctx.ImageFrame.PixelSource.AsPixelSource();
		ctx.Metadata = new MagicMetadataFilter(ctx);

		MagicTransforms.AddAnimationFrameBuffer(ctx);

		ctx.FinalizeSettings();
		ctx.Settings.UnsharpMask = ctx.UsedSettings.UnsharpMask;
		ctx.Settings.EncoderOptions = ctx.UsedSettings.EncoderOptions;

		if (ctx.Settings.ScaleRatio != 1d && !ctx.Settings.Interpolation.IsPointSampler && ctx.Source is IFramePixelSource fsrc && fsrc.Frame is IPlanarDecoder pldec)
		{
			if (pldec.TryGetYccFrame(out var plfrm))
				ctx.ImageFrame = plfrm;
		}

		bool outputPlanar = closedPipeline;
		var planarEncoder = ctx.Settings.EncoderInfo as IPlanarImageEncoderInfo;
		if (ctx.ImageFrame is IYccImageFrame yccFrame)
		{
			var matrix = planarEncoder?.DefaultMatrix ?? YccMatrix.Rec601;
			outputPlanar = outputPlanar && yccFrame.RgbYccMatrix.IsRoughlyEqualTo(matrix);

			ctx.Source = new PlanarPixelSource(yccFrame);
		}

		MagicTransforms.AddNativeCropper(ctx);
		MagicTransforms.AddNativeScaler(ctx);

		if ((ctx.Source is PlanarPixelSource plan ? plan.SourceY : ctx.Source) is IProfileSource prof)
			ctx.AddProfiler(prof);

		MagicTransforms.AddColorProfileReader(ctx);

		if (ctx.Source is PlanarPixelSource)
		{
			bool savePlanar = outputPlanar
				&& planarEncoder is not null
				&& ctx.Settings.OuterSize == ctx.Settings.InnerSize
				&& ctx.DestColorProfile == ctx.SourceColorProfile;

			MagicTransforms.AddCropper(ctx);
			MagicTransforms.AddHybridScaler(ctx);
			MagicTransforms.AddHighQualityScaler(ctx, savePlanar);
			MagicTransforms.AddUnsharpMask(ctx);

			if (savePlanar)
			{
				MagicTransforms.AddExternalFormatConverter(ctx, true);
				MagicTransforms.AddExifFlipRotator(ctx);
			}
			else
			{
				MagicTransforms.AddPlanarConverter(ctx);
				MagicTransforms.AddColorspaceConverter(ctx);
				MagicTransforms.AddExifFlipRotator(ctx);
				MagicTransforms.AddPad(ctx);
			}
		}
		else
		{
			MagicTransforms.AddCropper(ctx);
			MagicTransforms.AddHybridScaler(ctx);
			MagicTransforms.AddNormalizingFormatConverter(ctx);
			MagicTransforms.AddHybridScaler(ctx);
			MagicTransforms.AddHighQualityScaler(ctx);
			MagicTransforms.AddColorspaceConverter(ctx);
			MagicTransforms.AddMatte(ctx);
			MagicTransforms.AddUnsharpMask(ctx);
			MagicTransforms.AddExifFlipRotator(ctx);
			MagicTransforms.AddPad(ctx);
		}
	}
}
