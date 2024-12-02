// Copyright © Clinton Ingram and Contributors
// SPDX-License-Identifier: MIT

using System;
using System.Diagnostics;
using System.Buffers.Binary;
using System.Runtime.InteropServices;
using System.Runtime.CompilerServices;

namespace PhotoSauce.MagicScaler;

internal unsafe ref struct SpanBufferReader(ReadOnlySpan<byte> buff)
{
	private readonly ReadOnlySpan<byte> span = buff;
	private int pos;

	public readonly int Position => pos;

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public T Read<T>() where T : unmanaged
	{
		Debug.Assert((uint)span.Length >= (uint)(pos + sizeof(T)));

		T val = Unsafe.ReadUnaligned<T>(ref Unsafe.Add(ref MemoryMarshal.GetReference(span), (nint)(uint)pos));
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

internal unsafe ref struct SpanBufferWriter(Span<byte> buff)
{
	private readonly Span<byte> span = buff;
	private int pos;

	public readonly int Position => pos;

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public void Write<T>(T val) where T : unmanaged
	{
		Unsafe.WriteUnaligned(ref Unsafe.Add(ref MemoryMarshal.GetReference(span), (nint)(uint)pos), val);
		pos += sizeof(T);
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public void Write<T>(ReadOnlySpan<T> val) where T : unmanaged
	{
		MemoryMarshal.AsBytes(val).CopyTo(span[pos..]);
		pos += val.Length;
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public void TryWrite<T>(ReadOnlySpan<T> val) where T : unmanaged
	{
		int len = Math.Min(span.Length - pos, MemoryMarshal.AsBytes(val).Length);

		MemoryMarshal.AsBytes(val)[..len].CopyTo(span[pos..]);
		pos += val.Length;
	}

	public void Advance(int size) => pos += size;
}

internal static class BufferUtil
{
	public static SpanBufferReader AsReader(this scoped in ReadOnlySpan<byte> span, Range range) => new(span[range]);

	public static SpanBufferReader AsReader(this scoped in ReadOnlySpan<byte> span, int offset, int length) => new(span.Slice(offset, length));

	public static SpanBufferWriter AsWriter(this scoped in Span<byte> span, Range range) => new(span[range]);

	public static SpanBufferWriter AsWriter(this scoped in Span<byte> span, int offset, int length) => new(span.Slice(offset, length));

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static unsafe T ReverseEndianness<T>(T val) where T : unmanaged
	{
		if (typeof(T) == typeof(Rational) || typeof(T) == typeof(SRational))
		{
			var (n, d) = UnsafeUtil.BitCast<T, Rational>(val);
			return UnsafeUtil.BitCast<Rational, T>(new Rational(BinaryPrimitives.ReverseEndianness(n), BinaryPrimitives.ReverseEndianness(d)));
		}

		if (sizeof(T) == sizeof(ushort))
			return UnsafeUtil.BitCast<ushort, T>(BinaryPrimitives.ReverseEndianness(UnsafeUtil.BitCast<T, ushort>(val)));
		if (sizeof(T) == sizeof(uint))
			return UnsafeUtil.BitCast<uint, T>(BinaryPrimitives.ReverseEndianness(UnsafeUtil.BitCast<T, uint>(val)));
		if (sizeof(T) == sizeof(ulong))
			return UnsafeUtil.BitCast<ulong, T>(BinaryPrimitives.ReverseEndianness(UnsafeUtil.BitCast<T, ulong>(val)));

		throw new ArgumentException($"Reverse not implemented for {typeof(T).Name}", nameof(T));
	}
}
