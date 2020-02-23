using System;
using System.Numerics;

#if HWINTRINSICS
using System.Runtime.Intrinsics;
using System.Runtime.Intrinsics.X86;
using System.Runtime.CompilerServices;
#endif

using PhotoSauce.Interop.Wic;

using static PhotoSauce.MagicScaler.MathUtil;

namespace PhotoSauce.MagicScaler.Transforms
{
	internal class PlanarConversionTransform : PixelSource, IDisposable
	{
		private const int ichromaOffset = 128;
		private const float videoChromaScale = 224f;
		private const float fchromaOffset = (float)ichromaOffset / byte.MaxValue;

		private readonly PixelSource sourceCb, sourceCr;
		private readonly Vector4 vec0, vec1, vec2;

		private ArraySegment<byte> lineBuff;

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
			{
				matrix.M22 *= byte.MaxValue / videoChromaScale;
				matrix.M23 *= byte.MaxValue / videoChromaScale;
				matrix.M31 *= byte.MaxValue / videoChromaScale;
				matrix.M32 *= byte.MaxValue / videoChromaScale;
			}

			vec0 = new Vector4(matrix.M13, matrix.M23, matrix.M33, 0f);
			vec1 = new Vector4(matrix.M12, matrix.M22, matrix.M32, 0f);
			vec2 = new Vector4(matrix.M11, matrix.M21, matrix.M31, 0f);

			Format = srcY.Format.FormatGuid == Consts.GUID_WICPixelFormat8bppY ? PixelFormat.FromGuid(Consts.GUID_WICPixelFormat24bppBGR) : PixelFormat.Bgrx128BppFloat;
			if (HWIntrinsics.IsAvxSupported)
				BufferStride = PowerOfTwoCeiling(BufferStride, HWIntrinsics.VectorCount<byte>());

			lineBuff = BufferPool.Rent(BufferStride * 3, true);
		}

		unsafe protected override void CopyPixelsInternal(in PixelArea prc, int cbStride, int cbBufferSize, IntPtr pbBuffer)
		{
			if (lineBuff.Array is null) throw new ObjectDisposedException(nameof(PlanarConversionTransform));

			fixed (byte* bstart = &lineBuff.Array[lineBuff.Offset])
			{
				uint cb = (uint)DivCeiling(prc.Width * Source.Format.BitsPerPixel, 8);
				uint bstride = (uint)BufferStride;

				for (int y = 0; y < prc.Height; y++)
				{
					var lrc = new PixelArea(prc.X, prc.Y + y, prc.Width, 1);

					Profiler.PauseTiming();
					Source.CopyPixels(lrc, (int)bstride, (int)bstride, (IntPtr)bstart);
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

		unsafe private void copyPixelsByte(byte* bstart, byte* opstart, uint bstride, uint cb)
		{
			byte* op = opstart;
			byte* ip0 = bstart, ip1 = bstart + bstride, ip2 = bstart + bstride * 2, ipe = ip0 + cb;

			int c0 = Fix15(vec0.Y);
			int c1 = Fix15(vec1.Y);
			int c2 = Fix15(vec1.Z);
			int c3 = Fix15(vec2.Z);

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

		unsafe private void copyPixelsFloat(byte* bstart, byte* opstart, uint bstride, uint cb)
		{
			float* op = (float*)opstart;
			float* ip0 = (float*)bstart, ip1 = (float*)(bstart + bstride), ip2 = (float*)(bstart + bstride * 2), ipe = (float*)(bstart + cb);

			float c0 = vec0.Y;
			float c1 = vec1.Y;
			float c2 = vec1.Z;
			float c3 = vec2.Z;

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
		unsafe private void copyPixelsIntrinsic(byte* bstart, byte* opstart, uint bstride, uint cb)
		{
			uint stride = bstride / sizeof(float);
			float* op = (float*)opstart;
			float* ip = (float*)bstart, ipe = (float*)(bstart + cb);

			if (Avx.IsSupported)
			{
				var vc0 = Vector256.Create(vec0.Y);
				var vc1 = Vector256.Create(vec1.Y);
				var vc2 = Vector256.Create(vec1.Z);
				var vc3 = Vector256.Create(vec2.Z);
				var voff = Vector256.Create(-fchromaOffset);

				ipe -= Vector256<float>.Count;
				while (ip <= ipe)
				{
					var viy = Avx.LoadVector256(ip);
					var vib = Avx.Add(voff, Avx.LoadVector256(ip + stride));
					var vir = Avx.Add(voff, Avx.LoadVector256(ip + stride * 2));
					ip += Vector256<float>.Count;

					var vt0 = HWIntrinsics.MultiplyAdd(viy, vib, vc0);
					var vt1 = HWIntrinsics.MultiplyAdd(HWIntrinsics.MultiplyAdd(viy, vib, vc1), vir, vc2);
					var vt2 = HWIntrinsics.MultiplyAdd(viy, vir, vc3);
					var vt3 = Vector256<float>.Zero;

					var vte = Avx.UnpackLow(vt0, vt2);
					var vto = Avx.UnpackLow(vt1, vt3);

					var vtll = Avx.UnpackLow(vte, vto);
					var vtlh = Avx.UnpackHigh(vte, vto);

					vte = Avx.UnpackHigh(vt0, vt2);
					vto = Avx.UnpackHigh(vt1, vt3);

					var vthl = Avx.UnpackLow(vte, vto);
					var vthh = Avx.UnpackHigh(vte, vto);

					Avx.Store(op, Avx.Permute2x128(vtll, vtlh, HWIntrinsics.PermuteMaskLowLow2x128));
					Avx.Store(op + Vector256<float>.Count, Avx.Permute2x128(vthl, vthh, HWIntrinsics.PermuteMaskLowLow2x128));
					Avx.Store(op + Vector256<float>.Count * 2, Avx.Permute2x128(vtll, vtlh, HWIntrinsics.PermuteMaskHighHigh2x128));
					Avx.Store(op + Vector256<float>.Count * 3, Avx.Permute2x128(vthl, vthh, HWIntrinsics.PermuteMaskHighHigh2x128));

					op += Vector256<float>.Count * 4;
				}
				ipe += Vector256<float>.Count;
			}
			else
			{
				var vc0 = Vector128.Create(vec0.Y);
				var vc1 = Vector128.Create(vec1.Y);
				var vc2 = Vector128.Create(vec1.Z);
				var vc3 = Vector128.Create(vec2.Z);
				var voff = Vector128.Create(-fchromaOffset);

				ipe -= Vector128<float>.Count;
				while (ip <= ipe)
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
				ipe += Vector128<float>.Count;
			}

			if (ip < ipe)
				copyPixelsFloat((byte*)ip, (byte*)op, bstride, (uint)(ipe - ip) * sizeof(float));
		}
#endif

		public void Dispose()
		{
			BufferPool.Return(lineBuff);
			lineBuff = default;
		}
	}
}
