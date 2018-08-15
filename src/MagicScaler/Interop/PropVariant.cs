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

#pragma warning disable 0618 // VarEnum is obsolete

using System;
using System.Linq;
using System.Collections;
using System.Runtime.InteropServices;
using System.Runtime.CompilerServices;
using System.Diagnostics.CodeAnalysis;

namespace PhotoSauce.MagicScaler.Interop
{
	[SuppressMessage("Style", "IDE1006:Naming Styles")]
	[StructLayout(LayoutKind.Explicit)]
	internal struct UnmanagedPropVariant
	{
		[StructLayout(LayoutKind.Sequential)]
		internal struct PropVariantVector
		{
			public uint count;
			public IntPtr ptr;
		}

		[FieldOffset(0)] private ushort _vt;

		[FieldOffset(2)] private readonly ushort reserved1;
		[FieldOffset(4)] private readonly ushort reserved2;
		[FieldOffset(6)] private readonly ushort reserved3;

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

		public VarEnum vt { get =>(VarEnum)_vt; set => _vt = (ushort)value; }
	}

	internal sealed class PropVariant : IEquatable<PropVariant>
	{
#if !CUSTOM_MARSHAL
		public interface ICustomMarshaler { }
#endif

		public enum PropVariantMarshalType { Automatic, Ascii, Blob }

		private static VarEnum getUnmanagedType(object o, PropVariantMarshalType marshalType)
		{
			if (o is null) return VarEnum.VT_EMPTY;
			if (Marshal.IsComObject(o)) return VarEnum.VT_UNKNOWN;
			if (o is PropVariant pv) return pv.UnmanagedType;

			var type = o.GetType();
			if ((marshalType == PropVariantMarshalType.Blob ) && type.Equals(typeof(byte  []))) return VarEnum.VT_BLOB;
			if ((marshalType == PropVariantMarshalType.Ascii) && type.Equals(typeof(string  ))) return VarEnum.VT_LPSTR;
			if ((marshalType == PropVariantMarshalType.Ascii) && type.Equals(typeof(string[]))) return VarEnum.VT_LPSTR | VarEnum.VT_VECTOR;
			if (type.Equals(typeof(sbyte   ))) return VarEnum.VT_I1;
			if (type.Equals(typeof(sbyte []))) return VarEnum.VT_I1     | VarEnum.VT_VECTOR;
			if (type.Equals(typeof(byte    ))) return VarEnum.VT_UI1;
			if (type.Equals(typeof(byte  []))) return VarEnum.VT_UI1    | VarEnum.VT_VECTOR;
			if (type.Equals(typeof(short   ))) return VarEnum.VT_I2;
			if (type.Equals(typeof(short []))) return VarEnum.VT_I2     | VarEnum.VT_VECTOR;
			if (type.Equals(typeof(ushort  ))) return VarEnum.VT_UI2;
			if (type.Equals(typeof(ushort[]))) return VarEnum.VT_UI2    | VarEnum.VT_VECTOR;
			if (type.Equals(typeof(int     ))) return VarEnum.VT_I4;
			if (type.Equals(typeof(int   []))) return VarEnum.VT_I4     | VarEnum.VT_VECTOR;
			if (type.Equals(typeof(uint    ))) return VarEnum.VT_UI4;
			if (type.Equals(typeof(uint  []))) return VarEnum.VT_UI4    | VarEnum.VT_VECTOR;
			if (type.Equals(typeof(long    ))) return VarEnum.VT_I8;
			if (type.Equals(typeof(long  []))) return VarEnum.VT_I8     | VarEnum.VT_VECTOR;
			if (type.Equals(typeof(ulong   ))) return VarEnum.VT_UI8;
			if (type.Equals(typeof(ulong []))) return VarEnum.VT_UI8    | VarEnum.VT_VECTOR;
			if (type.Equals(typeof(float   ))) return VarEnum.VT_R4;
			if (type.Equals(typeof(float []))) return VarEnum.VT_R4     | VarEnum.VT_VECTOR;
			if (type.Equals(typeof(double  ))) return VarEnum.VT_R8;
			if (type.Equals(typeof(double[]))) return VarEnum.VT_R8     | VarEnum.VT_VECTOR;
			if (type.Equals(typeof(bool    ))) return VarEnum.VT_BOOL;
			if (type.Equals(typeof(bool  []))) return VarEnum.VT_BOOL   | VarEnum.VT_VECTOR;
			if (type.Equals(typeof(string  ))) return VarEnum.VT_LPWSTR;
			if (type.Equals(typeof(string[]))) return VarEnum.VT_LPWSTR | VarEnum.VT_VECTOR;
			if (type.Equals(typeof(DateTime))) return VarEnum.VT_FILETIME;

			throw new ArgumentException($"Value is not supported by {nameof(PropVariant)}", nameof(o));
		}

		public object Value { get; private set; }
		public PropVariantMarshalType MarshalType { get; private set; }
		public VarEnum UnmanagedType { get; private set; }

		public PropVariant() : this (null) { }

		public PropVariant(object value) : this(value, PropVariantMarshalType.Automatic) { }

		public PropVariant(object value, PropVariantMarshalType marshalType)
		{
			Value = value;
			MarshalType = marshalType;

			if ((value is Array) && value.GetType().Equals(typeof(object[])))
			{
				var objectArray = (object[])value;
				var typedArray = Array.CreateInstance(objectArray[0].GetType(), objectArray.Length);
				Array.Copy(objectArray, typedArray, objectArray.Length);

				Value = typedArray;
			}

			UnmanagedType = getUnmanagedType(Value, MarshalType);
		}

		public bool Equals(PropVariant other)
		{
			if (other is null)
				return false;

			if ((Value is Array) != (other.Value is Array))
				return false;

			if ((Value is Array))
				return ((IEnumerable)Value).Cast<object>().SequenceEqual(((IEnumerable)other.Value).Cast<object>());

			return Equals(Value, other.Value);
		}

		public static bool operator ==(PropVariant left, PropVariant right) => left?.Equals(right) ?? ReferenceEquals(left, right);
		public static bool operator !=(PropVariant left, PropVariant right) => !(left == right);

		public override bool Equals(object o) => Equals(o as PropVariant);
		public override int GetHashCode() => Value?.GetHashCode() ?? 0;
		public override string ToString() => $"{(UnmanagedType & ~VarEnum.VT_VECTOR)}: {(Value is Array ? string.Join(" ", (Array)Value) : Value)}";

		internal sealed class Marshaler : ICustomMarshaler
		{
			[DllImport("ole32", EntryPoint = "PropVariantClear", PreserveSig = false)]
			private extern static void propVariantClear([In, Out] IntPtr pvar);

			unsafe private static T[] toArrayOf<T>(UnmanagedPropVariant pv) where T : unmanaged
			{
				if (pv.vectorValue.count == 0)
					return Array.Empty<T>();

				var res = new T[pv.vectorValue.count];
				fixed (T* pres = &res[0])
					Unsafe.CopyBlock(pres, pv.vectorValue.ptr.ToPointer(), (uint)(res.Length * Unsafe.SizeOf<T>()));

				return res;
			}

			public static ICustomMarshaler GetInstance(string str) => new Marshaler();

			private PropVariant pv;

			public void CleanUpManagedData(object obj) { }

			public void CleanUpNativeData(IntPtr pNativeData)
			{
				propVariantClear(pNativeData);
				Marshal.FreeCoTaskMem(pNativeData);
			}

			public int GetNativeDataSize() => -1;

			unsafe public IntPtr MarshalManagedToNative(object o)
			{
				if (o is null)
					return IntPtr.Zero;

				var marshalType = PropVariantMarshalType.Automatic;
				var unmanagedType = getUnmanagedType(o, marshalType);
				if (o is PropVariant)
				{
					pv = (PropVariant)o;
					o = pv.Value;
					marshalType = pv.MarshalType;
				}

				int cbNative = Marshal.SizeOf<UnmanagedPropVariant>();
				var pNativeData = Marshal.AllocCoTaskMem(cbNative);
				Unsafe.InitBlock(pNativeData.ToPointer(), default, (uint)cbNative);

				if (o is null)
					return pNativeData;

				if (!(o is Array))
				{
					if (o is DateTime || o is string)
					{
						var upv = new UnmanagedPropVariant { vt = unmanagedType };
						if (o is DateTime dt)
							upv.int64Value = dt.ToFileTimeUtc();
						else
							upv.pointerValue = marshalType == PropVariantMarshalType.Ascii ? Marshal.StringToCoTaskMemAnsi((string)o) : Marshal.StringToCoTaskMemUni((string)o);

						Marshal.StructureToPtr(upv, pNativeData, false);
					}
					else
					{
						Marshal.GetNativeVariantForObject(o, pNativeData);
					}

					return pNativeData;
				}

				var type = o.GetType();
				if (type.Equals(typeof(byte  [])) || type.Equals(typeof(sbyte [])) ||
				    type.Equals(typeof(short [])) || type.Equals(typeof(ushort[])) ||
				    type.Equals(typeof(int   [])) || type.Equals(typeof(uint  [])) ||
				    type.Equals(typeof(long  [])) || type.Equals(typeof(ulong [])) ||
				    type.Equals(typeof(float [])) || type.Equals(typeof(double[])) ||
				    type.Equals(typeof(string[])))
				{
					var a = (Array)o;
					int bufflen = type.Equals(typeof(string[])) ? IntPtr.Size * a.Length : Buffer.ByteLength(a);
					var pNativeBuffer = Marshal.AllocCoTaskMem(bufflen);

					if (o is string[] sa)
					{
						for (int i = 0; i < sa.Length; i++)
						{
							var strPtr = marshalType == PropVariantMarshalType.Ascii ? Marshal.StringToCoTaskMemAnsi(sa[i]) : Marshal.StringToCoTaskMemUni(sa[i]);
							Marshal.WriteIntPtr(pNativeBuffer, IntPtr.Size * i, strPtr);
						}
					}
					else
					{
						var gch = GCHandle.Alloc(a, GCHandleType.Pinned);
						Unsafe.CopyBlockUnaligned(pNativeBuffer.ToPointer(), Marshal.UnsafeAddrOfPinnedArrayElement(a, 0).ToPointer(), (uint)bufflen);
						gch.Free();
					}

					var upv = new UnmanagedPropVariant { vt = unmanagedType };
					upv.vectorValue.count = (uint)a.Length;
					upv.vectorValue.ptr = pNativeBuffer;
					Marshal.StructureToPtr(upv, pNativeData, false);

					return pNativeData;
				}

				Marshal.FreeCoTaskMem(pNativeData);
				throw new NotImplementedException();
			}

			public object MarshalNativeToManaged(IntPtr pNativeData)
			{
				if ((pNativeData == IntPtr.Zero) || (pv is null))
					return null;

				var upv = Marshal.PtrToStructure<UnmanagedPropVariant>(pNativeData);
				pv.MarshalType = PropVariantMarshalType.Automatic;
				pv.UnmanagedType = upv.vt;

				if (!upv.vt.HasFlag(VarEnum.VT_VECTOR) && upv.vt != VarEnum.VT_BLOB)
				{
					switch (upv.vt)
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

					return pv;
				}

				var elementVt = upv.vt & ~VarEnum.VT_VECTOR;
				switch (elementVt)
				{
					case VarEnum.VT_I1:
						pv.Value = toArrayOf<sbyte>(upv);
						break;
					case VarEnum.VT_UI1:
						pv.Value = toArrayOf<byte>(upv);
						break;
					case VarEnum.VT_I2:
						pv.Value = toArrayOf<short>(upv);
						break;
					case VarEnum.VT_UI2:
						pv.Value = toArrayOf<ushort>(upv);
						break;
					case VarEnum.VT_I4:
						pv.Value = toArrayOf<int>(upv);
						break;
					case VarEnum.VT_UI4:
						pv.Value = toArrayOf<uint>(upv);
						break;
					case VarEnum.VT_I8:
						pv.Value = toArrayOf<long>(upv);
						break;
					case VarEnum.VT_UI8:
						pv.Value = toArrayOf<ulong>(upv);
						break;
					case VarEnum.VT_R4:
						pv.Value = toArrayOf<float>(upv);
						break;
					case VarEnum.VT_R8:
						pv.Value = toArrayOf<double>(upv);
						break;
					case VarEnum.VT_BLOB:
						pv.MarshalType = PropVariantMarshalType.Blob;
						pv.Value = toArrayOf<byte>(upv);
						break;
					case VarEnum.VT_LPSTR:
						pv.MarshalType = PropVariantMarshalType.Ascii;
						pv.Value = toArrayOf<IntPtr>(upv).ConvertAll(Marshal.PtrToStringAnsi);
						break;
					case VarEnum.VT_LPWSTR:
						pv.Value = toArrayOf<IntPtr>(upv).ConvertAll(Marshal.PtrToStringUni);
						break;
					default: throw new NotImplementedException();
				}

				return pv;
			}
		}
	}
}
