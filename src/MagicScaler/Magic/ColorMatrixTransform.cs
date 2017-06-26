using System;
using System.Drawing;
using System.Numerics;

using PhotoSauce.MagicScaler.Interop;

namespace PhotoSauce.MagicScaler
{
	public static class ColorMatrix
	{
		public static Matrix4x4 Grey     = new Matrix4x4(0.114f, 0.114f, 0.114f, 0f,
		                                                 0.587f, 0.587f, 0.587f, 0f,
		                                                 0.299f, 0.299f, 0.299f, 0f,
		                                                     0f,     0f,     0f, 0f);


		public static Matrix4x4 Sepia    = new Matrix4x4(0.131f, 0.168f, 0.189f, 0f,
		                                                 0.534f, 0.686f, 0.769f, 0f,
		                                                 0.272f, 0.349f, 0.393f, 0f,
		                                                     0f,     0f,     0f, 0f);

		public static Matrix4x4 Polaroid = new Matrix4x4( 1.483f, -0.016f, -0.016f, 0f,
		                                                 -0.122f,  1.378f, -0.122f, 0f,
		                                                 -0.062f, -0.062f,  1.438f, 0f,
		                                                 -0.020f,  0.050f, -0.030f, 0f);

		public static Matrix4x4 Negative = new Matrix4x4(-1f,  0f,  0f, 0f,
		                                                  0f, -1f,  0f, 0f,
		                                                  0f,  0f, -1f, 0f,
		                                                  1f,  1f,  1f, 0f);
	}

	public sealed class ColorMatrixTransform : IPixelTransform
	{
		private IPixelSource source;
		private Vector4 vec0, vec1, vec2, vec3;

		public Guid Format => source.Format;

		public int Width => source.Width;

		public int Height => source.Height;

		public ColorMatrixTransform(Matrix4x4 matrix)
		{
			matrix = Matrix4x4.Transpose(matrix);
			vec0 = new Vector4(matrix.M11, matrix.M12, matrix.M13, matrix.M14);
			vec1 = new Vector4(matrix.M21, matrix.M22, matrix.M23, matrix.M24);
			vec2 = new Vector4(matrix.M31, matrix.M32, matrix.M33, matrix.M34);
			vec3 = new Vector4(matrix.M31, matrix.M32, matrix.M33, matrix.M34);
		}

		unsafe public void CopyPixels(Rectangle sourceArea, long cbStride, long cbBufferSize, IntPtr pbBuffer)
		{
			source.CopyPixels(sourceArea, cbStride, cbBufferSize, pbBuffer);

			for (int y = 0; y < sourceArea.Height; y++)
			{
				byte* ip = (byte*)pbBuffer + y * cbStride, ipe = ip + sourceArea.Width * 3;

				Vector4 vb = vec0, vg = vec1, vr = vec2, vdiv = new Vector4(1f / byte.MaxValue);
				float fmin = Vector3.Zero.X, fmax = new Vector3(byte.MaxValue).X, frnd = new Vector3(0.5f).X;

				while (ip < ipe)
				{
					var v0 = new Vector4(ip[0], ip[1], ip[2], fmax) * vdiv;
					float f0 = Vector4.Dot(v0, vb);
					float f1 = Vector4.Dot(v0, vg);
					float f2 = Vector4.Dot(v0, vr);

					ip[0] = (byte)(f0 * fmax + frnd).Clamp(fmin, fmax);
					ip[1] = (byte)(f1 * fmax + frnd).Clamp(fmin, fmax);
					ip[2] = (byte)(f2 * fmax + frnd).Clamp(fmin, fmax);

					ip += 3;
				}
			}
		}

		public void Init(IPixelSource source)
		{
			if (source.Format != Consts.GUID_WICPixelFormat24bppBGR)
				throw new NotSupportedException("Pixel format must be BGR");

			this.source = source;
		}
	}
}
