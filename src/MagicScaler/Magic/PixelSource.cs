using System;

using PhotoSauce.MagicScaler.Interop;

namespace PhotoSauce.MagicScaler
{
	internal abstract class WicBitmapSourceBase : WicBase, IWICBitmapSource
	{
		protected IWICBitmapSource Source;
		protected PixelFormat Format;
		protected uint Width;
		protected uint Height;
		protected uint Stride;
		protected int Bpp;

		protected WicBitmapSourceBase() { }

		protected WicBitmapSourceBase(IWICBitmapSource source)
		{
			Source = source;

			Source.GetSize(out Width, out Height);
			Format = PixelFormat.Cache[Source.GetPixelFormat()];
			Bpp = Format.BitsPerPixel / 8;
			Stride = Width * (uint)Bpp + 3u & ~3u;
		}

		public abstract void CopyPixels(WICRect prc, uint cbStride, uint cbBufferSize, IntPtr pbBuffer);

		public virtual Guid GetPixelFormat() => Format.FormatGuid;

		public virtual void GetResolution(out double pDpiX, out double pDpiY) => Source.GetResolution(out pDpiX, out pDpiY);

		public virtual void CopyPalette(IWICPalette pIPalette) => Source.CopyPalette(pIPalette);

		public virtual void GetSize(out uint puiWidth, out uint puiHeight)
		{
			puiWidth = Width;
			puiHeight = Height;
		}
	}
}
