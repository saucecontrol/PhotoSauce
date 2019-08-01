using System;
using System.Buffers;

namespace PhotoSauce.MagicScaler
{
	internal class PixelBuffer : IDisposable
	{
		public readonly int Stride;

		private readonly int minCapacity;
		private readonly int window;
		private int capacity;
		private int start;
		private int loaded;
		private int consumed;
		private byte[] buffer;

		public PixelBuffer(int minLines, int stride, int windowSize = 0)
		{
			minCapacity = minLines;
			window = windowSize;
			Stride = stride;
		}

		unsafe public Span<byte> PrepareLoad(ref int first, ref int lines, bool clear = false)
		{
			if (buffer is null || first < start || first > (start + loaded))
			{
				if (buffer is null)
				{
					buffer = ArrayPool<byte>.Shared.Rent(window != 0 ? window : Math.Max(minCapacity, lines) * Stride);
					capacity = (buffer.Length - window) / Stride;

					if (clear)
						buffer.AsSpan().Fill(0);
				}

				start = first;
				loaded = lines;
				consumed = 0;
				return new Span<byte>(buffer, 0, window != 0 ? window : lines * Stride);
			}

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
				int newStart = first;
				if (consumed < loaded)
					newStart -= loaded - consumed;

				toKeep = Math.Max(start + loaded - newStart, 0);
				toLoad = (first + lines) - (newStart + toKeep);
				first += lines - toLoad;
				int toKill = loaded - toKeep;
				int twKill = window == 0 ? 0 : Math.Min(minCapacity, loaded) - toKeep;

				if (capacity - toKeep < toLoad)
				{
					var tbuff = ArrayPool<byte>.Shared.Rent(buffer.Length * 2);

					if (toKeep > 0)
					fixed (byte* pb = &buffer[0], pt = &tbuff[0])
						Buffer.MemoryCopy(pb + toKill * Stride, pt, tbuff.Length, window != 0 ? window - twKill * Stride : toKeep * Stride);

					ArrayPool<byte>.Shared.Return(buffer);

					buffer = tbuff;
					capacity = (buffer.Length - window) / Stride;

					if (clear)
						buffer.AsSpan().Slice(toKeep * Stride).Fill(0);
				}
				else if (toKeep > 0)
				{
					fixed (byte* pb = &buffer[0])
						Buffer.MemoryCopy(pb + toKill * Stride, pb, buffer.Length, window != 0 ? window - twKill * Stride : toKeep * Stride);
				}

				start = newStart;
				consumed -= toKill;
			}

			int swindow = window == 0 ? 0 : window - (lines - toLoad) * Stride;
			int offset = first - start;
			lines = toLoad;
			loaded = toKeep + toLoad;
			return new Span<byte>(buffer, offset * Stride, window != 0 ? swindow : lines * Stride);
		}

		unsafe public ReadOnlySpan<byte> PrepareRead(int first, int lines)
		{
			int offset = first - start;
			consumed = Math.Max(consumed, offset + lines);

			return new ReadOnlySpan<byte>(buffer, offset * Stride, window != 0 ? window : lines * Stride);
		}

		public bool ContainsLine(int line) => line >= start && line < start + loaded;

		public void Dispose()
		{
			if (buffer is null)
				return;

			ArrayPool<byte>.Shared.Return(buffer);
			buffer = null;
			capacity = start = loaded = consumed = 0;
		}
	}
}
