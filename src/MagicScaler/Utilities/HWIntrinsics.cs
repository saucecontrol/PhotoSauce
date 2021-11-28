// Copyright © Clinton Ingram and Contributors.  Licensed under the MIT License.

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
		private const byte _ = 0x80;
		public const byte BlendMaskAlpha = 0b_1000_1000;
		public const byte ShuffleMaskAlpha = 0b_11_11_11_11;
		public const byte ShuffleMaskLoPairs = 0b_01_00_01_00;
		public const byte ShuffleMaskHiPairs = 0b_11_10_11_10;
		public const byte ShuffleMaskEvPairs = 0b_10_00_10_00;
		public const byte ShuffleMaskOdPairs = 0b_11_01_11_01;
		public const byte ShuffleMaskOddToEven = 0b_11_11_01_01;
		public const byte PermuteMaskDeinterleave4x64 = 0b_11_01_10_00;

		public static ReadOnlySpan<byte> PermuteMaskDeinterleave8x32 => new byte[] { 0, 0, 0, 0, 4, 0, 0, 0, 1, 0, 0, 0, 5, 0, 0, 0, 2, 0, 0, 0, 6, 0, 0, 0, 3, 0, 0, 0, 7, 0, 0, 0 };
		public static ReadOnlySpan<byte> PermuteMaskEvenOdd8x32 => new byte[] { 0, 0, 0, 0, 2, 0, 0, 0, 4, 0, 0, 0, 6, 0, 0, 0, 1, 0, 0, 0, 3, 0, 0, 0, 5, 0, 0, 0, 7, 0, 0, 0 };
		public static ReadOnlySpan<byte> PermuteMask3To3xChan => new byte[] { 0, 0, 0, 0, 1, 0, 0, 0, 2, 0, 0, 0, 2, 0, 0, 0, 3, 0, 0, 0, 4, 0, 0, 0, 5, 0, 0, 0, 5, 0, 0, 0 };
		public static ReadOnlySpan<byte> PermuteMask3xTo3Chan => new byte[] { 0, 0, 0, 0, 1, 0, 0, 0, 2, 0, 0, 0, 4, 0, 0, 0, 5, 0, 0, 0, 6, 0, 0, 0, 3, 0, 0, 0, 7, 0, 0, 0 };
		public static ReadOnlySpan<byte> ShuffleMask3ChanPairs => new byte[] { 0, 3, 1, 4, 2, 5, _, _, 6, 9, 7, 10, 8, 11, _, _ };
		public static ReadOnlySpan<byte> ShuffleMask4ChanPairs => new byte[] { 0, 4, 1, 5, 2, 6, 3, 7, 8, 12, 9, 13, 10, 14, 11, 15 };
		public static ReadOnlySpan<byte> ShuffleMask3To3xChan => new byte[] { 0, 1, 2, _, 3, 4, 5, _, 6, 7, 8, _, 9, 10, 11, _ };
		public static ReadOnlySpan<byte> ShuffleMask3xTo3Chan => new byte[] { 0, 1, 2, 4, 5, 6, 8, 9, 10, 12, 13, 14, _, _, _, _ };
		public static ReadOnlySpan<byte> ShuffleMask8bitAlpha => new byte[] { 3, 3, 7, 7, 3, 3, 7, 7, 11, 11, 15, 15, 11, 11, 15, 15 };
		public static ReadOnlySpan<byte> ShuffleMask8bitEven => new byte[] { _, 0, _, 4, _, 2, _, 6, _, 8, _, 12, _, 10, _, 14 };
		public static ReadOnlySpan<byte> ShuffleMask8bitOdd => new byte[] { _, 1, _, 5, _, 3, _, 7, _, 9, _, 13, _, 11, _, 15 };

		public static ReadOnlySpan<byte> GatherMask3x => new byte[] { 0, 0, 0, 0x80, 0, 0, 0, 0x80, 0, 0, 0, 0x80, 0, 0, 0, 0 };
		public static ReadOnlySpan<byte> ScaleUQ15WithAlphaInt => new byte[] { 0, 0x80, 0, 0, 0, 0x80, 0, 0, 0, 0x80, 0, 0, 0xff, 0, 0, 0 };
		public static ReadOnlySpan<byte> ScaleUQ15WithAlphaFloat => new byte[] { 0, 0, 0, 0x47, 0, 0, 0, 0x47, 0, 0, 0, 0x47, 0, 0, 0x7f, 0x43 };

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static Vector256<float> AvxCompareEqual(Vector256<float> v1, Vector256<float> v2) => Avx.Compare(v1, v2, FloatComparisonMode.OrderedEqualNonSignaling);

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static float HorizontalAdd(this Vector128<float> v)
		{	                                      //  a | b | c | d
			var high = Sse3.IsSupported ?         //  b |___| d |___
				Sse3.MoveHighAndDuplicate(v) :
				Sse.Shuffle(v, v, ShuffleMaskOddToEven);
			var sums = Sse.Add(v, high);          // a+b|___|c+d|___
			high = Sse.MoveHighToLow(high, sums); // c+d|___|___|___

			return Sse.AddScalar(sums, high).ToScalar();
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static Vector256<float> Lerp(in Vector256<float> vl, in Vector256<float> vh, in Vector256<float> vd)
		{
			var diff = Avx.Subtract(vh, vl);
			if (Fma.IsSupported)
				return Fma.MultiplyAdd(diff, vd, vl);
			else
				return Avx.Add(Avx.Multiply(diff, vd), vl);
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static unsafe Vector128<float> MultiplyAdd(in Vector128<float> va, in Vector128<float> vm, float* mp)
		{
			if (Fma.IsSupported)
				return Fma.MultiplyAdd(Sse.LoadVector128(mp), vm, va);
			else
				return Sse.Add(Sse.Multiply(vm, Sse.LoadVector128(mp)), va);
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static Vector128<float> MultiplyAdd(in Vector128<float> va, in Vector128<float> vm0, in Vector128<float> vm1)
		{
			if (Fma.IsSupported)
				return Fma.MultiplyAdd(vm1, vm0, va);
			else
				return Sse.Add(Sse.Multiply(vm0, vm1), va);
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static unsafe Vector256<float> MultiplyAdd(in Vector256<float> va, in Vector256<float> vm, float* mp)
		{
			if (Fma.IsSupported)
				return Fma.MultiplyAdd(Avx.LoadVector256(mp), vm, va);
			else
				return Avx.Add(Avx.Multiply(vm, Avx.LoadVector256(mp)), va);
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static Vector256<float> MultiplyAdd(in Vector256<float> va, in Vector256<float> vm0, in Vector256<float> vm1)
		{
			if (Fma.IsSupported)
				return Fma.MultiplyAdd(vm1, vm0, va);
			else
				return Avx.Add(Avx.Multiply(vm0, vm1), va);
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static Vector128<byte> BlendVariable(in Vector128<byte> vl, in Vector128<byte> vr, in Vector128<byte> vm)
		{
			if (Sse41.IsSupported)
				return Sse41.BlendVariable(vl, vr, vm);
			else
				return Sse2.Or(Sse2.And(vr, vm), Sse2.AndNot(vm, vl));
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static Vector128<uint> BlendVariable(in Vector128<uint> vl, in Vector128<uint> vr, in Vector128<uint> vm) =>
			BlendVariable(vl.AsByte(), vr.AsByte(), vm.AsByte()).AsUInt32();
#endif
	}
}
