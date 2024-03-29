// Copyright © Tanner Gooding and Contributors. Licensed under the MIT License (MIT).
// See third-party-notices in the repository root for more information.

// Ported from um/wincodec.h in the Windows SDK for Windows 10.0.22000.0
// Original source is Copyright © Microsoft. All rights reserved.

using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace TerraFX.Interop.Windows;

[Guid("B66F034F-D0E2-40AB-B436-6DE39E321A94")]
[NativeTypeName("struct IWICColorTransform : IWICBitmapSource")]
[NativeInheritance("IWICBitmapSource")]
internal unsafe partial struct IWICColorTransform : IWICColorTransform.Interface
{
    public void** lpVtbl;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public HRESULT QueryInterface([NativeTypeName("const IID &")] Guid* riid, void** ppvObject)
    {
        return ((delegate* unmanaged[Stdcall]<IWICColorTransform*, Guid*, void**, int>)(lpVtbl[0]))((IWICColorTransform*)Unsafe.AsPointer(ref this), riid, ppvObject);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [return: NativeTypeName("ULONG")]
    public uint AddRef()
    {
        return ((delegate* unmanaged[Stdcall]<IWICColorTransform*, uint>)(lpVtbl[1]))((IWICColorTransform*)Unsafe.AsPointer(ref this));
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [return: NativeTypeName("ULONG")]
    public uint Release()
    {
        return ((delegate* unmanaged[Stdcall]<IWICColorTransform*, uint>)(lpVtbl[2]))((IWICColorTransform*)Unsafe.AsPointer(ref this));
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public HRESULT GetSize(uint* puiWidth, uint* puiHeight)
    {
        return ((delegate* unmanaged[Stdcall]<IWICColorTransform*, uint*, uint*, int>)(lpVtbl[3]))((IWICColorTransform*)Unsafe.AsPointer(ref this), puiWidth, puiHeight);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public HRESULT GetPixelFormat([NativeTypeName("WICPixelFormatGUID *")] Guid* pPixelFormat)
    {
        return ((delegate* unmanaged[Stdcall]<IWICColorTransform*, Guid*, int>)(lpVtbl[4]))((IWICColorTransform*)Unsafe.AsPointer(ref this), pPixelFormat);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public HRESULT GetResolution(double* pDpiX, double* pDpiY)
    {
        return ((delegate* unmanaged[Stdcall]<IWICColorTransform*, double*, double*, int>)(lpVtbl[5]))((IWICColorTransform*)Unsafe.AsPointer(ref this), pDpiX, pDpiY);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public HRESULT CopyPalette(IWICPalette* pIPalette)
    {
        return ((delegate* unmanaged[Stdcall]<IWICColorTransform*, IWICPalette*, int>)(lpVtbl[6]))((IWICColorTransform*)Unsafe.AsPointer(ref this), pIPalette);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public HRESULT CopyPixels([NativeTypeName("const WICRect *")] WICRect* prc, uint cbStride, uint cbBufferSize, byte* pbBuffer)
    {
        return ((delegate* unmanaged[Stdcall]<IWICColorTransform*, WICRect*, uint, uint, byte*, int>)(lpVtbl[7]))((IWICColorTransform*)Unsafe.AsPointer(ref this), prc, cbStride, cbBufferSize, pbBuffer);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public HRESULT Initialize(IWICBitmapSource* pIBitmapSource, IWICColorContext* pIContextSource, IWICColorContext* pIContextDest, [NativeTypeName("REFWICPixelFormatGUID")] Guid* pixelFmtDest)
    {
        return ((delegate* unmanaged[Stdcall]<IWICColorTransform*, IWICBitmapSource*, IWICColorContext*, IWICColorContext*, Guid*, int>)(lpVtbl[8]))((IWICColorTransform*)Unsafe.AsPointer(ref this), pIBitmapSource, pIContextSource, pIContextDest, pixelFmtDest);
    }

    public interface Interface : IWICBitmapSource.Interface
    {
        [VtblIndex(8)]
        HRESULT Initialize(IWICBitmapSource* pIBitmapSource, IWICColorContext* pIContextSource, IWICColorContext* pIContextDest, [NativeTypeName("REFWICPixelFormatGUID")] Guid* pixelFmtDest);
    }
}
