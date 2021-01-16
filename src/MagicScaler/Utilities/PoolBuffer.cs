// Copyright © Clinton Ingram and Contributors.  Licensed under the MIT License.

using System;
using System.Buffers;
using System.Runtime.InteropServices;
using System.Runtime.CompilerServices;

namespace PhotoSauce.MagicScaler
{
	internal readonly ref struct PoolBuffer<T> where T : struct
	{
		private readonly int length;
		private readonly byte[] array;

		public PoolBuffer(int length, bool clear = false)
		{
			this.length = length;
			array = ArrayPool<byte>.Shared.Rent(length * Unsafe.SizeOf<T>());

			if (clear)
				array.AsSpan(0, length).Clear();
		}

		public int Length => (int)((uint)length / (uint)Unsafe.SizeOf<T>());

		public Span<T> Span => MemoryMarshal.Cast<byte, T>(new Span<byte>(array, 0, length));

		public void Dispose() => ArrayPool<byte>.Shared.TryReturn(array);
	}

	internal readonly ref struct PoolArray<T> where T : struct
	{
		private readonly T[] array;
		private readonly int length;

		public PoolArray(int length, bool clear = false)
		{
			this.length = length;
			array = ArrayPool<T>.Shared.Rent(length);

			if (clear)
				array.AsSpan(0, length).Clear();
		}

		public T[] Array => array;

		public int Length => length;

		public Span<T> Span => new(array, 0, length);

		public void Dispose() => ArrayPool<T>.Shared.TryReturn(array);
	}
}
