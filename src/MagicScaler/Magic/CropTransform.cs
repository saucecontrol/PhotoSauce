using System;

namespace PhotoSauce.MagicScaler
{
	internal class CropTransform : PixelSource
	{
		private readonly PixelArea srcArea;

		public CropTransform(PixelSource source, in PixelArea crop) : base(source)
		{
			if ((uint)(crop.X + crop.Width) > Source.Width || (uint)(crop.Y + crop.Height) > Source.Height)
				throw new ArgumentOutOfRangeException(nameof(crop));

			srcArea = crop;

			Width = srcArea.Width;
			Height = srcArea.Height;
		}

		protected override void CopyPixelsInternal(in PixelArea prc, int cbStride, int cbBufferSize, IntPtr pbBuffer)
		{
			Timer.Stop();
			Source.CopyPixels(new PixelArea(srcArea.X + prc.X, srcArea.Y + prc.Y, prc.Width, prc.Height), cbStride, cbBufferSize, pbBuffer);
			Timer.Start();
		}
	}
}
