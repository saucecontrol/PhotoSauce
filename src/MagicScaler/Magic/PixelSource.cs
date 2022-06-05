// Copyright © Clinton Ingram and Contributors.  Licensed under the MIT License.

using System;
using System.Drawing;
using System.Diagnostics;
using System.Runtime.CompilerServices;

namespace PhotoSauce.MagicScaler
{
	internal abstract class PixelSource : IPixelSource, IProfileSource, IDisposable
	{
		public abstract PixelFormat Format { get; }

		Guid IPixelSource.Format => Format.FormatGuid;

		public abstract int Width { get; }
		public abstract int Height { get; }

		public IProfiler Profiler { get; }

		public PixelArea Area => new(0, 0, Width, Height);

		protected PixelSource() => Profiler = StatsManager.GetProfiler(this);

		[Conditional("GUARDRAILS")]
		private unsafe void checkBounds(in PixelArea prc, int cbStride, int cbBufferSize, void* pbBuffer)
		{
			int cbLine = MathUtil.DivCeiling(prc.Width * Format.BitsPerPixel, 8);

			if (prc.X + prc.Width > Width || prc.Y + prc.Height > Height)
				throw new ArgumentOutOfRangeException(nameof(prc), "Requested area does not fall within the image bounds");

			if (cbLine > cbStride)
				throw new ArgumentOutOfRangeException(nameof(cbStride), "Stride is too small for the requested area");

			if ((prc.Height - 1) * cbStride + cbLine > cbBufferSize)
				throw new ArgumentOutOfRangeException(nameof(cbBufferSize), "Buffer is too small for the requested area");

			if (pbBuffer is null)
				throw new ArgumentOutOfRangeException(nameof(pbBuffer), "Buffer pointer is invalid");
		}

		protected abstract unsafe void CopyPixelsInternal(in PixelArea prc, int cbStride, int cbBufferSize, byte* pbBuffer);

		public unsafe void CopyPixels(in PixelArea prc, int cbStride, int cbBufferSize, byte* pbBuffer)
		{
			checkBounds(prc, cbStride, cbBufferSize, pbBuffer);

			Profiler.ResumeTiming(prc);
			CopyPixelsInternal(prc, cbStride, cbBufferSize, pbBuffer);
			Profiler.PauseTiming();
		}

		unsafe void IPixelSource.CopyPixels(Rectangle sourceArea, int cbStride, Span<byte> buffer)
		{
			var prc = (PixelArea)sourceArea;
			int cbLine = MathUtil.DivCeiling(prc.Width * Format.BitsPerPixel, 8);
			int cbBuffer = buffer.Length;

			if (prc.X + prc.Width > Width || prc.Y + prc.Height > Height)
				throw new ArgumentOutOfRangeException(nameof(sourceArea), "Requested area does not fall within the image bounds");

			if (cbLine > cbStride)
				throw new ArgumentOutOfRangeException(nameof(cbStride), "Stride is too small for the requested area");

			if ((prc.Height - 1) * cbStride + cbLine > cbBuffer)
				throw new ArgumentOutOfRangeException(nameof(buffer), "Buffer is too small for the requested area");

			fixed (byte* pbBuffer = buffer)
				CopyPixels(prc, cbStride, cbBuffer, pbBuffer);
		}

		protected virtual void Dispose(bool disposing) { }

		public void Dispose()
		{
			Dispose(true);
			GC.SuppressFinalize(this);
		}
	}

	internal sealed class PixelSourceFrame : IImageFrame
	{
		public IPixelSource PixelSource { get; }

		public PixelSourceFrame(IPixelSource source) => PixelSource = source;

		public void Dispose() { }
	}

	internal sealed class PixelSourceContainer : IImageContainer
	{
		private readonly IPixelSource pixelSource;

		public string? MimeType => null;
		public int FrameCount => 1;

		public PixelSourceContainer(IPixelSource source) => pixelSource = source;

		public IImageFrame GetFrame(int index) => index == 0 ? new PixelSourceFrame(pixelSource) : throw new ArgumentOutOfRangeException(nameof(index));

		void IDisposable.Dispose() { }
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

		public virtual void ReInit(PixelSource newSource)
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

			prev.Dispose();

			PrevSource = newSource;
		}

		protected override void Dispose(bool disposing)
		{
			if (disposing)
				PrevSource.Dispose();

			base.Dispose(disposing);
		}
	}

	internal sealed class NoopPixelSource : PixelSource
	{
		public static readonly PixelSource Instance = new NoopPixelSource();

		public override PixelFormat Format => PixelFormat.Grey32Float;
		public override int Width => default;
		public override int Height => default;

		protected override unsafe void CopyPixelsInternal(in PixelArea prc, int cbStride, int cbBufferSize, byte* pbBuffer) { }
	}

	internal sealed class FrameBufferSource : PixelSource
	{
		private readonly bool multiDispose;
		private RentedBuffer<byte> frameBuff;

		public int Stride { get; }

		public Span<byte> Span => frameBuff.Span;

		public override PixelFormat Format { get; }
		public override int Width { get; }
		public override int Height { get; }

		public FrameBufferSource(int width, int height, PixelFormat format, bool multidispose = false)
		{
			Format = format;
			Width = width;
			Height = height;
			multiDispose = multidispose;

			Stride = MathUtil.PowerOfTwoCeiling(width * Format.BytesPerPixel, HWIntrinsics.VectorCount<byte>());

			frameBuff = BufferPool.RentAligned<byte>(checked(Stride * height));
		}

		protected override void Dispose(bool disposing)
		{
			if (disposing && !multiDispose)
				DisposeBuffer();

			base.Dispose(disposing);
		}

		public void DisposeBuffer()
		{
			frameBuff.Dispose();
			frameBuff = default;
		}

		public override string ToString() => $"{nameof(FrameBufferSource)}: {Format.Name}";

		protected override unsafe void CopyPixelsInternal(in PixelArea prc, int cbStride, int cbBufferSize, byte* pbBuffer)
		{
			var buffspan = frameBuff.Span;
			if (buffspan.IsEmpty) throw new ObjectDisposedException(nameof(FrameBufferSource));

			int bpp = Format.BytesPerPixel;
			int cb = prc.Width * bpp;

			ref byte buff = ref buffspan[prc.Y * Stride + prc.X * bpp];

			for (int y = 0; y < prc.Height; y++)
				Unsafe.CopyBlockUnaligned(ref *(pbBuffer + y * cbStride), ref Unsafe.Add(ref buff, y * Stride), (uint)cb);
		}
	}

	internal sealed class PlanarPixelSource : PixelSource
	{
		public PixelSource SourceY, SourceCb, SourceCr;
		public ChromaSubsampleMode ChromaSubsampling;

		public override PixelFormat Format => SourceY.Format;
		public override int Width => SourceY.Width;
		public override int Height => SourceY.Height;


		public PlanarPixelSource(PixelSource sourceY, PixelSource sourceCb, PixelSource sourceCr)
		{
			if (sourceY.Format != PixelFormat.Y8) throw new ArgumentException("Invalid pixel format", nameof(sourceY));
			if (sourceCb.Format != PixelFormat.Cb8) throw new ArgumentException("Invalid pixel format", nameof(sourceCb));
			if (sourceCr.Format != PixelFormat.Cr8) throw new ArgumentException("Invalid pixel format", nameof(sourceCr));

			SourceY = sourceY;
			SourceCb = sourceCb;
			SourceCr = sourceCr;

			ChromaSubsampling = GetSubsampling();
		}

		public PlanarPixelSource(IYccImageFrame frame)
		{
			if (frame.PixelSource is PixelSource srcY && frame.PixelSourceCb is PixelSource srcCb && frame.PixelSourceCr is PixelSource srcCr)
			{
				SourceY = srcY;
				SourceCb = srcCb;
				SourceCr = srcCr;
			}
			else
			{
				if (frame.PixelSource.Format != PixelFormat.Y8.FormatGuid) throw new NotSupportedException($"Invalid pixel format {nameof(IYccImageFrame.PixelSource)}");
				if (frame.PixelSourceCb.Format != PixelFormat.Cb8.FormatGuid) throw new NotSupportedException($"Invalid pixel format {nameof(IYccImageFrame.PixelSourceCb)}");
				if (frame.PixelSourceCr.Format != PixelFormat.Cr8.FormatGuid) throw new NotSupportedException($"Invalid pixel format {nameof(IYccImageFrame.PixelSourceCr)}");

				SourceY = frame.PixelSource.AsPixelSource(frame.IsFullRange ? PixelFormat.Y8 : PixelFormat.Y8Video);
				SourceCb = frame.PixelSourceCb.AsPixelSource(frame.IsFullRange ? PixelFormat.Cb8 : PixelFormat.Cb8Video);
				SourceCr = frame.PixelSourceCr.AsPixelSource(frame.IsFullRange ? PixelFormat.Cr8 : PixelFormat.Cr8Video);
			}

			ChromaSubsampling = GetSubsampling();
		}

		public ChromaSubsampleMode GetSubsampling()
		{
			if (SourceCb.Width != SourceCr.Width || SourceCb.Height != SourceCr.Height) throw new ArgumentException("Chroma planes must be same size.");
			bool subsampleX = SourceY.Width != SourceCb.Width && ((SourceY.Width + 1 & ~1) == (SourceCb.Width << 1) ? true : throw new NotSupportedException("Horizontal subsampling ratio must be 1:1 or 2:1."));
			bool subsampleY = SourceY.Height != SourceCb.Height && ((SourceY.Height + 1 & ~1) == (SourceCb.Height << 1) ? true : throw new NotSupportedException("Vertical subsampling ratio must be 1:1 or 2:1."));

			return (subsampleX, subsampleY) switch {
				(true, true)  => ChromaSubsampleMode.Subsample420,
				(true, false) => ChromaSubsampleMode.Subsample422,
				(false, true) => ChromaSubsampleMode.Subsample440,
				_             => ChromaSubsampleMode.Subsample444
			};
		}

		protected override unsafe void CopyPixelsInternal(in PixelArea prc, int cbStride, int cbBufferSize, byte* pbBuffer) => throw new NotImplementedException();

		protected override void Dispose(bool disposing)
		{
			if (disposing)
			{
				SourceY.Dispose();
				SourceCb.Dispose();
				SourceCr.Dispose();
			}

			base.Dispose(disposing);
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

			public PixelSourceFromIPixelSource(IPixelSource source, PixelFormat format) => (upstreamSource, Format) = (source, format);

			protected override unsafe void CopyPixelsInternal(in PixelArea prc, int cbStride, int cbBufferSize, byte* pbBuffer) =>
				upstreamSource.CopyPixels(prc, cbStride, new Span<byte>(pbBuffer, cbBufferSize));

			public override string? ToString() => upstreamSource.ToString();
		}

		public static PixelSource AsPixelSource(this IPixelSource source) => source as PixelSource ?? new PixelSourceFromIPixelSource(source);

		public static PixelSource AsPixelSource(this IPixelSource source, PixelFormat fmt) => new PixelSourceFromIPixelSource(source, fmt);
	}
}
