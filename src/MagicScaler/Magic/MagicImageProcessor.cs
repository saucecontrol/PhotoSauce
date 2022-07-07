// Copyright © Clinton Ingram and Contributors.  Licensed under the MIT License.

#pragma warning disable CS1573 // https://github.com/dotnet/roslyn/issues/40325

using System;
using System.IO;
using System.Numerics;
using System.ComponentModel;

using PhotoSauce.MagicScaler.Transforms;

namespace PhotoSauce.MagicScaler
{
	/// <summary>Provides a set of methods for constructing a MagicScaler processing pipeline or for all-at-once processing of an image.</summary>
	public static class MagicImageProcessor
	{
		/// <summary>This property is no longer functional.  Use the <c>PhotoSauce.MagicScaler.MaxPooledBufferSize</c> <see cref="AppContext"/> value instead.</summary>
		[Obsolete($"Use {nameof(AppContext)} value {BufferPool.MaxPooledBufferSizeName} instead."), EditorBrowsable(EditorBrowsableState.Never)]
		public static bool EnableLargeBufferPool { get; set; }

		/// <summary>The property is obsolete.  Use DecoderOptions instead.</summary>
		[Obsolete($"Use {nameof(ProcessImageSettings.DecoderOptions)} with a type implementing {nameof(IPlanarDecoderOptions)} instead."), EditorBrowsable(EditorBrowsableState.Never)]
		public static bool EnablePlanarPipeline { get; set; } = true;

		/// <summary>This feature will be removed in a future version.</summary>
		[Obsolete($"This feature will be removed in a future version."), EditorBrowsable(EditorBrowsableState.Never)]
		public static bool EnableXmpOrientation { get; set; }

		/// <summary>This property is no longer functional.  Use the <c>PhotoSauce.MagicScaler.EnablePixelSourceStats</c> <see cref="AppContext"/> switch instead.</summary>
		[Obsolete($"Use {nameof(AppContext)} switch {StatsManager.SwitchName} instead."), EditorBrowsable(EditorBrowsableState.Never)]
		public static bool EnablePixelSourceStats { get; set; }

		/// <summary>Overrides the default <a href="https://en.wikipedia.org/wiki/SIMD">SIMD</a> support detection to force floating point processing on or off.</summary>
		/// <include file='Docs/Remarks.xml' path='doc/member[@name="EnableSimd"]/*'/>
		/// <value>Default value: <see langword="true" /> if the runtime/JIT and hardware support hardware-accelerated <see cref="Vector{T}" />, otherwise <see langword="false" /></value>
		[Obsolete($"This feature will be removed in a future version."), EditorBrowsable(EditorBrowsableState.Never)]
		public static bool EnableSimd { get; set; } = Vector.IsHardwareAccelerated && (Vector<float>.Count is 4 or 8);

		private static void setEncoderFromPath(ProcessImageSettings settings, string path)
		{
			if (settings.EncoderInfo is not null)
				return;

			string extension = Path.GetExtension(path);
			if (CodecManager.TryGetEncoderForFileExtension(extension, out var info))
				settings.EncoderInfo = info;
			else
				throw new NotSupportedException($"An encoder for file extension '{extension}' could not be found.");
		}

		/// <summary>All-in-one processing of an image according to the specified <paramref name="settings" />.</summary>
		/// <param name="imgPath">The path to a file containing the input image.</param>
		/// <param name="outStream">The stream to which the output image will be written. The stream must allow Seek and Write.</param>
		/// <param name="settings">The settings for this processing operation.</param>
		/// <returns>A <see cref="ProcessImageResult" /> containing the settings used and basic instrumentation for the pipeline.</returns>
		public static ProcessImageResult ProcessImage(string imgPath, Stream outStream, ProcessImageSettings settings)
		{
			Guard.ValidForOutput(outStream);
			Guard.NotNullOrEmpty(imgPath);
			Guard.NotNull(settings);

			using var fs = new FileStream(imgPath, FileMode.Open, FileAccess.Read, FileShare.Read, 1);
			using var bfs = new PoolBufferedStream(fs);
			using var ctx = new PipelineContext(settings, CodecManager.GetDecoderForStream(bfs, settings.DecoderOptions));
			buildPipeline(ctx);

			return WriteOutput(ctx, outStream);
		}

		/// <inheritdoc cref="ProcessImage(string, Stream, ProcessImageSettings)" />
		/// <param name="outPath">The path to which the output image will be written.</param>
		/// <remarks>If <paramref name="outPath"/> already exists, it will be overwritten.</remarks>
		public static ProcessImageResult ProcessImage(string imgPath, string outPath, ProcessImageSettings settings)
		{
			Guard.NotNullOrEmpty(outPath);
			Guard.NotNullOrEmpty(imgPath);
			Guard.NotNull(settings);

			setEncoderFromPath(settings, outPath);
			using var fso = new FileStream(outPath, FileMode.Create, FileAccess.Write, FileShare.Read, 1);

			return ProcessImage(imgPath, fso, settings);
		}

		/// <inheritdoc cref="ProcessImage(string, Stream, ProcessImageSettings)" />
		/// <param name="imgBuffer">A buffer containing a supported input image container.</param>
		public static unsafe ProcessImageResult ProcessImage(ReadOnlySpan<byte> imgBuffer, Stream outStream, ProcessImageSettings settings)
		{
			Guard.ValidForOutput(outStream);
			Guard.NotEmpty(imgBuffer);
			Guard.NotNull(settings);

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
		/// <remarks>If <paramref name="outPath"/> already exists, it will be overwritten.</remarks>
		public static unsafe ProcessImageResult ProcessImage(ReadOnlySpan<byte> imgBuffer, string outPath, ProcessImageSettings settings)
		{
			Guard.NotNullOrEmpty(outPath);
			Guard.NotEmpty(imgBuffer);
			Guard.NotNull(settings);

			setEncoderFromPath(settings, outPath);
			using var fso = new FileStream(outPath, FileMode.Create, FileAccess.Write, FileShare.Read, 1);

			return ProcessImage(imgBuffer, fso, settings);
		}

		/// <inheritdoc cref="ProcessImage(string, Stream, ProcessImageSettings)" />
		/// <param name="imgStream">A stream containing a supported input image container. The stream must allow Seek and Read.</param>
		public static ProcessImageResult ProcessImage(Stream imgStream, Stream outStream, ProcessImageSettings settings)
		{
			Guard.ValidForOutput(outStream);
			Guard.ValidForInput(imgStream);
			Guard.NotNull(settings);

			using var bfs = PoolBufferedStream.WrapIfFile(imgStream);
			using var ctx = new PipelineContext(settings, CodecManager.GetDecoderForStream(bfs ?? imgStream, settings.DecoderOptions));
			buildPipeline(ctx);

			return WriteOutput(ctx, outStream);
		}

		/// <inheritdoc cref="ProcessImage(string, string, ProcessImageSettings)" />
		/// <param name="imgStream">A stream containing a supported input image container. The stream must allow Seek and Read.</param>
		public static unsafe ProcessImageResult ProcessImage(Stream imgStream, string outPath, ProcessImageSettings settings)
		{
			Guard.NotNullOrEmpty(outPath);
			Guard.ValidForInput(imgStream);
			Guard.NotNull(settings);

			setEncoderFromPath(settings, outPath);
			using var fso = new FileStream(outPath, FileMode.Create, FileAccess.Write, FileShare.Read, 1);

			return ProcessImage(imgStream, fso, settings);
		}

		/// <inheritdoc cref="ProcessImage(string, Stream, ProcessImageSettings)" />
		/// <param name="imgSource">A custom pixel source to use as input.</param>
		public static ProcessImageResult ProcessImage(IPixelSource imgSource, Stream outStream, ProcessImageSettings settings)
		{
			Guard.ValidForOutput(outStream);
			Guard.NotNull(imgSource);
			Guard.NotNull(settings);

			using var ctx = new PipelineContext(settings, new PixelSourceContainer(imgSource));
			buildPipeline(ctx);

			return WriteOutput(ctx, outStream);
		}

		/// <inheritdoc cref="ProcessImage(string, string, ProcessImageSettings)" />
		/// <param name="imgSource">A custom pixel source to use as input.</param>
		public static unsafe ProcessImageResult ProcessImage(IPixelSource imgSource, string outPath, ProcessImageSettings settings)
		{
			Guard.NotNullOrEmpty(outPath);
			Guard.NotNull(imgSource);
			Guard.NotNull(settings);

			setEncoderFromPath(settings, outPath);
			using var fso = new FileStream(outPath, FileMode.Create, FileAccess.Write, FileShare.Read, 1);

			return ProcessImage(imgSource, fso, settings);
		}

		/// <inheritdoc cref="ProcessImage(string, Stream, ProcessImageSettings)" />
		/// <param name="imgContainer">A custom <see cref="IImageContainer"/> to use as input.</param>
		public static ProcessImageResult ProcessImage(IImageContainer imgContainer, Stream outStream, ProcessImageSettings settings)
		{
			Guard.ValidForOutput(outStream);
			Guard.NotNull(imgContainer);
			Guard.NotNull(settings);

			using var ctx = new PipelineContext(settings, imgContainer, false);
			buildPipeline(ctx);

			return WriteOutput(ctx, outStream);
		}

		/// <inheritdoc cref="ProcessImage(string, string, ProcessImageSettings)" />
		/// <param name="imgContainer">A custom <see cref="IImageContainer"/> to use as input.</param>
		public static unsafe ProcessImageResult ProcessImage(IImageContainer imgContainer, string outPath, ProcessImageSettings settings)
		{
			Guard.NotNullOrEmpty(outPath);
			Guard.NotNull(imgContainer);
			Guard.NotNull(settings);

			setEncoderFromPath(settings, outPath);
			using var fso = new FileStream(outPath, FileMode.Create, FileAccess.Write, FileShare.Read, 1);

			return ProcessImage(imgContainer, fso, settings);
		}

		/// <summary>Constructs a new processing pipeline from which pixels can be retrieved.</summary>
		/// <param name="imgPath">The path to a file containing the input image.</param>
		/// <param name="settings">The settings for this processing operation.</param>
		/// <returns>A <see cref="ProcessingPipeline" /> containing the <see cref="IPixelSource" />, settings used, and basic instrumentation for the pipeline.</returns>
		public static ProcessingPipeline BuildPipeline(string imgPath, ProcessImageSettings settings)
		{
			Guard.NotNullOrEmpty(imgPath);
			Guard.NotNull(settings);

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
			Guard.ValidForInput(imgStream);
			Guard.NotNull(settings);

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
			Guard.NotNull(imgSource);
			Guard.NotNull(settings);

			var ctx = new PipelineContext(settings, new PixelSourceContainer(imgSource));
			buildPipeline(ctx, false);

			return new ProcessingPipeline(ctx);
		}

		/// <inheritdoc cref="BuildPipeline(string, ProcessImageSettings)" />
		/// <param name="imgContainer">A custom <see cref="IImageContainer"/> to use as input.</param>
		public static ProcessingPipeline BuildPipeline(IImageContainer imgContainer, ProcessImageSettings settings)
		{
			Guard.NotNull(imgContainer);
			Guard.NotNull(settings);

			var ctx = new PipelineContext(settings, imgContainer, false);
			buildPipeline(ctx, false);

			return new ProcessingPipeline(ctx);
		}

		internal static unsafe ProcessImageResult WriteOutput(PipelineContext ctx, Stream ostm)
		{
			MagicTransforms.AddExternalFormatConverter(ctx, true);

			using var bfs = PoolBufferedStream.WrapIfFile(ostm);
			using var enc = ctx.Settings.EncoderInfo!.Factory(bfs ?? ostm, ctx.Settings.EncoderOptions);

			if (ctx.IsAnimationPipeline && enc is WicImageEncoder wenc)
			{
				using var gif = new WicAnimatedGifEncoder(ctx, wenc);
				gif.WriteGlobalMetadata();
				gif.WriteFrames();
			}
			else
			{
				MagicTransforms.AddIndexedColorConverter(ctx);
				enc.WriteFrame(ctx.Source, ctx.Metadata, PixelArea.Default);
			}

			enc.Commit();

			return new ProcessImageResult(ctx.UsedSettings, ctx.Stats);
		}

		private static unsafe void buildPipeline(PipelineContext ctx, bool closedPipeline = true)
		{
			ctx.Settings.ColorProfileMode = closedPipeline ? ctx.Settings.ColorProfileMode : ColorProfileMode.ConvertToSrgb;

			ctx.ImageFrame = ctx.ImageContainer.GetFrame(0);
			ctx.Source = ctx.ImageFrame.PixelSource.AsPixelSource();
			ctx.Metadata = new MagicMetadataFilter(ctx);

			MagicTransforms.AddAnimationFrameBuffer(ctx);

			ctx.FinalizeSettings();
			ctx.Settings.UnsharpMask = ctx.UsedSettings.UnsharpMask;
			ctx.Settings.EncoderOptions = ctx.UsedSettings.EncoderOptions;

			bool outputPlanar = closedPipeline;

			if (ctx.Settings.ScaleRatio != 1d && !ctx.Settings.Interpolation.IsPointSampler && ctx.Source is IFramePixelSource fsrc && fsrc.Frame is IPlanarDecoder pldec)
			{
				if (pldec.TryGetYccFrame(out var plfrm, true))
					ctx.ImageFrame = plfrm;
			}

			if (ctx.ImageFrame is IYccImageFrame yccFrame)
			{
				outputPlanar = outputPlanar && yccFrame.RgbYccMatrix.IsRouglyEqualTo(YccMatrix.Rec601);

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
					&& ctx.Settings.EncoderInfo is IPlanarImageEncoderInfo
					&& ctx.Settings.OuterSize == ctx.Settings.InnerSize
					&& ctx.DestColorProfile == ctx.SourceColorProfile;

				MagicTransforms.AddCropper(ctx);
				MagicTransforms.AddHybridScaler(ctx);
				MagicTransforms.AddHighQualityScaler(ctx, savePlanar);
				MagicTransforms.AddUnsharpMask(ctx);

				if (savePlanar)
				{
					MagicTransforms.AddExternalFormatConverter(ctx);
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
}
