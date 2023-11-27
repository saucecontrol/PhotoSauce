// Copyright © Clinton Ingram and Contributors.  Licensed under the MIT License.

using System;
using System.Runtime.InteropServices;
using System.Diagnostics.CodeAnalysis;

using PhotoSauce.MagicScaler.Transforms;

namespace PhotoSauce.MagicScaler;

internal sealed unsafe class AnimationEncoder : IDisposable
{
	public sealed class AnimationBufferFrame : IMetadataSource, IDisposable
	{
		public readonly FrameBufferSource Source;
		public PixelArea Area;
		public Rational Delay;
		public FrameDisposalMethod Disposal;
		public AlphaBlendMethod Blend;
		public bool HasTransparency;

		private readonly PipelineContext context;

		public AnimationBufferFrame(PipelineContext ctx)
		{
			var src = ctx.Source;

			Source = new FrameBufferSource(src.Width, src.Height, src.Format, true);
			context = ctx;
		}

		public bool TryGetMetadata<T>([NotNullWhen(true)] out T? metadata) where T : IMetadata
		{
			if (typeof(T) == typeof(AnimationFrame))
			{
				metadata = (T)(object)(new AnimationFrame(Area.X, Area.Y, Delay, Disposal, Blend, true));
				return true;
			}

			return context.Metadata.TryGetMetadata(out metadata);
		}

		public void Dispose() => Source.DisposeBuffer();
	}

	private readonly PipelineContext context;
	private readonly IAnimatedImageEncoder encoder;
	private readonly PixelSource lastSource;
	private readonly ChainedPixelSource? convertSource;
	private readonly AnimationBufferFrame[] frames = new AnimationBufferFrame[3];
	private readonly AnimationContainer anicnt;
	private readonly int lastFrame;

	private int currentFrame;

	public AnimationBufferFrame EncodeFrame { get; }
	public AnimationBufferFrame Current => frames[currentFrame % 3];
	public AnimationBufferFrame? Previous => currentFrame == 0 ? null : frames[(currentFrame - 1) % 3];
	public AnimationBufferFrame? Next => currentFrame == lastFrame ? null : frames[(currentFrame + 1) % 3];

	public AnimationEncoder(PipelineContext ctx, IAnimatedImageEncoder enc)
	{
		if (!ctx.TryGetAnimationMetadata(out anicnt, out var anifrm))
			throw new InvalidOperationException("Not an animation source.");

		context = ctx;
		encoder = enc;

		if (ctx.Source.Format != PixelFormat.Bgra32)
			ctx.Source = ctx.AddProfiler(new ConversionTransform(ctx.Source, PixelFormat.Bgra32));

		lastSource = ctx.Source;
		lastFrame = ctx.ImageContainer.FrameCount - 1;

		EncodeFrame = new AnimationBufferFrame(ctx);
		for (int i = 0; i < Math.Min(frames.Length, lastFrame + 1); i++)
			frames[i] = new AnimationBufferFrame(ctx);

		if (ctx.Settings.EncoderInfo is IImageEncoderInfo encinfo && !encinfo.SupportsPixelFormat(lastSource.Format.FormatGuid))
		{
			var fmt = encinfo.GetClosestPixelFormat(lastSource.Format);
			if (fmt == lastSource.Format && encinfo.SupportsPixelFormat(PixelFormat.Indexed8.FormatGuid))
				convertSource = ctx.AddProfiler(new IndexedColorTransform(EncodeFrame.Source));
			else
				convertSource = ctx.AddProfiler(new ConversionTransform(EncodeFrame.Source, fmt));
		}

		loadFrame(Current, anifrm);
		Current.Source.Span.CopyTo(EncodeFrame.Source.Span);

		moveToFrame(1, out anifrm);
		loadFrame(Next!, anifrm);
	}

	public void WriteGlobalMetadata() => encoder.WriteAnimationMetadata(context.Metadata);

	public void WriteFrames()
	{
		var ppt = context.AddProfiler(nameof(TemporalFilters));
		var ppq = context.AddProfiler($"{nameof(OctreeQuantizer)}: {nameof(OctreeQuantizer.CreatePalette)}");

		writeFrame(Current, ppq);

		while (moveNext())
		{
			var frame = Current;
			ppt.ResumeTiming(frame.Source.Area);
			TemporalFilters.Dedupe(this, frame.Disposal, (uint)anicnt.BackgroundColor, frame.Blend != AlphaBlendMethod.Source);
			ppt.PauseTiming();

			writeFrame(frame, ppq);
		}
	}

	public void Dispose()
	{
		lastSource.Dispose();
		EncodeFrame.Dispose();
		convertSource?.Dispose();
		for (int i = 0; i < frames.Length; i++)
			frames[i]?.Dispose();
	}

	private bool moveNext()
	{
		if (currentFrame == lastFrame)
			return false;

		var frame = Current;
		if (frame.Disposal is FrameDisposalMethod.RestoreBackground)
			frame.Source.Clear(frame.Area, (uint)anicnt.BackgroundColor);

		if (++currentFrame != lastFrame)
		{
			moveToFrame(currentFrame + 1, out var anifrm);
			loadFrame(Next!, anifrm);
		}

		return true;
	}

	private void moveToFrame(int index, out AnimationFrame anifrm)
	{
		context.ImageFrame.Dispose();
		context.ImageFrame = context.ImageContainer.GetFrame(index);

		if (context.ImageFrame is IYccImageFrame yccFrame)
			context.Source = new PlanarPixelSource(yccFrame);
		else
			context.Source = context.ImageFrame.PixelSource.AsPixelSource();

		if (context.ImageFrame is not IMetadataSource fmsrc || !fmsrc.TryGetMetadata<AnimationFrame>(out anifrm))
			anifrm = AnimationFrame.Default;

		MagicTransforms.AddAnimationTransforms(context, anicnt, anifrm);

		if ((context.Source is PlanarPixelSource plan ? plan.SourceY : context.Source) is IProfileSource prof)
			context.AddProfiler(prof);

		if (lastSource is ChainedPixelSource chain && chain.Passthrough)
		{
			chain.ReInit(context.Source);
			context.Source = chain;
		}
	}

	private void loadFrame(AnimationBufferFrame frame, in AnimationFrame anifrm)
	{
		frame.Delay = anifrm.Duration;
		frame.Disposal = anifrm.Disposal is FrameDisposalMethod.RestoreBackground || (anifrm.Disposal is FrameDisposalMethod.RestorePrevious && anicnt.BackgroundColor is 0) ? FrameDisposalMethod.RestoreBackground : FrameDisposalMethod.Preserve;
		frame.Blend = anifrm.Blend;
		frame.HasTransparency = anifrm.HasAlpha;
		frame.Area = context.Source.Area;

		var buff = frame.Source;
		fixed (byte* pbuff = buff.Span)
			context.Source.CopyPixels(frame.Area, buff.Stride, buff.Span.Length, pbuff);
	}

	private void writeFrame(AnimationBufferFrame frame, IProfiler ppq)
	{
		if (context.Settings.EncoderInfo is PlanarEncoderInfo)
			frame.Area = frame.Area.SnapTo(ChromaSubsampleMode.Subsample420.SubsampleRatioX(), ChromaSubsampleMode.Subsample420.SubsampleRatioY(), frame.Source.Width, frame.Source.Height);

		if (convertSource is IndexedColorTransform indexedSource)
		{
			var idxopt = context.Settings.EncoderOptions is IIndexedEncoderOptions iopt ? iopt : GifEncoderOptions.Default;
			if (idxopt.PredefinedPalette is not null)
			{
				indexedSource.SetPalette(MemoryMarshal.Cast<int, uint>(idxopt.PredefinedPalette.AsSpan()), idxopt.Dither is DitherMode.None);
			}
			else
			{
				using var quant = new OctreeQuantizer(ppq);
				var buffC = EncodeFrame.Source;
				var buffCSpan = buffC.Span.Slice(frame.Area.Y * buffC.Stride + frame.Area.X * buffC.Format.BytesPerPixel);

				bool isExact = quant.CreatePalette(idxopt.MaxPaletteSize, buffC.Format.AlphaRepresentation != PixelAlphaRepresentation.None, buffCSpan, frame.Area.Width, frame.Area.Height, buffC.Stride);
				indexedSource.SetPalette(quant.Palette, isExact || idxopt.Dither is DitherMode.None);
			}
		}

		convertSource?.ReInit(EncodeFrame.Source);
		context.Source = convertSource ?? (PixelSource)EncodeFrame.Source;

		encoder.WriteFrame(context.Source, frame, frame.Area);
	}
}