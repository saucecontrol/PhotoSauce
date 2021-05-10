// Copyright © Clinton Ingram and Contributors.  Licensed under the MIT License.

using System;
using System.Drawing;
using System.Runtime.InteropServices;

using TerraFX.Interop;

using PhotoSauce.MagicScaler.Transforms;

namespace PhotoSauce.MagicScaler
{
	internal sealed class AnimationPipelineContext : IDisposable
	{
		public FrameBufferSource? FrameBufferSource;
		public FrameDisposalMethod LastDisposal = FrameDisposalMethod.RestoreBackground;
		public int LastFrame = -1;

		public unsafe void UpdateFrameBuffer(IAnimationContainer cnt, IAnimationFrame frame)
		{
			const int bytesPerPixel = 4;

			var fbuff = FrameBufferSource ??= new FrameBufferSource(cnt.ScreenWidth, cnt.ScreenHeight, PixelFormat.Bgra32, true);
			var bspan = fbuff.Span;
			var area = frame.GetArea();

			fbuff.ResumeTiming();

			// Most GIF viewers clear the background to transparent instead of the background color when the next frame has transparency
			bool fullScreen = area.Width == cnt.ScreenWidth && area.Height == cnt.ScreenHeight;
			if (!fullScreen && LastDisposal == FrameDisposalMethod.RestoreBackground)
				MemoryMarshal.Cast<byte, uint>(bspan).Fill(frame.HasAlpha ? 0 : (uint)cnt.BackgroundColor.ToArgb());

			// Similarly, they overwrite a background color with transparent pixels but overlay instead when the previous frame is preserved
			var fspan = bspan.Slice(frame.Origin.Y * fbuff.Stride + frame.Origin.X * bytesPerPixel);
			fixed (byte* buff = fspan)
			{
				if (!frame.HasAlpha || LastDisposal == FrameDisposalMethod.RestoreBackground)
				{
					if (frame is WicGifFrame wicFrame)
					{
						var rect = new WICRect { Width = frame.Size.Width, Height = frame.Size.Height };
						HRESULT.Check(wicFrame.WicSource->CopyPixels(&rect, (uint)fbuff.Stride, (uint)fspan.Length, buff));
					}
					else
					{
						var rect = new Rectangle(0, 0, frame.Size.Width, frame.Size.Height);
						((IImageFrame)frame).PixelSource.CopyPixels(rect, fbuff.Stride, fspan);
					}
				}
				else
				{
					using var frameSource = frame is WicGifFrame wicFrame ? wicFrame.Source : ((IImageFrame)frame).PixelSource.AsPixelSource();
					using var overlay = new OverlayTransform(fbuff, frameSource, frame.Origin.X, frame.Origin.Y, true, true);
					overlay.CopyPixels(area, fbuff.Stride, fspan.Length, (IntPtr)buff);
				}
			}

			fbuff.PauseTiming();
		}

		public void Dispose()
		{
			FrameBufferSource?.DisposeBuffer();
		}
	}
}
