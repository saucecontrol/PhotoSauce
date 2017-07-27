using System;
using System.IO;
using System.Numerics;
using System.Runtime.CompilerServices;

using static System.Math;

namespace PhotoSauce.MagicScaler
{
	internal static class MathUtil
	{
		private const int ishift = 15;
		private const int iscale = 1 << ishift;
		private const int imax = (1 << ishift + 1) - 1;
		private const int iround = iscale >> 1;
		private const float fscale = iscale;
		private const float ifscale = 1f / fscale;
		private const float fround = 0.5f;
		private const double dscale = iscale;
		private const double idscale = 1d / dscale;
		private const double dround = 0.5;

		public const ushort UQ15Max = imax;
		public const ushort UQ15One = iscale;
		public const float FloatScale = fscale;
		public const float FloatRound = fround;
		public const double DoubleScale = dscale;
		public const double DoubleRound = dround;

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static int Clamp(this int x, int min, int max) => Min(Max(min, x), max);

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static double Clamp(this double x, double min, double max) => Min(Max(min, x), max);

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
#if NETCOREAPP10
		public static float Clamp(this float x, float min, float max) => MathF.Min(MathF.Max(min, x), max);
#else
		public static float Clamp(this float x, float min, float max) => x < min ? min : x > max ? max : x;
#endif

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static Vector<T> Clamp<T>(this Vector<T> x, Vector<T> min, Vector<T> max) where T : struct => Vector.Min(Vector.Max(min, x), max);

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static ushort ClampToUQ15(int x) => (ushort)Min(Max(0, x), UQ15One);

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static byte ClampToByte(int x) => (byte)Min(Max(0, x), byte.MaxValue);

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static int Fix15(double x) => (int)Round(x * dscale);

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static ushort FixToUQ15(double x) => ClampToUQ15((int)(x * dscale + dround));

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static ushort FixToUQ15(float x) => ClampToUQ15((int)(x * fscale + fround));

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static byte FixToByte(double x) => ClampToByte((int)(x * byte.MaxValue + dround));

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static byte FixToByte(float x) => ClampToByte((int)(x * byte.MaxValue + fround));

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static double UnFix15ToDouble(int x) => x * idscale;

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static int UnFix15(int x) => x + iround >> ishift;

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static ushort UnFixToUQ15(int x) => ClampToUQ15(UnFix15(x));

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static byte UnFix15ToByte(int x) => ClampToByte(UnFix15(x));

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
#if NETCOREAPP10
		public static float Sqrt(this float x) => MathF.Sqrt(x);
#else
		public static float Sqrt(this float x) => (float)Math.Sqrt(x);
#endif

		public static uint ReadBigEndianUInt32(this BinaryReader rdr)
		{
			return (uint)(rdr.ReadByte() << 24 | rdr.ReadByte() << 16 | rdr.ReadByte() << 8 | rdr.ReadByte());
		}

		public static ushort ReadBigEndianUInt16(this BinaryReader rdr)
		{
			return (ushort)(rdr.ReadByte() << 8 | rdr.ReadByte());
		}
	}
}