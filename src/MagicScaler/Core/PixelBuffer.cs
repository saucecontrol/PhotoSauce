// Copyright © Clinton Ingram and Contributors.  Licensed under the MIT License.

using System;
using System.Runtime.CompilerServices;

namespace PhotoSauce.MagicScaler;

internal sealed class PixelBuffer<T> : IDisposable where T : struct, BufferType
{
	private readonly T tval;

	private int capacity;
	private int start;
	private int loaded;
	private int read;

	private byte[]? buffArray;
	private byte buffOffset;

	public readonly int Stride;

	private int buffLength
	{
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		get => window + capacity * Stride;
	}

	private int window
	{
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		get
		{
			if (typeof(T) == typeof(BufferType.Windowed))
			{
				var wval = (BufferType.Windowed)(object)tval;
				return wval.Window;
			}

			return 0;
		}
	}

	private int consumed
	{
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		get => typeof(T) == typeof(BufferType.Windowed) ? loaded : read;
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		set
		{
			if (typeof(T) != typeof(BufferType.Windowed))
				read = value;
		}
	}

	public PixelBuffer(int minLines, int stride, T param = default)
	{
		capacity = minLines;
		Stride = stride;
		tval = param;
		read = typeof(T) == typeof(BufferType.Windowed) ? minLines : 0;
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	private (int, int) getValidRange() => typeof(T) == typeof(BufferType.Windowed) && loaded > read
		? (start + loaded - read, read)
		: (start, loaded);

	private Span<byte> init(int first, int lines)
	{
		if (buffArray is null)
		{
			if (capacity == 0)
				throw new ObjectDisposedException(nameof(PixelBuffer<T>));

			var buff = BufferPool.RentRawAligned(checked(window + Math.Max(capacity, lines) * Stride));

			buffArray = buff.Array!;
			buffOffset = (byte)buff.Offset;
			capacity = (buffArray.Length - buffOffset - window) / Stride;

			if (typeof(T) == typeof(BufferType.Sliding) || typeof(T) == typeof(BufferType.Windowed))
				Unsafe.InitBlockUnaligned(ref buffArray[(nuint)buffOffset], 0, (uint)buffLength);
		}
		else if (lines > capacity)
		{
			grow(0, 0);
		}

		start = first;
		loaded = lines;
		consumed = 0;

		return new Span<byte>(buffArray, buffOffset, window != 0 ? window : lines * Stride);
	}

	private void grow(int cbKeep, int cbKill)
	{
		var tbuff = BufferPool.RentRawAligned(checked(window + capacity * Stride * 2));

		if (cbKeep > 0)
			Unsafe.CopyBlockUnaligned(ref tbuff.Array![tbuff.Offset], ref buffArray![buffOffset + cbKill], (uint)cbKeep);

		BufferPool.ReturnRaw(new ArraySegment<byte>(buffArray!, buffOffset, buffLength));

		buffArray = tbuff.Array!;
		buffOffset = (byte)tbuff.Offset;
		capacity = (buffArray.Length - buffOffset - window) / Stride;

		if (typeof(T) == typeof(BufferType.Sliding) || typeof(T) == typeof(BufferType.Windowed))
			Unsafe.InitBlockUnaligned(ref buffArray[buffOffset + cbKeep], 0, (uint)(buffLength - cbKeep));
	}

	private unsafe void slide(int cbKeep, int cbKill)
	{
		fixed (byte* pb = &buffArray![(nuint)buffOffset])
			Buffer.MemoryCopy(pb + cbKill, pb, buffLength, cbKeep);
	}

	public Span<byte> PrepareLoad(scoped ref int first, scoped ref int lines)
	{
		var (firstValid, linesValid) = getValidRange();
		if (buffArray is null || first < firstValid || first > (firstValid + linesValid))
			return init(first, lines);

		int toLoad, toKeep;
		int next = first;
		if (first + lines <= start + capacity)
		{
			toKeep = loaded;
			toLoad = (first + lines) - (start + loaded);
			next = start + loaded;
		}
		else
		{
			int newStart = Math.Min(start + consumed, first);
			toKeep = Math.Max(start + loaded - newStart, 0);
			toLoad = (first + lines) - (newStart + toKeep);
			next += lines - toLoad;
			int toKill = loaded - toKeep;

			if (toKeep != 0)
			{
				int cbKeep = window == 0 ? toKeep * Stride : window - (Math.Min(lines, loaded) - toKeep) * Stride;
				int cbKill = toKill * Stride;

				if (capacity - toKeep < toLoad)
					grow(cbKeep, cbKill);
				else
					slide(cbKeep, cbKill);
			}

			start = newStart;
			consumed -= toKill;
		}

		loaded = toKeep + toLoad;

		if (typeof(T) == typeof(BufferType.Caching))
			return default;

		int reqlines = lines;
		lines = toLoad;
		first = next;

		return new Span<byte>(buffArray, buffOffset + (next - start) * Stride, window == 0 ? toLoad * Stride : window - (reqlines - toLoad) * Stride);
	}

	public Span<byte> PrepareLoad(int first, int lines)
	{
		if (typeof(T) == typeof(BufferType.Caching))
		{
			PrepareLoad(ref first, ref lines);

			int offset = first - start;
			return new Span<byte>(buffArray, buffOffset + offset * Stride, lines * Stride);
		}
		if (typeof(T) == typeof(BufferType.Sliding))
		{
			return PrepareLoad(ref first, ref lines);
		}

		throw new NotSupportedException();
	}

	public ReadOnlySpan<byte> PrepareRead(int first, int lines)
	{
		int offset = first - start;
		consumed = Math.Max(consumed, offset + lines);

		return new ReadOnlySpan<byte>(buffArray, buffOffset + offset * Stride, window != 0 ? window : lines * Stride);
	}

	public bool ContainsLine(int line) => ContainsRange(line, 1);

	public bool ContainsRange(int first, int lines)
	{
		var (firstValid, linesValid) = getValidRange();
		return first >= firstValid && first + lines <= firstValid + linesValid;
	}

	public void Reset()
	{
		start = loaded = 0;
		if (typeof(T) != typeof(BufferType.Windowed))
			read = 0;
	}

	public void Dispose()
	{
		if (buffArray is null)
			return;

		Reset();

		BufferPool.ReturnRaw(new ArraySegment<byte>(buffArray!, buffOffset, buffLength));
		buffArray = null;
		capacity = 0;
	}
}

internal interface BufferType
{
	public readonly struct Caching : BufferType { }
	public readonly struct Sliding : BufferType { }
	public readonly struct Windowed : BufferType { public readonly int Window; public Windowed(int w) => Window = w; }
}
