// Copyright © Tanner Gooding and Contributors. Licensed under the MIT License (MIT).
// See third-party-notices in the repository root for more information.

// Ported from um/wincodec.h in the Windows SDK for Windows 10.0.22000.0
// Original source is Copyright © Microsoft. All rights reserved.

using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace TerraFX.Interop.Windows;

[Guid("00000121-A8F2-4877-BA0A-FD2B6645FB94")]
[NativeTypeName("struct IWICBitmap : IWICBitmapSource")]
[NativeInheritance("IWICBitmapSource")]
internal unsafe partial struct IWICBitmap : IWICBitmap.Interface
{
    public void** lpVtbl;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public HRESULT QueryInterface([NativeTypeName("const IID &")] Guid* riid, void** ppvObject)
    {
        return ((delegate* unmanaged[Stdcall]<IWICBitmap*, Guid*, void**, int>)(lpVtbl[0]))((IWICBitmap*)Unsafe.AsPointer(ref this), riid, ppvObject);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [return: NativeTypeName("ULONG")]
    public uint AddRef()
    {
        return ((delegate* unmanaged[Stdcall]<IWICBitmap*, uint>)(lpVtbl[1]))((IWICBitmap*)Unsafe.AsPointer(ref this));
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [return: NativeTypeName("ULONG")]
    public uint Release()
    {
        return ((delegate* unmanaged[Stdcall]<IWICBitmap*, uint>)(lpVtbl[2]))((IWICBitmap*)Unsafe.AsPointer(ref this));
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public HRESULT GetSize(uint* puiWidth, uint* puiHeight)
    {
        return ((delegate* unmanaged[Stdcall]<IWICBitmap*, uint*, uint*, int>)(lpVtbl[3]))((IWICBitmap*)Unsafe.AsPointer(ref this), puiWidth, puiHeight);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public HRESULT GetPixelFormat([NativeTypeName("WICPixelFormatGUID *")] Guid* pPixelFormat)
    {
        return ((delegate* unmanaged[Stdcall]<IWICBitmap*, Guid*, int>)(lpVtbl[4]))((IWICBitmap*)Unsafe.AsPointer(ref this), pPixelFormat);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public HRESULT GetResolution(double* pDpiX, double* pDpiY)
    {
        return ((delegate* unmanaged[Stdcall]<IWICBitmap*, double*, double*, int>)(lpVtbl[5]))((IWICBitmap*)Unsafe.AsPointer(ref this), pDpiX, pDpiY);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public HRESULT CopyPalette(IWICPalette* pIPalette)
    {
        return ((delegate* unmanaged[Stdcall]<IWICBitmap*, IWICPalette*, int>)(lpVtbl[6]))((IWICBitmap*)Unsafe.AsPointer(ref this), pIPalette);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public HRESULT CopyPixels([NativeTypeName("const WICRect *")] WICRect* prc, uint cbStride, uint cbBufferSize, byte* pbBuffer)
    {
        return ((delegate* unmanaged[Stdcall]<IWICBitmap*, WICRect*, uint, uint, byte*, int>)(lpVtbl[7]))((IWICBitmap*)Unsafe.AsPointer(ref this), prc, cbStride, cbBufferSize, pbBuffer);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public HRESULT Lock([NativeTypeName("const WICRect *")] WICRect* prcLock, [NativeTypeName("DWORD")] uint flags, IWICBitmapLock** ppILock)
    {
        return ((delegate* unmanaged[Stdcall]<IWICBitmap*, WICRect*, uint, IWICBitmapLock**, int>)(lpVtbl[8]))((IWICBitmap*)Unsafe.AsPointer(ref this), prcLock, flags, ppILock);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public HRESULT SetPalette(IWICPalette* pIPalette)
    {
        return ((delegate* unmanaged[Stdcall]<IWICBitmap*, IWICPalette*, int>)(lpVtbl[9]))((IWICBitmap*)Unsafe.AsPointer(ref this), pIPalette);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public HRESULT SetResolution(double dpiX, double dpiY)
    {
        return ((delegate* unmanaged[Stdcall]<IWICBitmap*, double, double, int>)(lpVtbl[10]))((IWICBitmap*)Unsafe.AsPointer(ref this), dpiX, dpiY);
    }

    public interface Interface : IWICBitmapSource.Interface
    {
        [VtblIndex(8)]
        HRESULT Lock([NativeTypeName("const WICRect *")] WICRect* prcLock, [NativeTypeName("DWORD")] uint flags, IWICBitmapLock** ppILock);

        [VtblIndex(9)]
        HRESULT SetPalette(IWICPalette* pIPalette);

        [VtblIndex(10)]
        HRESULT SetResolution(double dpiX, double dpiY);
    }
}
