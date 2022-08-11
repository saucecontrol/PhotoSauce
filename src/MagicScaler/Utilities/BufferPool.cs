// Copyright © Clinton Ingram and Contributors.  Licensed under the MIT License.

using System;
using System.Buffers;
using System.Diagnostics;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Runtime.CompilerServices;

namespace PhotoSauce.MagicScaler
{
	internal static partial class BufferPool
	{
		private const byte marker = 0b_01010101;

		private static readonly int sharedPoolMax = hasLargeSharedPool() ? 1 << 30 : 1 << 20;
		private static readonly int largePoolMax = AppConfig.MaxPooledBufferSize > 0 ? AppConfig.MaxPooledBufferSize : hasLargeSharedPool() ? 1 << 30 : 1 << 24;
		private static readonly ArrayPool<byte> largeBytePool = largePoolMax > sharedPoolMax ? ArrayPool<byte>.Create(largePoolMax, 4) : ArrayPool<byte>.Shared;

		private static ArrayPool<byte> getBytePool(int length) => length <= sharedPoolMax ? ArrayPool<byte>.Shared : length <= largePoolMax ? largeBytePool : NoopArrayPool<byte>.Instance;

		private static byte[] rentBytes(int length) => getBytePool(length).Rent(length);

		private static void returnBytes(byte[] arr) => getBytePool(arr.Length).Return(arr);

		private static bool hasLargeSharedPool()
		{
			// Starting from .NET 6, the shared ArrayPool allows arrays up to length 2^30 to be pooled.
#if NET6_0_OR_GREATER
			return true;
#else
			string ver = RuntimeInformation.FrameworkDescription;
			return ver.Length > 6 && ver.StartsWith(".NET ", StringComparison.Ordinal) && char.IsNumber(ver[5]) && ver[5] != '5';
#endif
		}

		[Conditional("GUARDRAILS")]
		private static void addBoundsMarkers(ArraySegment<byte> buff)
		{
			var arr = buff.Array!;
			if (buff.Offset > 0)
				new Span<byte>(arr, 0, buff.Offset).Fill(marker);

			int end = buff.Offset + buff.Count;
			if (arr.Length > end)
				new Span<byte>(arr, end, arr.Length - end).Fill(marker);
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

			int end = buff.Offset + buff.Count;
			if (arr.Length > end)
			{
				int chkCnt = 0;
				int i = end;
				while (i < arr.Length && arr[i++] != marker)
					chkCnt++;

				if (chkCnt > 0)
					throw new AccessViolationException($"Buffer overrun detected! {chkCnt} byte(s) clobbered.");
			}
		}

		public static unsafe ArraySegment<byte> RentRaw(int length, bool clear = false)
		{
			var arr = rentBytes(length);

			var buff = new ArraySegment<byte>(arr, 0, length);
			addBoundsMarkers(buff);

			if (clear)
				Unsafe.InitBlock(ref arr.GetDataRef(), 0, (uint)length);

			return buff;
		}

		public static unsafe ArraySegment<byte> RentRawAligned(int length, bool clear = false)
		{
			int pad = HWIntrinsics.VectorCount<byte>() - sizeof(nuint);
			var arr = rentBytes(length + pad);

			nint mask = (nint)HWIntrinsics.VectorCount<byte>() - 1;
			nint offs = (mask + 1 - ((nint)Unsafe.AsPointer(ref arr.GetDataRef()) & mask)) & mask;

			var buff = new ArraySegment<byte>(arr, (int)offs, length);
			addBoundsMarkers(buff);

			if (clear)
				Unsafe.InitBlock(ref Unsafe.Add(ref arr.GetDataRef(), offs), 0, (uint)length);

			return buff;
		}

		public static void ReturnRaw(ArraySegment<byte> buff)
		{
			if (buff.Array is null)
				return;

			checkBounds(buff);

			returnBytes(buff.Array);
		}

		public static RentedBuffer<T> Rent<T>(int length, bool clear = false) where T : unmanaged =>
			RentedBuffer<T>.Wrap(RentRaw(length * Unsafe.SizeOf<T>(), clear));

		public static RentedBuffer<T> RentAligned<T>(int length, bool clear = false) where T : unmanaged =>
			RentedBuffer<T>.Wrap(RentRawAligned(length * Unsafe.SizeOf<T>(), clear));

		public static LocalBuffer<T> RentLocal<T>(int length, bool clear = false) where T : unmanaged =>
			LocalBuffer<T>.Wrap(RentRaw(length * Unsafe.SizeOf<T>(), clear));

		public static LocalBuffer<T> RentLocalAligned<T>(int length, bool clear = false) where T : unmanaged =>
			LocalBuffer<T>.Wrap(RentRawAligned(length * Unsafe.SizeOf<T>(), clear));

		public static LocalBuffer<T> WrapLocal<T>(Span<T> span) where T : unmanaged =>
			LocalBuffer<T>.Wrap(span);

		public readonly ref struct LocalBuffer<T> where T : unmanaged
		{
			private readonly ArraySegment<byte> buffer;
			private readonly Span<T> span;

			private LocalBuffer(ArraySegment<byte> buf, Span<T> spn)
			{
				buffer = buf;
				span = spn;
			}

			public static LocalBuffer<T> Wrap(Span<T> span) => new(default, span);

			public static LocalBuffer<T> Wrap(ArraySegment<byte> buff) => new(buff, MemoryMarshal.Cast<byte, T>(buff));

			public int Length => span.Length;
			public Span<T> Span => span;

			public ref T GetPinnableReference() => ref span.GetPinnableReference();

			public void Dispose() => ReturnRaw(buffer);
		}
	}

	internal readonly struct RentedBuffer<T> where T : unmanaged
	{
		private readonly ArraySegment<byte> buffer;

		private RentedBuffer(ArraySegment<byte> buff) => buffer = buff;

		public static RentedBuffer<T> Wrap(ArraySegment<byte> buff) => new(buff);

		public bool IsEmpty => buffer.Count == 0;
		public int Length => (int)((uint)buffer.Count / (uint)Unsafe.SizeOf<T>());
		public Span<T> Span => MemoryMarshal.Cast<byte, T>(buffer);

		public ref T GetPinnableReference() => ref Unsafe.As<byte, T>(ref buffer.Array![buffer.Offset]);

		public void Dispose() => BufferPool.ReturnRaw(buffer);
	}

#if !NET5_0_OR_GREATER
	internal static partial class BufferPool
	{
		public static LocalArray<T> RentLocalArray<T>(int length) where T : unmanaged =>
			LocalArray<T>.Rent(length);

		public readonly ref struct LocalArray<T> where T : unmanaged
		{
			private readonly T[] array;
			private readonly int length;

			private LocalArray(T[] arr, int len) => (array, length) = (arr, len);

			public static LocalArray<T> Rent(int len) => new(ArrayPool<T>.Shared.Rent(len), len);

			public T[] Array => array;
			public int Length => length;
			public Span<T> Span => array.AsSpan(0, length);

			public ref T GetPinnableReference() => ref array.GetDataRef();

			public void Dispose() => ArrayPool<T>.Shared.TryReturn(array);
		}
	}
#endif

	internal sealed class NoopArrayPool<T> : ArrayPool<T>
	{
		public static NoopArrayPool<T> Instance = new();

		public override T[] Rent(int minimumLength) =>
#if NET5_0_OR_GREATER
			GC.AllocateUninitializedArray<T>(minimumLength);
#else
			new T[minimumLength];
#endif

		public override void Return(T[] array, bool clearArray = false) { }
	}
}
