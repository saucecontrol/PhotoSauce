// Copyright © Clinton Ingram and Contributors
// SPDX-License-Identifier: MIT

using System;

using static System.Math;
using static PhotoSauce.MagicScaler.MathUtil;

namespace PhotoSauce.MagicScaler;

internal static class LookupTables
{
	public const int GammaScaleUQ15 = UQ15One;
	public const int GammaScaleFloat = 1023;
	public const int InverseGammaScale = byte.MaxValue;

	// These are padded out to allow for overrun when using i32gather or when interpolating
	public const int GammaLengthUQ15 = GammaScaleUQ15 + 4;
	public const int GammaLengthFloat = GammaScaleFloat + 2;
	public const int InverseGammaLength = InverseGammaScale + 2;

	private static readonly Lazy<float[]> alphaTable = new(() => {
		float[] tbl = new float[InverseGammaLength];

		for (int i = 0; i < tbl.Length; i++)
			tbl[i] = (float)((double)i / byte.MaxValue);

		Fixup(tbl, InverseGammaScale);

		return tbl;
	});

	//http://www.w3.org/Graphics/Color/srgb
	private static readonly Lazy<Tuple<float[], byte[]>> gammaTable = new(() => {
		float[] tbl = new float[GammaLengthFloat];

		for (int i = 0; i < tbl.Length; i++)
		{
			double d = (double)i / GammaScaleFloat;
			if (d <= 0.04045 / 12.92)
				d *= 12.92;
			else
				d = 1.055 * Pow(d, 1.0 / 2.4) - 0.055;

			tbl[i] = (float)d;
		}

		Fixup(tbl, GammaScaleFloat);

		return Tuple.Create(tbl, MakeUQ15Gamma(tbl));
	});

	private static readonly Lazy<Tuple<float[], ushort[]>> inverseGammaTable = new(() => {
		float[] igtf = new float[InverseGammaLength];
		ushort[] igtq = new ushort[InverseGammaLength];

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

	private static readonly Lazy<uint[]> octreeIndexTable = new(() => {
		uint[] tbl = new uint[256 * 3];

		for (uint i = 0; i < 256; i++)
		{
			uint b = 0, g = 0, r = 0;

			for (int p = 0; p < 8; p++)
			{
				uint m = i & (1u << p);
				int s = 23 - (p << 2);

				if (s < 0)
				{
					s = -s;
					b |= m >> s + 2;
					g |= m >> s;
					r |= m >> s + 1;
				}
				else
				{
					b |= m << s - 2;
					g |= m << s;
					r |= m << s - 1;
				}
			}

			tbl[i      ] = b;
			tbl[i + 256] = g;
			tbl[i + 512] = r;
		}

		return tbl;
	});

	public static float[] Alpha => alphaTable.Value;
	public static float[] SrgbGamma => gammaTable.Value.Item1;
	public static byte[] SrgbGammaUQ15 => gammaTable.Value.Item2;
	public static float[] SrgbInverseGamma => inverseGammaTable.Value.Item1;
	public static ushort[] SrgbInverseGammaUQ15 => inverseGammaTable.Value.Item2;
	public static uint[] OctreeIndexTable => octreeIndexTable.Value;

	public static void Fixup<T>(T[] t, int maxValid)
	{
		for (int i = maxValid + 1; i < t.Length; i++)
			t[i] = t[maxValid];
	}

	public static byte[] MakeUQ15Gamma(float[] gt)
	{
		byte[] gtq = new byte[GammaLengthUQ15];
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
		if (igt == SrgbInverseGamma)
			return SrgbInverseGammaUQ15;

		ushort[] igtq = new ushort[InverseGammaLength];
		for (int i = 0; i < igtq.Length; i++)
			igtq[i] = FixToUQ15One(igt[i]);

		Fixup(igtq, InverseGammaScale);

		return igtq;
	}

	public static float[] MakeVideoGamma(float[] gt)
	{
		const int minVal = VideoLumaMin << 2;
		const int maxVal = VideoLumaMax << 2;

		float[] gtv = new float[gt.Length];
		for (int i = 0; i < gtv.Length; i++)
			gtv[i] = (float)(((double)gt[i] * (maxVal - minVal) + minVal) / GammaScaleFloat);

		Fixup(gtv, GammaScaleFloat);

		return gtv;
	}

	public static float[] MakeVideoInverseGamma(float[] igt)
	{
		const int minVal = VideoLumaMin;
		const int maxVal = VideoLumaMax;

		float[] igtv = new float[igt.Length];
		for (int i = 0; i < igtv.Length; i++)
		{
			double val = (double)(i.Clamp(minVal, maxVal) - minVal) / (maxVal - minVal);
			double pos = val * InverseGammaScale;

			int idx = (int)pos;
			val = Lerp(igt[idx], igt[idx + 1], pos - idx);

			igtv[i] = (float)val;
		}

		Fixup(igtv, InverseGammaScale);

		return igtv;
	}
}