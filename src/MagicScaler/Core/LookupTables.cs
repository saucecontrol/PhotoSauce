using System;

using static System.Math;
using static PhotoSauce.MagicScaler.MathUtil;

namespace PhotoSauce.MagicScaler
{
	internal static class LookupTables
	{
		private static readonly Lazy<ushort[]> alphaTable = new Lazy<ushort[]>(() => {
			const double ascale = 1d / 255d;
			var at = new ushort[256];
			for (int i = 0; i < 256; i++)
				at[i] = ScaleToUQ15(i * ascale);

			return at;
		});

		//http://www.w3.org/Graphics/Color/srgb
		private static readonly Lazy<byte[]> gammaTable = new Lazy<byte[]>(() => {
			var gt = new byte[MaxUQ15 + 1];
			for (int i = 0; i < gt.Length; i++)
			{
				double d = UnscaleToDouble(i);
				if (d <= 0.0031308)
					d *= 12.92;
				else
					d = 1.055 * Pow(d, 1.0 / 2.4) - 0.055;

				gt[i] = ClampToByte((int)(d * 255.0 + 0.5));
			}

			return gt;
		});

		private static readonly Lazy<ushort[]> inverseGammaTable = new Lazy<ushort[]>(() => {
			var igt = new ushort[256];
			for (int i = 0; i < igt.Length; i++)
			{
				double d = i / 255d;
				if (d <= 0.04045)
					d /= 12.92;
				else
					d = Pow(((d + 0.055) / 1.055), 2.4);

				igt[i] = ScaleToUQ15(d);
			}

			return igt;
		});

		public static ushort[] Alpha => alphaTable.Value;
		public static byte[] Gamma => gammaTable.Value;
		public static ushort[] InverseGamma => inverseGammaTable.Value;
	}
}