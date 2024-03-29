// Copyright © Tanner Gooding and Contributors. Licensed under the MIT License (MIT).
// See third-party-notices in the repository root for more information.

// Ported from um/wincodec.h in the Windows SDK for Windows 10.0.22000.0
// Original source is Copyright © Microsoft. All rights reserved.

using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace TerraFX.Interop.Windows;

[Guid("23BC3F0A-698B-4357-886B-F24D50671334")]
[NativeTypeName("struct IWICComponentInfo : IUnknown")]
[NativeInheritance("IUnknown")]
internal unsafe partial struct IWICComponentInfo : IWICComponentInfo.Interface
{
    public void** lpVtbl;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [return: NativeTypeName("HRESULT")]
    public HRESULT QueryInterface([NativeTypeName("const IID &")] Guid* riid, void** ppvObject)
    {
        return ((delegate* unmanaged[Stdcall]<IWICComponentInfo*, Guid*, void**, int>)(lpVtbl[0]))((IWICComponentInfo*)Unsafe.AsPointer(ref this), riid, ppvObject);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [return: NativeTypeName("ULONG")]
    public uint AddRef()
    {
        return ((delegate* unmanaged[Stdcall]<IWICComponentInfo*, uint>)(lpVtbl[1]))((IWICComponentInfo*)Unsafe.AsPointer(ref this));
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [return: NativeTypeName("ULONG")]
    public uint Release()
    {
        return ((delegate* unmanaged[Stdcall]<IWICComponentInfo*, uint>)(lpVtbl[2]))((IWICComponentInfo*)Unsafe.AsPointer(ref this));
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public HRESULT GetComponentType(WICComponentType* pType)
    {
        return ((delegate* unmanaged[Stdcall]<IWICComponentInfo*, WICComponentType*, int>)(lpVtbl[3]))((IWICComponentInfo*)Unsafe.AsPointer(ref this), pType);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public HRESULT GetCLSID([NativeTypeName("CLSID *")] Guid* pclsid)
    {
        return ((delegate* unmanaged[Stdcall]<IWICComponentInfo*, Guid*, int>)(lpVtbl[4]))((IWICComponentInfo*)Unsafe.AsPointer(ref this), pclsid);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public HRESULT GetSigningStatus([NativeTypeName("DWORD *")] uint* pStatus)
    {
        return ((delegate* unmanaged[Stdcall]<IWICComponentInfo*, uint*, int>)(lpVtbl[5]))((IWICComponentInfo*)Unsafe.AsPointer(ref this), pStatus);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public HRESULT GetAuthor(uint cchAuthor, [NativeTypeName("WCHAR *")] ushort* wzAuthor, uint* pcchActual)
    {
        return ((delegate* unmanaged[Stdcall]<IWICComponentInfo*, uint, ushort*, uint*, int>)(lpVtbl[6]))((IWICComponentInfo*)Unsafe.AsPointer(ref this), cchAuthor, wzAuthor, pcchActual);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public HRESULT GetVendorGUID(Guid* pguidVendor)
    {
        return ((delegate* unmanaged[Stdcall]<IWICComponentInfo*, Guid*, int>)(lpVtbl[7]))((IWICComponentInfo*)Unsafe.AsPointer(ref this), pguidVendor);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public HRESULT GetVersion(uint cchVersion, [NativeTypeName("WCHAR *")] ushort* wzVersion, uint* pcchActual)
    {
        return ((delegate* unmanaged[Stdcall]<IWICComponentInfo*, uint, ushort*, uint*, int>)(lpVtbl[8]))((IWICComponentInfo*)Unsafe.AsPointer(ref this), cchVersion, wzVersion, pcchActual);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public HRESULT GetSpecVersion(uint cchSpecVersion, [NativeTypeName("WCHAR *")] ushort* wzSpecVersion, uint* pcchActual)
    {
        return ((delegate* unmanaged[Stdcall]<IWICComponentInfo*, uint, ushort*, uint*, int>)(lpVtbl[9]))((IWICComponentInfo*)Unsafe.AsPointer(ref this), cchSpecVersion, wzSpecVersion, pcchActual);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public HRESULT GetFriendlyName(uint cchFriendlyName, [NativeTypeName("WCHAR *")] ushort* wzFriendlyName, uint* pcchActual)
    {
        return ((delegate* unmanaged[Stdcall]<IWICComponentInfo*, uint, ushort*, uint*, int>)(lpVtbl[10]))((IWICComponentInfo*)Unsafe.AsPointer(ref this), cchFriendlyName, wzFriendlyName, pcchActual);
    }

    public interface Interface : IUnknown.Interface
    {
        [VtblIndex(3)]
        HRESULT GetComponentType(WICComponentType* pType);

        [VtblIndex(4)]
        HRESULT GetCLSID([NativeTypeName("CLSID *")] Guid* pclsid);

        [VtblIndex(5)]
        HRESULT GetSigningStatus([NativeTypeName("DWORD *")] uint* pStatus);

        [VtblIndex(6)]
        HRESULT GetAuthor(uint cchAuthor, [NativeTypeName("WCHAR *")] ushort* wzAuthor, uint* pcchActual);

        [VtblIndex(7)]
        HRESULT GetVendorGUID(Guid* pguidVendor);

        [VtblIndex(8)]
        HRESULT GetVersion(uint cchVersion, [NativeTypeName("WCHAR *")] ushort* wzVersion, uint* pcchActual);

        [VtblIndex(9)]
        HRESULT GetSpecVersion(uint cchSpecVersion, [NativeTypeName("WCHAR *")] ushort* wzSpecVersion, uint* pcchActual);

        [VtblIndex(10)]
        HRESULT GetFriendlyName(uint cchFriendlyName, [NativeTypeName("WCHAR *")] ushort* wzFriendlyName, uint* pcchActual);
    }
}
