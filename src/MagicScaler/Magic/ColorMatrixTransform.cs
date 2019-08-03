using System;
using System.Numerics;
using System.Runtime.CompilerServices;

using PhotoSauce.MagicScaler.Interop;
using static PhotoSauce.MagicScaler.MathUtil;

namespace PhotoSauce.MagicScaler
{
	/// <summary>Contains standard 4x4 matrices for use with the <see cref="ColorMatrixTransform" /> filter.</summary>
	public static class ColorMatrix
	{
		/// <summary>Converts a color image to greyscale using the <a href="https://en.wikipedia.org/wiki/Rec._601">Rec. 601</a> luma coefficients.</summary>
		public static readonly Matrix4x4 Grey = new Matrix4x4(
			Rec601.B, Rec601.B, Rec601.B, 0f,
			Rec601.G, Rec601.G, Rec601.G, 0f,
			Rec601.R, Rec601.R, Rec601.R, 0f,
			0f,       0f,       0f,       1f
		);

		/// <summary>Applies <a href="https://en.wikipedia.org/wiki/Photographic_print_toning#Sepia_toning">sepia toning</a> to an image.</summary>
		public static readonly Matrix4x4 Sepia = new Matrix4x4(
			0.131f, 0.168f, 0.189f, 0f,
			0.534f, 0.686f, 0.769f, 0f,
			0.272f, 0.349f, 0.393f, 0f,
			0f,     0f,     0f,     1f
		);

		/// <summary>An example of a stylized matrix, with a teal tint, increased contrast, and overblown highlights.</summary>
		public static readonly Matrix4x4 Polaroid = new Matrix4x4(
			 1.483f, -0.016f, -0.016f, 0f,
			-0.122f,  1.378f, -0.122f, 0f,
			-0.062f, -0.062f,  1.438f, 0f,
			-0.020f,  0.050f, -0.030f, 1f
		);

		/// <summary>Inverts the channel values of an image, producing a color or greyscale negative.</summary>
		public static readonly Matrix4x4 Negative = new Matrix4x4(
			-1f,  0f,  0f, 0f,
			 0f, -1f,  0f, 0f,
			 0f,  0f, -1f, 0f,
			 1f,  1f,  1f, 1f
		);
	}

	internal class ColorMatrixTransformInternal: PixelSource
	{
		private readonly Vector4 vec0, vec1, vec2, vec3;
		private readonly int[] matrixFixed;

		public ColorMatrixTransformInternal(PixelSource source, Matrix4x4 matrix) : base(source)
		{
			vec0 = new Vector4(matrix.M11, matrix.M21, matrix.M31, matrix.M41);
			vec1 = new Vector4(matrix.M12, matrix.M22, matrix.M32, matrix.M42);
			vec2 = new Vector4(matrix.M13, matrix.M23, matrix.M33, matrix.M43);
			vec3 = new Vector4(matrix.M14, matrix.M24, matrix.M34, matrix.M44);

			matrixFixed = new[] {
				Fix15(matrix.M11), Fix15(matrix.M21), Fix15(matrix.M31), Fix15(matrix.M41),
				Fix15(matrix.M12), Fix15(matrix.M22), Fix15(matrix.M32), Fix15(matrix.M42),
				Fix15(matrix.M13), Fix15(matrix.M23), Fix15(matrix.M33), Fix15(matrix.M43),
				Fix15(matrix.M14), Fix15(matrix.M24), Fix15(matrix.M34), Fix15(matrix.M44)
			};
		}

		unsafe protected override void CopyPixelsInternal(in PixelArea prc, uint cbStride, uint cbBufferSize, IntPtr pbBuffer)
		{
			Timer.Stop();
			Source.CopyPixels(prc, cbStride, cbBufferSize, pbBuffer);
			Timer.Start();

			if (Format.NumericRepresentation == PixelNumericRepresentation.Float)
				copyPixelsFloat(prc, cbStride, pbBuffer);
			else if (Format.NumericRepresentation == PixelNumericRepresentation.Fixed)
				copyPixelsFixed(prc, cbStride, pbBuffer);
			else
				copyPixelsByte(prc, cbStride, pbBuffer);
		}

		unsafe private void copyPixelsByte(in PixelArea prc, uint cbStride, IntPtr pbBuffer)
		{
			int chan = Format.ChannelCount;
			bool alpha = chan == 4 && matrixFixed[15] != UQ15One;

			fixed (int* pm = &matrixFixed[0])
			for (int y = 0; y < prc.Height; y++)
			{
				byte* ip = (byte*)pbBuffer + y * cbStride, ipe = ip + prc.Width * chan;
				while (ip < ipe)
				{
					int i0 = ip[0];
					int i1 = ip[1];
					int i2 = ip[2];

					byte o0 = UnFix15ToByte(i0 * pm[0] + i1 * pm[1] + i2 * pm[ 2] + byte.MaxValue * pm[ 3]);
					byte o1 = UnFix15ToByte(i0 * pm[4] + i1 * pm[5] + i2 * pm[ 6] + byte.MaxValue * pm[ 7]);
					byte o2 = UnFix15ToByte(i0 * pm[8] + i1 * pm[9] + i2 * pm[10] + byte.MaxValue * pm[11]);

					ip[0] = o0;
					ip[1] = o1;
					ip[2] = o2;

					if (alpha)
						ip[3] = UnFix15ToByte(ip[3] * pm[15]);

					ip += chan;
				}
			}
		}

		unsafe private void copyPixelsFixed(in PixelArea prc, uint cbStride, IntPtr pbBuffer)
		{
			int chan = Format.ChannelCount;
			bool alpha = chan == 4 && matrixFixed[15] != UQ15One;

			fixed (int* pm = &matrixFixed[0])
			for (int y = 0; y < prc.Height; y++)
			{
				ushort* ip = (ushort*)((byte*)pbBuffer + y * cbStride), ipe = ip + prc.Width * chan;
				while (ip < ipe)
				{
					int i0 = ip[0];
					int i1 = ip[1];
					int i2 = ip[2];

					ushort o0 = UnFixToUQ15(i0 * pm[0] + i1 * pm[1] + i2 * pm[ 2] + UQ15One * pm[ 3]);
					ushort o1 = UnFixToUQ15(i0 * pm[4] + i1 * pm[5] + i2 * pm[ 6] + UQ15One * pm[ 7]);
					ushort o2 = UnFixToUQ15(i0 * pm[8] + i1 * pm[9] + i2 * pm[10] + UQ15One * pm[11]);

					ip[0] = o0;
					ip[1] = o1;
					ip[2] = o2;

					if (alpha)
						ip[3] = UnFixToUQ15(ip[3] * pm[15]);

					ip += chan;
				}
			}
		}

		unsafe private void copyPixelsFloat(in PixelArea prc, uint cbStride, IntPtr pbBuffer)
		{
			Vector4 vb = vec0, vg = vec1, vr = vec2, va = vec3;
			float falpha = va.W, fone = Vector4.One.X;
			int chan = Format.ChannelCount;
			bool alpha = Format.AlphaRepresentation != PixelAlphaRepresentation.None;

			for (int y = 0; y < prc.Height; y++)
			{
				float* ip = (float*)((byte*)pbBuffer + y * cbStride), ipe = ip + prc.Width * chan;
				while (ip < ipe)
				{
					var v = Unsafe.Read<Vector4>(ip);
					v.W = fone;

					float f0 = Vector4.Dot(v, vb); 
					float f1 = Vector4.Dot(v, vg);
					float f2 = Vector4.Dot(v, vr);

					ip[0] = f0;
					ip[1] = f1;
					ip[2] = f2;

					if (alpha)
						ip[3] *= falpha;

					ip += 4;
				}
			}
		}
	}

	/// <summary>Transforms an image according to coefficients in a <see cref="Matrix4x4" />.</summary>
	public sealed class ColorMatrixTransform : PixelTransform, IPixelTransformInternal
	{
		private readonly Matrix4x4 matrix;

		/// <summary>Constructs a new <see cref="ColorMatrixTransform" /> using the specified <paramref name="matrix" />.</summary>
		/// <param name="matrix">A 4x4 matrix of coefficients.  The channel order is BGRA.</param>
		public ColorMatrixTransform(Matrix4x4 matrix) => this.matrix = matrix;

		void IPixelTransformInternal.Init(WicProcessingContext ctx)
		{
			MagicTransforms.AddExternalFormatConverter(ctx);

			if (matrix != default && !matrix.IsIdentity)
			{
				var fmt = matrix.M44 < 1f || ctx.Source.Format.AlphaRepresentation != PixelAlphaRepresentation.None ? Consts.GUID_WICPixelFormat32bppBGRA : Consts.GUID_WICPixelFormat24bppBGR;
				if (ctx.Source.Format.FormatGuid != fmt)
					ctx.Source = ctx.AddDispose(new FormatConversionTransformInternal(ctx.Source, null, null, fmt));

				ctx.Source = new ColorMatrixTransformInternal(ctx.Source, matrix);
			}

			Source = ctx.Source;
		}
	}
}
