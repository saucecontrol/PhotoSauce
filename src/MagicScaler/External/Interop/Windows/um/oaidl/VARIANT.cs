// Copyright © Tanner Gooding and Contributors. Licensed under the MIT License (MIT).
// See third-party-notices in the repository root for more information.

// Ported from um/oaidl.h in the Windows SDK for Windows 10.0.22000.0
// Original source is Copyright © Microsoft. All rights reserved.

using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace TerraFX.Interop.Windows;

internal unsafe partial struct VARIANT
{
#if false
    [NativeTypeName("tagVARIANT::(anonymous union at C:/Program Files (x86)/Windows Kits/10/Include/10.0.22000.0/um/oaidl.h:478:5)")]
    public _Anonymous_e__Union Anonymous;

    public ref ushort vt
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get
        {
#if BUILTIN_SPAN
            return ref MemoryMarshal.GetReference(MemoryMarshal.CreateSpan(ref Anonymous.Anonymous.vt, 1));
#else
            return ref ((_Anonymous_e__Union._Anonymous_e__Struct*)Unsafe.AsPointer(ref Anonymous.Anonymous))->vt;
#endif
        }
    }

    public ref ushort wReserved1
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get
        {
#if BUILTIN_SPAN
            return ref MemoryMarshal.GetReference(MemoryMarshal.CreateSpan(ref Anonymous.Anonymous.wReserved1, 1));
#else
            return ref ((_Anonymous_e__Union._Anonymous_e__Struct*)Unsafe.AsPointer(ref Anonymous.Anonymous))->wReserved1;
#endif
        }
    }

    public ref ushort wReserved2
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get
        {
#if BUILTIN_SPAN
            return ref MemoryMarshal.GetReference(MemoryMarshal.CreateSpan(ref Anonymous.Anonymous.wReserved2, 1));
#else
            return ref ((_Anonymous_e__Union._Anonymous_e__Struct*)Unsafe.AsPointer(ref Anonymous.Anonymous))->wReserved2;
#endif
        }
    }

    public ref ushort wReserved3
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get
        {
#if BUILTIN_SPAN
            return ref MemoryMarshal.GetReference(MemoryMarshal.CreateSpan(ref Anonymous.Anonymous.wReserved3, 1));
#else
            return ref ((_Anonymous_e__Union._Anonymous_e__Struct*)Unsafe.AsPointer(ref Anonymous.Anonymous))->wReserved3;
#endif
        }
    }

    public ref long llVal
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get
        {
#if BUILTIN_SPAN
            return ref MemoryMarshal.GetReference(MemoryMarshal.CreateSpan(ref Anonymous.Anonymous.Anonymous.llVal, 1));
#else
            return ref ((_Anonymous_e__Union._Anonymous_e__Struct._Anonymous_e__Union*)Unsafe.AsPointer(ref Anonymous.Anonymous.Anonymous))->llVal;
#endif
        }
    }

    public ref int lVal
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get
        {
#if BUILTIN_SPAN
            return ref MemoryMarshal.GetReference(MemoryMarshal.CreateSpan(ref Anonymous.Anonymous.Anonymous.lVal, 1));
#else
            return ref ((_Anonymous_e__Union._Anonymous_e__Struct._Anonymous_e__Union*)Unsafe.AsPointer(ref Anonymous.Anonymous.Anonymous))->lVal;
#endif
        }
    }

    public ref byte bVal
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get
        {
#if BUILTIN_SPAN
            return ref MemoryMarshal.GetReference(MemoryMarshal.CreateSpan(ref Anonymous.Anonymous.Anonymous.bVal, 1));
#else
            return ref ((_Anonymous_e__Union._Anonymous_e__Struct._Anonymous_e__Union*)Unsafe.AsPointer(ref Anonymous.Anonymous.Anonymous))->bVal;
#endif
        }
    }

    public ref short iVal
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get
        {
#if BUILTIN_SPAN
            return ref MemoryMarshal.GetReference(MemoryMarshal.CreateSpan(ref Anonymous.Anonymous.Anonymous.iVal, 1));
#else
            return ref ((_Anonymous_e__Union._Anonymous_e__Struct._Anonymous_e__Union*)Unsafe.AsPointer(ref Anonymous.Anonymous.Anonymous))->iVal;
#endif
        }
    }

    public ref float fltVal
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get
        {
#if BUILTIN_SPAN
            return ref MemoryMarshal.GetReference(MemoryMarshal.CreateSpan(ref Anonymous.Anonymous.Anonymous.fltVal, 1));
#else
            return ref ((_Anonymous_e__Union._Anonymous_e__Struct._Anonymous_e__Union*)Unsafe.AsPointer(ref Anonymous.Anonymous.Anonymous))->fltVal;
#endif
        }
    }

    public ref double dblVal
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get
        {
#if BUILTIN_SPAN
            return ref MemoryMarshal.GetReference(MemoryMarshal.CreateSpan(ref Anonymous.Anonymous.Anonymous.dblVal, 1));
#else
            return ref ((_Anonymous_e__Union._Anonymous_e__Struct._Anonymous_e__Union*)Unsafe.AsPointer(ref Anonymous.Anonymous.Anonymous))->dblVal;
#endif
        }
    }

    public ref short boolVal
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get
        {
#if BUILTIN_SPAN
            return ref MemoryMarshal.GetReference(MemoryMarshal.CreateSpan(ref Anonymous.Anonymous.Anonymous.boolVal, 1));
#else
            return ref ((_Anonymous_e__Union._Anonymous_e__Struct._Anonymous_e__Union*)Unsafe.AsPointer(ref Anonymous.Anonymous.Anonymous))->boolVal;
#endif
        }
    }

    public ref short __OBSOLETE__VARIANT_BOOL
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get
        {
#if BUILTIN_SPAN
            return ref MemoryMarshal.GetReference(MemoryMarshal.CreateSpan(ref Anonymous.Anonymous.Anonymous.__OBSOLETE__VARIANT_BOOL, 1));
#else
            return ref ((_Anonymous_e__Union._Anonymous_e__Struct._Anonymous_e__Union*)Unsafe.AsPointer(ref Anonymous.Anonymous.Anonymous))->__OBSOLETE__VARIANT_BOOL;
#endif
        }
    }

    public ref int scode
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get
        {
#if BUILTIN_SPAN
            return ref MemoryMarshal.GetReference(MemoryMarshal.CreateSpan(ref Anonymous.Anonymous.Anonymous.scode, 1));
#else
            return ref ((_Anonymous_e__Union._Anonymous_e__Struct._Anonymous_e__Union*)Unsafe.AsPointer(ref Anonymous.Anonymous.Anonymous))->scode;
#endif
        }
    }

    public ref CY cyVal
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get
        {
#if BUILTIN_SPAN
            return ref MemoryMarshal.GetReference(MemoryMarshal.CreateSpan(ref Anonymous.Anonymous.Anonymous.cyVal, 1));
#else
            return ref ((_Anonymous_e__Union._Anonymous_e__Struct._Anonymous_e__Union*)Unsafe.AsPointer(ref Anonymous.Anonymous.Anonymous))->cyVal;
#endif
        }
    }

    public ref double date
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get
        {
#if BUILTIN_SPAN
            return ref MemoryMarshal.GetReference(MemoryMarshal.CreateSpan(ref Anonymous.Anonymous.Anonymous.date, 1));
#else
            return ref ((_Anonymous_e__Union._Anonymous_e__Struct._Anonymous_e__Union*)Unsafe.AsPointer(ref Anonymous.Anonymous.Anonymous))->date;
#endif
        }
    }

    public ref ushort* bstrVal
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get
        {
            return ref ((_Anonymous_e__Union._Anonymous_e__Struct._Anonymous_e__Union*)Unsafe.AsPointer(ref Anonymous.Anonymous.Anonymous))->bstrVal;
        }
    }

    public ref IUnknown* punkVal
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get
        {
            return ref ((_Anonymous_e__Union._Anonymous_e__Struct._Anonymous_e__Union*)Unsafe.AsPointer(ref Anonymous.Anonymous.Anonymous))->punkVal;
        }
    }

    public ref IDispatch* pdispVal
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get
        {
            return ref ((_Anonymous_e__Union._Anonymous_e__Struct._Anonymous_e__Union*)Unsafe.AsPointer(ref Anonymous.Anonymous.Anonymous))->pdispVal;
        }
    }

    public ref SAFEARRAY* parray
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get
        {
            return ref ((_Anonymous_e__Union._Anonymous_e__Struct._Anonymous_e__Union*)Unsafe.AsPointer(ref Anonymous.Anonymous.Anonymous))->parray;
        }
    }

    public ref byte* pbVal
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get
        {
            return ref ((_Anonymous_e__Union._Anonymous_e__Struct._Anonymous_e__Union*)Unsafe.AsPointer(ref Anonymous.Anonymous.Anonymous))->pbVal;
        }
    }

    public ref short* piVal
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get
        {
            return ref ((_Anonymous_e__Union._Anonymous_e__Struct._Anonymous_e__Union*)Unsafe.AsPointer(ref Anonymous.Anonymous.Anonymous))->piVal;
        }
    }

    public ref int* plVal
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get
        {
            return ref ((_Anonymous_e__Union._Anonymous_e__Struct._Anonymous_e__Union*)Unsafe.AsPointer(ref Anonymous.Anonymous.Anonymous))->plVal;
        }
    }

    public ref long* pllVal
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get
        {
            return ref ((_Anonymous_e__Union._Anonymous_e__Struct._Anonymous_e__Union*)Unsafe.AsPointer(ref Anonymous.Anonymous.Anonymous))->pllVal;
        }
    }

    public ref float* pfltVal
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get
        {
            return ref ((_Anonymous_e__Union._Anonymous_e__Struct._Anonymous_e__Union*)Unsafe.AsPointer(ref Anonymous.Anonymous.Anonymous))->pfltVal;
        }
    }

    public ref double* pdblVal
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get
        {
            return ref ((_Anonymous_e__Union._Anonymous_e__Struct._Anonymous_e__Union*)Unsafe.AsPointer(ref Anonymous.Anonymous.Anonymous))->pdblVal;
        }
    }

    public ref short* pboolVal
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get
        {
            return ref ((_Anonymous_e__Union._Anonymous_e__Struct._Anonymous_e__Union*)Unsafe.AsPointer(ref Anonymous.Anonymous.Anonymous))->pboolVal;
        }
    }

    public ref short* __OBSOLETE__VARIANT_PBOOL
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get
        {
            return ref ((_Anonymous_e__Union._Anonymous_e__Struct._Anonymous_e__Union*)Unsafe.AsPointer(ref Anonymous.Anonymous.Anonymous))->__OBSOLETE__VARIANT_PBOOL;
        }
    }

    public ref int* pscode
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get
        {
            return ref ((_Anonymous_e__Union._Anonymous_e__Struct._Anonymous_e__Union*)Unsafe.AsPointer(ref Anonymous.Anonymous.Anonymous))->pscode;
        }
    }

    public ref CY* pcyVal
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get
        {
            return ref ((_Anonymous_e__Union._Anonymous_e__Struct._Anonymous_e__Union*)Unsafe.AsPointer(ref Anonymous.Anonymous.Anonymous))->pcyVal;
        }
    }

    public ref double* pdate
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get
        {
            return ref ((_Anonymous_e__Union._Anonymous_e__Struct._Anonymous_e__Union*)Unsafe.AsPointer(ref Anonymous.Anonymous.Anonymous))->pdate;
        }
    }

    public ref ushort** pbstrVal
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get
        {
            return ref ((_Anonymous_e__Union._Anonymous_e__Struct._Anonymous_e__Union*)Unsafe.AsPointer(ref Anonymous.Anonymous.Anonymous))->pbstrVal;
        }
    }

    public ref IUnknown** ppunkVal
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get
        {
            return ref ((_Anonymous_e__Union._Anonymous_e__Struct._Anonymous_e__Union*)Unsafe.AsPointer(ref Anonymous.Anonymous.Anonymous))->ppunkVal;
        }
    }

    public ref IDispatch** ppdispVal
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get
        {
            return ref ((_Anonymous_e__Union._Anonymous_e__Struct._Anonymous_e__Union*)Unsafe.AsPointer(ref Anonymous.Anonymous.Anonymous))->ppdispVal;
        }
    }

    public ref SAFEARRAY** pparray
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get
        {
            return ref ((_Anonymous_e__Union._Anonymous_e__Struct._Anonymous_e__Union*)Unsafe.AsPointer(ref Anonymous.Anonymous.Anonymous))->pparray;
        }
    }

    public ref VARIANT* pvarVal
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get
        {
            return ref ((_Anonymous_e__Union._Anonymous_e__Struct._Anonymous_e__Union*)Unsafe.AsPointer(ref Anonymous.Anonymous.Anonymous))->pvarVal;
        }
    }

    public ref void* byref
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get
        {
            return ref ((_Anonymous_e__Union._Anonymous_e__Struct._Anonymous_e__Union*)Unsafe.AsPointer(ref Anonymous.Anonymous.Anonymous))->byref;
        }
    }

    public ref sbyte cVal
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get
        {
#if BUILTIN_SPAN
            return ref MemoryMarshal.GetReference(MemoryMarshal.CreateSpan(ref Anonymous.Anonymous.Anonymous.cVal, 1));
#else
            return ref ((_Anonymous_e__Union._Anonymous_e__Struct._Anonymous_e__Union*)Unsafe.AsPointer(ref Anonymous.Anonymous.Anonymous))->cVal;
#endif
        }
    }

    public ref ushort uiVal
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get
        {
#if BUILTIN_SPAN
            return ref MemoryMarshal.GetReference(MemoryMarshal.CreateSpan(ref Anonymous.Anonymous.Anonymous.uiVal, 1));
#else
            return ref ((_Anonymous_e__Union._Anonymous_e__Struct._Anonymous_e__Union*)Unsafe.AsPointer(ref Anonymous.Anonymous.Anonymous))->uiVal;
#endif
        }
    }

    public ref uint ulVal
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get
        {
#if BUILTIN_SPAN
            return ref MemoryMarshal.GetReference(MemoryMarshal.CreateSpan(ref Anonymous.Anonymous.Anonymous.ulVal, 1));
#else
            return ref ((_Anonymous_e__Union._Anonymous_e__Struct._Anonymous_e__Union*)Unsafe.AsPointer(ref Anonymous.Anonymous.Anonymous))->ulVal;
#endif
        }
    }

    public ref ulong ullVal
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get
        {
#if BUILTIN_SPAN
            return ref MemoryMarshal.GetReference(MemoryMarshal.CreateSpan(ref Anonymous.Anonymous.Anonymous.ullVal, 1));
#else
            return ref ((_Anonymous_e__Union._Anonymous_e__Struct._Anonymous_e__Union*)Unsafe.AsPointer(ref Anonymous.Anonymous.Anonymous))->ullVal;
#endif
        }
    }

    public ref int intVal
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get
        {
#if BUILTIN_SPAN
            return ref MemoryMarshal.GetReference(MemoryMarshal.CreateSpan(ref Anonymous.Anonymous.Anonymous.intVal, 1));
#else
            return ref ((_Anonymous_e__Union._Anonymous_e__Struct._Anonymous_e__Union*)Unsafe.AsPointer(ref Anonymous.Anonymous.Anonymous))->intVal;
#endif
        }
    }

    public ref uint uintVal
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get
        {
#if BUILTIN_SPAN
            return ref MemoryMarshal.GetReference(MemoryMarshal.CreateSpan(ref Anonymous.Anonymous.Anonymous.uintVal, 1));
#else
            return ref ((_Anonymous_e__Union._Anonymous_e__Struct._Anonymous_e__Union*)Unsafe.AsPointer(ref Anonymous.Anonymous.Anonymous))->uintVal;
#endif
        }
    }

    public ref DECIMAL* pdecVal
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get
        {
            return ref ((_Anonymous_e__Union._Anonymous_e__Struct._Anonymous_e__Union*)Unsafe.AsPointer(ref Anonymous.Anonymous.Anonymous))->pdecVal;
        }
    }

    public ref sbyte* pcVal
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get
        {
            return ref ((_Anonymous_e__Union._Anonymous_e__Struct._Anonymous_e__Union*)Unsafe.AsPointer(ref Anonymous.Anonymous.Anonymous))->pcVal;
        }
    }

    public ref ushort* puiVal
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get
        {
            return ref ((_Anonymous_e__Union._Anonymous_e__Struct._Anonymous_e__Union*)Unsafe.AsPointer(ref Anonymous.Anonymous.Anonymous))->puiVal;
        }
    }

    public ref uint* pulVal
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get
        {
            return ref ((_Anonymous_e__Union._Anonymous_e__Struct._Anonymous_e__Union*)Unsafe.AsPointer(ref Anonymous.Anonymous.Anonymous))->pulVal;
        }
    }

    public ref ulong* pullVal
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get
        {
            return ref ((_Anonymous_e__Union._Anonymous_e__Struct._Anonymous_e__Union*)Unsafe.AsPointer(ref Anonymous.Anonymous.Anonymous))->pullVal;
        }
    }

    public ref int* pintVal
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get
        {
            return ref ((_Anonymous_e__Union._Anonymous_e__Struct._Anonymous_e__Union*)Unsafe.AsPointer(ref Anonymous.Anonymous.Anonymous))->pintVal;
        }
    }

    public ref uint* puintVal
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get
        {
            return ref ((_Anonymous_e__Union._Anonymous_e__Struct._Anonymous_e__Union*)Unsafe.AsPointer(ref Anonymous.Anonymous.Anonymous))->puintVal;
        }
    }

    public ref void* pvRecord
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get
        {
            return ref ((_Anonymous_e__Union._Anonymous_e__Struct._Anonymous_e__Union._Anonymous_e__Struct*)Unsafe.AsPointer(ref Anonymous.Anonymous.Anonymous.Anonymous))->pvRecord;
        }
    }

    public ref IRecordInfo* pRecInfo
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get
        {
            return ref ((_Anonymous_e__Union._Anonymous_e__Struct._Anonymous_e__Union._Anonymous_e__Struct*)Unsafe.AsPointer(ref Anonymous.Anonymous.Anonymous.Anonymous))->pRecInfo;
        }
    }

    public ref DECIMAL decVal
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get
        {
#if BUILTIN_SPAN
            return ref MemoryMarshal.GetReference(MemoryMarshal.CreateSpan(ref Anonymous.decVal, 1));
#else
            return ref ((_Anonymous_e__Union*)Unsafe.AsPointer(ref Anonymous))->decVal;
#endif
        }
    }

    [StructLayout(LayoutKind.Explicit)]
    public unsafe partial struct _Anonymous_e__Union
    {
        [FieldOffset(0)]
        [NativeTypeName("tagVARIANT::(anonymous struct at C:/Program Files (x86)/Windows Kits/10/Include/10.0.22000.0/um/oaidl.h:480:9)")]
        public _Anonymous_e__Struct Anonymous;

        [FieldOffset(0)]
        public DECIMAL decVal;

        public unsafe partial struct _Anonymous_e__Struct
        {
#endif
            [NativeTypeName("VARTYPE")]
            public ushort vt;

            [NativeTypeName("WORD")]
            public ushort wReserved1;

            [NativeTypeName("WORD")]
            public ushort wReserved2;

            [NativeTypeName("WORD")]
            public ushort wReserved3;

            [NativeTypeName("tagVARIANT::(anonymous union at C:/Program Files (x86)/Windows Kits/10/Include/10.0.22000.0/um/oaidl.h:486:13)")]
            public _Anonymous_e__Union Anonymous;

            [StructLayout(LayoutKind.Explicit)]
            public unsafe partial struct _Anonymous_e__Union
            {
                [FieldOffset(0)]
                [NativeTypeName("LONGLONG")]
                public long llVal;

                [FieldOffset(0)]
                [NativeTypeName("LONG")]
                public int lVal;

                [FieldOffset(0)]
                public byte bVal;

                [FieldOffset(0)]
                public short iVal;

                [FieldOffset(0)]
                public float fltVal;

                [FieldOffset(0)]
                public double dblVal;

                [FieldOffset(0)]
                [NativeTypeName("VARIANT_BOOL")]
                public short boolVal;
#if false
                [FieldOffset(0)]
                [NativeTypeName("VARIANT_BOOL")]
                public short __OBSOLETE__VARIANT_BOOL;

                [FieldOffset(0)]
                [NativeTypeName("SCODE")]
                public int scode;

                [FieldOffset(0)]
                public CY cyVal;
#endif
                [FieldOffset(0)]
                [NativeTypeName("DATE")]
                public double date;

                [FieldOffset(0)]
                [NativeTypeName("BSTR")]
                public ushort* bstrVal;

                [FieldOffset(0)]
                public IUnknown* punkVal;
#if false
                [FieldOffset(0)]
                public IDispatch* pdispVal;

                [FieldOffset(0)]
                public SAFEARRAY* parray;
#endif
                [FieldOffset(0)]
                public byte* pbVal;

                [FieldOffset(0)]
                public short* piVal;

                [FieldOffset(0)]
                [NativeTypeName("LONG *")]
                public int* plVal;

                [FieldOffset(0)]
                [NativeTypeName("LONGLONG *")]
                public long* pllVal;

                [FieldOffset(0)]
                public float* pfltVal;

                [FieldOffset(0)]
                public double* pdblVal;

                [FieldOffset(0)]
                [NativeTypeName("VARIANT_BOOL *")]
                public short* pboolVal;
#if false
                [FieldOffset(0)]
                [NativeTypeName("VARIANT_BOOL *")]
                public short* __OBSOLETE__VARIANT_PBOOL;

                [FieldOffset(0)]
                [NativeTypeName("SCODE *")]
                public int* pscode;

                [FieldOffset(0)]
                public CY* pcyVal;
#endif
                [FieldOffset(0)]
                [NativeTypeName("DATE *")]
                public double* pdate;

                [FieldOffset(0)]
                [NativeTypeName("BSTR *")]
                public ushort** pbstrVal;

                [FieldOffset(0)]
                public IUnknown** ppunkVal;
#if false
                [FieldOffset(0)]
                public IDispatch** ppdispVal;

                [FieldOffset(0)]
                public SAFEARRAY** pparray;
#endif
                [FieldOffset(0)]
                public VARIANT* pvarVal;

                [FieldOffset(0)]
                [NativeTypeName("PVOID")]
                public void* byref;

                [FieldOffset(0)]
                [NativeTypeName("CHAR")]
                public sbyte cVal;

                [FieldOffset(0)]
                public ushort uiVal;

                [FieldOffset(0)]
                [NativeTypeName("ULONG")]
                public uint ulVal;

                [FieldOffset(0)]
                [NativeTypeName("ULONGLONG")]
                public ulong ullVal;

                [FieldOffset(0)]
                public int intVal;

                [FieldOffset(0)]
                public uint uintVal;
#if false
                [FieldOffset(0)]
                public DECIMAL* pdecVal;
#endif
                [FieldOffset(0)]
                [NativeTypeName("CHAR *")]
                public sbyte* pcVal;

                [FieldOffset(0)]
                public ushort* puiVal;

                [FieldOffset(0)]
                [NativeTypeName("ULONG *")]
                public uint* pulVal;

                [FieldOffset(0)]
                [NativeTypeName("ULONGLONG *")]
                public ulong* pullVal;

                [FieldOffset(0)]
                public int* pintVal;

                [FieldOffset(0)]
                public uint* puintVal;
#if false
                [FieldOffset(0)]
                [NativeTypeName("tagVARIANT::(anonymous struct at C:/Program Files (x86)/Windows Kits/10/Include/10.0.22000.0/um/oaidl.h:533:17)")]
                public _Anonymous_e__Struct Anonymous;

                public unsafe partial struct _Anonymous_e__Struct
                {
                    [NativeTypeName("PVOID")]
                    public void* pvRecord;

                    public IRecordInfo* pRecInfo;
                }
#endif
            }
#if false
        }
    }
#endif
}
