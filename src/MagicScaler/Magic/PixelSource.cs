using System;
using System.Diagnostics;

using PhotoSauce.MagicScaler.Interop;

namespace PhotoSauce.MagicScaler
{
	internal abstract class PixelSource
	{
		private readonly Lazy<PixelSourceStats> stats;

		public PixelFormat Format { get; protected set; }
		public uint Width { get; protected set; }
		public uint Height { get; protected set; }
		public IWICBitmapSource WicSource { get; protected set; }

		protected Stopwatch Timer { get; } = new Stopwatch();
		protected PixelSource Source { get; set; }
		protected uint BufferStride { get; set; }

		public PixelSourceStats Stats => stats.Value;

		protected PixelSource()
		{
			stats = new Lazy<PixelSourceStats>(() => new PixelSourceStats { SourceName = ToString().Replace($"{nameof(PhotoSauce)}.{nameof(MagicScaler)}.", string.Empty) });
		}

		protected PixelSource(PixelSource source) : this()
		{
			Source = source;
			WicSource = new PixelSourceAsIWICBitmapSource(this);
			Format = Source.Format;
			Width = Source.Width;
			Height = Source.Height;
			BufferStride = (Width * (uint)Format.BitsPerPixel + 7u & ~7u) / 8u + ((uint)IntPtr.Size - 1u) & ~((uint)IntPtr.Size - 1u);
		}

		protected abstract void CopyPixelsInternal(WICRect prc, uint cbStride, uint cbBufferSize, IntPtr pbBuffer);

		public void CopyPixels(WICRect prc, uint cbStride, uint cbBufferSize, IntPtr pbBuffer)
		{
			int cbLine = (prc.Width * Format.BitsPerPixel + 7 & ~7) / 8;

			if (prc.X < 0 || prc.Y < 0 || prc.Width < 0 || prc.Height < 0 || prc.X + prc.Width > (int)Width || prc.Y + prc.Height > (int)Height)
				throw new ArgumentOutOfRangeException(nameof(prc), "Requested area does not fall within the image bounds");

			if (cbLine > (int)cbStride)
				throw new ArgumentOutOfRangeException(nameof(cbStride), "Stride is too small for the requested area");

			if ((prc.Height - 1) * (int)cbStride + cbLine > (int)cbBufferSize)
				throw new ArgumentOutOfRangeException(nameof(cbBufferSize), "Buffer is to small for the requested area");

			if (pbBuffer == IntPtr.Zero)
				throw new ArgumentOutOfRangeException(nameof(pbBuffer), "Buffer pointer is invalid");

			Timer.Restart();
			CopyPixelsInternal(prc, cbStride, cbBufferSize, pbBuffer);
			Timer.Stop();

			var stats = Stats;
			stats.CallCount++;
			stats.PixelCount += prc.Width * prc.Height;
			stats.ProcessingTime += (double)Timer.ElapsedTicks / Stopwatch.Frequency * 1000;
		}
	}

	internal class WicBitmapSourceWrapper : PixelSource
	{
		private IWICBitmapSource realSource;
		private string sourceName;

		public WicBitmapSourceWrapper(IWICBitmapSource source, string name, bool profile = true) : base()
		{
			realSource = source;
			sourceName = profile ? name : $"{name} (nonprofiling)";
			WicSource = profile ? new PixelSourceAsIWICBitmapSource(this) : source;
			Format = PixelFormat.Cache[source.GetPixelFormat()];
			source.GetSize(out uint width, out uint height);
			Width = width;
			Height = height;
		}

		protected override void CopyPixelsInternal(WICRect prc, uint cbStride, uint cbBufferSize, IntPtr pbBuffer) => realSource.CopyPixels(prc, cbStride, cbBufferSize, pbBuffer);

		public override string ToString() => sourceName;
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
