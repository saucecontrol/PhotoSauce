// Copyright © Clinton Ingram and Contributors.  Licensed under the MIT License.

using System;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Runtime.CompilerServices;

namespace PhotoSauce.MagicScaler
{
	internal static unsafe class UnsafeUtil
	{
		private sealed class RawData { public byte Bytes; }

		[StructLayout(LayoutKind.Sequential)]
		private sealed class LayoutData { public nint Nint; public int Int; public byte Byte1; public byte Byte2; }

		// Check the assumption that we know the layout of FieldDesc
		// as described in https://github.com/dotnet/runtime/blob/main/src/coreclr/vm/field.h
		// m_pMTOfEnclosingClass PTR_MethodTable
		// m_dword1 union
		// m_dword2 union
		//   m_dwOffset : 27
		//   m_type     : 5
		public static bool IsFieldDescLayoutKnown =
			typeof(LayoutData).GetField(nameof(LayoutData.Nint))!.GetFieldOffset() == 0 &&
			typeof(LayoutData).GetField(nameof(LayoutData.Byte2))!.GetFieldOffset() == sizeof(nint) + sizeof(int) + sizeof(byte);

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static nint GetFieldOffset(this FieldInfo fi) =>
			*(int*)((byte*)fi.FieldHandle.Value + sizeof(nint) + sizeof(int)) & ((1 << 27) - 1);

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

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static ref T GetFieldRef<T>(object o, nint offset) where T : struct =>
			ref Unsafe.As<byte, T>(ref Unsafe.AddByteOffset(ref Unsafe.As<RawData>(o).Bytes, offset));

#if !NET5_0_OR_GREATER
		public static T CreateMethodDelegate<T>(this Type t, string method) where T : Delegate =>
			(T)t.GetMethod(method)!.CreateDelegate(typeof(T), null);
#endif
	}
}
