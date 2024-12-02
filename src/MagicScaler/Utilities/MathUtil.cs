// Copyright © Clinton Ingram and Contributors
// SPDX-License-Identifier: MIT

using System;
using System.Numerics;
using System.Runtime.InteropServices;
using System.Runtime.CompilerServices;
#if HWINTRINSICS
using System.Runtime.Intrinsics;
using System.Runtime.Intrinsics.X86;
#endif

using static System.Math;

namespace PhotoSauce.MagicScaler;

[StructLayout(LayoutKind.Sequential, Pack = 1)]
internal readonly struct Triple(uint v)
{
	public readonly ushort v1 = (ushort)v;
	public readonly byte v2 = (byte)(v >> 16);

	public static explicit operator Triple(uint v) => new(v);
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
	public static int DivCeiling(int x, int y) => (int)(((uint)x + ((uint)y - 1)) / (uint)y);

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
	public static bool IsOdd(this int x) => (x & 1) != 0;

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
			return UnsafeUtil.BitCast<(int, int), (T, T)>((0, 1));

		if (va > maxVal)
			return UnsafeUtil.BitCast<(int, int), (T, T)>((sign, 0));

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

		return UnsafeUtil.BitCast<(int, int), (T, T)>(((int)(n * sign), (int)d));
	}

	public static Rational ToRational(this double d) => d.toFraction<uint>();

	public static SRational ToSRational(this double d) => d.toFraction<int>();

	public static bool IsRoughlyEqualTo(this double x, double y) => Abs(x - y) < 1e-4;

	public static unsafe bool IsRoughlyEqualTo(this in Matrix4x4 m1, in Matrix4x4 m2)
	{
		const float epsilon = 1e-3f;

#if HWINTRINSICS
		if (Sse.IsSupported)
		{
			ref var r1 = ref Unsafe.As<Matrix4x4, Vector128<float>>(ref Unsafe.AsRef(in m1));
			ref var r2 = ref Unsafe.As<Matrix4x4, Vector128<float>>(ref Unsafe.AsRef(in m2));
			var veps = Vector128.Create(epsilon);
			var vmsk = Vector128.Create(0x7fffffff).AsSingle();

			var v0 = Sse.CompareNotLessThan(Sse.And(Sse.Subtract(Unsafe.Add(ref r1, 0), Unsafe.Add(ref r2, 0)), vmsk), veps);
			var v1 = Sse.CompareNotLessThan(Sse.And(Sse.Subtract(Unsafe.Add(ref r1, 1), Unsafe.Add(ref r2, 1)), vmsk), veps);
			var v2 = Sse.CompareNotLessThan(Sse.And(Sse.Subtract(Unsafe.Add(ref r1, 2), Unsafe.Add(ref r2, 2)), vmsk), veps);
			var v3 = Sse.CompareNotLessThan(Sse.And(Sse.Subtract(Unsafe.Add(ref r1, 3), Unsafe.Add(ref r2, 3)), vmsk), veps);

			return HWIntrinsics.IsZero(Sse.Or(Sse.Or(v0, v1), Sse.Or(v2, v3)));
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
}
