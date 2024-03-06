// Copyright © Clinton Ingram and Contributors.  Licensed under the MIT License.

using System.Numerics;
using System.Runtime.CompilerServices;

#if HWINTRINSICS
using System;
using System.Runtime.Intrinsics;
using System.Runtime.Intrinsics.X86;
#endif

namespace PhotoSauce.MagicScaler;

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
	public static bool HasFastGather = getFastGather();

	private static bool getFastGather()
	{
		if (!AppConfig.GdsMitigationsDisabled)
			return false;

		bool intel = X86Base.CpuId(0, 0) is (_, 0x756e6547, 0x6c65746e, 0x49656e69); // "Genu", "ntel", "ineI"
		uint fms = (uint)X86Base.CpuId(1, 0).Eax;
		uint fam = ((fms & 0xfu << 20) >> 16) + ((fms & 0xfu << 8) >> 8);
		uint mod = ((fms & 0xfu << 16) >> 12) + ((fms & 0xfu << 4) >> 4);

		// Intel Skylake and newer -- AMD is still slow as of Zen 4
		return intel && fam == 6 && mod >= 0x4e;
	}

	private const byte _ = 0x80;
	public const byte BlendMaskAlpha = 0b_1000_1000;
	public const byte ShuffleMaskChan0 = 0b_00_00_00_00;
	public const byte ShuffleMaskChan1 = 0b_01_01_01_01;
	public const byte ShuffleMaskChan2 = 0b_10_10_10_10;
	public const byte ShuffleMaskAlpha = 0b_11_11_11_11;
	public const byte ShuffleMaskOddToEven = 0b_11_11_01_01;
	public const byte PermuteMaskDeinterleave4x64 = 0b_11_01_10_00;

	public static ReadOnlySpan<byte> PermuteMaskDeinterleave8x32 => new byte[] { 0, 0, 0, 0, 4, 0, 0, 0, 1, 0, 0, 0, 5, 0, 0, 0, 2, 0, 0, 0, 6, 0, 0, 0, 3, 0, 0, 0, 7, 0, 0, 0 };
	public static ReadOnlySpan<byte> PermuteMask3To3xChan => new byte[] { 0, 0, 0, 0, 1, 0, 0, 0, 2, 0, 0, 0, 2, 0, 0, 0, 3, 0, 0, 0, 4, 0, 0, 0, 5, 0, 0, 0, 5, 0, 0, 0 };
	public static ReadOnlySpan<byte> PermuteMask3xTo3Chan => new byte[] { 0, 0, 0, 0, 1, 0, 0, 0, 2, 0, 0, 0, 4, 0, 0, 0, 5, 0, 0, 0, 6, 0, 0, 0, 3, 0, 0, 0, 7, 0, 0, 0 };
	public static ReadOnlySpan<byte> ShuffleMask3ChanPairs => new byte[] { 0, 3, 1, 4, 2, 5, _, _, 6, 9, 7, 10, 8, 11, _, _ };
	public static ReadOnlySpan<byte> ShuffleMask4ChanPairs => new byte[] { 0, 4, 1, 5, 2, 6, 3, 7, 8, 12, 9, 13, 10, 14, 11, 15 };
	public static ReadOnlySpan<byte> ShuffleMask3To3xChan => new byte[] { 0, 1, 2, _, 3, 4, 5, _, 6, 7, 8, _, 9, 10, 11, _ };
	public static ReadOnlySpan<byte> ShuffleMask3xTo3Chan => new byte[] { 0, 1, 2, 4, 5, 6, 8, 9, 10, 12, 13, 14, _, _, _, _ };
	public static ReadOnlySpan<byte> ShuffleMaskDeinterleave1x16 => new byte[] { 0, 4, 8, 12, 1, 5, 9, 13, 2, 6, 10, 14, 3, 7, 11, 15 };
	public static ReadOnlySpan<byte> ShuffleMaskWidenAlpha => new byte[] { 3, _, 7, _, 11, _, 15, _, 3, _, 7, _, 11, _, 15, _ };
	public static ReadOnlySpan<byte> ShuffleMaskWidenEven => new byte[] { 0, _, 4, _, 8, _, 12, _, 2, _, 6, _, 10, _, 14, _ };
	public static ReadOnlySpan<byte> ShuffleMaskWidenOdd => new byte[] { 1, _, 5, _, 9, _, 13, _, 3, _, 7, _, 11, _, 15, _ };

	public static ReadOnlySpan<byte> GatherMask3x => new byte[] { 0, 0, 0, 0x80, 0, 0, 0, 0x80, 0, 0, 0, 0x80, 0, 0, 0, 0 };

	// https://github.com/dotnet/runtime/issues/64784
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static unsafe Vector128<ulong> CreateVector128(ulong val) =>
		sizeof(nuint) == sizeof(uint)
			? Sse2.UnpackLow(Vector128.Create((uint)val), Vector128.Create((uint)(val >> 32))).AsUInt64()
			: Vector128.Create(val);

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static unsafe Vector256<ulong> CreateVector256(ulong val) =>
		sizeof(nuint) == sizeof(uint)
			? Avx2.UnpackLow(Vector256.Create((uint)val), Vector256.Create((uint)(val >> 32))).AsUInt64()
			: Vector256.Create(val);

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

#pragma warning disable IDE0075 // https://github.com/dotnet/runtime/issues/4207
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static bool IsZero(in Vector128<float> v)
	{
		if (Sse41.IsSupported)
			return Sse41.TestZ(v.AsByte(), v.AsByte()) ? true : false;
		else
			return Sse.MoveMask(v) == 0;
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static bool IsZero(in Vector128<byte> v)
	{
		if (Sse41.IsSupported)
			return Sse41.TestZ(v, v) ? true : false;
		else
			return Sse2.MoveMask(v) == 0;
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static bool IsMaskedZero(in Vector128<byte> v, in Vector128<byte> m)
	{
		if (Sse41.IsSupported)
			return Sse41.TestZ(v, m) ? true : false;
		else
			return Sse2.MoveMask(Sse2.And(v, m)) == 0;
	}
#pragma warning restore IDE0075
#endif
}
