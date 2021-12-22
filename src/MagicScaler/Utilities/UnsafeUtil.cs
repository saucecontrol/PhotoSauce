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
#pragma warning disable CS0649 // fields are never initialized
		private sealed class RawArrayData { public nuint LengthPadded; public byte Data; }
#pragma warning restore CS0649

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static ref T GetDataRef<T>(this T[] array) =>
#if NET5_0_OR_GREATER
			ref MemoryMarshal.GetArrayDataReference(array);
#else
			ref Unsafe.As<byte, T>(ref Unsafe.As<RawArrayData>(array).Data);
#endif

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static nint ByteOffset<T>(T* tgt, T* cur) where T : unmanaged =>
			Unsafe.ByteOffset(ref *tgt, ref *cur);

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static T* SubtractOffset<T>(T* ptr, nint offset) where T : unmanaged =>
			(T*)Unsafe.AsPointer(ref Unsafe.SubtractByteOffset(ref *ptr, offset));

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static nint ConvertOffset<TFrom, TTo>(nint offset) where TFrom : unmanaged where TTo : unmanaged
		{
			if (sizeof(TFrom) > sizeof(TTo))
				return offset / (sizeof(TFrom) / sizeof(TTo));
			else if (sizeof(TFrom) < sizeof(TTo))
				return offset * (sizeof(TTo) / sizeof(TFrom));
			else
				return offset;
		}

#if !NET5_0_OR_GREATER
		public static T CreateMethodDelegate<T>(this Type t, string method) where T : Delegate =>
			(T)t.GetMethod(method, BindingFlags.NonPublic | BindingFlags.Instance)!.CreateDelegate(typeof(T), null);
#endif

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static Guid* GetAddressOf(in this Guid val) => (Guid*)Unsafe.AsPointer(ref Unsafe.AsRef(val));

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static byte* GetAddressOf(this ReadOnlySpan<byte> val) => (byte*)Unsafe.AsPointer(ref MemoryMarshal.GetReference(val));
	}
}
