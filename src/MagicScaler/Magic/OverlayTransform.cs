using System;

#if HWINTRINSICS
using System.Runtime.Intrinsics;
using System.Runtime.Intrinsics.X86;
using System.Runtime.InteropServices;
using System.Runtime.CompilerServices;
#endif

namespace PhotoSauce.MagicScaler.Transforms
{
	internal class OverlayTransform : PixelSource, IDisposable
	{
		const int bytesPerPixel = 4;

		private readonly PixelSource overSource;
		private readonly int offsX, offsY;
		private readonly bool passthrough;

		private ArraySegment<byte> lineBuff;

		public OverlayTransform(PixelSource source, PixelSource over, int left, int top, bool alpha, bool replay = false) : base(source)
		{
			if (Format.NumericRepresentation != PixelNumericRepresentation.UnsignedInteger || Format.ChannelCount != bytesPerPixel || Format.BitsPerPixel != bytesPerPixel * 8)
				throw new NotSupportedException("Pixel format not supported.");

			if (over.Format != Format)
				throw new NotSupportedException("Sources must be same pixel format.");

			overSource = over;
			offsX = left;
			offsY = top;
			passthrough = replay;

			if (alpha)
				lineBuff = BufferPool.Rent(over.Width * bytesPerPixel, true);
		}

		unsafe protected override void CopyPixelsInternal(in PixelArea prc, int cbStride, int cbBufferSize, IntPtr pbBuffer)
		{
			var inner = new PixelArea(offsX, offsY, overSource.Width, overSource.Height);

			int tx = Math.Max(prc.X - inner.X, 0);
			int tw = Math.Min(prc.Width, Math.Min(Math.Max(prc.X + prc.Width - inner.X, 0), inner.Width - tx));
			int cx = Math.Max(inner.X - prc.X, 0);
			byte* pb = (byte*)pbBuffer;

			for (int y = 0; y < prc.Height; y++)
			{
				int cy = prc.Y + y;

				if (!passthrough || tw < prc.Width || cy < inner.Y || cy >= inner.Y + inner.Height)
				{
					Profiler.PauseTiming();
					Source.CopyPixels(new PixelArea(prc.X, cy, prc.Width, 1), cbStride, cbBufferSize, (IntPtr)pb);
					Profiler.ResumeTiming();
				}

				if (tw > 0 && cy >= inner.Y && cy < inner.Y + inner.Height)
				{
					var area = new PixelArea(tx, cy - inner.Y, tw, 1);
					var ptr = (IntPtr)(pb + cx * bytesPerPixel);

					if (lineBuff.Array is null)
						copyPixelsDirect(area, cbStride, cbBufferSize, ptr);
					else
						copyPixelsBuffered(area, ptr);
				}

				pb += cbStride;
			}
		}

		private void copyPixelsDirect(in PixelArea prc, int cbStride, int cbBufferSize, IntPtr pbBuffer)
		{
			Profiler.PauseTiming();
			overSource.CopyPixels(prc, cbStride, cbBufferSize, pbBuffer);
			Profiler.ResumeTiming();
		}

		unsafe private void copyPixelsBuffered(in PixelArea prc, IntPtr pbBuffer)
		{
			fixed (byte* buff = &lineBuff.Array![lineBuff.Offset])
			{
				Profiler.PauseTiming();
				overSource.CopyPixels(prc, lineBuff.Count, lineBuff.Count, (IntPtr)buff);
				Profiler.ResumeTiming();

				uint* ip = (uint*)buff, ipe = ip + prc.Width;
				uint* op = (uint*)pbBuffer;

#if HWINTRINSICS
				var shuffleMaskAlpha = (ReadOnlySpan<byte>)(new byte[] { 3, 3, 3, 3, 7, 7, 7, 7, 11, 11, 11, 11, 15, 15, 15, 15 });

				if (Avx2.IsSupported && prc.Width >= Vector256<uint>.Count)
				{
					var vshufa = Avx2.BroadcastVector128ToVector256((byte*)Unsafe.AsPointer(ref MemoryMarshal.GetReference(shuffleMaskAlpha)));

					ipe -= Vector256<uint>.Count;
					do
					{
						var vi = Avx.LoadVector256(ip);
						ip += Vector256<uint>.Count;

						var va = Avx2.Shuffle(vi.AsByte(), vshufa).AsUInt32();
						var vo = Avx2.Or(Avx2.And(va, vi), Avx2.AndNot(va, Avx.LoadVector256(op)));

						Avx.Store(op, vo);
						op += Vector256<uint>.Count;

					} while (ip <= ipe);
					ipe += Vector256<uint>.Count;
				}
				else if (Ssse3.IsSupported && prc.Width >= Vector128<uint>.Count)
				{
					var vshufa = Sse2.LoadVector128((byte*)Unsafe.AsPointer(ref MemoryMarshal.GetReference(shuffleMaskAlpha)));

					ipe -= Vector128<uint>.Count;
					do
					{
						var vi = Sse2.LoadVector128(ip);
						ip += Vector128<uint>.Count;

						var va = Ssse3.Shuffle(vi.AsByte(), vshufa).AsUInt32();
						var vo = Sse2.Or(Sse2.And(va, vi), Sse2.AndNot(va, Sse2.LoadVector128(op)));

						Sse2.Store(op, vo);
						op += Vector128<uint>.Count;

					} while (ip <= ipe);
					ipe += Vector128<uint>.Count;
				}
#endif

				while (ip < ipe)
				{
					uint i = *ip++;
					if (i >> 24 != 0)
						*op = i;

					op++;
				}
			}
		}

		public void Dispose()
		{
			BufferPool.Return(lineBuff);
			lineBuff = default;
		}
	}
}
