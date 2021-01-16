// Copyright © Clinton Ingram and Contributors.  Licensed under the MIT License.

using System;
using System.Buffers;
using System.Diagnostics;
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
				var buff = BufferPool.Rent(window != 0 ? window + Math.Max(minCapacity, lines) * Stride : Math.Max(minCapacity, lines) * Stride, true);

				capacity = (buff.Array!.Length - buff.Offset - window) / Stride;
				buffer = new ArraySegment<byte>(buff.Array, buff.Offset, window + capacity * Stride);

				if (clear)
					Unsafe.InitBlock(ref buffer.Array![buffer.Offset], 0, (uint)buffer.Count);
			}

			start = first;
			loaded = lines;
			consumed = 0;

			return buffer.AsSpan(0, window != 0 ? window : lines * Stride);
		}

		private void grow(int cbKeep, int cbKill)
		{
			var tbuff = BufferPool.Rent(buffer.Count * 2, true);

			if (cbKeep > 0)
				Unsafe.CopyBlockUnaligned(ref tbuff.Array![tbuff.Offset], ref buffer.Array![buffer.Offset + cbKill], (uint)cbKeep);

			BufferPool.Return(buffer);

			capacity = (tbuff.Array!.Length - tbuff.Offset - window) / Stride;
			buffer = new ArraySegment<byte>(tbuff.Array, tbuff.Offset, window + capacity * Stride);

			if (clear)
				Unsafe.InitBlockUnaligned(ref buffer.Array![buffer.Offset + cbKeep], 0, (uint)(buffer.Count - cbKeep));
		}

		unsafe private void slide(int cbKeep, int cbKill)
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

			BufferPool.Return(buffer);
			buffer = default;
			capacity = 0;
		}
	}

	internal static class BufferPool
	{
		private const byte marker = 0b_01010101;

		[Conditional("GUARDRAILS")]
		private static void addBoundsMarkers(byte[] buff, int offs, int length)
		{
			if (offs > 0)
				new Span<byte>(buff, 0, offs).Fill(marker);

			if (buff.Length > length + offs)
				new Span<byte>(buff, length + offs, buff.Length - length - offs).Fill(marker);
		}

		[Conditional("GUARDRAILS")]
		private static void checkBounds(ArraySegment<byte> buff)
		{
			var arr = buff.Array!;
			if (buff.Offset > 0)
			{
				int chkCnt = 0;
				int i = buff.Offset;
				while (i > 0 && arr[--i] != marker)
					chkCnt++;

				if (chkCnt > 0)
					throw new AccessViolationException($"Buffer offset violation detected! {chkCnt} byte(s) clobbered.");
			}

			if (arr.Length > buff.Count)
			{
				int chkCnt = 0;
				int i = buff.Offset + buff.Count;
				while (i < arr.Length && arr[i++] != marker)
					chkCnt++;

				if (chkCnt > 0)
					throw new AccessViolationException($"Buffer overrun detected! {chkCnt} byte(s) clobbered.");
			}
		}

		unsafe public static ArraySegment<byte> Rent(int length, bool aligned = false)
		{
			int pad = aligned ? HWIntrinsics.VectorCount<byte>() - sizeof(nuint) : 0;
			var buff = ArrayPool<byte>.Shared.Rent(length + pad);

			int mask = aligned ? HWIntrinsics.VectorCount<byte>() - 1 : 0;
			int offs = (mask + 1 - ((int)Unsafe.AsPointer(ref buff[0]) & mask)) & mask;

			addBoundsMarkers(buff, offs, length);

			return new ArraySegment<byte>(buff, offs, length);
		}

		public static void Return(ArraySegment<byte> buff)
		{
			if (buff.Array is null)
				return;

			checkBounds(buff);

			ArrayPool<byte>.Shared.Return(buff.Array);
		}
	}
}
