// Copyright © Clinton Ingram and Contributors.  Licensed under the MIT License.

using System;
using System.Runtime.InteropServices;

using TerraFX.Interop.Windows;

using PhotoSauce.MagicScaler.Transforms;

namespace PhotoSauce.MagicScaler
{
	internal sealed class AnimationPipelineContext : IDisposable
	{
		public FrameBufferSource? FrameBufferSource;
		public FrameDisposalMethod LastDisposal = FrameDisposalMethod.RestoreBackground;

		public unsafe void UpdateFrameBuffer(IImageFrame frame, in AnimationContainer anicnt, in AnimationFrame anifrm)
		{
			var wicFrame = frame as WicGifFrame;
			var src = wicFrame is null ? frame.PixelSource : null;

			var (fwidth, fheight) = wicFrame is not null ? (wicFrame.Size.Width, wicFrame.Size.Height) : (src!.Width, src.Height);
			var fbuff = FrameBufferSource ??= new(anicnt.ScreenWidth, anicnt.ScreenHeight, PixelFormat.Bgra32, true);
			var bspan = fbuff.Span;

			fbuff.Profiler.ResumeTiming();

			// Most GIF viewers clear the background to transparent instead of the background color when the next frame has transparency
			bool fullScreen = fwidth == anicnt.ScreenWidth && fheight == anicnt.ScreenHeight;
			if (!fullScreen && LastDisposal == FrameDisposalMethod.RestoreBackground)
				MemoryMarshal.Cast<byte, uint>(bspan).Fill(anifrm.HasAlpha ? 0 : (uint)anicnt.BackgroundColor);

			// Similarly, they overwrite a background color with transparent pixels but overlay instead when the previous frame is preserved
			var fspan = bspan.Slice(anifrm.OffsetTop * fbuff.Stride + anifrm.OffsetLeft * fbuff.Format.BytesPerPixel);
			fixed (byte* buff = fspan)
			{
				if (!anifrm.HasAlpha || LastDisposal == FrameDisposalMethod.RestoreBackground)
				{
					if (wicFrame is not null)
						HRESULT.Check(wicFrame.WicSource->CopyPixels(null, (uint)fbuff.Stride, (uint)fspan.Length, buff));
					else
						src!.CopyPixels(new(0, 0, src.Width, src.Height), fbuff.Stride, fspan);
				}
				else
				{
					var area = new PixelArea(anifrm.OffsetLeft, anifrm.OffsetTop, fwidth, fheight);
					using var frameSource = wicFrame is not null ? wicFrame.Source : src!.AsPixelSource();
					using var overlay = new OverlayTransform(fbuff, frameSource, anifrm.OffsetLeft, anifrm.OffsetTop, true, true);
					overlay.CopyPixels(area, fbuff.Stride, fspan.Length, buff);
				}
			}

			fbuff.Profiler.PauseTiming();
		}

		public void Dispose()
		{
			FrameBufferSource?.DisposeBuffer();
		}
	}
}
