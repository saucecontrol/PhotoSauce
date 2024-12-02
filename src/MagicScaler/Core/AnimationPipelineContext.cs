// Copyright © Clinton Ingram and Contributors
// SPDX-License-Identifier: MIT

using System;

using PhotoSauce.MagicScaler.Transforms;

namespace PhotoSauce.MagicScaler;

internal sealed class AnimationPipelineContext : IDisposable
{
	private ChainedPixelSource? converter;
	private OverlayTransform? overlay;

	public FrameBufferSource? ScreenBuffer;
	public FrameDisposalMethod LastDisposal = FrameDisposalMethod.RestoreBackground;
	public PixelArea LastArea;

	public unsafe void UpdateFrameBuffer(IPixelSource src, in AnimationContainer anicnt, in AnimationFrame anifrm)
	{
		if (ScreenBuffer is null)
		{
			ScreenBuffer = new(anicnt.ScreenWidth, anicnt.ScreenHeight, PixelFormat.Bgra32, true);
			ScreenBuffer.Clear(ScreenBuffer.Area, (uint)anicnt.BackgroundColor);
		}

		if (src.Format != PixelFormat.Bgra32.FormatGuid)
		{
			var psrc = src.AsPixelSource();
			if (converter?.IsCompatible(psrc) ?? false)
			{
				converter.ReInit(psrc);
				src = converter;
			}
			else
			{
				converter?.Dispose();
				src = converter = psrc.Format == PixelFormat.Indexed8 ? new PaletteTransform(psrc, PixelFormat.Bgra32) : new ConversionTransform(psrc, PixelFormat.Bgra32);
			}
		}

		var fbuff = ScreenBuffer;
		fbuff.Profiler.ResumeTiming();

		var farea = new PixelArea(anifrm.OffsetLeft, anifrm.OffsetTop, src.Width, src.Height).Intersect(fbuff.Area);
		var fspan = fbuff.Span.Slice(anifrm.OffsetTop * fbuff.Stride + anifrm.OffsetLeft * fbuff.Format.BytesPerPixel);
		fixed (byte* buff = fspan)
		{
			if (anifrm.Blend == AlphaBlendMethod.Source)
			{
				src.CopyPixels(PixelArea.FromSize(farea.Width, farea.Height), fbuff.Stride, fspan);
			}
			else
			{
				if (overlay is null)
					overlay = new OverlayTransform(fbuff, src.AsPixelSource(), anifrm.OffsetLeft, anifrm.OffsetTop, anifrm.HasAlpha, anifrm.Blend);
				else
					overlay.SetOver(src.AsPixelSource(), anifrm.OffsetLeft, anifrm.OffsetTop, anifrm.HasAlpha, anifrm.Blend);

				overlay.CopyPixels(farea, fbuff.Stride, fspan.Length, buff);
			}
		}

		fbuff.Profiler.PauseTiming();
	}

	public void ClearFrameBuffer(uint color)
	{
		var fbuff = ScreenBuffer!;
		fbuff.Profiler.ResumeTiming();
		fbuff.Clear(LastArea, color);
		fbuff.Profiler.PauseTiming();
	}

	public void Dispose()
	{
		converter?.Dispose();
		overlay?.Dispose();
		ScreenBuffer?.DisposeBuffer();
	}
}
