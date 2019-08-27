using System;
using System.Drawing;
using System.Diagnostics;

using PhotoSauce.MagicScaler.Interop;

namespace PhotoSauce.MagicScaler
{
	/// <summary>Provides a mechanism for accessing raw pixel data from an image.</summary>
	public interface IPixelSource
	{
		/// <summary>The binary representation of the pixel data.  Must be one of the values from <see cref="PixelFormats" />.</summary>
		Guid Format { get; }
		/// <summary>The width of the image in pixels</summary>
		int Width { get; }
		/// <summary>The height of the image in pixels</summary>
		int Height { get; }

		/// <summary>Copies the image pixels bounded by <paramref name="sourceArea" /> to the provided <paramref name="buffer" />.</summary>
		/// <param name="sourceArea">A <see cref="Rectangle" /> that bounds the area of interest.</param>
		/// <param name="cbStride">The number of bytes between pixels in the same image column within the buffer.</param>
		/// <param name="buffer">A target memory buffer that will receive the pixel data.</param>
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

		public PixelArea Area => new PixelArea(0, 0, (int)Width, (int)Height);

		public PixelSourceStats Stats => stats.Value;

		protected PixelSource() =>
			stats = new Lazy<PixelSourceStats>(() => new PixelSourceStats { SourceName = ToString().Replace(nameof(PhotoSauce) + "." + nameof(MagicScaler) + ".", null) });

		protected PixelSource(PixelSource source) : this()
		{
			Source = source;
			WicSource = this.AsIWICBitmapSource();
			Format = Source.Format;
			Width = Source.Width;
			Height = Source.Height;
			BufferStride = (uint)MathUtil.PowerOfTwoCeiling(MathUtil.DivCeiling((int)Width * Format.BitsPerPixel, 8), IntPtr.Size);
		}

		protected abstract void CopyPixelsInternal(in PixelArea prc, uint cbStride, uint cbBufferSize, IntPtr pbBuffer);

		public void CopyPixels(in PixelArea prc, uint cbStride, uint cbBufferSize, IntPtr pbBuffer)
		{
			uint cbLine = (uint)MathUtil.DivCeiling(prc.Width * Format.BitsPerPixel, 8);

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
			stats.TimerTicks += Timer.ElapsedTicks;
		}
	}

	internal static class PixelSourceExtensions
	{
		private class PixelSourceFromIWICBitmapSource : PixelSource
		{
			private readonly IWICBitmapSource upstreamSource;
			private readonly WICRect sourceRect;
			private readonly string sourceName;

			public PixelSourceFromIWICBitmapSource(IWICBitmapSource source, string name, bool profile = true) : base()
			{
				upstreamSource = source;
				sourceRect = new WICRect();
				sourceName = profile ? name : $"{name} (nonprofiling)";
				WicSource = profile ? this.AsIWICBitmapSource() : source;
				Format = PixelFormat.FromGuid(source.GetPixelFormat());
				source.GetSize(out uint width, out uint height);
				Width = width;
				Height = height;
			}

			protected override void CopyPixelsInternal(in PixelArea prc, uint cbStride, uint cbBufferSize, IntPtr pbBuffer) =>
				upstreamSource.CopyPixels(prc.CopyToWicRect(sourceRect), cbStride, cbBufferSize, pbBuffer);

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

			public void CopyPixels(WICRect prc, uint cbStride, uint cbBufferSize, IntPtr pbBuffer) =>
				source.CopyPixels(PixelArea.FromWicRect(prc), cbStride, cbBufferSize, pbBuffer);
		}

		private class PixelSourceFromIPixelSource : PixelSource
		{
			private readonly IPixelSource upstreamSource;

			public PixelSourceFromIPixelSource(IPixelSource source) : base()
			{
				upstreamSource = source;
				WicSource = this.AsIWICBitmapSource();
				Format = PixelFormat.FromGuid(source.Format);
				Width = (uint)source.Width;
				Height = (uint)source.Height;
			}

			unsafe protected override void CopyPixelsInternal(in PixelArea prc, uint cbStride, uint cbBufferSize, IntPtr pbBuffer) =>
				upstreamSource.CopyPixels(prc.ToGdiRect(), (int)cbStride, new Span<byte>(pbBuffer.ToPointer(), (int)cbBufferSize));

			public override string ToString() => upstreamSource.ToString();
		}

		private class PixelSourceAsIPixelSource : IPixelSource
		{
			private readonly PixelSource source;

			public PixelSourceAsIPixelSource(PixelSource src) => source = src;

			public Guid Format => source.Format.FormatGuid;
			public int Width => (int)source.Width;
			public int Height => (int)source.Height;

			unsafe public void CopyPixels(Rectangle sourceArea, int cbStride, Span<byte> buffer)
			{
				fixed (byte* pbBuffer = buffer)
					source.CopyPixels(PixelArea.FromGdiRect(sourceArea), (uint)cbStride, (uint)buffer.Length, (IntPtr)pbBuffer);
			}
		}

		public static PixelSource AsPixelSource(this IWICBitmapSource source, string name, bool profile = true) => new PixelSourceFromIWICBitmapSource(source, name, profile);
		public static IWICBitmapSource AsIWICBitmapSource(this PixelSource source) => new PixelSourceAsIWICBitmapSource(source);

		public static PixelSource AsPixelSource(this IPixelSource source) => new PixelSourceFromIPixelSource(source);
		public static IPixelSource AsIPixelSource(this PixelSource source) => new PixelSourceAsIPixelSource(source);
	}
}
