// Copyright © Clinton Ingram and Contributors.  Licensed under the MIT License.

using System;
using System.Runtime.InteropServices;

using PhotoSauce.MagicScaler.Transforms;

namespace PhotoSauce.MagicScaler;

internal sealed class AnimationPipelineContext : IDisposable
{
	public FrameBufferSource? ScreenBuffer;
	public FrameDisposalMethod LastDisposal = FrameDisposalMethod.RestoreBackground;

	public unsafe void UpdateFrameBuffer(IImageFrame frame, in AnimationContainer anicnt, in AnimationFrame anifrm)
	{
		var src = frame.PixelSource;
		int width = Math.Min(src.Width, anicnt.ScreenWidth - anifrm.OffsetLeft);
		int height = Math.Min(src.Height, anicnt.ScreenHeight - anifrm.OffsetTop);

		var fbuff = ScreenBuffer ??= new(anicnt.ScreenWidth, anicnt.ScreenHeight, PixelFormat.Bgra32, true);
		var bspan = fbuff.Span;

		fbuff.Profiler.ResumeTiming();

		// Most GIF viewers clear the background to transparent instead of the background color when the next frame has transparency
		bool fullScreen = width == anicnt.ScreenWidth && height == anicnt.ScreenHeight;
		if (!fullScreen && LastDisposal == FrameDisposalMethod.RestoreBackground)
			MemoryMarshal.Cast<byte, uint>(bspan).Fill(anifrm.HasAlpha ? 0 : (uint)anicnt.BackgroundColor);

		// Similarly, they overwrite a background color with transparent pixels but overlay instead when the previous frame is preserved
		var fspan = bspan.Slice(anifrm.OffsetTop * fbuff.Stride + anifrm.OffsetLeft * fbuff.Format.BytesPerPixel);
		fixed (byte* buff = fspan)
		{
			if (!anifrm.HasAlpha || LastDisposal == FrameDisposalMethod.RestoreBackground)
			{
				src.CopyPixels(new(0, 0, width, height), fbuff.Stride, fspan);
			}
			else
			{
				var area = new PixelArea(anifrm.OffsetLeft, anifrm.OffsetTop, width, height);
				using var overlay = new OverlayTransform(fbuff, src.AsPixelSource(), anifrm.OffsetLeft, anifrm.OffsetTop, true, true);
				overlay.CopyPixels(area, fbuff.Stride, fspan.Length, buff);
			}
		}

		fbuff.Profiler.PauseTiming();
	}

	public void Dispose() => ScreenBuffer?.DisposeBuffer();
}
