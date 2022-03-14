// Copyright © Tanner Gooding and Contributors. Licensed under the MIT License (MIT).
// See third-party-notices in the repository root for more information.

// Ported from um/wincodec.h in the Windows SDK for Windows 10.0.22000.0
// Original source is Copyright © Microsoft. All rights reserved.

using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace TerraFX.Interop.Windows;

[Guid("94C9B4EE-A09F-4F92-8A1E-4A9BCE7E76FB")]
[NativeTypeName("struct IWICBitmapEncoderInfo : IWICBitmapCodecInfo")]
[NativeInheritance("IWICBitmapCodecInfo")]
internal unsafe partial struct IWICBitmapEncoderInfo : IWICBitmapEncoderInfo.Interface
{
    public void** lpVtbl;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public HRESULT QueryInterface([NativeTypeName("const IID &")] Guid* riid, void** ppvObject)
    {
        return ((delegate* unmanaged[Stdcall]<IWICBitmapEncoderInfo*, Guid*, void**, int>)(lpVtbl[0]))((IWICBitmapEncoderInfo*)Unsafe.AsPointer(ref this), riid, ppvObject);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [return: NativeTypeName("ULONG")]
    public uint AddRef()
    {
        return ((delegate* unmanaged[Stdcall]<IWICBitmapEncoderInfo*, uint>)(lpVtbl[1]))((IWICBitmapEncoderInfo*)Unsafe.AsPointer(ref this));
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [return: NativeTypeName("ULONG")]
    public uint Release()
    {
        return ((delegate* unmanaged[Stdcall]<IWICBitmapEncoderInfo*, uint>)(lpVtbl[2]))((IWICBitmapEncoderInfo*)Unsafe.AsPointer(ref this));
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public HRESULT GetComponentType(WICComponentType* pType)
    {
        return ((delegate* unmanaged[Stdcall]<IWICBitmapEncoderInfo*, WICComponentType*, int>)(lpVtbl[3]))((IWICBitmapEncoderInfo*)Unsafe.AsPointer(ref this), pType);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public HRESULT GetCLSID([NativeTypeName("CLSID *")] Guid* pclsid)
    {
        return ((delegate* unmanaged[Stdcall]<IWICBitmapEncoderInfo*, Guid*, int>)(lpVtbl[4]))((IWICBitmapEncoderInfo*)Unsafe.AsPointer(ref this), pclsid);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public HRESULT GetSigningStatus([NativeTypeName("DWORD *")] uint* pStatus)
    {
        return ((delegate* unmanaged[Stdcall]<IWICBitmapEncoderInfo*, uint*, int>)(lpVtbl[5]))((IWICBitmapEncoderInfo*)Unsafe.AsPointer(ref this), pStatus);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public HRESULT GetAuthor(uint cchAuthor, [NativeTypeName("WCHAR *")] ushort* wzAuthor, uint* pcchActual)
    {
        return ((delegate* unmanaged[Stdcall]<IWICBitmapEncoderInfo*, uint, ushort*, uint*, int>)(lpVtbl[6]))((IWICBitmapEncoderInfo*)Unsafe.AsPointer(ref this), cchAuthor, wzAuthor, pcchActual);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public HRESULT GetVendorGUID(Guid* pguidVendor)
    {
        return ((delegate* unmanaged[Stdcall]<IWICBitmapEncoderInfo*, Guid*, int>)(lpVtbl[7]))((IWICBitmapEncoderInfo*)Unsafe.AsPointer(ref this), pguidVendor);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public HRESULT GetVersion(uint cchVersion, [NativeTypeName("WCHAR *")] ushort* wzVersion, uint* pcchActual)
    {
        return ((delegate* unmanaged[Stdcall]<IWICBitmapEncoderInfo*, uint, ushort*, uint*, int>)(lpVtbl[8]))((IWICBitmapEncoderInfo*)Unsafe.AsPointer(ref this), cchVersion, wzVersion, pcchActual);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public HRESULT GetSpecVersion(uint cchSpecVersion, [NativeTypeName("WCHAR *")] ushort* wzSpecVersion, uint* pcchActual)
    {
        return ((delegate* unmanaged[Stdcall]<IWICBitmapEncoderInfo*, uint, ushort*, uint*, int>)(lpVtbl[9]))((IWICBitmapEncoderInfo*)Unsafe.AsPointer(ref this), cchSpecVersion, wzSpecVersion, pcchActual);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public HRESULT GetFriendlyName(uint cchFriendlyName, [NativeTypeName("WCHAR *")] ushort* wzFriendlyName, uint* pcchActual)
    {
        return ((delegate* unmanaged[Stdcall]<IWICBitmapEncoderInfo*, uint, ushort*, uint*, int>)(lpVtbl[10]))((IWICBitmapEncoderInfo*)Unsafe.AsPointer(ref this), cchFriendlyName, wzFriendlyName, pcchActual);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public HRESULT GetContainerFormat(Guid* pguidContainerFormat)
    {
        return ((delegate* unmanaged[Stdcall]<IWICBitmapEncoderInfo*, Guid*, int>)(lpVtbl[11]))((IWICBitmapEncoderInfo*)Unsafe.AsPointer(ref this), pguidContainerFormat);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public HRESULT GetPixelFormats(uint cFormats, Guid* pguidPixelFormats, uint* pcActual)
    {
        return ((delegate* unmanaged[Stdcall]<IWICBitmapEncoderInfo*, uint, Guid*, uint*, int>)(lpVtbl[12]))((IWICBitmapEncoderInfo*)Unsafe.AsPointer(ref this), cFormats, pguidPixelFormats, pcActual);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public HRESULT GetColorManagementVersion(uint cchColorManagementVersion, [NativeTypeName("WCHAR *")] ushort* wzColorManagementVersion, uint* pcchActual)
    {
        return ((delegate* unmanaged[Stdcall]<IWICBitmapEncoderInfo*, uint, ushort*, uint*, int>)(lpVtbl[13]))((IWICBitmapEncoderInfo*)Unsafe.AsPointer(ref this), cchColorManagementVersion, wzColorManagementVersion, pcchActual);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public HRESULT GetDeviceManufacturer(uint cchDeviceManufacturer, [NativeTypeName("WCHAR *")] ushort* wzDeviceManufacturer, uint* pcchActual)
    {
        return ((delegate* unmanaged[Stdcall]<IWICBitmapEncoderInfo*, uint, ushort*, uint*, int>)(lpVtbl[14]))((IWICBitmapEncoderInfo*)Unsafe.AsPointer(ref this), cchDeviceManufacturer, wzDeviceManufacturer, pcchActual);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public HRESULT GetDeviceModels(uint cchDeviceModels, [NativeTypeName("WCHAR *")] ushort* wzDeviceModels, uint* pcchActual)
    {
        return ((delegate* unmanaged[Stdcall]<IWICBitmapEncoderInfo*, uint, ushort*, uint*, int>)(lpVtbl[15]))((IWICBitmapEncoderInfo*)Unsafe.AsPointer(ref this), cchDeviceModels, wzDeviceModels, pcchActual);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public HRESULT GetMimeTypes(uint cchMimeTypes, [NativeTypeName("WCHAR *")] ushort* wzMimeTypes, uint* pcchActual)
    {
        return ((delegate* unmanaged[Stdcall]<IWICBitmapEncoderInfo*, uint, ushort*, uint*, int>)(lpVtbl[16]))((IWICBitmapEncoderInfo*)Unsafe.AsPointer(ref this), cchMimeTypes, wzMimeTypes, pcchActual);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public HRESULT GetFileExtensions(uint cchFileExtensions, [NativeTypeName("WCHAR *")] ushort* wzFileExtensions, uint* pcchActual)
    {
        return ((delegate* unmanaged[Stdcall]<IWICBitmapEncoderInfo*, uint, ushort*, uint*, int>)(lpVtbl[17]))((IWICBitmapEncoderInfo*)Unsafe.AsPointer(ref this), cchFileExtensions, wzFileExtensions, pcchActual);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public HRESULT DoesSupportAnimation(BOOL* pfSupportAnimation)
    {
        return ((delegate* unmanaged[Stdcall]<IWICBitmapEncoderInfo*, BOOL*, int>)(lpVtbl[18]))((IWICBitmapEncoderInfo*)Unsafe.AsPointer(ref this), pfSupportAnimation);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public HRESULT DoesSupportChromakey(BOOL* pfSupportChromakey)
    {
        return ((delegate* unmanaged[Stdcall]<IWICBitmapEncoderInfo*, BOOL*, int>)(lpVtbl[19]))((IWICBitmapEncoderInfo*)Unsafe.AsPointer(ref this), pfSupportChromakey);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public HRESULT DoesSupportLossless(BOOL* pfSupportLossless)
    {
        return ((delegate* unmanaged[Stdcall]<IWICBitmapEncoderInfo*, BOOL*, int>)(lpVtbl[20]))((IWICBitmapEncoderInfo*)Unsafe.AsPointer(ref this), pfSupportLossless);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public HRESULT DoesSupportMultiframe(BOOL* pfSupportMultiframe)
    {
        return ((delegate* unmanaged[Stdcall]<IWICBitmapEncoderInfo*, BOOL*, int>)(lpVtbl[21]))((IWICBitmapEncoderInfo*)Unsafe.AsPointer(ref this), pfSupportMultiframe);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public HRESULT MatchesMimeType([NativeTypeName("LPCWSTR")] ushort* wzMimeType, BOOL* pfMatches)
    {
        return ((delegate* unmanaged[Stdcall]<IWICBitmapEncoderInfo*, ushort*, BOOL*, int>)(lpVtbl[22]))((IWICBitmapEncoderInfo*)Unsafe.AsPointer(ref this), wzMimeType, pfMatches);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public HRESULT CreateInstance(IWICBitmapEncoder** ppIBitmapEncoder)
    {
        return ((delegate* unmanaged[Stdcall]<IWICBitmapEncoderInfo*, IWICBitmapEncoder**, int>)(lpVtbl[23]))((IWICBitmapEncoderInfo*)Unsafe.AsPointer(ref this), ppIBitmapEncoder);
    }

    public interface Interface : IWICBitmapCodecInfo.Interface
    {
        [VtblIndex(23)]
        HRESULT CreateInstance(IWICBitmapEncoder** ppIBitmapEncoder);
    }
}
