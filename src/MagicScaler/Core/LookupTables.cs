using System;

using static System.Math;
using static PhotoSauce.MagicScaler.MathUtil;

namespace PhotoSauce.MagicScaler
{
	internal static class LookupTables
	{
		public const int GammaScaleUQ15 = UQ15One;
		public const int GammaScaleFloat = 1023;
		public const int InverseGammaScale = byte.MaxValue;

		// These are padded out to allow for overrun when using i32gather or when interpolating
		public const int GammaLengthUQ15 = GammaScaleUQ15 + 4;
		public const int GammaLengthFloat = GammaScaleFloat + 2;
		public const int InverseGammaLength = InverseGammaScale + 2;

		private static readonly Lazy<Tuple<float[], ushort[]>> alphaTable = new Lazy<Tuple<float[], ushort[]>>(() => {
			var atf = new float[InverseGammaLength];
			var atq = new ushort[InverseGammaLength];

			for (int i = 0; i < atf.Length; i++)
			{
				double d = (double)i / byte.MaxValue;

				atf[i] = (float)d;
				atq[i] = FixToUQ15One(d);
			}

			Fixup(atf, InverseGammaScale);
			Fixup(atq, InverseGammaScale);

			return Tuple.Create(atf, atq);
		});

		//http://www.w3.org/Graphics/Color/srgb
		private static readonly Lazy<Tuple<float[], byte[]>> gammaTable = new Lazy<Tuple<float[], byte[]>>(() => {
			var gt = new float[GammaLengthFloat];

			for (int i = 0; i < gt.Length; i++)
			{
				double d = (double)i / GammaScaleFloat;
				if (d <= (0.04045 / 12.92))
					d *= 12.92;
				else
					d = 1.055 * Pow(d, 1.0 / 2.4) - 0.055;

				gt[i] = (float)d;
			}

			Fixup(gt, GammaScaleFloat);

			return Tuple.Create(gt, MakeUQ15Gamma(gt));
		});

		private static readonly Lazy<Tuple<float[], ushort[]>> inverseGammaTable = new Lazy<Tuple<float[], ushort[]>>(() => {
			var igtf = new float[InverseGammaLength];
			var igtq = new ushort[InverseGammaLength];

			for (int i = 0; i < igtf.Length; i++)
			{
				double d = (double)i / InverseGammaScale;
				if (d <= 0.04045)
					d /= 12.92;
				else
					d = Pow((d + 0.055) / 1.055, 2.4);

				igtf[i] = (float)d;
				igtq[i] = FixToUQ15One(d);
			}

			Fixup(igtf, InverseGammaScale);
			Fixup(igtq, InverseGammaScale);

			return Tuple.Create(igtf, igtq);
		});

		public static float[] AlphaFloat => alphaTable.Value.Item1;
		public static ushort[] AlphaUQ15 => alphaTable.Value.Item2;
		public static float[] SrgbGammaFloat => gammaTable.Value.Item1;
		public static byte[] SrgbGammaUQ15 => gammaTable.Value.Item2;
		public static float[] SrgbInverseGammaFloat => inverseGammaTable.Value.Item1;
		public static ushort[] SrgbInverseGammaUQ15 => inverseGammaTable.Value.Item2;

		public static void Fixup<T>(T[] t, int maxValid)
		{
			for (int i = maxValid + 1; i < t.Length; i++)
				t[i] = t[maxValid];
		}

		public static byte[] MakeUQ15Gamma(float[] gt)
		{
			var gtq = new byte[GammaLengthUQ15];

			for (int i = 0; i < gtq.Length; i++)
			{
				double val = (double)i / GammaScaleUQ15;
				double pos = val * GammaScaleFloat;

				int idx = (int)pos;
				val = Lerp(gt[idx], gt[idx + 1], pos - idx);

				gtq[i] = FixToByte(val);
			}

			Fixup(gtq, GammaScaleUQ15);

			return gtq;
		}

		public static ushort[] MakeUQ15InverseGamma(float[] igt)
		{
			var igtq = new ushort[InverseGammaLength];

			for (int i = 0; i < igtq.Length; i++)
				igtq[i] = FixToUQ15One(igt[i]);

			Fixup(igtq, InverseGammaScale);

			return igtq;
		}
	}
}