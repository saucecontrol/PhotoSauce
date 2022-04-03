// Copyright © Clinton Ingram and Contributors.  Licensed under the MIT License.

using System;
using System.Diagnostics;
using System.Buffers.Binary;
using System.Runtime.InteropServices;
using System.Runtime.CompilerServices;

namespace PhotoSauce.MagicScaler;

internal unsafe ref struct SpanBufferReader
{
	private readonly ReadOnlySpan<byte> span;
	private nint pos;

	public readonly int Position => (int)pos;

	public SpanBufferReader(ReadOnlySpan<byte> buff)
	{
		span = buff;
		pos = default;
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public T Read<T>() where T : unmanaged
	{
		Debug.Assert((uint)span.Length >= (uint)(pos + sizeof(T)));

		T val = Unsafe.ReadUnaligned<T>(ref Unsafe.Add(ref MemoryMarshal.GetReference(span), pos));
		pos += sizeof(T);

		return val;
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public T Read<T>(bool bswap) where T : unmanaged
	{
		T val = Read<T>();
		if (sizeof(T) > sizeof(byte) && bswap)
			val = BufferUtil.ReverseEndianness(val);

		return val;
	}
}

internal unsafe ref struct SpanBufferWriter
{
	private readonly Span<byte> span;
	private nint pos;

	public readonly int Position => (int)pos;

	public SpanBufferWriter(Span<byte> buff)
	{
		span = buff;
		pos = default;
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public void Write<T>(T val) where T : unmanaged
	{
		Debug.Assert((uint)span.Length >= (uint)(pos + sizeof(T)));

		Unsafe.WriteUnaligned(ref Unsafe.Add(ref MemoryMarshal.GetReference(span), pos), val);
		pos += sizeof(T);
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public void Write<T>(ReadOnlySpan<T> val) where T : unmanaged
	{
		Debug.Assert((uint)span.Length >= (uint)(pos + MemoryMarshal.AsBytes(val).Length));

		MemoryMarshal.AsBytes(val).CopyTo(span[(int)pos..]);
		pos += val.Length;
	}
}

internal static class BufferUtil
{
	public static SpanBufferReader AsReader(this in ReadOnlySpan<byte> span, Range range) => new(span[range]);

	public static SpanBufferReader AsReader(this in ReadOnlySpan<byte> span, int offset, int length) => new(span.Slice(offset, length));

	public static SpanBufferWriter AsWriter(this in Span<byte> span, Range range) => new(span[range]);

	public static SpanBufferWriter AsWriter(this in Span<byte> span, int offset, int length) => new(span.Slice(offset, length));

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static unsafe T ReverseEndianness<T>(T val) where T : unmanaged
	{
		if (typeof(T) == typeof(Rational) || typeof(T) == typeof(SRational))
		{
			var (n, d) = Unsafe.As<T, (uint, uint)>(ref Unsafe.AsRef(val));
			return Unsafe.As<(uint, uint), T>(ref Unsafe.AsRef((BinaryPrimitives.ReverseEndianness(n), BinaryPrimitives.ReverseEndianness(d))));
		}

		if (sizeof(T) == sizeof(ushort))
			return Unsafe.As<ushort, T>(ref Unsafe.AsRef(BinaryPrimitives.ReverseEndianness(Unsafe.As<T, ushort>(ref Unsafe.AsRef(val)))));
		if (sizeof(T) == sizeof(uint))
			return Unsafe.As<uint, T>(ref Unsafe.AsRef(BinaryPrimitives.ReverseEndianness(Unsafe.As<T, uint>(ref Unsafe.AsRef(val)))));
		if (sizeof(T) == sizeof(ulong))
			return Unsafe.As<ulong, T>(ref Unsafe.AsRef(BinaryPrimitives.ReverseEndianness(Unsafe.As<T, ulong>(ref Unsafe.AsRef(val)))));

		throw new ArgumentException($"Reverse not implemented for {typeof(T).Name}", nameof(T));
	}
}
