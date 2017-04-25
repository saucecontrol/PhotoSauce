using System.IO;
using System.Linq;
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

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static int Clamp(this int x, int min, int max) => Min(Max(min, x), max);

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static double Clamp(this double x, double min, double max) => Min(Max(min, x), max);

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static Vector<T> Clamp<T>(this Vector<T> x, Vector<T> min, Vector<T> max) where T : struct => Vector.Min(Vector.Max(min, x), max);

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static ushort ClampToUQ15(int x) => (ushort)Min(Max(0, x), imax);

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static byte ClampToByte(int x) => (byte)Min(Max(0, x), byte.MaxValue);

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static int ScaleToInt32(double x) => (int)(x * dscale);

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static int ScaleToInt32(float x) => (int)(x * fscale);

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static ushort ScaleToUQ15(double x) => ClampToUQ15(ScaleToInt32(x));

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static ushort ScaleToUQ15(float x) => ClampToUQ15(ScaleToInt32(x));

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static byte ScaleToByte(double x) => ClampToByte((int)(x * byte.MaxValue + dround));

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static byte ScaleToByte(float x) => ClampToByte((int)(x * byte.MaxValue + fround));

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static double UnscaleToDouble(int x) => x * idscale;

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static float UnscaleToFloat(int x) => x * ifscale;

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static int UnscaleToInt32(int x) => x + iround >> ishift;

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static ushort UnscaleToUQ15(int x) => ClampToUQ15(UnscaleToInt32(x));

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static byte UnscaleToByte(int x) => ClampToByte(UnscaleToInt32(x));

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static ushort LumaFromBgr(ushort b, ushort g, ushort r)
		{
			//http://en.wikipedia.org/wiki/Relative_luminance
			const int rY = (ushort)(0.2126 * dscale + dround);
			const int gY = (ushort)(0.7152 * dscale + dround);
			const int bY = (ushort)(0.0722 * dscale + dround);

			return UnscaleToUQ15(r * rY + g * gY + b * bY);
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static byte LumaFromBgr(byte b, byte g, byte r)
		{
			//http://www.w3.org/TR/AERT#color-contrast
			const int rY = (ushort)(0.299 * dscale + dround);
			const int gY = (ushort)(0.587 * dscale + dround);
			const int bY = (ushort)(0.114 * dscale + dround);

			return UnscaleToByte(r * rY + g * gY + b * bY);
		}

		public static uint ReadBigEndianUInt32(this BinaryReader rdr)
		{
			return (uint)(rdr.ReadByte() << 24 | rdr.ReadByte() << 16 | rdr.ReadByte() << 8 | rdr.ReadByte());
		}

		public static ushort ReadBigEndianUInt16(this BinaryReader rdr)
		{
			return (ushort)(rdr.ReadByte() << 8 | rdr.ReadByte());
		}

		//http://en.wikipedia.org/wiki/Gaussian_blur
		public class GaussianFactory
		{
			private readonly double sigma;
			private readonly double dx;

			public GaussianFactory(double sigma)
			{
				this.sigma = sigma;
				dx = 1d / Sqrt(2d * PI * (sigma * sigma));
			}

			public double Support => sigma * 3d;

			public double GetValue(double d) => dx * Exp(-((d * d) / (2d * (sigma * sigma))));

			public double[] MakeKernel()
			{
				int dist = (int)Ceiling(Support);
				var kernel = new double[dist * 2 + 1];

				for (int i = -dist; i <= dist; i++)
					kernel[i + dist] = GetValue(i);

				double sum = kernel.Sum();
				return kernel.Select(d => d / sum).ToArray();
			}
		}
	}
}