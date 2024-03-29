// Copyright © Tanner Gooding and Contributors. Licensed under the MIT License (MIT).
// See third-party-notices in the repository root for more information.

// Ported from um/wincodec.h in the Windows SDK for Windows 10.0.22000.0
// Original source is Copyright © Microsoft. All rights reserved.

using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace TerraFX.Interop.Windows;

[Guid("A721791A-0DEF-4D06-BD91-2118BF1DB10B")]
[NativeTypeName("struct IWICMetadataQueryWriter : IWICMetadataQueryReader")]
[NativeInheritance("IWICMetadataQueryReader")]
internal unsafe partial struct IWICMetadataQueryWriter : IWICMetadataQueryWriter.Interface
{
    public void** lpVtbl;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public HRESULT QueryInterface([NativeTypeName("const IID &")] Guid* riid, void** ppvObject)
    {
        return ((delegate* unmanaged[Stdcall]<IWICMetadataQueryWriter*, Guid*, void**, int>)(lpVtbl[0]))((IWICMetadataQueryWriter*)Unsafe.AsPointer(ref this), riid, ppvObject);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [return: NativeTypeName("ULONG")]
    public uint AddRef()
    {
        return ((delegate* unmanaged[Stdcall]<IWICMetadataQueryWriter*, uint>)(lpVtbl[1]))((IWICMetadataQueryWriter*)Unsafe.AsPointer(ref this));
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [return: NativeTypeName("ULONG")]
    public uint Release()
    {
        return ((delegate* unmanaged[Stdcall]<IWICMetadataQueryWriter*, uint>)(lpVtbl[2]))((IWICMetadataQueryWriter*)Unsafe.AsPointer(ref this));
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public HRESULT GetContainerFormat(Guid* pguidContainerFormat)
    {
        return ((delegate* unmanaged[Stdcall]<IWICMetadataQueryWriter*, Guid*, int>)(lpVtbl[3]))((IWICMetadataQueryWriter*)Unsafe.AsPointer(ref this), pguidContainerFormat);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public HRESULT GetLocation(uint cchMaxLength, [NativeTypeName("WCHAR *")] ushort* wzNamespace, uint* pcchActualLength)
    {
        return ((delegate* unmanaged[Stdcall]<IWICMetadataQueryWriter*, uint, ushort*, uint*, int>)(lpVtbl[4]))((IWICMetadataQueryWriter*)Unsafe.AsPointer(ref this), cchMaxLength, wzNamespace, pcchActualLength);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public HRESULT GetMetadataByName([NativeTypeName("LPCWSTR")] ushort* wzName, PROPVARIANT* pvarValue)
    {
        return ((delegate* unmanaged[Stdcall]<IWICMetadataQueryWriter*, ushort*, PROPVARIANT*, int>)(lpVtbl[5]))((IWICMetadataQueryWriter*)Unsafe.AsPointer(ref this), wzName, pvarValue);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public HRESULT GetEnumerator(IEnumString** ppIEnumString)
    {
        return ((delegate* unmanaged[Stdcall]<IWICMetadataQueryWriter*, IEnumString**, int>)(lpVtbl[6]))((IWICMetadataQueryWriter*)Unsafe.AsPointer(ref this), ppIEnumString);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public HRESULT SetMetadataByName([NativeTypeName("LPCWSTR")] ushort* wzName, [NativeTypeName("const PROPVARIANT *")] PROPVARIANT* pvarValue)
    {
        return ((delegate* unmanaged[Stdcall]<IWICMetadataQueryWriter*, ushort*, PROPVARIANT*, int>)(lpVtbl[7]))((IWICMetadataQueryWriter*)Unsafe.AsPointer(ref this), wzName, pvarValue);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public HRESULT RemoveMetadataByName([NativeTypeName("LPCWSTR")] ushort* wzName)
    {
        return ((delegate* unmanaged[Stdcall]<IWICMetadataQueryWriter*, ushort*, int>)(lpVtbl[8]))((IWICMetadataQueryWriter*)Unsafe.AsPointer(ref this), wzName);
    }

    public interface Interface : IWICMetadataQueryReader.Interface
    {
        [VtblIndex(7)]
        HRESULT SetMetadataByName([NativeTypeName("LPCWSTR")] ushort* wzName, [NativeTypeName("const PROPVARIANT *")] PROPVARIANT* pvarValue);

        [VtblIndex(8)]
        HRESULT RemoveMetadataByName([NativeTypeName("LPCWSTR")] ushort* wzName);
    }
}
