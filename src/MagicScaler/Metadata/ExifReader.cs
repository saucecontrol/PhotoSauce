using System;
using System.Runtime.InteropServices;
using System.Runtime.CompilerServices;
using System.Diagnostics.CodeAnalysis;

namespace PhotoSauce.MagicScaler;

internal readonly record struct ExifItem(ushort ID, ExifType Type, uint Count, Range Range);

[StructLayout(LayoutKind.Auto)]
internal unsafe ref struct ExifReader
{
	private readonly ReadOnlySpan<byte> span;
	private readonly int firstTag;
	private readonly ushort count;
	private readonly bool swap;
	private ushort read;
	private ExifItem curr;

	public readonly int TagCount => count;

	private ExifReader(ReadOnlySpan<byte> src, int offset, bool bswap)
	{
		ushort cnt = MemoryMarshal.Read<ushort>(src[offset..]);
		if (bswap)
			cnt = BufferUtil.ReverseEndianness(cnt);

		span = src;
		firstTag = offset + sizeof(ushort);
		count = cnt;
		swap = bswap;
	}

	public static ExifReader Create(ReadOnlySpan<byte> span)
	{
		if (span.Length < ExifConstants.MinExifLength)
			return default;

		bool bswap = !BitConverter.IsLittleEndian;
		var rdr = span.AsReader(..ExifConstants.TiffHeadLength);

		uint mark = rdr.Read<uint>(bswap);
		if (mark is ExifConstants.MarkerII or ExifConstants.MarkerMM)
		{
			bswap = bswap == (mark is ExifConstants.MarkerII);
			uint offs = rdr.Read<uint>(bswap);

			if (offs <= (uint)(span.Length - ExifConstants.MinDirLength))
				return new(span, (int)offs, bswap);
		}

		return default;
	}

	public readonly T GetValue<T>(in ExifItem item) where T : unmanaged
	{
		T val = MemoryMarshal.Read<T>(span[item.Range]);
		if (sizeof(T) > sizeof(byte) && swap)
			val = BufferUtil.ReverseEndianness(val);

		return val;
	}

	public readonly void GetValues<T>(in ExifItem item, Span<T> dest) where T : unmanaged
	{
		int cv = Math.Min((int)item.Count, dest.Length);
		MemoryMarshal.Cast<byte, T>(span[item.Range])[..cv].CopyTo(dest);

		if (sizeof(T) == sizeof(byte) || !swap)
			return;

		foreach (ref T val in dest)
			val = BufferUtil.ReverseEndianness(val);
	}

	public readonly T CoerceValue<T>(in ExifItem item) where T : unmanaged
	{
		if (!UnsafeUtil.IsIntegerPrimitive<T>() && !UnsafeUtil.IsFloatPrimitive<T>())
			throw new ArgumentException($"Coercion not implemented for type {typeof(T).Name}.", nameof(T));

		int dlen = item.Type.GetElementSize();
		if (dlen == sizeof(T) && item.Type.IsFloating() == UnsafeUtil.IsFloatPrimitive<T>())
			return GetValue<T>(item);

		if (typeof(T) == typeof(float))
		{
			if (item.Type == ExifType.Double)
				return Unsafe.As<float, T>(ref Unsafe.AsRef((float)GetValue<double>(item)));

			return Unsafe.As<float, T>(ref Unsafe.AsRef((float)CoerceValue<int>(item)));
		}

		if (typeof(T) == typeof(double))
		{
			if (item.Type == ExifType.Float)
				return Unsafe.As<double, T>(ref Unsafe.AsRef((double)GetValue<float>(item)));

			return Unsafe.As<double, T>(ref Unsafe.AsRef((double)CoerceValue<long>(item)));
		}

		if (dlen >= sizeof(T))
		{
			if (item.Type == ExifType.Float)
				return Unsafe.As<int, T>(ref Unsafe.AsRef((int)GetValue<float>(item)));
			if (item.Type == ExifType.Double)
				return Unsafe.As<long, T>(ref Unsafe.AsRef((long)GetValue<double>(item)));
			if (dlen == sizeof(short))
				return Unsafe.As<short, T>(ref Unsafe.AsRef(GetValue<short>(item)));
			if (dlen == sizeof(int))
				return Unsafe.As<int, T>(ref Unsafe.AsRef(GetValue<int>(item)));

			return Unsafe.As<long, T>(ref Unsafe.AsRef(GetValue<long>(item)));
		}

		long val;
		if (item.Type == ExifType.Float)
			val = (long)GetValue<float>(item);
		else if (item.Type.IsSigned())
			val = dlen switch {
				sizeof(sbyte)  => GetValue<sbyte>(item),
				sizeof(short)  => GetValue<short>(item),
				sizeof(int)    => GetValue<int>(item),
				sizeof(long)   => GetValue<long>(item),
				_              => default
			};
		else
			val = (long)(dlen switch {
				sizeof(byte)   => GetValue<byte>(item),
				sizeof(ushort) => GetValue<ushort>(item),
				sizeof(uint)   => GetValue<uint>(item),
				sizeof(ulong)  => GetValue<ulong>(item),
				_              => default
			});

		return Unsafe.As<long, T>(ref Unsafe.AsRef(val));
	}

	public readonly ExifReader GetReader(in ExifItem item)
	{
		uint offs = GetValue<uint>(item);
		if (offs <= (uint)(span.Length - ExifConstants.MinDirLength))
			return new ExifReader(span, (int)offs, swap);

		return default;
	}

	public bool MoveNext()
	{
		int pos = read++;
		int offs = firstTag + pos * ExifConstants.MinTagLength;
		if (pos >= count || offs + ExifConstants.MinTagLength > span.Length)
			return false;

		var rdr = span.AsReader(offs, ExifConstants.MinTagLength);
		ushort id = rdr.Read<ushort>(swap);
		ushort dt = rdr.Read<ushort>(swap);
		uint cv = rdr.Read<uint>(swap);

		uint dpos = (uint)offs + (uint)rdr.Position;
		uint cb = cv * (uint)((ExifType)dt).GetElementSize();
		if (cb > sizeof(uint))
			dpos = rdr.Read<uint>(swap);

		uint dend = dpos + cb;
		if (cb == 0 || dend > (uint)span.Length)
			return false;

		curr = new ExifItem(id, (ExifType)dt, cv, (int)dpos..(int)dend);
		return true;
	}

	[UnscopedRef]
	public readonly ref readonly ExifItem Current => ref curr;
}
