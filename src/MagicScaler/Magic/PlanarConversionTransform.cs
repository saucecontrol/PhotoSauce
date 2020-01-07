using System;
using System.Buffers;
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

		private byte[] lineBuff;

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

			lineBuff = ArrayPool<byte>.Shared.Rent(BufferStride * 3);
		}

		unsafe protected override void CopyPixelsInternal(in PixelArea prc, int cbStride, int cbBufferSize, IntPtr pbBuffer)
		{
			fixed (byte* bstart = &lineBuff[0])
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
						if (Avx.IsSupported && cb > Vector256<byte>.Count * 4)
							copyPixelsAvx(bstart, op, bstride, cb);
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
		unsafe private void copyPixelsAvx(byte* bstart, byte* opstart, uint bstride, uint cb)
		{
			uint stride = bstride / sizeof(float);
			float* op = (float*)opstart;
			float* ip = (float*)bstart, ipe = (float*)(bstart + cb);

			var vt0 = vec0;
			var vt1 = vec1;
			var vt2 = vec2;

			var vm0 = Avx.BroadcastVector128ToVector256((float*)&vt0);
			var vm1 = Avx.BroadcastVector128ToVector256((float*)&vt1);
			var vm2 = Avx.BroadcastVector128ToVector256((float*)&vt2);
			var voff = Vector256.Create(fchromaOffset);

			ipe -= Vector256<float>.Count;
			while (ip <= ipe)
			{
				var viy = Avx.LoadVector256(ip);
				var vib = Avx.LoadVector256(ip + stride);
				var vir = Avx.LoadVector256(ip + stride * 2);
				var viz = Vector256<float>.Zero;
				ip += Vector256<float>.Count;

				vib = Avx.Subtract(vib, voff);
				vir = Avx.Subtract(vir, voff);

				var vyr0 = Avx.UnpackLow(viy, vir);
				var vyr1 = Avx.UnpackHigh(viy, vir);
				var vbz0 = Avx.UnpackLow(vib, viz);
				var vbz1 = Avx.UnpackHigh(vib, viz);

				var vi0 = Avx.UnpackLow(vyr0, vbz0);
				var vi1 = Avx.UnpackHigh(vyr0, vbz0);
				var vi2 = Avx.UnpackLow(vyr1, vbz1);
				var vi3 = Avx.UnpackHigh(vyr1, vbz1);

				var vo0 = Avx.Permute2x128(vi0, vi1, 0b_0010_0000);
				var vo2 = Avx.Permute2x128(vi0, vi1, 0b_0011_0001);

				var vr0 = Avx.DotProduct(vo0, vm0, 0b_0111_0001);
				var vr1 = Avx.DotProduct(vo0, vm1, 0b_0111_0010);
				var vr2 = Avx.DotProduct(vo0, vm2, 0b_0111_0100);
				vo0 = Avx.Blend(vr0, vr1, 0b_0010_0010);
				vo0 = Avx.Blend(vo0, vr2, 0b_0100_0100);
				Avx.Store(op, vo0);

				vr0 = Avx.DotProduct(vo2, vm0, 0b_0111_0001);
				vr1 = Avx.DotProduct(vo2, vm1, 0b_0111_0010);
				vr2 = Avx.DotProduct(vo2, vm2, 0b_0111_0100);
				vo2 = Avx.Blend(vr0, vr1, 0b_0010_0010);
				vo2 = Avx.Blend(vo2, vr2, 0b_0100_0100);
				Avx.Store(op + Vector256<float>.Count * 2, vo2);

				var vo1 = Avx.Permute2x128(vi2, vi3, 0b_0010_0000);
				var vo3 = Avx.Permute2x128(vi2, vi3, 0b_0011_0001);

				vr0 = Avx.DotProduct(vo1, vm0, 0b_0111_0001);
				vr1 = Avx.DotProduct(vo1, vm1, 0b_0111_0010);
				vr2 = Avx.DotProduct(vo1, vm2, 0b_0111_0100);
				vo1 = Avx.Blend(vr0, vr1, 0b_0010_0010);
				vo1 = Avx.Blend(vo1, vr2, 0b_0100_0100);
				Avx.Store(op + Vector256<float>.Count, vo1);

				vr0 = Avx.DotProduct(vo3, vm0, 0b_0111_0001);
				vr1 = Avx.DotProduct(vo3, vm1, 0b_0111_0010);
				vr2 = Avx.DotProduct(vo3, vm2, 0b_0111_0100);
				vo3 = Avx.Blend(vr0, vr1, 0b_0010_0010);
				vo3 = Avx.Blend(vo3, vr2, 0b_0100_0100);
				Avx.Store(op + Vector256<float>.Count * 3, vo3);

				op += Vector256<float>.Count * 4;
			}
			ipe += Vector256<float>.Count;

			if (ip < ipe)
				copyPixelsFloat((byte*)ip, (byte*)op, bstride, (uint)(ipe - ip) * sizeof(float));
		}
#endif

		public void Dispose()
		{
			if (lineBuff is null)
				return;

			ArrayPool<byte>.Shared.Return(lineBuff);
			lineBuff = null!;
		}
	}
}
