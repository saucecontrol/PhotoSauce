using System;

using PhotoSauce.MagicScaler.Interop;

namespace PhotoSauce.MagicScaler
{
	internal abstract class PixelSource
	{
		public PixelFormat Format { get; protected set; }
		public uint Width { get; protected set; }
		public uint Height { get; protected set; }
		public IWICBitmapSource WicSource { get; protected set; }

		protected PixelSource Source { get; set; }
		protected uint BufferStride { get; set; }

		protected PixelSource() { }

		protected PixelSource(PixelSource source) : this()
		{
			Source = source;
			WicSource = new PixelSourceAsIWICBitmapSource(this);
			Format = Source.Format;
			Width = Source.Width;
			Height = Source.Height;
			BufferStride = (Width * (uint)Format.BitsPerPixel + 7u & ~7u) / 8u + ((uint)IntPtr.Size - 1u) & ~((uint)IntPtr.Size - 1u);
		}

		public abstract void CopyPixels(WICRect prc, uint cbStride, uint cbBufferSize, IntPtr pbBuffer);
	}

	internal class WicBitmapSourceWrapper : PixelSource
	{
		private IWICBitmapSource realSource;

		public WicBitmapSourceWrapper(IWICBitmapSource source) : base()
		{
			realSource = source;
			WicSource = source;
			Format = PixelFormat.Cache[source.GetPixelFormat()];
			source.GetSize(out uint width, out uint height);
			Width = width;
			Height = height;
		}

		public override void CopyPixels(WICRect prc, uint cbStride, uint cbBufferSize, IntPtr pbBuffer) => realSource.CopyPixels(prc, cbStride, cbBufferSize, pbBuffer);
	}

	internal class PixelSourceAsIWICBitmapSource : IWICBitmapSource
	{
		private PixelSource source;

		public PixelSourceAsIWICBitmapSource(PixelSource src) => source = src;

		public void GetSize(out uint puiWidth, out uint puiHeight)
		{
			puiWidth = source.Width;
			puiHeight = source.Height;
		}

		public Guid GetPixelFormat() => source.Format.FormatGuid;

		public void GetResolution(out double pDpiX, out double pDpiY) => pDpiX = pDpiY = 96d;

		public void CopyPalette(IWICPalette pIPalette) => throw new NotImplementedException();

		public void CopyPixels(WICRect prc, uint cbStride, uint cbBufferSize, IntPtr pbBuffer) => source.CopyPixels(prc, cbStride, cbBufferSize, pbBuffer);
	}
}
