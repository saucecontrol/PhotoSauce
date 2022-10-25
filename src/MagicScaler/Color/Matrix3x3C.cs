using System.Numerics;
using System.Runtime.InteropServices;
using System.Runtime.CompilerServices;
using System.Diagnostics.CodeAnalysis;

#if HWINTRINSICS
using System.Runtime.Intrinsics;
using System.Runtime.Intrinsics.X86;
#endif

using static System.Math;

namespace PhotoSauce.MagicScaler;

[StructLayout(LayoutKind.Sequential)]
internal readonly record struct Matrix3x3C
{
	public readonly double M11;
	public readonly double M12;
	public readonly double M13;
	private readonly double _14;
	public readonly double M21;
	public readonly double M22;
	public readonly double M23;
	private readonly double _24;
	public readonly double M31;
	public readonly double M32;
	public readonly double M33;
	private readonly double _34;

	public Matrix3x3C(
		double m11, double m12, double m13,
		double m21, double m22, double m23,
		double m31, double m32, double m33
	)
	{
		M11 = m11;
		M12 = m12;
		M13 = m13;
		M21 = m21;
		M22 = m22;
		M23 = m23;
		M31 = m31;
		M32 = m32;
		M33 = m33;
	}

	private static readonly Matrix3x3C identity = new(
		1, 0, 0,
		0, 1, 0,
		0, 0, 1
	);

	public bool IsIdentity => this == identity;

	[UnscopedRef]
	public ref readonly Vector3C Row1
	{
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		get => ref Unsafe.As<double, Vector3C>(ref Unsafe.AsRef(M11));
	}

	[UnscopedRef]
	public ref readonly Vector3C Row2
	{
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		get => ref Unsafe.As<double, Vector3C>(ref Unsafe.AsRef(M21));
	}

	[UnscopedRef]
	public ref readonly Vector3C Row3
	{
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		get => ref Unsafe.As<double, Vector3C>(ref Unsafe.AsRef(M31));
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static Matrix3x3C FromRows(in Vector3C r0, in Vector3C r1, in Vector3C r2)
	{
#if NET5_0_OR_GREATER
		Unsafe.SkipInit(out Matrix3x3C r);
#else
		var r = default(Matrix3x3C);
#endif

		ref var rr = ref Unsafe.As<Matrix3x3C, Vector3C>(ref r);
		Unsafe.Add(ref rr, 0) = r0;
		Unsafe.Add(ref rr, 1) = r1;
		Unsafe.Add(ref rr, 2) = r2;

		return r;
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static Matrix3x3C FromColumns(in Vector3C c0, in Vector3C c1, in Vector3C c2)
	{
#if HWINTRINSICS
		if (Avx.IsSupported)
		{
			var m0 = Unsafe.As<Vector3C, Vector256<double>>(ref Unsafe.AsRef(c0));
			var m1 = Unsafe.As<Vector3C, Vector256<double>>(ref Unsafe.AsRef(c1));
			var m2 = Unsafe.As<Vector3C, Vector256<double>>(ref Unsafe.AsRef(c2));
			var m3 = Vector256<double>.Zero;

			var m1l = Avx.UnpackLow(m0, m1);
			var m1h = Avx.UnpackHigh(m0, m1);
			var m2l = Avx.UnpackLow(m2, m3);
			var m2h = Avx.UnpackHigh(m2, m3);

			var r0 = Avx.Permute2x128(m1l, m2l, 0b_0010_0000);
			var r1 = Avx.Permute2x128(m1h, m2h, 0b_0010_0000);
			var r2 = Avx.Permute2x128(m1l, m2l, 0b_0011_0001);

#if NET5_0_OR_GREATER
			Unsafe.SkipInit(out Matrix3x3C r);
#else
			var r = default(Matrix3x3C);
#endif
			ref var rr = ref Unsafe.As<Matrix3x3C, Vector256<double>>(ref r);
			Unsafe.Add(ref rr, 0) = r0;
			Unsafe.Add(ref rr, 1) = r1;
			Unsafe.Add(ref rr, 2) = r2;

			return r;
		}
#endif

		return Transpose(FromRows(c0, c1, c2));
	}

	public static bool Invert(in Matrix3x3C m, out Matrix3x3C r)
	{
#if HWINTRINSICS
		if (Fma.IsSupported)
		{
			ref var rm = ref Unsafe.As<Matrix3x3C, Vector256<double>>(ref Unsafe.AsRef(m));
			var m0 = Unsafe.Add(ref rm, 0);
			var m1 = Unsafe.Add(ref rm, 1);
			var m2 = Unsafe.Add(ref rm, 2);

			var a0 = Vector256.Create(1.0, 0, 0, 0);
			var a1 = Vector256.Create(0, 1.0, 0, 0);
			var a2 = Vector256.Create(0, 0, 1.0, 0);

			var v0 = Vector256.Create(m0.ToScalar());
			var v1 = Vector256.Create(m1.ToScalar());
			var v2 = Vector256.Create(m2.ToScalar());

			double ds = v0.ToScalar();
			m0 = Avx.Divide(m0, v0);
			m1 = Fma.MultiplyAddNegated(m0, v1, m1);
			m2 = Fma.MultiplyAddNegated(m0, v2, m2);

			a0 = Avx.Divide(a0, v0);
			a1 = Fma.MultiplyAddNegated(a0, v1, a1);
			a2 = Fma.MultiplyAddNegated(a0, v2, a2);

			v0 = Vector256.Create(m0.GetElement(1));
			v1 = Vector256.Create(m1.GetElement(1));
			v2 = Vector256.Create(m2.GetElement(1));

			ds *= v1.ToScalar();
			m1 = Avx.Divide(m1, v1);
			m0 = Fma.MultiplyAddNegated(m1, v0, m0);
			m2 = Fma.MultiplyAddNegated(m1, v2, m2);

			a1 = Avx.Divide(a1, v1);
			a0 = Fma.MultiplyAddNegated(a1, v0, a0);
			a2 = Fma.MultiplyAddNegated(a1, v2, a2);

			v0 = Vector256.Create(m0.GetElement(2));
			v1 = Vector256.Create(m1.GetElement(2));
			v2 = Vector256.Create(m2.GetElement(2));

			ds *= v2.ToScalar();
			a2 = Avx.Divide(a2, v2);
			a0 = Fma.MultiplyAddNegated(a2, v0, a0);
			a1 = Fma.MultiplyAddNegated(a2, v1, a1);

#if NET5_0_OR_GREATER
			Unsafe.SkipInit(out r);
#else
			r = default;
#endif
			ref var rr = ref Unsafe.As<Matrix3x3C, Vector256<double>>(ref r);
			Unsafe.Add(ref rr, 0) = a0;
			Unsafe.Add(ref rr, 1) = a1;
			Unsafe.Add(ref rr, 2) = a2;

			return Abs(ds) >= double.Epsilon;
		}
#endif

		double a = m.M22 * m.M33 - m.M23 * m.M32;
		double b = m.M23 * m.M31 - m.M21 * m.M33;
		double c = m.M21 * m.M32 - m.M22 * m.M31;
		double d = m.M13 * m.M32 - m.M12 * m.M33;
		double e = m.M11 * m.M33 - m.M13 * m.M31;
		double f = m.M12 * m.M31 - m.M11 * m.M32;
		double g = m.M12 * m.M23 - m.M13 * m.M22;
		double h = m.M13 * m.M21 - m.M11 * m.M23;
		double i = m.M11 * m.M22 - m.M12 * m.M21;

		double det = m.M11 * a + m.M12 * b + m.M13 * c;
		r = new(
			a / det, d / det, g / det,
			b / det, e / det, h / det,
			c / det, f / det, i / det
		);

		return Abs(det) >= double.Epsilon;
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static Matrix3x3C Transpose(in Matrix3x3C m)
	{
#if HWINTRINSICS
		if (Avx.IsSupported)
		{
			ref var rm = ref Unsafe.As<Matrix3x3C, Vector256<double>>(ref Unsafe.AsRef(m));
			var m0 = Unsafe.Add(ref rm, 0);
			var m1 = Unsafe.Add(ref rm, 1);
			var m2 = Unsafe.Add(ref rm, 2);
			var m3 = Vector256<double>.Zero;

			var m0l = Avx.UnpackLow(m0, m1);
			var m0h = Avx.UnpackHigh(m0, m1);
			var m1l = Avx.UnpackLow(m2, m3);
			var m1h = Avx.UnpackHigh(m2, m3);

			m0 = Avx.Permute2x128(m0l, m1l, 0b_0010_0000);
			m1 = Avx.Permute2x128(m0h, m1h, 0b_0010_0000);
			m2 = Avx.Permute2x128(m0l, m1l, 0b_0011_0001);

#if NET5_0_OR_GREATER
			Unsafe.SkipInit(out Matrix3x3C r);
#else
			var r = default(Matrix3x3C);
#endif
			ref var rr = ref Unsafe.As<Matrix3x3C, Vector256<double>>(ref r);
			Unsafe.Add(ref rr, 0) = m0;
			Unsafe.Add(ref rr, 1) = m1;
			Unsafe.Add(ref rr, 2) = m2;

			return r;
		}
#endif

		return new(
			m.M11, m.M21, m.M31,
			m.M12, m.M22, m.M32,
			m.M13, m.M23, m.M33
		);
	}

	public unsafe bool IsRouglyEqualTo(in Matrix3x3C m)
	{
		const double epsilon = 1e-3d;

#if HWINTRINSICS && NET5_0_OR_GREATER
		if (Avx.IsSupported)
		{
			ref var r1 = ref Unsafe.As<Matrix3x3C, Vector256<double>>(ref Unsafe.AsRef(this));
			ref var r2 = ref Unsafe.As<Matrix3x3C, Vector256<double>>(ref Unsafe.AsRef(m));
			var veps = Vector256.Create(epsilon);
			var vmsk = Vector256.Create(0x7fffffffffffffff).AsDouble();

			var v0 = Avx.CompareNotLessThan(Avx.And(Avx.Subtract(Unsafe.Add(ref r1, 0), Unsafe.Add(ref r2, 0)), vmsk), veps);
			var v1 = Avx.CompareNotLessThan(Avx.And(Avx.Subtract(Unsafe.Add(ref r1, 1), Unsafe.Add(ref r2, 1)), vmsk), veps);
			var v2 = Avx.CompareNotLessThan(Avx.And(Avx.Subtract(Unsafe.Add(ref r1, 2), Unsafe.Add(ref r2, 2)), vmsk), veps);

			var r = Avx.Or(Avx.Or(v0, v1), v2);
			return Avx.TestZ(r, r);
		}
#endif

		return
			Abs(M11 - m.M11) < epsilon && Abs(M12 - m.M12) < epsilon && Abs(M13 - m.M13) < epsilon &&
			Abs(M21 - m.M21) < epsilon && Abs(M22 - m.M22) < epsilon && Abs(M23 - m.M23) < epsilon &&
			Abs(M31 - m.M31) < epsilon && Abs(M32 - m.M32) < epsilon && Abs(M33 - m.M33) < epsilon;
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static bool operator ==(in Matrix3x3C left, in Matrix3x3C right)
	{
#if HWINTRINSICS && NET5_0_OR_GREATER
		if (Avx.IsSupported)
		{
			ref var rl = ref Unsafe.As<Matrix3x3C, Vector256<double>>(ref Unsafe.AsRef(left));
			ref var rr = ref Unsafe.As<Matrix3x3C, Vector256<double>>(ref Unsafe.AsRef(right));

			var v0 = Avx.CompareNotEqual(Unsafe.Add(ref rl, 0), Unsafe.Add(ref rr, 0));
			var v1 = Avx.CompareNotEqual(Unsafe.Add(ref rl, 1), Unsafe.Add(ref rr, 1));
			var v2 = Avx.CompareNotEqual(Unsafe.Add(ref rl, 2), Unsafe.Add(ref rr, 2));

			var r = Avx.Or(Avx.Or(v0, v1), v2);
			return Avx.TestZ(r, r);
		}
#endif

		return
			left.M11 == right.M11 && left.M12 == right.M12 && left.M13 == right.M13 &&
			left.M21 == right.M21 && left.M22 == right.M22 && left.M23 == right.M23 &&
			left.M31 == right.M31 && left.M32 == right.M32 && left.M33 == right.M33;
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static Matrix3x3C operator *(in Matrix3x3C left, in Matrix3x3C right)
	{
#if HWINTRINSICS
		if (Avx2.IsSupported)
		{
			ref var rl = ref Unsafe.As<Matrix3x3C, Vector256<double>>(ref Unsafe.AsRef(left));
			ref var rr = ref Unsafe.As<Matrix3x3C, Vector256<double>>(ref Unsafe.AsRef(right));
			var vm = Vector256.Create(~0ul, ~0ul, ~0ul, 0ul).AsDouble();

			var vr0 = Unsafe.Add(ref rr, 0);
			var vr1 = Unsafe.Add(ref rr, 1);
			var vr2 = Unsafe.Add(ref rr, 2);

			var vl = Avx.And(rl, vm);
			var m0 = Avx.Add(
				Avx.Add(
					Avx.Multiply(Avx2.Permute4x64(vl, 0b_11_00_00_00), vr0),
					Avx.Multiply(Avx2.Permute4x64(vl, 0b_11_01_01_01), vr1)
				),
				Avx.Multiply(Avx2.Permute4x64(vl, 0b_11_10_10_10), vr2)
			);

			vl = Avx.And(Unsafe.Add(ref rl, 1), vm);
			var m1 = Avx.Add(
				Avx.Add(
					Avx.Multiply(Avx2.Permute4x64(vl, 0b_11_00_00_00), vr0),
					Avx.Multiply(Avx2.Permute4x64(vl, 0b_11_01_01_01), vr1)
				),
				Avx.Multiply(Avx2.Permute4x64(vl, 0b_11_10_10_10), vr2)
			);

			vl = Avx.And(Unsafe.Add(ref rl, 2), vm);
			var m2 = Avx.Add(
				Avx.Add(
					Avx.Multiply(Avx2.Permute4x64(vl, 0b_11_00_00_00), vr0),
					Avx.Multiply(Avx2.Permute4x64(vl, 0b_11_01_01_01), vr1)
				),
				Avx.Multiply(Avx2.Permute4x64(vl, 0b_11_10_10_10), vr2)
			);

#if NET5_0_OR_GREATER
			Unsafe.SkipInit(out Matrix3x3C r);
#else
			var r = default(Matrix3x3C);
#endif
			ref var ro = ref Unsafe.As<Matrix3x3C, Vector256<double>>(ref r);
			Unsafe.Add(ref ro, 0) = m0;
			Unsafe.Add(ref ro, 1) = m1;
			Unsafe.Add(ref ro, 2) = m2;

			return r;
		}
#endif

		return new(
			left.M11 * right.M11 + left.M12 * right.M21 + left.M13 * right.M31,
			left.M11 * right.M12 + left.M12 * right.M22 + left.M13 * right.M32,
			left.M11 * right.M13 + left.M12 * right.M23 + left.M13 * right.M33,
			left.M21 * right.M11 + left.M22 * right.M21 + left.M23 * right.M31,
			left.M21 * right.M12 + left.M22 * right.M22 + left.M23 * right.M32,
			left.M21 * right.M13 + left.M22 * right.M23 + left.M23 * right.M33,
			left.M31 * right.M11 + left.M32 * right.M21 + left.M33 * right.M31,
			left.M31 * right.M12 + left.M32 * right.M22 + left.M33 * right.M32,
			left.M31 * right.M13 + left.M32 * right.M23 + left.M33 * right.M33
		);
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static Matrix3x3C operator *(in Matrix3x3C left, double right)
	{
#if HWINTRINSICS
		if (Avx2.IsSupported)
		{
			ref var rl = ref Unsafe.As<Matrix3x3C, Vector256<double>>(ref Unsafe.AsRef(left));
			var vr = Avx2.Permute4x64(Vector128.CreateScalar(right).ToVector256Unsafe(), 0b_11_00_00_00);

			var m0 = Avx.Multiply(Unsafe.Add(ref rl, 0), vr);
			var m1 = Avx.Multiply(Unsafe.Add(ref rl, 1), vr);
			var m2 = Avx.Multiply(Unsafe.Add(ref rl, 2), vr);

#if NET5_0_OR_GREATER
			Unsafe.SkipInit(out Matrix3x3C r);
#else
			var r = default(Matrix3x3C);
#endif
			ref var rr = ref Unsafe.As<Matrix3x3C, Vector256<double>>(ref r);
			Unsafe.Add(ref rr, 0) = m0;
			Unsafe.Add(ref rr, 1) = m1;
			Unsafe.Add(ref rr, 2) = m2;

			return r;
		}
#endif

		return new(
			left.M11 * right,
			left.M12 * right,
			left.M13 * right,
			left.M21 * right,
			left.M22 * right,
			left.M23 * right,
			left.M31 * right,
			left.M32 * right,
			left.M33 * right
		);
	}

	public static explicit operator Matrix3x3C(in Matrix4x4 m) => new(
		m.M11, m.M12, m.M13,
		m.M21, m.M22, m.M23,
		m.M31, m.M32, m.M33
	);

	public static explicit operator Matrix4x4(in Matrix3x3C m) => new(
		(float)m.M11, (float)m.M12, (float)m.M13, 0f,
		(float)m.M21, (float)m.M22, (float)m.M23, 0f,
		(float)m.M31, (float)m.M32, (float)m.M33, 0f,
		          0f,           0f,           0f, 1f
	);
}
