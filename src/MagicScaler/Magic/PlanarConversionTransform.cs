using System;
using System.Buffers;
using System.Numerics;

using PhotoSauce.Interop.Wic;

using static PhotoSauce.MagicScaler.MathUtil;

namespace PhotoSauce.MagicScaler
{
	internal class PlanarConversionTransform : PixelSource, IDisposable
	{
		private readonly PixelSource sourceCb, sourceCr;
		private readonly Vector4 vec0, vec1, vec2;
		private readonly int[] matrixFixed;

		private byte[] lineBuff;

		public PlanarConversionTransform(PixelSource srcY, PixelSource srcCb, PixelSource srcCr, Matrix4x4 matrix) : base(srcY)
		{
			if (srcCb.Width != srcY.Width || srcCb.Height != srcY.Height) throw new ArgumentException("Chroma plane incorrect size", nameof(srcCb));
			if (srcCr.Width != srcY.Width || srcCr.Height != srcY.Height) throw new ArgumentException("Chroma plane incorrect size", nameof(srcCr));
			if (srcCb.Format.BitsPerPixel != srcY.Format.BitsPerPixel) throw new ArgumentException("Chroma plane incorrect format", nameof(srcCb));
			if (srcCr.Format.BitsPerPixel != srcY.Format.BitsPerPixel) throw new ArgumentException("Chroma plane incorrect format", nameof(srcCr));

			Format = srcY.Format.FormatGuid == Consts.GUID_WICPixelFormat8bppY ? PixelFormat.FromGuid(Consts.GUID_WICPixelFormat24bppBGR) : PixelFormat.Bgrx128BppFloat;

			sourceCb = srcCb;
			sourceCr = srcCr;
			matrix = matrix.InvertPrecise();

			lineBuff = ArrayPool<byte>.Shared.Rent(BufferStride * 3);
			matrixFixed = ArrayPool<int>.Shared.Rent(9);

			vec0 = new Vector4(matrix.M13, matrix.M23, matrix.M33, 0f);
			vec1 = new Vector4(matrix.M12, matrix.M22, matrix.M32, 0f);
			vec2 = new Vector4(matrix.M11, matrix.M21, matrix.M31, 0f);

			stackalloc[] {
				Fix15(matrix.M13), Fix15(matrix.M23), Fix15(matrix.M33),
				Fix15(matrix.M12), Fix15(matrix.M22), Fix15(matrix.M32),
				Fix15(matrix.M11), Fix15(matrix.M21), Fix15(matrix.M31)
			}.CopyTo(matrixFixed);
		}

		unsafe protected override void CopyPixelsInternal(in PixelArea prc, int cbStride, int cbBufferSize, IntPtr pbBuffer)
		{
			fixed (byte* bstart = &lineBuff[0])
			{
				int cb = DivCeiling(prc.Width * Source.Format.BitsPerPixel, 8);
				int bstride = BufferStride;

				for (int y = 0; y < prc.Height; y++)
				{
					var lrc = new PixelArea(prc.X, prc.Y + y, prc.Width, 1);

					Profiler.PauseTiming();
					Source.CopyPixels(lrc, bstride, bstride, (IntPtr)bstart);
					sourceCb.CopyPixels(lrc, bstride, bstride, (IntPtr)(bstart + bstride));
					sourceCr.CopyPixels(lrc, bstride, bstride, (IntPtr)(bstart + bstride * 2));
					Profiler.ResumeTiming();

					byte* op = (byte*)pbBuffer + y * cbStride;
					if (Format.NumericRepresentation == PixelNumericRepresentation.Float)
						copyPixelsFloat(bstart, op, bstride, cb);
					else
						copyPixelsByte(bstart, op, bstride, cb);
				}
			}
		}

		unsafe private void copyPixelsByte(byte* bstart, byte* opstart, int bstride, int cb)
		{
			fixed (int* pm = &matrixFixed[0])
			{
				byte* op = opstart;
				byte* ip0 = bstart, ip1 = bstart + bstride, ip2 = bstart + bstride * 2, ipe = ip0 + cb;
				while (ip0 < ipe)
				{
					int i0 = *ip0;
					int i1 = *ip1 - 128;
					int i2 = *ip2 - 128;

					byte o0 = UnFix15ToByte(i0 * pm[0] + i1 * pm[1] + i2 * pm[2]);
					byte o1 = UnFix15ToByte(i0 * pm[3] + i1 * pm[4] + i2 * pm[5]);
					byte o2 = UnFix15ToByte(i0 * pm[6] + i1 * pm[7] + i2 * pm[8]);

					op[0] = o0;
					op[1] = o1;
					op[2] = o2;

					ip0++;
					ip1++;
					ip2++;
					op += 3;
				}
			}
		}

		unsafe private void copyPixelsFloat(byte* bstart, byte* opstart, int bstride, int cb)
		{
			Vector4 vm0 = vec0, vm1 = vec1, vm2 = vec2;
			var voff = new Vector4(0f, 128/255f, 128/255f, 0f);
			float fzero = voff.X;

			float* op = (float*)opstart;
			float* ip0 = (float*)bstart, ip1 = (float*)(bstart + bstride), ip2 = (float*)(bstart + bstride * 2), ipe = (float*)(bstart + cb);
			while (ip0 < ipe)
			{
				var vi = Vector4.Zero;
				vi.X = *ip0;
				vi.Y = *ip1;
				vi.Z = *ip2;
				vi -= voff;

				var vo = vi;
				op[0] = Vector4.Dot(vi, vm0);
				op[1] = Vector4.Dot(vi, vm1);
				op[2] = Vector4.Dot(vi, vm2);
				op[3] = fzero;

				ip0++;
				ip1++;
				ip2++;
				op += 4;
			}
		}

		public void Dispose()
		{
			ArrayPool<byte>.Shared.Return(lineBuff ?? Array.Empty<byte>());
			lineBuff = null!;
		}
	}
}
