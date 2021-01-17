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

		//FieldDesc layout:
		// m_pMTOfEnclosingClass PTR_MethodTable
		// m_dword1 union
		// m_dword2 union
		//   m_dwOffset : 27
		//   m_type     : 5
		//see https://github.com/dotnet/runtime/blob/master/src/coreclr/vm/field.h
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static nint GetFieldOffset(this FieldInfo fi) =>
			*(int*)((byte*)fi.FieldHandle.Value + sizeof(nint) + sizeof(int)) & ((1 << 27) - 1);

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static ref T GetFieldRef<T>(object o, nint offset) =>
			ref Unsafe.As<byte, T>(ref Unsafe.AddByteOffset(ref Unsafe.As<RawData>(o).Bytes, offset));

		private sealed class RawData { public byte Bytes; }
	}
}
