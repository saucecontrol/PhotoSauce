using System;

namespace PhotoSauce.MagicScaler
{
	internal class CropTransform : PixelSource
	{
		private readonly PixelArea newArea;

		public CropTransform(PixelSource source, PixelArea crop) : base(source)
		{
			if (crop.X < 0 || crop.Y < 0 || crop.X + crop.Width > (int)Source.Width || crop.Y + crop.Height > Source.Height)
				throw new ArgumentOutOfRangeException(nameof(crop));

			newArea = crop;
			Width = (uint)crop.Width;
			Height = (uint)crop.Height;
		}

		protected override void CopyPixelsInternal(in PixelArea prc, uint cbStride, uint cbBufferSize, IntPtr pbBuffer)
		{
			Timer.Stop();
			Source.CopyPixels(new PixelArea(prc.X + newArea.X, prc.Y + newArea.Y, prc.Width, prc.Height), cbStride, cbBufferSize, pbBuffer);
			Timer.Start();
		}
	}
}
