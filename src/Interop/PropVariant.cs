// This file was originally part of WIC Tools, published under the Ms-PL license
// https://web.archive.org/web/20101223145710/http://code.msdn.microsoft.com/wictools/Project/License.aspx
// It has been modified from its original version.  Changes copyright Clinton Ingram.

//----------------------------------------------------------------------------------------
// THIS CODE AND INFORMATION IS PROVIDED "AS-IS" WITHOUT WARRANTY OF
// ANY KIND, EITHER EXPRESSED OR IMPLIED, INCLUDING BUT NOT LIMITED TO
// THE IMPLIED WARRANTIES OF MERCHANTABILITY AND/OR FITNESS FOR A
// PARTICULAR PURPOSE.
//
// Copyright (c) Microsoft Corporation. All rights reserved.
//----------------------------------------------------------------------------------------

using System;
using System.Linq;
using System.Collections;
using System.Runtime.InteropServices;

namespace PhotoSauce.MagicScaler.Interop
{
	[StructLayout(LayoutKind.Explicit, Size = 16)]
	internal struct UnmanagedPropVariant
	{
		[StructLayout(LayoutKind.Sequential)]
		internal struct PropVariantVector
		{
			public uint count;
			public IntPtr ptr;
		}

		[FieldOffset(0)] public ushort vt;

		[FieldOffset(2)] public ushort reserved1;
		[FieldOffset(4)] public ushort reserved2;
		[FieldOffset(6)] public ushort reserved3;

		[FieldOffset(8)] public sbyte sbyteValue;
		[FieldOffset(8)] public byte byteValue;
		[FieldOffset(8)] public short int16Value;
		[FieldOffset(8)] public ushort uint16Value;
		[FieldOffset(8)] public int int32Value;
		[FieldOffset(8)] public uint uint32Value;
		[FieldOffset(8)] public long int64Value;
		[FieldOffset(8)] public ulong uint64Value;
		[FieldOffset(8)] public float floatValue;
		[FieldOffset(8)] public double doubleValue;

		[FieldOffset(8)] public IntPtr pointerValue;
		[FieldOffset(8)] public PropVariantVector vectorValue;
	}

	internal sealed class PropVariant : IEquatable<PropVariant>
	{
		public object Value { get; private set; }
		public PropVariantMarshalType MarshalType { get; private set; }

		public PropVariant() : this (null) { }

		public PropVariant(object value) : this(value, PropVariantMarshalType.Automatic) { }

		public PropVariant(object value, PropVariantMarshalType marshalType)
		{
			MarshalType = marshalType;
			Value = value;

			if ((value is Array) && value.GetType().Equals(typeof(object[])))
			{
				var objectArray = (object[])value;
				var typedArray = Array.CreateInstance(objectArray[0].GetType(), objectArray.Length);
				Array.Copy(objectArray, typedArray, objectArray.Length);

				Value = typedArray;
			}

			GetUnmanagedType();
		}

		private VarEnum GetUnmanagedType()
		{
			if (Value == null) return VarEnum.VT_EMPTY;

			var type = Value.GetType();
			     if ((MarshalType == PropVariantMarshalType.Blob ) && type.Equals(typeof(byte  []))) return VarEnum.VT_BLOB;
			else if ((MarshalType == PropVariantMarshalType.Ascii) && type.Equals(typeof(string  ))) return VarEnum.VT_LPSTR;
			else if ((MarshalType == PropVariantMarshalType.Ascii) && type.Equals(typeof(string[]))) return VarEnum.VT_LPSTR | VarEnum.VT_VECTOR;
			else if (type.Equals(typeof(sbyte   ))) return VarEnum.VT_I1;
			else if (type.Equals(typeof(sbyte []))) return VarEnum.VT_I1     | VarEnum.VT_VECTOR;
			else if (type.Equals(typeof(byte    ))) return VarEnum.VT_UI1;
			else if (type.Equals(typeof(byte  []))) return VarEnum.VT_UI1    | VarEnum.VT_VECTOR;
			else if (type.Equals(typeof(short   ))) return VarEnum.VT_I2;
			else if (type.Equals(typeof(short []))) return VarEnum.VT_I2     | VarEnum.VT_VECTOR;
			else if (type.Equals(typeof(ushort  ))) return VarEnum.VT_UI2;
			else if (type.Equals(typeof(ushort[]))) return VarEnum.VT_UI2    | VarEnum.VT_VECTOR;
			else if (type.Equals(typeof(int     ))) return VarEnum.VT_I4;
			else if (type.Equals(typeof(int   []))) return VarEnum.VT_I4     | VarEnum.VT_VECTOR;
			else if (type.Equals(typeof(uint    ))) return VarEnum.VT_UI4;
			else if (type.Equals(typeof(uint  []))) return VarEnum.VT_UI4    | VarEnum.VT_VECTOR;
			else if (type.Equals(typeof(long    ))) return VarEnum.VT_I8;
			else if (type.Equals(typeof(long  []))) return VarEnum.VT_I8     | VarEnum.VT_VECTOR;
			else if (type.Equals(typeof(ulong   ))) return VarEnum.VT_UI8;
			else if (type.Equals(typeof(ulong []))) return VarEnum.VT_UI8    | VarEnum.VT_VECTOR;
			else if (type.Equals(typeof(float   ))) return VarEnum.VT_R4;
			else if (type.Equals(typeof(float []))) return VarEnum.VT_R4     | VarEnum.VT_VECTOR;
			else if (type.Equals(typeof(double  ))) return VarEnum.VT_R8;
			else if (type.Equals(typeof(double[]))) return VarEnum.VT_R8     | VarEnum.VT_VECTOR;
			else if (type.Equals(typeof(bool    ))) return VarEnum.VT_BOOL;
			else if (type.Equals(typeof(bool  []))) return VarEnum.VT_BOOL   | VarEnum.VT_VECTOR;
			else if (type.Equals(typeof(string  ))) return VarEnum.VT_LPWSTR;
			else if (type.Equals(typeof(string[]))) return VarEnum.VT_LPWSTR | VarEnum.VT_VECTOR;
			else if (type.Equals(typeof(DateTime))) return VarEnum.VT_FILETIME;
			else if (type.IsCOMObject)              return VarEnum.VT_UNKNOWN;

			throw new NotImplementedException();
		}

		public VarEnum UnmanagedType => GetUnmanagedType();

		public bool Equals(PropVariant other)
		{
			if (ReferenceEquals(other, null))
				return false;

			if ((Value is Array) != (other.Value is Array))
				return false;

			if ((Value is Array))
				return ((IEnumerable)Value).Cast<object>().SequenceEqual(((IEnumerable)other.Value).Cast<object>());

			return ReferenceEquals(Value, other.Value) || !ReferenceEquals(Value, null) && Value.Equals(other.Value);
		}

		public static bool operator ==(PropVariant pv1, PropVariant pv2) => ReferenceEquals(pv1, pv2) || !ReferenceEquals(pv1, null) && pv1.Equals(pv2);
		public static bool operator !=(PropVariant pv1, PropVariant pv2) => !(pv1 == pv2);

		public override bool Equals(object o) => Equals(o as PropVariant);
		public override int GetHashCode() => Value == null ? 0 : Value.GetHashCode();
		public override string ToString() => $"{(UnmanagedType & ~VarEnum.VT_VECTOR)}: {(Value is Array ? string.Join(" ", (Array)Value) : Value)}";

		internal enum PropVariantMarshalType { Automatic, Ascii, Blob }

		internal sealed class Marshaler : ICustomMarshaler
		{
			[DllImport("kernel32", EntryPoint="RtlZeroMemory", SetLastError=false)]
			private extern static void ZeroMemory(IntPtr dst, int length);

			[DllImport("ole32", PreserveSig = false)]
			private extern static void PropVariantClear([In, Out] IntPtr pvar);

			private static T[] ToArrayOf<T>(IntPtr p, int size) where T : struct
			{
				var res = new T[size];
				for (int i = 0; i < size; i++)
					res[i] = Marshal.PtrToStructure<T>(new IntPtr(p.ToInt64() + i * Marshal.SizeOf(typeof(T))));

				return res;
			}

			public static ICustomMarshaler GetInstance(string str) => new Marshaler();

			private PropVariant pv;

			private IntPtr AllocatePropVariant()
			{
				int size = Marshal.SizeOf(typeof(UnmanagedPropVariant));
				var pNativeData = Marshal.AllocCoTaskMem(size);
				ZeroMemory(pNativeData, size);

				return pNativeData;
			}

			public void CleanUpManagedData(object obj) { }

			public void CleanUpNativeData(IntPtr pNativeData)
			{
				PropVariantClear(pNativeData);
				Marshal.FreeCoTaskMem(pNativeData);
			}

			public int GetNativeDataSize() => -1;

			public IntPtr MarshalManagedToNative(object o)
			{
				if (o == null)
					return IntPtr.Zero;

				var marshalType = PropVariantMarshalType.Automatic;
				if (o is PropVariant)
				{
					pv = (PropVariant)o;
					o = pv.Value;
					marshalType = pv.MarshalType;
				}

				var pNativeData = AllocatePropVariant();
				if (o == null)
					return pNativeData;

				var type = o.GetType();
				if (!(o is Array))
				{
					if (marshalType == PropVariantMarshalType.Ascii)
					{
						var upv = new UnmanagedPropVariant();
						upv.vt = (ushort)VarEnum.VT_LPSTR;
						upv.pointerValue = Marshal.StringToCoTaskMemAnsi((string)o);
						Marshal.StructureToPtr(upv, pNativeData, false);
					}
					else if (o is string)
					{
						var upv = new UnmanagedPropVariant();
						upv.vt = (ushort)VarEnum.VT_LPWSTR;
						upv.pointerValue = Marshal.StringToCoTaskMemUni((string)o);
						Marshal.StructureToPtr(upv, pNativeData, false);
					}
					else if (o is DateTime)
					{
						var upv = new UnmanagedPropVariant();
						upv.vt = (ushort)VarEnum.VT_FILETIME;
						upv.int64Value = ((DateTime)o).ToFileTimeUtc();
						Marshal.StructureToPtr(upv, pNativeData, false);
					}
					else
					{
						Marshal.GetNativeVariantForObject(o, pNativeData);
					}
				}
				else if ((type.Equals(typeof(byte []))) || (type.Equals(typeof(sbyte []))) ||
				         (type.Equals(typeof(short[]))) || (type.Equals(typeof(ushort[]))) ||
				         (type.Equals(typeof(int  []))) || (type.Equals(typeof(uint  []))) ||
				         (type.Equals(typeof(long []))) || (type.Equals(typeof(ulong []))) ||
				         (type.Equals(typeof(float[]))) || (type.Equals(typeof(double[]))))
				{
					var a = (Array)o;
					int count = a.Length;
					int elementSize = Marshal.SizeOf(type.GetElementType());
					var pNativeBuffer = Marshal.AllocCoTaskMem(elementSize * count);

					var gch = GCHandle.Alloc(a, GCHandleType.Pinned);
					for (int i = 0; i < count; i++)
					{
						var pNativeValue = Marshal.UnsafeAddrOfPinnedArrayElement(a, i);
						for (int j = 0; j < elementSize; j++)
						{
							byte value = Marshal.ReadByte(pNativeValue, j);
							Marshal.WriteByte(pNativeBuffer, elementSize * i + j, value);
						}
					}
					gch.Free();

					var upv = new UnmanagedPropVariant();
					upv.vectorValue.count = (uint)count;
					upv.vectorValue.ptr = pNativeBuffer;
					upv.vt = (ushort)(pv ?? new PropVariant(o)).UnmanagedType;

					Marshal.StructureToPtr(upv, pNativeData, false);
				}
				else if (type.Equals(typeof(string[])))
				{
					int count = ((Array)o).Length;
					var pNativeBuffer = Marshal.AllocCoTaskMem(IntPtr.Size * count);

					for (int i = 0; i < count; i++)
					{
						var strPtr = Marshal.StringToCoTaskMemUni(((string[])o)[i]);
						Marshal.WriteIntPtr(pNativeBuffer, IntPtr.Size * i, strPtr);
					}

					var upv = new UnmanagedPropVariant();
					upv.vectorValue.count = (uint)count;
					upv.vectorValue.ptr = pNativeBuffer;
					upv.vt = (ushort)(pv ?? new PropVariant(o)).UnmanagedType;

					Marshal.StructureToPtr(upv, pNativeData, false);
				}
				else
				{
					throw new NotImplementedException();
				}

				return pNativeData;
			}

			public object MarshalNativeToManaged(IntPtr pNativeData)
			{
				if ((pNativeData == IntPtr.Zero) || (pv == null))
					return null;

				var upv = Marshal.PtrToStructure<UnmanagedPropVariant>(pNativeData);
				pv.MarshalType = PropVariantMarshalType.Automatic;

				if (((upv.vt & (ushort)VarEnum.VT_VECTOR) == 0) && (upv.vt != (ushort)VarEnum.VT_BLOB))
				{
					switch ((VarEnum)upv.vt)
					{
						case VarEnum.VT_EMPTY:
							pv.Value = null;
							break;
						case VarEnum.VT_CLSID:
							if (upv.vectorValue.ptr != IntPtr.Zero)
								pv.Value = Marshal.PtrToStructure<Guid>(upv.vectorValue.ptr);
							break;
						case VarEnum.VT_LPSTR:
							pv.MarshalType = PropVariantMarshalType.Ascii;
							pv.Value = Marshal.PtrToStringAnsi(upv.pointerValue);
							break;
						case VarEnum.VT_LPWSTR:
							pv.Value = Marshal.PtrToStringUni(upv.pointerValue);
							break;
						case VarEnum.VT_FILETIME:
							pv.Value = DateTime.FromFileTimeUtc(upv.int64Value);
							break;
						default:
							pv.Value = Marshal.GetObjectForNativeVariant(pNativeData);
							break;
					}
				}
				else
				{
					var elementVt = (VarEnum)(upv.vt & ~(ushort)VarEnum.VT_VECTOR);
					int count = (int)upv.vectorValue.count;

					switch (elementVt)
					{
						case VarEnum.VT_I1:
							pv.Value = ToArrayOf<sbyte>(upv.vectorValue.ptr, count);
							break;
						case VarEnum.VT_UI1:
							pv.Value = ToArrayOf<byte>(upv.vectorValue.ptr, count);
							break;
						case VarEnum.VT_I2:
							pv.Value = ToArrayOf<short>(upv.vectorValue.ptr, count);
							break;
						case VarEnum.VT_UI2:
							pv.Value = ToArrayOf<ushort>(upv.vectorValue.ptr, count);
							break;
						case VarEnum.VT_I4:
							pv.Value = ToArrayOf<int>(upv.vectorValue.ptr, count);
							break;
						case VarEnum.VT_UI4:
							pv.Value = ToArrayOf<uint>(upv.vectorValue.ptr, count);
							break;
						case VarEnum.VT_I8:
							pv.Value = ToArrayOf<long>(upv.vectorValue.ptr, count);
							break;
						case VarEnum.VT_UI8:
							pv.Value = ToArrayOf<ulong>(upv.vectorValue.ptr, count);
							break;
						case VarEnum.VT_R4:
							pv.Value = ToArrayOf<float>(upv.vectorValue.ptr, count);
							break;
						case VarEnum.VT_R8:
							pv.Value = ToArrayOf<double>(upv.vectorValue.ptr, count);
							break;
						case VarEnum.VT_BLOB:
							pv.MarshalType = PropVariantMarshalType.Blob;
							pv.Value = ToArrayOf<byte>(upv.vectorValue.ptr, count); ;
							break;
						case VarEnum.VT_LPSTR:
							pv.MarshalType = PropVariantMarshalType.Ascii;
							pv.Value = Array.ConvertAll(ToArrayOf<IntPtr>(upv.vectorValue.ptr, count), Marshal.PtrToStringAnsi);
							break;
						case VarEnum.VT_LPWSTR:
							pv.Value = Array.ConvertAll(ToArrayOf<IntPtr>(upv.vectorValue.ptr, count), Marshal.PtrToStringUni);
							break;
						default: throw new NotImplementedException();
					}
				}

				return null;
			}
		}
	}
}
