using System.Numerics;
using System.Runtime.CompilerServices;

#if HWINTRINSICS
using System;
using System.Runtime.Intrinsics;
using System.Runtime.Intrinsics.X86;
#endif

namespace PhotoSauce.MagicScaler
{
	internal static class HWIntrinsics
	{
		public static bool IsSupported =>
#if HWINTRINSICS
			Sse2.IsSupported;
#else
			false;
#endif

		public static bool IsAvxSupported =>
#if HWINTRINSICS
			Avx.IsSupported;
#else
			false;
#endif

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static int VectorCount<T>() where T : struct =>
#if HWINTRINSICS
			Avx.IsSupported ? Vector256<T>.Count :
			Sse.IsSupported ? Vector128<T>.Count :
#endif
			Vector<T>.Count;

#if HWINTRINSICS
		public const byte BlendMaskAlpha = 0b_1000_1000;
		public const byte ShuffleMaskAlpha = 0b_11_11_11_11;
		public const byte PermuteMaskDeinterleave4x64 = 0b_11_01_10_00;
		public const byte PermuteMaskLowLow2x128 = 0b_0010_0000;
		public const byte PermuteMaskHighHigh2x128 = 0b_0011_0001;

		public static ReadOnlySpan<byte> PermuteMaskDeinterleave8x32 => new byte[] { 0, 0, 0, 0, 4, 0, 0, 0, 1, 0, 0, 0, 5, 0, 0, 0, 2, 0, 0, 0, 6, 0, 0, 0, 3, 0, 0, 0, 7, 0, 0, 0 };
		public static ReadOnlySpan<byte> PermuteMaskOddEven8x32 => new byte[] { 1, 0, 0, 0, 3, 0, 0, 0, 5, 0, 0, 0, 7, 0, 0, 0, 0, 0, 0, 0, 2, 0, 0, 0, 4, 0, 0, 0, 6, 0, 0, 0 };
		public static ReadOnlySpan<byte> PermuteMask3To3xChan => new byte[] { 0, 0, 0, 0, 1, 0, 0, 0, 2, 0, 0, 0, 2, 0, 0, 0, 3, 0, 0, 0, 4, 0, 0, 0, 5, 0, 0, 0, 5, 0, 0, 0 };
		public static ReadOnlySpan<byte> PermuteMask3xTo3Chan => new byte[] { 0, 0, 0, 0, 1, 0, 0, 0, 2, 0, 0, 0, 4, 0, 0, 0, 5, 0, 0, 0, 6, 0, 0, 0, 3, 0, 0, 0, 7, 0, 0, 0 };
		public static ReadOnlySpan<byte> ShuffleMask3ChanPairs => new byte[] { 0, 3, 1, 4, 2, 5, 0x80, 0x80, 6, 9, 7, 10, 8, 11, 0x80, 0x80 };
		public static ReadOnlySpan<byte> ShuffleMask4ChanPairs => new byte[] { 0, 4, 1, 5, 2, 6, 3, 7, 8, 12, 9, 13, 10, 14, 11, 15 };
		public static ReadOnlySpan<byte> ShuffleMask3xTo3Chan => new byte[] { 0, 1, 2, 4, 5, 6, 8, 9, 10, 12, 13, 14, 0x80, 0x80, 0x80, 0x80 };
		public static ReadOnlySpan<byte> ShuffleMask8bitAlpha => new byte[] { 3, 3, 7, 7, 3, 3, 7, 7, 11, 11, 15, 15, 11, 11, 15, 15 };
		public static ReadOnlySpan<byte> ShuffleMask8bitEven => new byte[] { 0x80, 0, 0x80, 4, 0x80, 2, 0x80, 6, 0x80, 8, 0x80, 12, 0x80, 10, 0x80, 14 };
		public static ReadOnlySpan<byte> ShuffleMask8bitOdd => new byte[] { 0x80, 1, 0x80, 5, 0x80, 3, 0x80, 7, 0x80, 9, 0x80, 13, 0x80, 11, 0x80, 15 };

		public static ReadOnlySpan<byte> GatherMask3x => new byte[] { 0, 0, 0, 0x80, 0, 0, 0, 0x80, 0, 0, 0, 0x80, 0, 0, 0, 0 };
		public static ReadOnlySpan<byte> ScaleUQ15WithAlphaInt => new byte[] { 0, 0x80, 0, 0, 0, 0x80, 0, 0, 0, 0x80, 0, 0, 0xff, 0, 0, 0 };
		public static ReadOnlySpan<byte> ScaleUQ15WithAlphaFloat => new byte[] { 0, 0, 0, 0x47, 0, 0, 0, 0x47, 0, 0, 0, 0x47, 0, 0, 0x7f, 0x43 };

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static Vector256<float> AvxCompareEqual(Vector256<float> v1, Vector256<float> v2) => Avx.Compare(v1, v2, FloatComparisonMode.OrderedEqualNonSignaling);

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static Vector256<float> AvxCompareGreaterThan(Vector256<float> v1, Vector256<float> v2) => Avx.Compare(v1, v2, FloatComparisonMode.UnorderedNotLessThanOrEqualSignaling);

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static float HorizontalAdd(this Vector128<float> v)
		{	                                      //  a | b | c | d
			var high = Sse3.IsSupported ?         //  b |___| d |___
				Sse3.MoveHighAndDuplicate(v) :
				Sse.Shuffle(v, v, 0b_11_11_01_01);
			var sums = Sse.Add(v, high);          // a+b|___|c+d|___
			high = Sse.MoveHighToLow(high, sums); // c+d|___|___|___

			return Sse.AddScalar(sums, high).ToScalar();
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static Vector256<float> Lerp(Vector256<float> l, Vector256<float> h, Vector256<float> d)
		{
			var diff = Avx.Subtract(h, l);
			if (Fma.IsSupported)
				return Fma.MultiplyAdd(diff, d, l);
			else
				return Avx.Add(Avx.Multiply(diff, d), l);
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		unsafe public static Vector128<float> MultiplyAdd(Vector128<float> va, Vector128<float> vm, float* mp)
		{
			if (Fma.IsSupported)
				return Fma.MultiplyAdd(Sse.LoadVector128(mp), vm, va);
			else
				return Sse.Add(va, Sse.Multiply(vm, Sse.LoadVector128(mp)));
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		unsafe public static Vector128<float> MultiplyAdd(in Vector128<float> va, in Vector128<float> vm0, in Vector128<float> vm1)
		{
			if (Fma.IsSupported)
				return Fma.MultiplyAdd(vm1, vm0, va);
			else
				return Sse.Add(va, Sse.Multiply(vm0, vm1));
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		unsafe public static Vector256<float> MultiplyAdd(Vector256<float> va, Vector256<float> vm, float* mp)
		{
			if (Fma.IsSupported)
				return Fma.MultiplyAdd(Avx.LoadVector256(mp), vm, va);
			else
				return Avx.Add(va, Avx.Multiply(vm, Avx.LoadVector256(mp)));
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		unsafe public static Vector256<float> MultiplyAdd(in Vector256<float> va, in Vector256<float> vm0, in Vector256<float> vm1)
		{
			if (Fma.IsSupported)
				return Fma.MultiplyAdd(vm1, vm0, va);
			else
				return Avx.Add(va, Avx.Multiply(vm0, vm1));
		}
#endif
	}
}
