using System;
using System.Drawing;
using System.Diagnostics;
using System.Runtime.InteropServices;

using PhotoSauce.MagicScaler.Interop;

namespace PhotoSauce.MagicScaler
{
	public interface IPixelSource
	{
		Guid Format { get; }
		int Width { get; }
		int Height { get; }

		void CopyPixels(Rectangle sourceArea, int cbStride, Span<byte> buffer);
	}

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
			WicSource = this.AsIWICBitmapSource();
			Format = Source.Format;
			Width = Source.Width;
			Height = Source.Height;
			BufferStride = (Width * (uint)Format.BitsPerPixel + 7u) / 8u + ((uint)IntPtr.Size - 1u) & ~((uint)IntPtr.Size - 1u);
		}

		protected abstract void CopyPixelsInternal(WICRect prc, uint cbStride, uint cbBufferSize, IntPtr pbBuffer);

		public void CopyPixels(WICRect prc, uint cbStride, uint cbBufferSize, IntPtr pbBuffer)
		{
			uint cbLine = (uint)(prc.Width * Format.BitsPerPixel + 7) / 8u;

			if (prc.X < 0 || prc.Y < 0 || prc.Width < 0 || prc.Height < 0 || prc.X + prc.Width > (int)Width || prc.Y + prc.Height > (int)Height)
				throw new ArgumentOutOfRangeException(nameof(prc), "Requested area does not fall within the image bounds");

			if (cbLine > cbStride)
				throw new ArgumentOutOfRangeException(nameof(cbStride), "Stride is too small for the requested area");

			if (((uint)prc.Height - 1u) * cbStride + cbLine > cbBufferSize)
				throw new ArgumentOutOfRangeException(nameof(cbBufferSize), "Buffer is too small for the requested area");

			if (pbBuffer == IntPtr.Zero)
				throw new ArgumentOutOfRangeException(nameof(pbBuffer), "Buffer pointer is invalid");

			Timer.Restart();
			CopyPixelsInternal(prc, cbStride, cbBufferSize, pbBuffer);
			Timer.Stop();

			var stats = Stats;
			stats.CallCount++;
			stats.PixelCount += prc.Width * prc.Height;
			stats.ProcessingTime += Timer.ElapsedMilliseconds();
		}
	}

	internal static class PixelSourceExtensions
	{
		private class PixelSourceFromIWICBitmapSource : PixelSource
		{
			private readonly IWICBitmapSource realSource;
			private readonly string sourceName;

			public PixelSourceFromIWICBitmapSource(IWICBitmapSource source, string name, bool profile = true) : base()
			{
				realSource = source;
				sourceName = profile ? name : $"{name} (nonprofiling)";
				WicSource = profile ? this.AsIWICBitmapSource() : source;
				Format = PixelFormat.Cache[source.GetPixelFormat()];
				source.GetSize(out uint width, out uint height);
				Width = width;
				Height = height;
			}

			protected override void CopyPixelsInternal(WICRect prc, uint cbStride, uint cbBufferSize, IntPtr pbBuffer) => realSource.CopyPixels(prc, cbStride, cbBufferSize, pbBuffer);

			public override string ToString() => sourceName;
		}

		private class PixelSourceAsIWICBitmapSource : IWICBitmapSource
		{
			private readonly PixelSource source;

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

		private class PixelSourceFromIPixelSource : PixelSource
		{
			private readonly IPixelSource realSource;

			public PixelSourceFromIPixelSource(IPixelSource source) : base()
			{
				realSource = source;
				WicSource = this.AsIWICBitmapSource();
				Format = PixelFormat.Cache[source.Format];
				Width = (uint)source.Width;
				Height = (uint)source.Height;
			}

			unsafe protected override void CopyPixelsInternal(WICRect prc, uint cbStride, uint cbBufferSize, IntPtr pbBuffer) => realSource.CopyPixels(prc.ToGdiRect(), (int)cbStride, new Span<byte>(pbBuffer.ToPointer(), (int)cbBufferSize));

			public override string ToString() => realSource.ToString();
		}

		private class PixelSourceAsIPixelSource : IPixelSource
		{
			private readonly PixelSource source;

			public PixelSourceAsIPixelSource(PixelSource src) => source = src;

			public Guid Format => source.Format.FormatGuid;
			public int Width => (int)source.Width;
			public int Height => (int)source.Height;

			unsafe public void CopyPixels(Rectangle prc, int cbStride, Span<byte> buffer)
			{
				fixed (byte* pbBuffer = &MemoryMarshal.GetReference(buffer))
					source.CopyPixels(prc.ToWicRect(), (uint)cbStride, (uint)buffer.Length, (IntPtr)pbBuffer);
			}
		}

		public static PixelSource AsPixelSource(this IWICBitmapSource source, string name, bool profile = true) => new PixelSourceFromIWICBitmapSource(source, name, profile);
		public static IWICBitmapSource AsIWICBitmapSource(this PixelSource source) => new PixelSourceAsIWICBitmapSource(source);

		public static PixelSource AsPixelSource(this IPixelSource source) => new PixelSourceFromIPixelSource(source);
		public static IPixelSource AsIPixelSource(this PixelSource source) => new PixelSourceAsIPixelSource(source);
	}
}
