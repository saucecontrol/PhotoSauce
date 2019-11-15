using System.Numerics;

namespace PhotoSauce.MagicScaler
{
	/// <summary>Contains standard 4x4 matrices for use with the <see cref="ColorMatrixTransform" /> filter.</summary>
	public static class ColorMatrix
	{
		/// <summary>Converts a color image to greyscale using the <a href="https://en.wikipedia.org/wiki/Rec._601">Rec. 601</a> luma coefficients.</summary>
		public static readonly Matrix4x4 Grey = new Matrix4x4(
			(float)Rec601.R, (float)Rec601.R, (float)Rec601.R, 0,
			(float)Rec601.G, (float)Rec601.G, (float)Rec601.G, 0,
			(float)Rec601.B, (float)Rec601.B, (float)Rec601.B, 0,
			0,               0,               0,               1
		);

		/// <summary>Applies <a href="https://en.wikipedia.org/wiki/Photographic_print_toning#Sepia_toning">sepia toning</a> to an image.</summary>
		public static readonly Matrix4x4 Sepia = new Matrix4x4(
			0.393f, 0.349f, 0.272f, 0,
			0.769f, 0.686f, 0.534f, 0,
			0.189f, 0.168f, 0.131f, 0,
			0,      0,      0,      1
		);

		/// <summary>An example of a stylized matrix, with a teal tint, increased contrast, and overblown highlights.</summary>
		public static readonly Matrix4x4 Polaroid = new Matrix4x4(
			 1.438f, -0.062f, -0.062f, 0,
			-0.122f,  1.378f, -0.122f, 0,
			-0.016f, -0.016f,  1.483f, 0,
			-0.030f,  0.050f, -0.020f, 1
		);

		/// <summary>Inverts the channel values of an image, producing a color or greyscale negative.</summary>
		public static readonly Matrix4x4 Negative = new Matrix4x4(
			-1,  0,  0, 0,
			 0, -1,  0, 0,
			 0,  0, -1, 0,
			 1,  1,  1, 1
		);
	}

	/// <summary>Contains standard matrices for converting between Y'CbCr and R'G'B' formats.</summary>
	public static class YccRgbMatrix
	{
		/// <summary>Coefficients for converting <a href="https://en.wikipedia.org/wiki/Rec._601">Rec. 601</a> Y'CbCr to R'G'B'.</summary>
		public static readonly Matrix4x4 Rec601YccToRgb = createRgbToYcc(Rec601.R, Rec601.B).InvertPrecise();

		/// <summary>Coefficients for converting <a href="https://en.wikipedia.org/wiki/Rec._709">Rec. 709</a> Y'CbCr to R'G'B'.</summary>
		public static readonly Matrix4x4 Rec709YccToRgb = createRgbToYcc(Rec709.R, Rec709.B).InvertPrecise();

		/// <summary>Coefficients for converting <a href="https://en.wikipedia.org/wiki/Rec._2020">Rec. 2020</a> Y'CbCr to R'G'B'.</summary>
		public static readonly Matrix4x4 Rec2020YccToRgb = createRgbToYcc(0.2627, 0.0593).InvertPrecise();

		/// <summary>Coefficients for converting <a href="https://en.wikipedia.org/wiki/Luma_(video)">SMPTE 240M</a> (NTSC) Y'CbCr to R'G'B'.</summary>
		public static readonly Matrix4x4 Smpte240mYccToRgb = createRgbToYcc(0.212, 0.087).InvertPrecise();

		private static Matrix4x4 createRgbToYcc(double kr, double kb)
		{
			double kg = 1 - kr - kb;
			double kbs = 1 / ((1 - kb) * 2);
			double krs = 1 / ((1 - kr) * 2);

			return new Matrix4x4() {
				M11 = (float)kr,
				M21 = (float)kg,
				M31 = (float)kb,
				M12 = (float)(-kr * kbs),
				M22 = (float)(-kg * kbs),
				M32 = 0.5f,
				M13 = 0.5f,
				M23 = (float)(-kg * krs),
				M33 = (float)(-kb * krs),
				M44 = 1
			};
		}
	}
}
