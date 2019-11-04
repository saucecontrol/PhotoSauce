using System;
using System.Drawing;

using PhotoSauce.Interop.Wic;

namespace PhotoSauce.MagicScaler
{
	internal abstract class PixelSource
	{
		public PixelFormat Format { get; protected set; }
		public int Width { get; protected set; }
		public int Height { get; protected set; }
		public IWICBitmapSource WicSource { get; protected set; }

		protected IPixelSourceProfiler Profiler { get; }
		protected PixelSource Source { get; set; }
		protected int BufferStride { get; set; }

		public PixelArea Area => new PixelArea(0, 0, Width, Height);

		public PixelSourceStats? Stats => Profiler is SourceStatsProfiler ps ? ps.Stats : null;

		protected PixelSource() : this(default(IWICBitmapSource)) { }

		protected PixelSource(IWICBitmapSource? wicSource)
		{
			Profiler = MagicImageProcessor.EnablePixelSourceStats ? new SourceStatsProfiler(this) : NoopProfiler.Instance;
			WicSource = wicSource ?? this.AsIWICBitmapSource();
			Source = this;
		}

		protected PixelSource(PixelSource source) : this(default(IWICBitmapSource))
		{
			Source = source;
			Format = source.Format;
			Width = source.Width;
			Height = source.Height;
			BufferStride = MathUtil.PowerOfTwoCeiling(MathUtil.DivCeiling(Width * Format.BitsPerPixel, 8), IntPtr.Size);
		}

		protected abstract void CopyPixelsInternal(in PixelArea prc, int cbStride, int cbBufferSize, IntPtr pbBuffer);

		public void CopyPixels(in PixelArea prc, int cbStride, int cbBufferSize, IntPtr pbBuffer)
		{
			int cbLine = MathUtil.DivCeiling(prc.Width * Format.BitsPerPixel, 8);

			if (prc.X + prc.Width > Width || prc.Y + prc.Height > Height)
				throw new ArgumentOutOfRangeException(nameof(prc), "Requested area does not fall within the image bounds");

			if (cbLine > cbStride)
				throw new ArgumentOutOfRangeException(nameof(cbStride), "Stride is too small for the requested area");

			if ((prc.Height - 1) * cbStride + cbLine > cbBufferSize)
				throw new ArgumentOutOfRangeException(nameof(cbBufferSize), "Buffer is too small for the requested area");

			if (pbBuffer == IntPtr.Zero)
				throw new ArgumentOutOfRangeException(nameof(pbBuffer), "Buffer pointer is invalid");

			Profiler.ResumeTiming();
			CopyPixelsInternal(prc, cbStride, cbBufferSize, pbBuffer);
			Profiler.PauseTiming();

			Profiler.LogCopyPixels(prc);
		}
	}

	internal class PixelSourceFrame : IImageFrame
	{
		public IPixelSource PixelSource { get; }

		public PixelSourceFrame(IPixelSource source) => PixelSource = source;
	}

	internal class PixelSourceContainer : IImageContainer
	{
		private readonly PixelSourceFrame frame;

		public FileFormat ContainerFormat => FileFormat.Unknown;

		public int FrameCount => 1;

		public PixelSourceContainer(IPixelSource source) => frame = new PixelSourceFrame(source);

		public IImageFrame GetFrame(int index) => index == 0 ? frame : throw new IndexOutOfRangeException();
	}

	internal class NoopPixelSource : PixelSource
	{
		public static readonly PixelSource Instance = new NoopPixelSource();

		protected override void CopyPixelsInternal(in PixelArea prc, int cbStride, int cbBufferSize, IntPtr pbBuffer) { }
	}

	internal static class PixelSourceExtensions
	{
		private class PixelSourceFromIWICBitmapSource : PixelSource
		{
			private readonly IWICBitmapSource upstreamSource;
			private readonly string sourceName;

			public PixelSourceFromIWICBitmapSource(IWICBitmapSource source, string name, bool profile = true) : base(profile ? null : source)
			{
				upstreamSource = source;
				sourceName = profile ? name : $"{name} (nonprofiling)";
				source.GetSize(out uint width, out uint height);
				Format = PixelFormat.FromGuid(source.GetPixelFormat());
				Width = (int)width;
				Height = (int)height;
			}

			protected override void CopyPixelsInternal(in PixelArea prc, int cbStride, int cbBufferSize, IntPtr pbBuffer) =>
				upstreamSource.CopyPixels(prc.ToWicRect(), (uint)cbStride, (uint)cbBufferSize, pbBuffer);

			public override string ToString() => sourceName;
		}

		private class PixelSourceAsIWICBitmapSource : IWICBitmapSource
		{
			private readonly PixelSource source;

			public PixelSourceAsIWICBitmapSource(PixelSource src) => source = src;

			public void GetSize(out uint puiWidth, out uint puiHeight)
			{
				puiWidth = (uint)source.Width;
				puiHeight = (uint)source.Height;
			}

			public Guid GetPixelFormat() => source.Format.FormatGuid;

			public void GetResolution(out double pDpiX, out double pDpiY) => pDpiX = pDpiY = 96d;

			public void CopyPalette(IWICPalette pIPalette) => throw new NotImplementedException();

			public void CopyPixels(in WICRect prc, uint cbStride, uint cbBufferSize, IntPtr pbBuffer) =>
				source.CopyPixels(PixelArea.FromWicRect(prc), (int)cbStride, (int)cbBufferSize, pbBuffer);
		}

		private class PixelSourceFromIPixelSource : PixelSource
		{
			private readonly IPixelSource upstreamSource;

			public PixelSourceFromIPixelSource(IPixelSource source)
			{
				upstreamSource = source;
				Format = PixelFormat.FromGuid(source.Format);
				Width = source.Width;
				Height = source.Height;
			}

			unsafe protected override void CopyPixelsInternal(in PixelArea prc, int cbStride, int cbBufferSize, IntPtr pbBuffer) =>
				upstreamSource.CopyPixels(prc.ToGdiRect(), cbStride, new Span<byte>(pbBuffer.ToPointer(), cbBufferSize));

			public override string? ToString() => upstreamSource.ToString();
		}

		private class PixelSourceAsIPixelSource : IPixelSource
		{
			private readonly PixelSource source;

			public PixelSourceAsIPixelSource(PixelSource src) => source = src;

			public Guid Format => source.Format.FormatGuid;
			public int Width => source.Width;
			public int Height => source.Height;

			unsafe public void CopyPixels(Rectangle sourceArea, int cbStride, Span<byte> buffer)
			{
				fixed (byte* pbBuffer = buffer)
					source.CopyPixels(PixelArea.FromGdiRect(sourceArea), cbStride, buffer.Length, (IntPtr)pbBuffer);
			}
		}

		public static PixelSource AsPixelSource(this IWICBitmapSource source, string name, bool profile = true) => new PixelSourceFromIWICBitmapSource(source, name, profile);
		public static IWICBitmapSource AsIWICBitmapSource(this PixelSource source) => new PixelSourceAsIWICBitmapSource(source);

		public static PixelSource AsPixelSource(this IPixelSource source) => new PixelSourceFromIPixelSource(source);
		public static IPixelSource AsIPixelSource(this PixelSource source) => new PixelSourceAsIPixelSource(source);
	}
}
