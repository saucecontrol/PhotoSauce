// Copyright © Clinton Ingram and Contributors.  Licensed under the MIT License.

using System;

namespace PhotoSauce.MagicScaler.Transforms
{
	internal sealed class CropTransform : ChainedPixelSource
	{
		private readonly PixelArea srcArea;

		public override int Width => srcArea.Width;
		public override int Height => srcArea.Height;

		public CropTransform(PixelSource source, in PixelArea crop) : base(source)
		{
			if (crop.X + crop.Width > PrevSource.Width || crop.Y + crop.Height > PrevSource.Height)
				throw new ArgumentOutOfRangeException(nameof(crop));

			srcArea = crop;
		}

		protected override void CopyPixelsInternal(in PixelArea prc, int cbStride, int cbBufferSize, IntPtr pbBuffer)
		{
			Profiler.PauseTiming();
			PrevSource.CopyPixels(new PixelArea(srcArea.X + prc.X, srcArea.Y + prc.Y, prc.Width, prc.Height), cbStride, cbBufferSize, pbBuffer);
			Profiler.ResumeTiming();
		}

		public override string ToString() => nameof(CropTransform);
	}
}
