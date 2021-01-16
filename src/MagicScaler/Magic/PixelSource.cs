// Copyright © Clinton Ingram and Contributors.  Licensed under the MIT License.

using System;
using System.Drawing;
using System.Diagnostics;
using System.Runtime.CompilerServices;

namespace PhotoSauce.MagicScaler
{
	internal abstract class PixelSource
	{
		protected IPixelSourceProfiler Profiler { get; }

		public abstract PixelFormat Format { get; }
		public abstract int Width { get; }
		public abstract int Height { get; }

		public PixelArea Area => new(0, 0, Width, Height);

		public PixelSourceStats? Stats => Profiler is SourceStatsProfiler ps ? ps.Stats : null;

		protected PixelSource() =>
			Profiler = MagicImageProcessor.EnablePixelSourceStats ? new SourceStatsProfiler(this) : NoopProfiler.Instance;

		[Conditional("GUARDRAILS")]
		private void checkBounds(in PixelArea prc, int cbStride, int cbBufferSize, IntPtr pbBuffer)
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
		}

		protected abstract void CopyPixelsInternal(in PixelArea prc, int cbStride, int cbBufferSize, IntPtr pbBuffer);

		public void CopyPixels(in PixelArea prc, int cbStride, int cbBufferSize, IntPtr pbBuffer)
		{
			checkBounds(prc, cbStride, cbBufferSize, pbBuffer);

			Profiler.ResumeTiming();
			CopyPixelsInternal(prc, cbStride, cbBufferSize, pbBuffer);
			Profiler.PauseTiming();

			Profiler.LogCopyPixels(prc);
		}
	}

	internal class PixelSourceFrame : IImageFrame
	{
		public double DpiX => 96d;
		public double DpiY => 96d;
		public Orientation ExifOrientation { get; set; } = Orientation.Normal;
		public ReadOnlySpan<byte> IccProfile => ReadOnlySpan<byte>.Empty;

		public IPixelSource PixelSource { get; }

		public PixelSourceFrame(IPixelSource source) => PixelSource = source;

		public void Dispose() { }
	}

	internal class PixelSourceContainer : IImageContainer
	{
		private readonly IPixelSource pixelSource;

		public FileFormat ContainerFormat => FileFormat.Unknown;
		public int FrameCount => 1;

		public PixelSourceContainer(IPixelSource source) => pixelSource = source;

		public IImageFrame GetFrame(int index) => index == 0 ? new PixelSourceFrame(pixelSource) : throw new IndexOutOfRangeException();
	}

	internal abstract class ChainedPixelSource : PixelSource
	{
		protected PixelSource PrevSource { get; private set; }

		protected int BufferStride => MathUtil.PowerOfTwoCeiling(PrevSource.Width * PrevSource.Format.BytesPerPixel, IntPtr.Size);

		protected ChainedPixelSource(PixelSource source) : base() => PrevSource = source;

		public override PixelFormat Format => PrevSource.Format;
		public override int Width => PrevSource.Width;
		public override int Height => PrevSource.Height;

		public virtual bool Passthrough => true;
		protected virtual void Reset() { }

		public void ReInit(PixelSource newSource)
		{
			Reset();

			if (newSource == this)
				return;

			var prev = PrevSource;
			if (prev is ChainedPixelSource chain && chain.Passthrough)
			{
				chain.ReInit(newSource);
				return;
			}

			if (prev.Format != newSource.Format || prev.Width != newSource.Width || prev.Height != newSource.Height)
				throw new NotSupportedException("New source is not compatible with current pipeline.");

			PrevSource = newSource;
		}
	}

	internal sealed class NoopPixelSource : PixelSource
	{
		public static readonly PixelSource Instance = new NoopPixelSource();

		public override PixelFormat Format => PixelFormat.Grey32BppFloat;
		public override int Width => default;
		public override int Height => default;

		protected override void CopyPixelsInternal(in PixelArea prc, int cbStride, int cbBufferSize, IntPtr pbBuffer) { }
	}

	internal sealed class FrameBufferSource : PixelSource, IDisposable
	{
		private ArraySegment<byte> frameBuff;

		public int Stride { get; }

		public Span<byte> Span => frameBuff.AsSpan();

		public override PixelFormat Format { get; }
		public override int Width { get; }
		public override int Height { get; }

		public FrameBufferSource(int width, int height, PixelFormat format) : base()
		{
			Format = format;
			Width = width;
			Height = height;

			Stride = MathUtil.PowerOfTwoCeiling(width * Format.BytesPerPixel, HWIntrinsics.VectorCount<byte>());

			frameBuff = BufferPool.Rent(Stride * height, true);
		}

		public void PauseTiming() => Profiler.PauseTiming();
		public void ResumeTiming() => Profiler.ResumeTiming();

		public void Dispose()
		{
			BufferPool.Return(frameBuff);
			frameBuff = default;
		}

		public override string ToString() => nameof(FrameBufferSource);

		protected override unsafe void CopyPixelsInternal(in PixelArea prc, int cbStride, int cbBufferSize, IntPtr pbBuffer)
		{
			if (frameBuff.Array is null) throw new ObjectDisposedException(nameof(FrameBufferSource));

			int bpp = Format.BytesPerPixel;
			int cb = prc.Width * bpp;

			ref byte buff = ref frameBuff.Array[frameBuff.Offset + prc.Y * Stride + prc.X * bpp];

			for (int y = 0; y < prc.Height; y++)
				Unsafe.CopyBlockUnaligned(ref *((byte*)pbBuffer + y * cbStride), ref Unsafe.Add(ref buff, y * Stride), (uint)cb);
		}
	}

	internal static class PixelSourceExtensions
	{
		private sealed class PixelSourceFromIPixelSource : PixelSource
		{
			private readonly IPixelSource upstreamSource;

			public override PixelFormat Format { get; }
			public override int Width => upstreamSource.Width;
			public override int Height => upstreamSource.Height;

			public PixelSourceFromIPixelSource(IPixelSource source) => (upstreamSource, Format) = (source, PixelFormat.FromGuid(source.Format));

			protected override unsafe void CopyPixelsInternal(in PixelArea prc, int cbStride, int cbBufferSize, IntPtr pbBuffer) =>
				upstreamSource.CopyPixels(prc.ToGdiRect(), cbStride, new Span<byte>(pbBuffer.ToPointer(), cbBufferSize));

			public override string? ToString() => upstreamSource.ToString();
		}

		private sealed class PixelSourceAsIPixelSource : IPixelSource
		{
			private readonly PixelSource source;

			public PixelSourceAsIPixelSource(PixelSource src) => source = src;

			public Guid Format => source.Format.FormatGuid;
			public int Width => source.Width;
			public int Height => source.Height;

			public unsafe void CopyPixels(Rectangle sourceArea, int cbStride, Span<byte> buffer)
			{
				var prc = PixelArea.FromGdiRect(sourceArea);
				int cbLine = MathUtil.DivCeiling(prc.Width * source.Format.BitsPerPixel, 8);
				int cbBuffer = buffer.Length;

				if (prc.X + prc.Width > Width || prc.Y + prc.Height > Height)
					throw new ArgumentOutOfRangeException(nameof(prc), "Requested area does not fall within the image bounds");

				if (cbLine > cbStride)
					throw new ArgumentOutOfRangeException(nameof(cbStride), "Stride is too small for the requested area");

				if ((prc.Height - 1) * cbStride + cbLine > cbBuffer)
					throw new ArgumentOutOfRangeException(nameof(buffer), "Buffer is too small for the requested area");

				fixed (byte* pbBuffer = buffer)
					source.CopyPixels(prc, cbStride, cbBuffer, (IntPtr)pbBuffer);
			}
		}

		public static PixelSource AsPixelSource(this IPixelSource source) => new PixelSourceFromIPixelSource(source);
		public static IPixelSource AsIPixelSource(this PixelSource source) => new PixelSourceAsIPixelSource(source);
	}
}
