// Copyright © Clinton Ingram and Contributors.  Licensed under the MIT License.

using System;
using System.Runtime.CompilerServices;

namespace PhotoSauce.MagicScaler
{
	internal sealed class PixelBuffer : IDisposable
	{
		private readonly int minCapacity;
		private readonly int window;
		private readonly bool clear;

		private int capacity;
		private int start;
		private int loaded;
		private int consumed;
		private ArraySegment<byte> buffer;

		public readonly int Stride;

		public PixelBuffer(int minLines, int stride, bool clearNew = false, int windowSize = 0)
		{
			minCapacity = minLines;
			window = windowSize;
			clear = clearNew;
			Stride = stride;
		}

		private Span<byte> init(int first, int lines)
		{
			if (buffer.Array is null)
			{
				var buff = BufferPool.RentRawAligned(window + Math.Max(minCapacity, lines) * Stride, clear);

				capacity = (buff.Array!.Length - buff.Offset - window) / Stride;
				buffer = new ArraySegment<byte>(buff.Array, buff.Offset, window + capacity * Stride);
			}

			start = first;
			loaded = lines;
			consumed = 0;

			return buffer.AsSpan(0, window != 0 ? window : lines * Stride);
		}

		private void grow(int cbKeep, int cbKill)
		{
			var tbuff = BufferPool.RentRawAligned(buffer.Count * 2);

			if (cbKeep > 0)
				Unsafe.CopyBlockUnaligned(ref tbuff.Array![tbuff.Offset], ref buffer.Array![buffer.Offset + cbKill], (uint)cbKeep);

			BufferPool.ReturnRaw(buffer);

			capacity = (tbuff.Array!.Length - tbuff.Offset - window) / Stride;
			buffer = new ArraySegment<byte>(tbuff.Array, tbuff.Offset, window + capacity * Stride);

			if (clear)
				Unsafe.InitBlockUnaligned(ref buffer.Array![buffer.Offset + cbKeep], 0, (uint)(buffer.Count - cbKeep));
		}

		private unsafe void slide(int cbKeep, int cbKill)
		{
			fixed (byte* pb = &buffer.Array![buffer.Offset])
				Buffer.MemoryCopy(pb + cbKill, pb, buffer.Count, cbKeep);
		}

		public Span<byte> PrepareLoad(ref int first, ref int lines)
		{
			if (buffer.Array is null || first < start || first > (start + loaded))
				return init(first, lines);

			int toLoad;
			int toKeep;
			if (first + lines <= start + capacity)
			{
				toKeep = loaded;
				toLoad = (first + lines) - (start + loaded);
				first = start + loaded;
			}
			else
			{
				int newStart = Math.Min(start + consumed, first);
				toKeep = Math.Max(start + loaded - newStart, 0);
				toLoad = (first + lines) - (newStart + toKeep);
				first += lines - toLoad;
				int toKill = loaded - toKeep;

				if (toKeep != 0)
				{
					int cbKeep = window == 0 ? toKeep * Stride : window - (Math.Min(minCapacity, loaded) - toKeep) * Stride;
					int cbKill = toKill * Stride;

					if (capacity - toKeep < toLoad)
						grow(cbKeep, cbKill);
					else
						slide(cbKeep, cbKill);
				}

				start = newStart;
				consumed -= toKill;
			}

			lines = toLoad;
			loaded = toKeep + toLoad;

			return buffer.AsSpan((first - start) * Stride, window == 0 ? lines * Stride : window - (minCapacity - toLoad) * Stride);
		}

		public ReadOnlySpan<byte> PrepareRead(int first, int lines)
		{
			int offset = first - start;
			consumed = Math.Max(consumed, offset + lines);

			return new ReadOnlySpan<byte>(buffer.Array, buffer.Offset + offset * Stride, window != 0 ? window : lines * Stride);
		}

		public bool ContainsLine(int line) => line >= start && line < start + loaded;

		public bool ContainsRange(int first, int lines) => first >= start && first + lines <= start + loaded;

		public void Reset() => start = loaded = consumed = 0;

		public void Dispose()
		{
			Reset();

			BufferPool.ReturnRaw(buffer);
			buffer = default;
			capacity = 0;
		}
	}
}
