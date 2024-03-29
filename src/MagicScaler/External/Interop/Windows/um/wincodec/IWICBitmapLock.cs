// Copyright © Tanner Gooding and Contributors. Licensed under the MIT License (MIT).
// See third-party-notices in the repository root for more information.

// Ported from um/wincodec.h in the Windows SDK for Windows 10.0.22000.0
// Original source is Copyright © Microsoft. All rights reserved.

using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace TerraFX.Interop.Windows;

[Guid("00000123-A8F2-4877-BA0A-FD2B6645FB94")]
[NativeTypeName("struct IWICBitmapLock : IUnknown")]
[NativeInheritance("IUnknown")]
internal unsafe partial struct IWICBitmapLock : IWICBitmapLock.Interface
{
    public void** lpVtbl;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public HRESULT QueryInterface([NativeTypeName("const IID &")] Guid* riid, void** ppvObject)
    {
        return ((delegate* unmanaged[Stdcall]<IWICBitmapLock*, Guid*, void**, int>)(lpVtbl[0]))((IWICBitmapLock*)Unsafe.AsPointer(ref this), riid, ppvObject);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [return: NativeTypeName("ULONG")]
    public uint AddRef()
    {
        return ((delegate* unmanaged[Stdcall]<IWICBitmapLock*, uint>)(lpVtbl[1]))((IWICBitmapLock*)Unsafe.AsPointer(ref this));
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [return: NativeTypeName("ULONG")]
    public uint Release()
    {
        return ((delegate* unmanaged[Stdcall]<IWICBitmapLock*, uint>)(lpVtbl[2]))((IWICBitmapLock*)Unsafe.AsPointer(ref this));
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public HRESULT GetSize(uint* puiWidth, uint* puiHeight)
    {
        return ((delegate* unmanaged[Stdcall]<IWICBitmapLock*, uint*, uint*, int>)(lpVtbl[3]))((IWICBitmapLock*)Unsafe.AsPointer(ref this), puiWidth, puiHeight);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public HRESULT GetStride(uint* pcbStride)
    {
        return ((delegate* unmanaged[Stdcall]<IWICBitmapLock*, uint*, int>)(lpVtbl[4]))((IWICBitmapLock*)Unsafe.AsPointer(ref this), pcbStride);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public HRESULT GetDataPointer(uint* pcbBufferSize, [NativeTypeName("WICInProcPointer *")] byte** ppbData)
    {
        return ((delegate* unmanaged[Stdcall]<IWICBitmapLock*, uint*, byte**, int>)(lpVtbl[5]))((IWICBitmapLock*)Unsafe.AsPointer(ref this), pcbBufferSize, ppbData);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public HRESULT GetPixelFormat([NativeTypeName("WICPixelFormatGUID *")] Guid* pPixelFormat)
    {
        return ((delegate* unmanaged[Stdcall]<IWICBitmapLock*, Guid*, int>)(lpVtbl[6]))((IWICBitmapLock*)Unsafe.AsPointer(ref this), pPixelFormat);
    }

    public interface Interface : IUnknown.Interface
    {
        [VtblIndex(3)]
        HRESULT GetSize(uint* puiWidth, uint* puiHeight);

        [VtblIndex(4)]
        HRESULT GetStride(uint* pcbStride);

        [VtblIndex(5)]
        HRESULT GetDataPointer(uint* pcbBufferSize, [NativeTypeName("WICInProcPointer *")] byte** ppbData);

        [VtblIndex(6)]
        HRESULT GetPixelFormat([NativeTypeName("WICPixelFormatGUID *")] Guid* pPixelFormat);
    }
}
