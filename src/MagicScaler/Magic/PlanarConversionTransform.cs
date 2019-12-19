using System;
using System.Buffers;
using System.Numerics;

using PhotoSauce.Interop.Wic;

using static PhotoSauce.MagicScaler.MathUtil;

namespace PhotoSauce.MagicScaler
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
			if (matrix.IsNaN()) throw new ArgumentNullException("Invalid YCC matrix", nameof(matrix));

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
			byte* op = opstart;
			byte* ip0 = bstart, ip1 = bstart + bstride, ip2 = bstart + bstride * 2, ipe = ip0 + cb;

			int c0 = Fix15(vec0.Y);
			int c1 = Fix15(vec1.Y);
			int c2 = Fix15(vec1.Z);
			int c3 = Fix15(vec2.Z);

			while (ip0 < ipe)
			{
				int i0 = *ip0 * UQ15One;
				int i1 = *ip1 - ichromaOffset;
				int i2 = *ip2 - ichromaOffset;

				byte o0 = UnFix15ToByte(i0 + i1 * c0);
				byte o1 = UnFix15ToByte(i0 + i1 * c1 + i2 * c2);
				byte o2 = UnFix15ToByte(i0 + i2 * c3);

				op[0] = o0;
				op[1] = o1;
				op[2] = o2;

				ip0++;
				ip1++;
				ip2++;
				op += 3;
			}
		}

		unsafe private void copyPixelsFloat(byte* bstart, byte* opstart, int bstride, int cb)
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
				float f0 = *ip0;
				float f1 = *ip1 - foff;
				float f2 = *ip2 - foff;

				op[0] = f0 + f1 * c0;
				op[1] = f0 + f1 * c1 + f2 * c2;
				op[2] = f0 + f2 * c3;
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
