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
	private readonly int lastFrame;

	private int currentFrame;

	public AnimationBufferFrame EncodeFrame { get; }
	public AnimationBufferFrame Current => frames[currentFrame % 3];
	public AnimationBufferFrame? Previous => currentFrame == 0 ? null : frames[(currentFrame - 1) % 3];
	public AnimationBufferFrame? Next => currentFrame == lastFrame ? null : frames[(currentFrame + 1) % 3];

	public AnimationEncoder(PipelineContext ctx, IAnimatedImageEncoder enc)
	{
		context = ctx;
		encoder = enc;

		if (ctx.Source.Format != PixelFormat.Bgra32)
			ctx.Source = ctx.AddProfiler(new ConversionTransform(ctx.Source, PixelFormat.Bgra32));

		lastSource = ctx.Source;
		lastFrame = ctx.ImageContainer.FrameCount - 1;

		EncodeFrame = new AnimationBufferFrame(context);
		for (int i = 0; i < Math.Min(frames.Length, lastFrame + 1); i++)
			frames[i] = new AnimationBufferFrame(context);

		if (ctx.Settings.EncoderInfo is IImageEncoderInfo encinfo && !encinfo.SupportsPixelFormat(lastSource.Format.FormatGuid))
		{
			var fmt = encinfo.GetClosestPixelFormat(lastSource.Format);
			if (fmt == lastSource.Format && encinfo.SupportsPixelFormat(PixelFormat.Indexed8.FormatGuid))
				convertSource = ctx.AddProfiler(new IndexedColorTransform(EncodeFrame.Source));
			else
				convertSource = ctx.AddProfiler(new ConversionTransform(EncodeFrame.Source, fmt));
		}

		loadFrame(Current);
		Current.Source.Span.CopyTo(EncodeFrame.Source.Span);

		moveToFrame(1);
		loadFrame(Next!);
	}

	public void WriteGlobalMetadata() => encoder.WriteAnimationMetadata(context.Metadata);

	public void WriteFrames()
	{
		uint bgColor = context.Metadata.TryGetMetadata<AnimationContainer>(out var anicnt) ? (uint)anicnt.BackgroundColor : default;

		var ppt = context.AddProfiler(nameof(TemporalFilters));
		var ppq = context.AddProfiler($"{nameof(OctreeQuantizer)}: {nameof(OctreeQuantizer.CreatePalette)}");

		var encopt = context.Settings.EncoderOptions is GifEncoderOptions gifopt ? gifopt : GifEncoderOptions.Default;
		writeFrame(Current, encopt, ppq);

		while (moveNext())
		{
			ppt.ResumeTiming(Current.Source.Area);
			TemporalFilters.Dedupe(this, bgColor, Current.Blend != AlphaBlendMethod.Source);
			ppt.PauseTiming();

			writeFrame(Current, encopt, ppq);
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

		if (++currentFrame != lastFrame)
		{
			moveToFrame(currentFrame + 1);
			loadFrame(Next!);
		}

		return true;
	}

	private void moveToFrame(int index)
	{
		context.ImageFrame.Dispose();
		context.ImageFrame = context.ImageContainer.GetFrame(index);

		if (context.ImageFrame is IYccImageFrame yccFrame)
			context.Source = new PlanarPixelSource(yccFrame);
		else
			context.Source = context.ImageFrame.PixelSource.AsPixelSource();

		MagicTransforms.AddAnimationFrameBuffer(context, false);

		if ((context.Source is PlanarPixelSource plan ? plan.SourceY : context.Source) is IProfileSource prof)
			context.AddProfiler(prof);

		if (lastSource is ChainedPixelSource chain && chain.Passthrough)
		{
			chain.ReInit(context.Source);
			context.Source = chain;
		}
	}

	private void loadFrame(AnimationBufferFrame frame)
	{
		if (context.ImageFrame is not IMetadataSource fmsrc || !fmsrc.TryGetMetadata<AnimationFrame>(out var anifrm))
			anifrm = AnimationFrame.Default;

		frame.Delay = anifrm.Duration;
		frame.Disposal = anifrm.Disposal == FrameDisposalMethod.RestoreBackground ? FrameDisposalMethod.RestoreBackground : FrameDisposalMethod.Preserve;
		frame.Blend = anifrm.Blend;
		frame.HasTransparency = anifrm.HasAlpha;
		frame.Area = context.Source.Area;

		var buff = frame.Source;
		fixed (byte* pbuff = buff.Span)
			context.Source.CopyPixels(frame.Area, buff.Stride, buff.Span.Length, pbuff);
	}

	private void writeFrame(AnimationBufferFrame src, in GifEncoderOptions gifopt, IProfiler ppq)
	{
		if (convertSource is IndexedColorTransform indexedSource)
		{
			if (gifopt.PredefinedPalette is not null)
			{
				indexedSource.SetPalette(MemoryMarshal.Cast<int, uint>(gifopt.PredefinedPalette.AsSpan()), gifopt.Dither == DitherMode.None);
			}
			else
			{
				using var quant = new OctreeQuantizer(ppq);
				var buffC = EncodeFrame.Source;
				var buffCSpan = buffC.Span.Slice(src.Area.Y * buffC.Stride + src.Area.X * buffC.Format.BytesPerPixel);

				bool isExact = quant.CreatePalette(gifopt.MaxPaletteSize, buffC.Format.AlphaRepresentation != PixelAlphaRepresentation.None, buffCSpan, src.Area.Width, src.Area.Height, buffC.Stride);
				indexedSource.SetPalette(quant.Palette, isExact || gifopt.Dither == DitherMode.None);
			}
		}

		convertSource?.ReInit(EncodeFrame.Source);
		context.Source = convertSource ?? (PixelSource)EncodeFrame.Source;

		encoder.WriteFrame(context.Source, src, src.Area);
	}
}