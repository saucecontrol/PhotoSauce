using System;

using PhotoSauce.MagicScaler.Interop;

namespace PhotoSauce.MagicScaler
{
	internal abstract class WicBitmapSourceBase : WicBase, IWICBitmapSource
	{
		protected IWICBitmapSource Source;
		protected Guid Format;
		protected uint Width;
		protected uint Height;
		protected uint Channels;
		protected uint Bpp;
		protected uint Stride;

		protected WicBitmapSourceBase() { }

		protected WicBitmapSourceBase(IWICBitmapSource source)
		{
			Source = source;

			Source.GetSize(out Width, out Height);
			Format = Source.GetPixelFormat();

			var pfi = AddRef(Wic.CreateComponentInfo(Format)) as IWICPixelFormatInfo;
			Channels = pfi.GetChannelCount();
			Bpp = pfi.GetBitsPerPixel() / 8u;
			Release(pfi);

			Stride = Width * Bpp + 3u & ~3u;
		}

		public abstract void CopyPixels(WICRect prc, uint cbStride, uint cbBufferSize, IntPtr pbBuffer);

		public virtual Guid GetPixelFormat() => Format;

		public virtual void GetResolution(out double pDpiX, out double pDpiY) => Source.GetResolution(out pDpiX, out pDpiY);

		public virtual void CopyPalette(IWICPalette pIPalette) => Source.CopyPalette(pIPalette);

		public virtual void GetSize(out uint puiWidth, out uint puiHeight)
		{
			puiWidth = Width;
			puiHeight = Height;
		}
	}
}
