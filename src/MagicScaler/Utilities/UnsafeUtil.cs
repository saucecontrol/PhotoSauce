// Copyright © Clinton Ingram and Contributors.  Licensed under the MIT License.

using System;
using System.Runtime.InteropServices;
using System.Runtime.CompilerServices;
#if !NET5_0_OR_GREATER
using System.Reflection;
#endif

namespace PhotoSauce.MagicScaler
{
	internal static unsafe class UnsafeUtil
	{
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static ref T GetDataRef<T>(this T[] array) =>
#if NET5_0_OR_GREATER
			ref MemoryMarshal.GetArrayDataReference(array);
#elif NETFRAMEWORK
			ref sizeof(nuint) == sizeof(ulong) ? ref getRefOrNull(array) : ref array[0];
#else
			ref getRefOrNull(array);
#endif

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static nuint ByteOffset<T>(T* tgt, T* cur) where T : unmanaged =>
			(nuint)(nint)Unsafe.ByteOffset(ref *tgt, ref *cur);

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static T* SubtractOffset<T>(T* ptr, nuint offset) where T : unmanaged =>
			(T*)Unsafe.AsPointer(ref Unsafe.SubtractByteOffset(ref *ptr, (nint)offset));

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static nuint ConvertOffset<TFrom, TTo>(nuint offset) where TFrom : unmanaged where TTo : unmanaged
		{
			if (sizeof(TFrom) > sizeof(TTo))
				return offset / (nuint)(sizeof(TFrom) / sizeof(TTo));
			else if (sizeof(TFrom) < sizeof(TTo))
				return offset * (nuint)(sizeof(TTo) / sizeof(TFrom));
			else
				return offset;
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static Guid* GetAddressOf(this in Guid val) => (Guid*)Unsafe.AsPointer(ref Unsafe.AsRef(val));

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static byte* GetAddressOf(this ReadOnlySpan<byte> val) => (byte*)Unsafe.AsPointer(ref MemoryMarshal.GetReference(val));

#if !NET5_0_OR_GREATER
		public static T CreateMethodDelegate<T>(this Type t, string method) where T : Delegate =>
			(T)t.GetMethod(method, BindingFlags.NonPublic | BindingFlags.Instance)!.CreateDelegate(typeof(T), null);

		private static ref T getRefOrNull<T>(this T[] array)
		{
			ref T ar = ref Unsafe.AsRef<T>(null);
			if (0 < (uint)array.Length)
				ar = ref array[0];

			return ref ar;
		}
#endif
	}
}
