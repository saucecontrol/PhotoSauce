// Copyright © Clinton Ingram and Contributors.  Licensed under the MIT License.

using System;

#if HWINTRINSICS
using System.Runtime.Intrinsics;
using System.Runtime.Intrinsics.X86;
using System.Runtime.CompilerServices;
#endif

namespace PhotoSauce.MagicScaler.Transforms
{
	internal sealed class OverlayTransform : ChainedPixelSource
	{
		const int bytesPerPixel = 4;

		private readonly PixelSource overSource;
		private readonly int offsX, offsY;
		private readonly bool copyBase;

		private RentedBuffer<byte> lineBuff;

		public override bool Passthrough => false;

		public OverlayTransform(PixelSource source, PixelSource over, int left, int top, bool alpha, bool replay = false) : base(source)
		{
			if (Format.NumericRepresentation != PixelNumericRepresentation.UnsignedInteger || Format.ChannelCount != bytesPerPixel || Format.BytesPerPixel != bytesPerPixel)
				throw new NotSupportedException("Pixel format not supported.");

			if (over.Format != Format)
				throw new NotSupportedException("Sources must be same pixel format.");

			overSource = over;
			offsX = left;
			offsY = top;
			copyBase = !replay;

			if (alpha)
				lineBuff = BufferPool.RentAligned<byte>(over.Width * bytesPerPixel);
		}

		protected override unsafe void CopyPixelsInternal(in PixelArea prc, int cbStride, int cbBufferSize, byte* pbBuffer)
		{
			var inner = new PixelArea(offsX, offsY, overSource.Width, overSource.Height);

			int tx = Math.Max(prc.X - inner.X, 0);
			int tw = Math.Min(prc.Width, Math.Min(Math.Max(prc.X + prc.Width - inner.X, 0), inner.Width - tx));
			int cx = Math.Max(inner.X - prc.X, 0);

			for (int y = 0; y < prc.Height; y++)
			{
				int cy = prc.Y + y;

				if (copyBase || tw < prc.Width || cy < inner.Y || cy >= inner.Y + inner.Height)
				{
					Profiler.PauseTiming();
					PrevSource.CopyPixels(new PixelArea(prc.X, cy, prc.Width, 1), cbStride, cbBufferSize, pbBuffer);
					Profiler.ResumeTiming();
				}

				if (tw > 0 && cy >= inner.Y && cy < inner.Y + inner.Height)
				{
					var area = new PixelArea(tx, cy - inner.Y, tw, 1);
					byte* ptr = pbBuffer + cx * bytesPerPixel;

					if (lineBuff.IsEmpty)
						copyPixelsDirect(area, cbStride, cbBufferSize, ptr);
					else
						copyPixelsBuffered(area, ptr);
				}

				pbBuffer += cbStride;
			}
		}

		private unsafe void copyPixelsDirect(in PixelArea prc, int cbStride, int cbBufferSize, byte* pbBuffer)
		{
			Profiler.PauseTiming();
			overSource.CopyPixels(prc, cbStride, cbBufferSize, pbBuffer);
			Profiler.ResumeTiming();
		}

		private unsafe void copyPixelsBuffered(in PixelArea prc, byte* pbBuffer)
		{
			var buffspan = lineBuff.Span;
			fixed (byte* buff = buffspan)
			{
				Profiler.PauseTiming();
				overSource.CopyPixels(prc, buffspan.Length, buffspan.Length, buff);
				Profiler.ResumeTiming();

				uint* ip = (uint*)buff, ipe = ip + prc.Width;
				uint* op = (uint*)pbBuffer;

#if HWINTRINSICS
				if (HWIntrinsics.IsSupported && prc.Width >= HWIntrinsics.VectorCount<uint>())
				{
					copyPixelsIntrinsic(ip, ipe, op);
					return;
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

#if HWINTRINSICS
		[MethodImpl(MethodImplOptions.AggressiveOptimization)]
		private static unsafe void copyPixelsIntrinsic(uint* ip, uint* ipe, uint* op)
		{
			if (Avx2.IsSupported)
			{
				var vzero = Vector256<uint>.Zero;
				ipe -= Vector256<uint>.Count;

				LoopTop:
				do
				{
					var vi = Avx.LoadVector256(ip);
					ip += Vector256<uint>.Count;

					var vm = Avx2.CompareEqual(Avx2.ShiftRightLogical(vi, 24), vzero);
					var vo = Avx2.BlendVariable(vi, Avx.LoadVector256(op), vm);

					Avx.Store(op, vo);
					op += Vector256<uint>.Count;
				}
				while (ip <= ipe);

				if (ip < ipe + Vector256<uint>.Count)
				{
					nint offs = UnsafeUtil.ByteOffset(ipe, ip);
					ip = UnsafeUtil.SubtractOffset(ip, offs);
					op = UnsafeUtil.SubtractOffset(op, offs);
					goto LoopTop;
				}
			}
			else
			{
				var vzero = Vector128<uint>.Zero;
				ipe -= Vector128<uint>.Count;

				LoopTop:
				do
				{
					var vi = Sse2.LoadVector128(ip);
					ip += Vector128<uint>.Count;

					var vm = Sse2.CompareEqual(Sse2.ShiftRightLogical(vi, 24), vzero);
					var vo = HWIntrinsics.BlendVariable(vi, Sse2.LoadVector128(op), vm);

					Sse2.Store(op, vo);
					op += Vector128<uint>.Count;
				}
				while (ip <= ipe);

				if (ip < ipe + Vector128<uint>.Count)
				{
					nint offs = UnsafeUtil.ByteOffset(ipe, ip);
					ip = UnsafeUtil.SubtractOffset(ip, offs);
					op = UnsafeUtil.SubtractOffset(op, offs);
					goto LoopTop;
				}
			}
		}
#endif

		protected override void Dispose(bool disposing)
		{
			if (disposing)
			{
				overSource.Dispose();

				lineBuff.Dispose();
				lineBuff = default;
			}

			base.Dispose(disposing);
		}

		public override string ToString() => nameof(OverlayTransform);
	}
}
