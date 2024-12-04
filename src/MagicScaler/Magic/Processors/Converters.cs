// Copyright © Clinton Ingram and Contributors
// SPDX-License-Identifier: MIT

using System;
using System.Runtime.CompilerServices;
#if HWINTRINSICS
using System.Runtime.Intrinsics;
using System.Runtime.Intrinsics.X86;
#endif

using static PhotoSauce.MagicScaler.MathUtil;

namespace PhotoSauce.MagicScaler.Converters;

internal interface EncodingType
{
	public readonly struct Companded : EncodingType { }
	public readonly struct Linear : EncodingType { }
}

internal interface EncodingRange
{
	public readonly struct Video : EncodingRange { }
	public readonly struct Full : EncodingRange { }
}

internal interface IConversionProcessor
{
	unsafe void ConvertLine(byte* istart, byte* ostart, nint cb);
}

internal interface IConversionProcessor<TFrom, TTo> : IConversionProcessor where TFrom : unmanaged where TTo : unmanaged { }

internal interface IConverter { }

internal interface IConverter<TFrom, TTo> : IConverter where TFrom : unmanaged where TTo : unmanaged
{
	public IConversionProcessor<TFrom, TTo> Processor { get; }
	public IConversionProcessor<TFrom, TTo> Processor3A { get; }
	public IConversionProcessor<TFrom, TTo> Processor3X { get; }
}

internal sealed unsafe class NarrowingConverter : IConversionProcessor<ushort, byte>
{
	public static readonly NarrowingConverter Instance = new();

	private NarrowingConverter() { }

	void IConversionProcessor.ConvertLine(byte* istart, byte* ostart, nint cb)
	{
		ushort* ip = (ushort*)istart, ipe = (ushort*)(istart + cb);
		byte* op = ostart;

#if HWINTRINSICS
		if (HWIntrinsics.IsSupported && cb >= HWIntrinsics.VectorCount<byte>() * 2)
			convertIntrinsic(ip, ipe, op);
		else
#endif
			convertScalar(ip, ipe, op);
	}

#if HWINTRINSICS
	private static void convertIntrinsic(ushort* ip, ushort* ipe, byte* op)
	{
		if (Avx2.IsSupported)
		{
			ipe -= Vector256<ushort>.Count * 2;

			LoopTop:
			do
			{
				var vs0 = Avx.LoadVector256(ip);
				var vs1 = Avx.LoadVector256(ip + Vector256<ushort>.Count);
				ip += Vector256<ushort>.Count * 2;

				vs0 = Avx2.ShiftRightLogical(vs0, 8);
				vs1 = Avx2.ShiftRightLogical(vs1, 8);

				var vb0 = Avx2.PackUnsignedSaturate(vs0.AsInt16(), vs1.AsInt16());
				vb0 = Avx2.Permute4x64(vb0.AsUInt64(), HWIntrinsics.PermuteMaskDeinterleave4x64).AsByte();

				Avx.Store(op, vb0);
				op += Vector256<byte>.Count;
			}
			while (ip <= ipe);

			if (ip < ipe + Vector256<ushort>.Count * 2)
			{
				nuint offs = UnsafeUtil.ByteOffset(ipe, ip);
				ip = UnsafeUtil.SubtractOffset(ip, offs);
				op = UnsafeUtil.SubtractOffset(op, UnsafeUtil.ConvertOffset<ushort, byte>(offs));
				goto LoopTop;
			}
		}
		else
		{
			ipe -= Vector128<ushort>.Count * 2;

			LoopTop:
			do
			{
				var vs0 = Sse2.LoadVector128(ip);
				var vs1 = Sse2.LoadVector128(ip + Vector128<ushort>.Count);
				ip += Vector128<ushort>.Count * 2;

				vs0 = Sse2.ShiftRightLogical(vs0, 8);
				vs1 = Sse2.ShiftRightLogical(vs1, 8);

				var vb0 = Sse2.PackUnsignedSaturate(vs0.AsInt16(), vs1.AsInt16());

				Sse2.Store(op, vb0);
				op += Vector128<byte>.Count;
			}
			while (ip <= ipe);

			if (ip < ipe + Vector128<ushort>.Count * 2)
			{
				nuint offs = UnsafeUtil.ByteOffset(ipe, ip);
				ip = UnsafeUtil.SubtractOffset(ip, offs);
				op = UnsafeUtil.SubtractOffset(op, UnsafeUtil.ConvertOffset<ushort, byte>(offs));
				goto LoopTop;
			}
		}
	}
#endif

	private static unsafe void convertScalar(ushort* ip, ushort* ipe, byte* op)
	{
		while (ip < ipe)
		{
			byte o0 = (byte)(ip[0] >> 8);
			byte o1 = (byte)(ip[1] >> 8);
			byte o2 = (byte)(ip[2] >> 8);
			byte o3 = (byte)(ip[3] >> 8);
			ip += 4;

			op[0] = o0;
			op[1] = o1;
			op[2] = o2;
			op[3] = o3;
			op += 4;
		}
	}
}

internal sealed class NoopConverter : IConverter<float, float>
{
	public static readonly NoopConverter Instance = new();

	private static readonly NoopProcessor processor = new();

	private NoopConverter() { }

	public IConversionProcessor<float, float> Processor => processor;
	public IConversionProcessor<float, float> Processor3A => processor;
	public IConversionProcessor<float, float> Processor3X => processor;

	private sealed unsafe class NoopProcessor : IConversionProcessor<float, float>
	{
		void IConversionProcessor.ConvertLine(byte* istart, byte* ostart, nint cb)
		{
			if (istart != ostart)
				Buffer.MemoryCopy(istart, ostart, cb, cb);
		}
	}
}

internal sealed unsafe class UQ15Converter<TRng> : IConverter<ushort, byte> where TRng : EncodingRange
{
	public static readonly IConverter<ushort, byte> Instance = new UQ15Converter<TRng>();

	private static readonly UQ15Processor processor = new();
	private static readonly UQ15Processor3A processor3A = new();

	private UQ15Converter() { }

	public IConversionProcessor<ushort, byte> Processor => processor;
	public IConversionProcessor<ushort, byte> Processor3A => processor3A;
	public IConversionProcessor<ushort, byte> Processor3X => throw new NotImplementedException();

	private sealed class UQ15Processor : IConversionProcessor<ushort, byte>
	{
		void IConversionProcessor.ConvertLine(byte* istart, byte* ostart, nint cb)
		{
			ushort* ip = (ushort*)istart, ipe = (ushort*)(istart + cb) - 4;
			byte* op = ostart;

			uint scale = typeof(TRng) == typeof(EncodingRange.Video) ? (uint)VideoLumaScale : byte.MaxValue;
			uint offs = typeof(TRng) == typeof(EncodingRange.Video) ? (uint)VideoLumaMin : byte.MinValue;

			while (ip <= ipe)
			{
				byte i0 = UnFix15ToByte(ip[0] * scale + offs);
				byte i1 = UnFix15ToByte(ip[1] * scale + offs);
				byte i2 = UnFix15ToByte(ip[2] * scale + offs);
				byte i3 = UnFix15ToByte(ip[3] * scale + offs);
				ip += 4;

				op[0] = i0;
				op[1] = i1;
				op[2] = i2;
				op[3] = i3;
				op += 4;
			}
			ipe += 4;

			while (ip < ipe)
			{
				op[0] = UnFix15ToByte(ip[0] * scale + offs);
				ip++;
				op++;
			}
		}
	}

	private sealed class UQ15Processor3A : IConversionProcessor<ushort, byte>
	{
		void IConversionProcessor.ConvertLine(byte* istart, byte* ostart, nint cb)
		{
			ushort* ip = (ushort*)istart, ipe = (ushort*)(istart + cb);
			byte* op = ostart;

			while (ip < ipe)
			{
				uint i3 = ip[3];
				if (i3 < (UQ15Round >> 8))
				{
					*(uint*)op = 0;
				}
				else
				{
					uint o3i = UQ15One * byte.MaxValue / i3;
					uint i0 = ip[0];
					uint i1 = ip[1];
					uint i2 = ip[2];

					byte o0 = UnFix15ToByte(i0 * o3i);
					byte o1 = UnFix15ToByte(i1 * o3i);
					byte o2 = UnFix15ToByte(i2 * o3i);
					byte o3 = UnFix15ToByte(i3 * byte.MaxValue);
					op[0] = o0;
					op[1] = o1;
					op[2] = o2;
					op[3] = o3;
				}

				ip += 4;
				op += 4;
			}
		}
	}
}

internal static unsafe class InvertConverter
{
	public static void InvertLine(byte* istart, nint cb)
	{
		byte* ip = istart, ipe = istart + cb;

#if HWINTRINSICS
		if (HWIntrinsics.IsSupported && cb >= HWIntrinsics.VectorCount<byte>())
			convertIntrinsic(ip, ipe);
		else
#endif
			convertScalar(ip, ipe);
	}

#if HWINTRINSICS
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	private static void convertIntrinsic(byte* ip, byte* ipe)
	{
		if (Avx2.IsSupported)
		{
			ipe -= Vector256<byte>.Count;
			var vlast = Avx.LoadVector256(ipe);
			var vmask = Avx2.CompareEqual(vlast, vlast);

			LoopTop:
			do
			{
				var vi = Avx2.Xor(vmask, Avx.LoadVector256(ip));

				Avx.Store(ip, vi);
				ip += Vector256<byte>.Count;
			}
			while (ip <= ipe);

			if (ip < ipe + Vector256<byte>.Count)
			{
				ip = ipe;
				Avx.Store(ip, vlast);
				goto LoopTop;
			}
		}
		else
		{
			ipe -= Vector128<byte>.Count;
			var vlast = Sse2.LoadVector128(ipe);
			var vmask = Sse2.CompareEqual(vlast, vlast);

			LoopTop:
			do
			{
				var vi = Sse2.LoadVector128(ip);

				Sse2.Store(ip, Sse2.Xor(vi, vmask));
				ip += Vector128<byte>.Count;
			}
			while (ip <= ipe);

			if (ip < ipe + Vector128<byte>.Count)
			{
				ip = ipe;
				Sse2.Store(ip, vlast);
				goto LoopTop;
			}
		}
	}
#endif

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	private static void convertScalar(byte* ip, byte* ipe)
	{
		while (ip <= ipe - sizeof(nuint))
		{
			*(nuint*)ip = ~*(nuint*)ip;
			ip += sizeof(nuint);
		}

		if (ip < ipe)
			*(uint*)ip = ~*(uint*)ip;
	}
}
