// Copyright © Clinton Ingram and Contributors.  Licensed under the MIT License.

using System.Reflection;
using System.Runtime.CompilerServices;

namespace PhotoSauce.MagicScaler
{
	internal static unsafe class UnsafeUtil
	{
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
	}
}
