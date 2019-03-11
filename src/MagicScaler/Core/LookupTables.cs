using System;

using static System.Math;
using static PhotoSauce.MagicScaler.MathUtil;

namespace PhotoSauce.MagicScaler
{
	internal static class LookupTables
	{
		private static readonly Lazy<Tuple<float[], ushort[]>> alphaTable = new Lazy<Tuple<float[], ushort[]>>(() => {
			const double ascale = 1d / byte.MaxValue;
			var atf = new float[256];
			var atq = new ushort[256];

			for (int i = 0; i < atf.Length; i++)
			{
				double d = i * ascale;
				atf[i] = (float)d;
				atq[i] = FixToUQ15One(d);
			}

			return Tuple.Create(atf, atq);
		});

		//http://www.w3.org/Graphics/Color/srgb
		private static readonly Lazy<byte[]> gammaTable = new Lazy<byte[]>(() => {
			var gt = new byte[UQ15One + 1];

			for (int i = 0; i < gt.Length; i++)
			{
				double d = UnFix15ToDouble(i);
				if (d <= (0.04045 / 12.92))
					d *= 12.92;
				else
					d = 1.055 * Pow(d, 1.0 / 2.4) - 0.055;

				gt[i] = FixToByte(d);
			}

			return gt;
		});

		private static readonly Lazy<Tuple<float[], ushort[]>> inverseGammaTable = new Lazy<Tuple<float[], ushort[]>>(() => {
			var igtf = new float[256];
			var igtq = new ushort[256];

			for (int i = 0; i < igtf.Length; i++)
			{
				double d = (double)i / byte.MaxValue;
				if (d <= 0.04045)
					d /= 12.92;
				else
					d = Pow(((d + 0.055) / 1.055), 2.4);

				igtf[i] = (float)d;
				igtq[i] = FixToUQ15One(d);
			}

			return Tuple.Create(igtf, igtq);
		});

		public static float[] AlphaFloat => alphaTable.Value.Item1;
		public static ushort[] AlphaUQ15 => alphaTable.Value.Item2;
		public static byte[] SrgbGamma => gammaTable.Value;
		public static float[] SrgbInverseGammaFloat => inverseGammaTable.Value.Item1;
		public static ushort[] SrgbInverseGammaUQ15 => inverseGammaTable.Value.Item2;
	}
}