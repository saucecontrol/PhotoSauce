using System.Runtime.InteropServices;
using System.Runtime.CompilerServices;

#if HWINTRINSICS
using System.Runtime.Intrinsics;
using System.Runtime.Intrinsics.X86;
#endif

namespace PhotoSauce.MagicScaler;

[StructLayout(LayoutKind.Sequential)]
internal readonly record struct Vector3C
{
	public readonly double X;
	public readonly double Y;
	public readonly double Z;
	private readonly double _W;

	public Vector3C(double v) : this(v, v, v) { }

	public Vector3C(double x, double y, double z)
	{
		X = x;
		Y = y;
		Z = z;
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static double Dot(in Vector3C left, in Vector3C right)
	{
#if HWINTRINSICS
		if (Avx.IsSupported)
		{
			var w = Avx.Multiply(
				Unsafe.As<Vector3C, Vector256<double>>(ref Unsafe.AsRef(left)),
				Unsafe.As<Vector3C, Vector256<double>>(ref Unsafe.AsRef(right))
			);

			var v = w.GetLower();
			return Sse2.Add(Sse2.Add(v, Sse2.UnpackHigh(v, v)), w.GetUpper()).ToScalar();
		}
#endif

		return
			left.X * right.X +
			left.Y * right.Y +
			left.Z * right.Z;
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static Vector3C operator /(in Vector3C left, in Vector3C right)
	{
#if HWINTRINSICS
		if (Avx.IsSupported)
		{
			var w = Avx.Divide(
				Unsafe.As<Vector3C, Vector256<double>>(ref Unsafe.AsRef(left)),
				Unsafe.As<Vector3C, Vector256<double>>(ref Unsafe.AsRef(right))
			);

			return Unsafe.As<Vector256<double>, Vector3C>(ref w);
		}
#endif

		return new(left.X / right.X, left.Y / right.Y, left.Z / right.Z);
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static Vector3C operator +(in Vector3C left, in Vector3C right)
	{
#if HWINTRINSICS
		if (Avx.IsSupported)
		{
			var w = Avx.Add(
				Unsafe.As<Vector3C, Vector256<double>>(ref Unsafe.AsRef(left)),
				Unsafe.As<Vector3C, Vector256<double>>(ref Unsafe.AsRef(right))
			);

			return Unsafe.As<Vector256<double>, Vector3C>(ref w);
		}
#endif

		return new(left.X + right.X, left.Y + right.Y, left.Z + right.Z);
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static Vector3C operator -(in Vector3C left, in Vector3C right)
	{
#if HWINTRINSICS
		if (Avx.IsSupported)
		{
			var w = Avx.Subtract(
				Unsafe.As<Vector3C, Vector256<double>>(ref Unsafe.AsRef(left)),
				Unsafe.As<Vector3C, Vector256<double>>(ref Unsafe.AsRef(right))
			);

			return Unsafe.As<Vector256<double>, Vector3C>(ref w);
		}
#endif

		return new(left.X - right.X, left.Y - right.Y, left.Z - right.Z);
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static Vector3C operator *(in Vector3C left, in Vector3C right)
	{
#if HWINTRINSICS
		if (Avx.IsSupported)
		{
			var w = Avx.Multiply(
				Unsafe.As<Vector3C, Vector256<double>>(ref Unsafe.AsRef(left)),
				Unsafe.As<Vector3C, Vector256<double>>(ref Unsafe.AsRef(right))
			);

			return Unsafe.As<Vector256<double>, Vector3C>(ref w);
		}
#endif

		return new(left.X * right.X, left.Y * right.Y, left.Z * right.Z);
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static Vector3C operator *(in Vector3C left, double right)
	{
#if HWINTRINSICS
		if (Avx2.IsSupported)
		{
			var w = Avx.Multiply(
				Unsafe.As<Vector3C, Vector256<double>>(ref Unsafe.AsRef(left)),
				Avx2.Permute4x64(Vector128.CreateScalar(right).ToVector256Unsafe(), 0b_11_00_00_00)
			);

			return Unsafe.As<Vector256<double>, Vector3C>(ref w);
		}
#endif

		return left * new Vector3C(right);
	}
}
