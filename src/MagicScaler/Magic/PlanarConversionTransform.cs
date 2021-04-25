// Copyright © Clinton Ingram and Contributors.  Licensed under the MIT License.

using System;
using System.Numerics;

#if HWINTRINSICS
using System.Runtime.Intrinsics;
using System.Runtime.Intrinsics.X86;
using System.Runtime.InteropServices;
using System.Runtime.CompilerServices;
#endif

using static PhotoSauce.MagicScaler.MathUtil;

namespace PhotoSauce.MagicScaler.Transforms
{
	internal sealed class PlanarConversionTransform : ChainedPixelSource
	{
		private const int ichromaOffset = 128;
		private const float videoChromaScale = 224f;
		private const float fchromaOffset = (float)ichromaOffset / byte.MaxValue;

		private readonly float coeffCb0, coeffCb1, coeffCr0, coeffCr1;

		private PixelSource sourceCb, sourceCr;
		private RentedBuffer<byte> lineBuff;

		public override PixelFormat Format { get; }

		public PlanarConversionTransform(PixelSource srcY, PixelSource srcCb, PixelSource srcCr, Matrix4x4 matrix, bool videoLevels) : base(srcY)
		{
			if (srcCb.Width != srcY.Width || srcCb.Height != srcY.Height) throw new ArgumentException("Chroma plane incorrect size", nameof(srcCb));
			if (srcCr.Width != srcY.Width || srcCr.Height != srcY.Height) throw new ArgumentException("Chroma plane incorrect size", nameof(srcCr));
			if (srcCb.Format.BitsPerPixel != srcY.Format.BitsPerPixel) throw new ArgumentException("Chroma plane incorrect format", nameof(srcCb));
			if (srcCr.Format.BitsPerPixel != srcY.Format.BitsPerPixel) throw new ArgumentException("Chroma plane incorrect format", nameof(srcCr));

			matrix = matrix.InvertPrecise();
			if (matrix.IsNaN()) throw new ArgumentException("Invalid YCC matrix", nameof(matrix));

			sourceCb = srcCb;
			sourceCr = srcCr;

			if (videoLevels)
				matrix *= byte.MaxValue / videoChromaScale;

			coeffCb0 = matrix.M23;
			coeffCb1 = matrix.M22;
			coeffCr0 = matrix.M32;
			coeffCr1 = matrix.M31;

			Format = srcY.Format == PixelFormat.Y8 ? PixelFormat.Bgr24 : PixelFormat.Bgrx128Float;

			int bufferStride = BufferStride;
			if (HWIntrinsics.IsAvxSupported)
				bufferStride = PowerOfTwoCeiling(bufferStride, HWIntrinsics.VectorCount<byte>());

			lineBuff = BufferPool.RentAligned<byte>(bufferStride * 3);
		}

		public override void ReInit(PixelSource newSource)
		{
			if (newSource is PlanarPixelSource plsrc)
			{
				base.ReInit(plsrc.SourceY);
				reInit(ref sourceCb, plsrc.SourceCb);
				reInit(ref sourceCr, plsrc.SourceCr);

				return;
			}

			base.ReInit(newSource);
		}

		private static void reInit(ref PixelSource cursrc, PixelSource newsrc)
		{
			if (cursrc is ChainedPixelSource chain && chain.Passthrough)
			{
				chain.ReInit(newsrc);
				return;
			}

			cursrc.Dispose();
			cursrc = newsrc;
		}

		protected override unsafe void CopyPixelsInternal(in PixelArea prc, int cbStride, int cbBufferSize, IntPtr pbBuffer)
		{
			var buffspan = lineBuff.Span;
			if (buffspan.Length == 0) throw new ObjectDisposedException(nameof(PlanarConversionTransform));

			fixed (byte* bstart = buffspan)
			{
				uint cb = (uint)DivCeiling(prc.Width * PrevSource.Format.BitsPerPixel, 8);
				uint bstride = (uint)buffspan.Length / 3u;

				for (int y = 0; y < prc.Height; y++)
				{
					var lrc = new PixelArea(prc.X, prc.Y + y, prc.Width, 1);

					Profiler.PauseTiming();
					PrevSource.CopyPixels(lrc, (int)bstride, (int)bstride, (IntPtr)bstart);
					sourceCb.CopyPixels(lrc, (int)bstride, (int)bstride, (IntPtr)(bstart + bstride));
					sourceCr.CopyPixels(lrc, (int)bstride, (int)bstride, (IntPtr)(bstart + bstride * 2));
					Profiler.ResumeTiming();

					byte* op = (byte*)pbBuffer + y * cbStride;
					if (Format.NumericRepresentation == PixelNumericRepresentation.Float)
#if HWINTRINSICS
						if (HWIntrinsics.IsSupported && cb >= (uint)Vector128<byte>.Count * 4)
							copyPixelsIntrinsic(bstart, op, bstride, cb);
						else
#endif
							copyPixelsFloat(bstart, op, bstride, cb);
					else
						copyPixelsByte(bstart, op, bstride, cb);
				}
			}
		}

		private unsafe void copyPixelsByte(byte* bstart, byte* opstart, uint bstride, uint cb)
		{
			byte* op = opstart;
			byte* ip0 = bstart, ip1 = bstart + bstride, ip2 = bstart + bstride * 2, ipe = ip0 + cb;

			int c0 = Fix15(coeffCb0);
			int c1 = Fix15(coeffCb1);
			int c2 = Fix15(coeffCr0);
			int c3 = Fix15(coeffCr1);

			while (ip0 < ipe)
			{
				int i0 = *ip0++ * UQ15One;
				int i1 = *ip1++ - ichromaOffset;
				int i2 = *ip2++ - ichromaOffset;

				byte o0 = UnFix15ToByte(i0 + i1 * c0);
				byte o1 = UnFix15ToByte(i0 + i1 * c1 + i2 * c2);
				byte o2 = UnFix15ToByte(i0 + i2 * c3);

				op[0] = o0;
				op[1] = o1;
				op[2] = o2;
				op += 3;
			}
		}

		private unsafe void copyPixelsFloat(byte* bstart, byte* opstart, uint bstride, uint cb)
		{
			float* op = (float*)opstart;
			float* ip0 = (float*)bstart, ip1 = (float*)(bstart + bstride), ip2 = (float*)(bstart + bstride * 2), ipe = (float*)(bstart + cb);

			float c0 = coeffCb0;
			float c1 = coeffCb1;
			float c2 = coeffCr0;
			float c3 = coeffCr1;

			var voff = new Vector4(0f, fchromaOffset, fchromaOffset, 0f);
			float fzero = voff.X, foff = voff.Y;

			while (ip0 < ipe)
			{
				float f0 = *ip0++;
				float f1 = *ip1++ - foff;
				float f2 = *ip2++ - foff;

				op[0] = f0 + f1 * c0;
				op[1] = f0 + f1 * c1 + f2 * c2;
				op[2] = f0 + f2 * c3;
				op[3] = fzero;
				op += 4;
			}
		}

#if HWINTRINSICS
		[MethodImpl(MethodImplOptions.AggressiveOptimization)]
		private unsafe void copyPixelsIntrinsic(byte* bstart, byte* opstart, uint bstride, uint cb)
		{
			uint stride = bstride / sizeof(float);
			float* op = (float*)opstart;
			float* ip = (float*)bstart, ipe = (float*)(bstart + cb);

			if (Avx2.IsSupported)
			{
				var vc0 = Vector256.Create(coeffCb0);
				var vc1 = Vector256.Create(coeffCb1);
				var vc2 = Vector256.Create(coeffCr0);
				var vc3 = Vector256.Create(coeffCr1);
				var voff = Vector256.Create(-fchromaOffset);
				var vmaskp = Avx.LoadVector256((int*)Unsafe.AsPointer(ref MemoryMarshal.GetReference(HWIntrinsics.PermuteMaskEvenOdd8x32)));

				ipe -= Vector256<float>.Count;

				LoopTop:
				do
				{
					var viy = Avx.LoadVector256(ip);
					var vib = Avx.Add(voff, Avx.LoadVector256(ip + stride));
					var vir = Avx.Add(voff, Avx.LoadVector256(ip + stride * 2));
					ip += Vector256<float>.Count;

					viy = Avx2.PermuteVar8x32(viy, vmaskp);
					vib = Avx2.PermuteVar8x32(vib, vmaskp);
					vir = Avx2.PermuteVar8x32(vir, vmaskp);

					var vt0 = HWIntrinsics.MultiplyAdd(viy, vib, vc0);
					var vt1 = HWIntrinsics.MultiplyAdd(HWIntrinsics.MultiplyAdd(viy, vib, vc1), vir, vc2);
					var vt2 = HWIntrinsics.MultiplyAdd(viy, vir, vc3);
					var vt3 = Vector256<float>.Zero;

					var vte = Avx.UnpackLow(vt0, vt2);
					var vto = Avx.UnpackLow(vt1, vt3);

					Avx.Store(op, Avx.UnpackLow(vte, vto));
					Avx.Store(op + Vector256<float>.Count, Avx.UnpackHigh(vte, vto));

					vte = Avx.UnpackHigh(vt0, vt2);
					vto = Avx.UnpackHigh(vt1, vt3);

					Avx.Store(op + Vector256<float>.Count * 2, Avx.UnpackLow(vte, vto));
					Avx.Store(op + Vector256<float>.Count * 3, Avx.UnpackHigh(vte, vto));
					op += Vector256<float>.Count * 4;
				}
				while (ip <= ipe);

				if (ip < ipe + Vector256<float>.Count)
				{
					nint offs = UnsafeUtil.ByteOffset(ipe, ip);
					ip = UnsafeUtil.SubtractOffset(ip, offs);
					op = UnsafeUtil.SubtractOffset(op, offs * 4);
					goto LoopTop;
				}
			}
			else
			{
				var vc0 = Vector128.Create(coeffCb0);
				var vc1 = Vector128.Create(coeffCb1);
				var vc2 = Vector128.Create(coeffCr0);
				var vc3 = Vector128.Create(coeffCr1);
				var voff = Vector128.Create(-fchromaOffset);

				ipe -= Vector128<float>.Count;

				LoopTop:
				do
				{
					var viy = Sse.LoadVector128(ip);
					var vib = Sse.Add(voff, Sse.LoadVector128(ip + stride));
					var vir = Sse.Add(voff, Sse.LoadVector128(ip + stride * 2));
					ip += Vector128<float>.Count;

					var vt0 = HWIntrinsics.MultiplyAdd(viy, vib, vc0);
					var vt1 = HWIntrinsics.MultiplyAdd(HWIntrinsics.MultiplyAdd(viy, vib, vc1), vir, vc2);
					var vt2 = HWIntrinsics.MultiplyAdd(viy, vir, vc3);
					var vt3 = Vector128<float>.Zero;

					var vte = Sse.UnpackLow(vt0, vt2);
					var vto = Sse.UnpackLow(vt1, vt3);

					Sse.Store(op, Sse.UnpackLow(vte, vto));
					Sse.Store(op + Vector128<float>.Count, Sse.UnpackHigh(vte, vto));

					vte = Sse.UnpackHigh(vt0, vt2);
					vto = Sse.UnpackHigh(vt1, vt3);

					Sse.Store(op + Vector128<float>.Count * 2, Sse.UnpackLow(vte, vto));
					Sse.Store(op + Vector128<float>.Count * 3, Sse.UnpackHigh(vte, vto));
					op += Vector128<float>.Count * 4;
				}
				while (ip <= ipe);

				if (ip < ipe + Vector128<float>.Count)
				{
					nint offs = UnsafeUtil.ByteOffset(ipe, ip);
					ip = UnsafeUtil.SubtractOffset(ip, offs);
					op = UnsafeUtil.SubtractOffset(op, offs * 4);
					goto LoopTop;
				}
			}
		}
#endif

		protected override void Dispose(bool disposing)
		{
			if (disposing)
			{
				sourceCb.Dispose();
				sourceCr.Dispose();

				lineBuff.Dispose();
				lineBuff = default;
			}

			base.Dispose(disposing);
		}

		public override string ToString() => nameof(PlanarConversionTransform);
	}
}
