// Copyright © Clinton Ingram and Contributors.  Licensed under the MIT License.

using System;
using System.Numerics;
using System.Runtime.InteropServices;
using System.Runtime.CompilerServices;

#if HWINTRINSICS
using System.Runtime.Intrinsics;
using System.Runtime.Intrinsics.X86;
#endif

using static System.Math;

namespace PhotoSauce.MagicScaler
{
	[StructLayout(LayoutKind.Sequential, Pack = 1)]
	internal readonly struct triple
	{
		public readonly ushort v1;
		public readonly byte v2;

		public triple(uint v)
		{
			v1 = (ushort)v;
			v2 = (byte)(v >> 16);
		}

		public static explicit operator triple(uint v) => new(v);
	}

	internal static class MathUtil
	{
		private const uint maskb = ~0x01010101u;

		private const int ishift = 15;
		private const int iscale = 1 << ishift;
		private const int imax = (1 << ishift + 1) - 1;
		private const int iround = iscale >> 1;

		public const ushort UQ15Max = imax;
		public const ushort UQ15One = iscale;
		public const ushort UQ15Round = iround;

		public const int VideoLumaMin = 16;
		public const int VideoLumaMax = 235;
		public const int VideoLumaScale = VideoLumaMax - VideoLumaMin;

		public const int VideoChromaMin = 16;
		public const int VideoChromaMax = 240;
		public const int VideoChromaScale = VideoChromaMax - VideoChromaMin;

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static int Clamp(this int x, int min, int max) => Min(Max(min, x), max);

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static uint Clamp(this uint x, uint min, uint max) => Min(Max(min, x), max);

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static double Clamp(this double x, double min, double max) => Min(Max(min, x), max);

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static float Clamp(this float x, float min, float max) => FastMin(FastMax(min, x), max);

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static Vector4 Clamp(this Vector4 x, Vector4 min, Vector4 max) => Vector4.Min(Vector4.Max(min, x), max);

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static Vector<T> Clamp<T>(this Vector<T> x, Vector<T> min, Vector<T> max) where T : unmanaged => Vector.Min(Vector.Max(min, x), max);

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static ushort ClampToUQ15(int x) => (ushort)Min(Max(0, x), UQ15Max);

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static ushort ClampToUQ15(uint x) => (ushort)Min(x, UQ15Max);

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static ushort ClampToUQ15One(int x) => (ushort)Min(Max(0, x), UQ15One);

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static ushort ClampToUQ15One(uint x) => (ushort)Min(x, UQ15One);

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static byte ClampToByte(int x) => (byte)Min(Max(0, x), byte.MaxValue);

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static byte ClampToByte(uint x) => (byte)Min(x, byte.MaxValue);

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static uint Fix15(byte x) => ((uint)x * (((UQ15One << 8) + byte.MaxValue / 2) / byte.MaxValue)) >> 8;

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static int Fix15(float x) => Round(x * UQ15One);

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static ushort FixToUQ15One(double x) => ClampToUQ15One((int)(x * UQ15One + 0.5));

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static ushort FixToUQ15One(float x) => ClampToUQ15One((int)(x * UQ15One + 0.5f));

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static byte FixToByte(double x) => ClampToByte((int)(x * byte.MaxValue + 0.5));

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static byte FixToByte(float x) => ClampToByte((int)(x * byte.MaxValue + 0.5f));

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static int UnFix8(int x) => x + (iround >> 7) >> 8;

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static int UnFix15(int x) => x + iround >> ishift;

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static uint UnFix15(uint x) => x + iround >> ishift;

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static int UnFix22(int x) => x + (iround << 7) >> 22;

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static uint UnFix22(uint x) => x + (iround << 7) >> 22;

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static ushort UnFixToUQ15(int x) => ClampToUQ15(UnFix15(x));

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static ushort UnFixToUQ15One(uint x) => ClampToUQ15One(UnFix15(x));

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static byte UnFix15ToByte(int x) => ClampToByte(UnFix15(x));

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static byte UnFix15ToByte(uint x) => ClampToByte(UnFix15(x));

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static byte UnFix22ToByte(int x) => ClampToByte(UnFix22(x));

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static byte UnFix22ToByte(uint x) => ClampToByte(UnFix22(x));

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static int DivCeiling(int x, int y) => (x + (y - 1)) / y;

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static int PowerOfTwoFloor(int x, int powerOfTwo) => x & ~(powerOfTwo - 1);

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static int PowerOfTwoCeiling(int x, int powerOfTwo) => x + (powerOfTwo - 1) & ~(powerOfTwo - 1);

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static ulong FastAvgBytesU(ulong x, ulong y, ulong m) => (x | y) - (((x ^ y) & m) >> 1);

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static ulong FastAvgBytesD(ulong x, ulong y, ulong m) => (x & y) + (((x ^ y) & m) >> 1);

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static uint FastAvgBytesU(uint x, uint y) => (x | y) - (((x ^ y) & maskb) >> 1);

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static uint FastAvgBytesD(uint x, uint y) => (x & y) + (((x ^ y) & maskb) >> 1);

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static int FastAbs(int x) => (x ^ (x >> 31)) - (x >> 31);

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static float FastMax(float x, float o)
		{
#if HWINTRINSICS
			if (Sse.IsSupported)
				return Sse.MaxScalar(Vector128.CreateScalarUnsafe(x), Vector128.CreateScalarUnsafe(o)).ToScalar();
			else
#endif
				return x < o ? o : x;
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static float FastMin(float x, float o)
		{
#if HWINTRINSICS
			if (Sse.IsSupported)
				return Sse.MinScalar(Vector128.CreateScalarUnsafe(x), Vector128.CreateScalarUnsafe(o)).ToScalar();
			else
#endif
				return x > o ? o : x;
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static float Sqrt(this float x) =>
#if BUILTIN_MATHF
			MathF.Sqrt(x);
#else
			(float)Math.Sqrt(x);
#endif

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static int Round(this float x) =>
#if BUILTIN_MATHF
			(int)MathF.Round(x);
#else
			(int)Math.Round(x);
#endif

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static double Lerp(double l, double h, double d) => (h - l) * d + l;

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static float Lerp(float l, float h, float d) => (h - l) * d + l;

		private static (T, T) toFraction<T>(this double v) where T : unmanaged
		{
			const double epsilon = 1e-8;
			const int maxIteration = 20;

			ulong maxVal;
			if (typeof(T) == typeof(uint))
				maxVal = uint.MaxValue;
			else if (typeof(T) == typeof(int))
				maxVal = int.MaxValue;
			else
				throw new ArgumentException("Invalid type.", nameof(T));

			if (double.IsNaN(v))
				return default;

			int sign = Sign(v);
			double va = Abs(v);

			if ((typeof(T) == typeof(uint) && sign <= 0) || va < 1d / maxVal)
				return Unsafe.As<(int, int), (T, T)>(ref Unsafe.AsRef((0, 1)));

			if (va > maxVal)
				return Unsafe.As<(int, int), (T, T)>(ref Unsafe.AsRef((sign, 0)));

			double f = va;
			long np = 0, n = 1;
			long dp = 1, d = 0;

			for (int i = 0; i < maxIteration; i++)
			{
				long wr = (long)(f + 0.5);
				long nn = wr * n + np;
				long dn = wr * d + dp;

				if (((ulong)nn > maxVal || (ulong)dn > maxVal) && i > 1)
					break;

				if (Abs(((double)nn / dn - va) / va) > epsilon)
				{
					long wf = (long)f;
					if (wf != wr)
					{
						nn = wf * n + np;
						dn = wf * d + dp;
					}

					f = 1 / (f - wf);
				}
				else
				{
					i = maxIteration;
				}

				(n, np) = (nn, n);
				(d, dp) = (dn, d);
			}

			return Unsafe.As<(int, int), (T, T)>(ref Unsafe.AsRef(((int)(n * sign), (int)d)));
		}

		public static Rational ToRational(this double d) => d.toFraction<uint>();

		public static SRational ToSRational(this double d) => d.toFraction<int>();

		public static bool IsRoughlyEqualTo(this double x, double y) => Abs(x - y) < 1e-4;

		public static unsafe bool IsRouglyEqualTo(this in Matrix4x4 m1, in Matrix4x4 m2)
		{
			const float epsilon = 1e-3f;

#if HWINTRINSICS
			if (Sse.IsSupported)
			{
				var veps = Vector128.Create(epsilon);
				var vmsk = Vector128.Create(0x7fffffff).AsSingle();

				var v0 = Sse.CompareNotLessThan(Sse.And(Sse.Subtract(m1.getRowVector(0), m2.getRowVector(0)), vmsk), veps);
				var v1 = Sse.CompareNotLessThan(Sse.And(Sse.Subtract(m1.getRowVector(1), m2.getRowVector(1)), vmsk), veps);
				var v2 = Sse.CompareNotLessThan(Sse.And(Sse.Subtract(m1.getRowVector(2), m2.getRowVector(2)), vmsk), veps);
				var v3 = Sse.CompareNotLessThan(Sse.And(Sse.Subtract(m1.getRowVector(3), m2.getRowVector(3)), vmsk), veps);

				return Sse.MoveMask(Sse.Or(Sse.Or(v0, v1), Sse.Or(v2, v3))) == 0;
			}
#endif

			var md = m1 - m2;

			return
				Abs(md.M11) < epsilon && Abs(md.M12) < epsilon && Abs(md.M13) < epsilon && Abs(md.M14) < epsilon &&
				Abs(md.M21) < epsilon && Abs(md.M22) < epsilon && Abs(md.M23) < epsilon && Abs(md.M24) < epsilon &&
				Abs(md.M31) < epsilon && Abs(md.M32) < epsilon && Abs(md.M33) < epsilon && Abs(md.M34) < epsilon &&
				Abs(md.M41) < epsilon && Abs(md.M42) < epsilon && Abs(md.M43) < epsilon && Abs(md.M44) < epsilon;
		}

		public static uint GCD(uint x, uint y)
		{
			if (x < y)
				(x, y) = (y, x);

			while (y != 0)
			{
				uint t = y;
				y = x % y;
				x = t;
			}

			return x;
		}

#if HWINTRINSICS
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		private static Vector128<float> getRowVector(this in Matrix4x4 m, int r) =>
			Unsafe.ReadUnaligned<Vector128<float>>(ref Unsafe.As<float, byte>(ref Unsafe.Add(ref Unsafe.AsRef(m.M11), r * Vector128<float>.Count)));
#endif

		// Implementation taken from
		//   https://github.com/dotnet/runtime/blob/release/6.0/src/libraries/System.Private.CoreLib/src/System/Numerics/Matrix4x4.cs#L1556
		// Because of the number of calculations and rounding steps, using float intermediates results in loss of precision.
		// This is the same logic but with double precision intermediate calculations.
		public static Matrix4x4 InvertPrecise(this in Matrix4x4 matrix)
		{
			double a = matrix.M11, b = matrix.M12, c = matrix.M13, d = matrix.M14;
			double e = matrix.M21, f = matrix.M22, g = matrix.M23, h = matrix.M24;
			double i = matrix.M31, j = matrix.M32, k = matrix.M33, l = matrix.M34;
			double m = matrix.M41, n = matrix.M42, o = matrix.M43, p = matrix.M44;

			double kp_lo = k * p - l * o;
			double jp_ln = j * p - l * n;
			double jo_kn = j * o - k * n;
			double ip_lm = i * p - l * m;
			double io_km = i * o - k * m;
			double in_jm = i * n - j * m;

			double a11 = +(f * kp_lo - g * jp_ln + h * jo_kn);
			double a12 = -(e * kp_lo - g * ip_lm + h * io_km);
			double a13 = +(e * jp_ln - f * ip_lm + h * in_jm);
			double a14 = -(e * jo_kn - f * io_km + g * in_jm);

			double det = a * a11 + b * a12 + c * a13 + d * a14;

			if (Abs(det) < float.Epsilon)
				return new Matrix4x4(
					float.NaN, float.NaN, float.NaN, float.NaN,
					float.NaN, float.NaN, float.NaN, float.NaN,
					float.NaN, float.NaN, float.NaN, float.NaN,
					float.NaN, float.NaN, float.NaN, float.NaN
				);

			var result = new Matrix4x4();
			double invDet = 1 / det;

			result.M11 = (float)(a11 * invDet);
			result.M21 = (float)(a12 * invDet);
			result.M31 = (float)(a13 * invDet);
			result.M41 = (float)(a14 * invDet);

			result.M12 = (float)(-(b * kp_lo - c * jp_ln + d * jo_kn) * invDet);
			result.M22 = (float)(+(a * kp_lo - c * ip_lm + d * io_km) * invDet);
			result.M32 = (float)(-(a * jp_ln - b * ip_lm + d * in_jm) * invDet);
			result.M42 = (float)(+(a * jo_kn - b * io_km + c * in_jm) * invDet);

			double gp_ho = g * p - h * o;
			double fp_hn = f * p - h * n;
			double fo_gn = f * o - g * n;
			double ep_hm = e * p - h * m;
			double eo_gm = e * o - g * m;
			double en_fm = e * n - f * m;

			result.M13 = (float)(+(b * gp_ho - c * fp_hn + d * fo_gn) * invDet);
			result.M23 = (float)(-(a * gp_ho - c * ep_hm + d * eo_gm) * invDet);
			result.M33 = (float)(+(a * fp_hn - b * ep_hm + d * en_fm) * invDet);
			result.M43 = (float)(-(a * fo_gn - b * eo_gm + c * en_fm) * invDet);

			double gl_hk = g * l - h * k;
			double fl_hj = f * l - h * j;
			double fk_gj = f * k - g * j;
			double el_hi = e * l - h * i;
			double ek_gi = e * k - g * i;
			double ej_fi = e * j - f * i;

			result.M14 = (float)(-(b * gl_hk - c * fl_hj + d * fk_gj) * invDet);
			result.M24 = (float)(+(a * gl_hk - c * el_hi + d * ek_gi) * invDet);
			result.M34 = (float)(-(a * fl_hj - b * el_hi + d * ej_fi) * invDet);
			result.M44 = (float)(+(a * fk_gj - b * ek_gi + c * ej_fi) * invDet);

			return result;
		}

		public static bool IsNaN(this in Matrix4x4 m)
		{
#if HWINTRINSICS
			if (Sse.IsSupported)
			{
				var v0 = Sse.CompareUnordered(m.getRowVector(0), m.getRowVector(1));
				var v1 = Sse.CompareUnordered(m.getRowVector(2), m.getRowVector(3));

				return Sse.MoveMask(Sse.Or(v0, v1)) != 0;
			}
#endif

			return
				float.IsNaN(m.M11) || float.IsNaN(m.M12) || float.IsNaN(m.M13) || float.IsNaN(m.M14) ||
				float.IsNaN(m.M21) || float.IsNaN(m.M22) || float.IsNaN(m.M23) || float.IsNaN(m.M24) ||
				float.IsNaN(m.M31) || float.IsNaN(m.M32) || float.IsNaN(m.M33) || float.IsNaN(m.M34) ||
				float.IsNaN(m.M41) || float.IsNaN(m.M42) || float.IsNaN(m.M43) || float.IsNaN(m.M44);
		}
	}
}
