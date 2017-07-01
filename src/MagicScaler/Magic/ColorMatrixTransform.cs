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
		private Vector4 vec0, vec1, vec2;
		private int channels;

		public Guid Format => source.Format;

		public int Width => source.Width;

		public int Height => source.Height;

		public ColorMatrixTransform(Matrix4x4 matrix)
		{
			vec0 = new Vector4(matrix.M11, matrix.M21, matrix.M31, matrix.M41);
			vec1 = new Vector4(matrix.M12, matrix.M22, matrix.M32, matrix.M42);
			vec2 = new Vector4(matrix.M13, matrix.M23, matrix.M33, matrix.M43);
		}

		unsafe public void CopyPixels(Rectangle sourceArea, long cbStride, long cbBufferSize, IntPtr pbBuffer)
		{
			source.CopyPixels(sourceArea, cbStride, cbBufferSize, pbBuffer);

			Vector4 vb = vec0, vg = vec1, vr = vec2, vdiv = new Vector4(1f / byte.MaxValue);
			Vector4 vmin = Vector4.Zero, vmax = new Vector4(byte.MaxValue), vrnd = new Vector4(0.5f);
			float fmax = vmax.X;
			int chan = channels;

			for (int y = 0; y < sourceArea.Height; y++)
			{
				byte* ip = (byte*)pbBuffer + y * cbStride, ipe = ip + sourceArea.Width * chan;
				while (ip < ipe)
				{
					var v0 = new Vector4(ip[0], ip[1], ip[2], fmax) * vdiv;
					float f0 = Vector4.Dot(v0, vb);
					float f1 = Vector4.Dot(v0, vg);
					float f2 = Vector4.Dot(v0, vr);

					v0 = Vector4.Clamp(new Vector4(f0, f1, f2, 0f) * vmax + vrnd, vmin, vmax);
					ip[0] = (byte)v0.X;
					ip[1] = (byte)v0.Y;
					ip[2] = (byte)v0.Z;

					ip += chan;
				}
			}
		}

		public void Init(IPixelSource source)
		{
			if (source.Format != Consts.GUID_WICPixelFormat24bppBGR && source.Format != Consts.GUID_WICPixelFormat32bppBGRA)
				throw new NotSupportedException("Pixel format must be BGR or BGRA");

			this.source = source;
			channels = PixelFormat.Cache[source.Format].ChannelCount;
		}
	}
}
