// Copyright © Tanner Gooding and Contributors. Licensed under the MIT License (MIT).
// See third-party-notices in the repository root for more information.

// Ported from um/wincodec.h in the Windows SDK for Windows 10.0.22000.0
// Original source is Copyright © Microsoft. All rights reserved.

using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace TerraFX.Interop.Windows;

[Guid("00000105-A8F2-4877-BA0A-FD2B6645FB94")]
[NativeTypeName("struct IWICBitmapFrameEncode : IUnknown")]
[NativeInheritance("IUnknown")]
internal unsafe partial struct IWICBitmapFrameEncode : IWICBitmapFrameEncode.Interface
{
    public void** lpVtbl;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public HRESULT QueryInterface([NativeTypeName("const IID &")] Guid* riid, void** ppvObject)
    {
        return ((delegate* unmanaged[Stdcall]<IWICBitmapFrameEncode*, Guid*, void**, int>)(lpVtbl[0]))((IWICBitmapFrameEncode*)Unsafe.AsPointer(ref this), riid, ppvObject);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [return: NativeTypeName("ULONG")]
    public uint AddRef()
    {
        return ((delegate* unmanaged[Stdcall]<IWICBitmapFrameEncode*, uint>)(lpVtbl[1]))((IWICBitmapFrameEncode*)Unsafe.AsPointer(ref this));
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [return: NativeTypeName("ULONG")]
    public uint Release()
    {
        return ((delegate* unmanaged[Stdcall]<IWICBitmapFrameEncode*, uint>)(lpVtbl[2]))((IWICBitmapFrameEncode*)Unsafe.AsPointer(ref this));
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public HRESULT Initialize(IPropertyBag2* pIEncoderOptions)
    {
        return ((delegate* unmanaged[Stdcall]<IWICBitmapFrameEncode*, IPropertyBag2*, int>)(lpVtbl[3]))((IWICBitmapFrameEncode*)Unsafe.AsPointer(ref this), pIEncoderOptions);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public HRESULT SetSize(uint uiWidth, uint uiHeight)
    {
        return ((delegate* unmanaged[Stdcall]<IWICBitmapFrameEncode*, uint, uint, int>)(lpVtbl[4]))((IWICBitmapFrameEncode*)Unsafe.AsPointer(ref this), uiWidth, uiHeight);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public HRESULT SetResolution(double dpiX, double dpiY)
    {
        return ((delegate* unmanaged[Stdcall]<IWICBitmapFrameEncode*, double, double, int>)(lpVtbl[5]))((IWICBitmapFrameEncode*)Unsafe.AsPointer(ref this), dpiX, dpiY);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public HRESULT SetPixelFormat([NativeTypeName("WICPixelFormatGUID *")] Guid* pPixelFormat)
    {
        return ((delegate* unmanaged[Stdcall]<IWICBitmapFrameEncode*, Guid*, int>)(lpVtbl[6]))((IWICBitmapFrameEncode*)Unsafe.AsPointer(ref this), pPixelFormat);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public HRESULT SetColorContexts(uint cCount, IWICColorContext** ppIColorContext)
    {
        return ((delegate* unmanaged[Stdcall]<IWICBitmapFrameEncode*, uint, IWICColorContext**, int>)(lpVtbl[7]))((IWICBitmapFrameEncode*)Unsafe.AsPointer(ref this), cCount, ppIColorContext);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public HRESULT SetPalette(IWICPalette* pIPalette)
    {
        return ((delegate* unmanaged[Stdcall]<IWICBitmapFrameEncode*, IWICPalette*, int>)(lpVtbl[8]))((IWICBitmapFrameEncode*)Unsafe.AsPointer(ref this), pIPalette);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public HRESULT SetThumbnail(IWICBitmapSource* pIThumbnail)
    {
        return ((delegate* unmanaged[Stdcall]<IWICBitmapFrameEncode*, IWICBitmapSource*, int>)(lpVtbl[9]))((IWICBitmapFrameEncode*)Unsafe.AsPointer(ref this), pIThumbnail);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public HRESULT WritePixels(uint lineCount, uint cbStride, uint cbBufferSize, byte* pbPixels)
    {
        return ((delegate* unmanaged[Stdcall]<IWICBitmapFrameEncode*, uint, uint, uint, byte*, int>)(lpVtbl[10]))((IWICBitmapFrameEncode*)Unsafe.AsPointer(ref this), lineCount, cbStride, cbBufferSize, pbPixels);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public HRESULT WriteSource(IWICBitmapSource* pIBitmapSource, WICRect* prc)
    {
        return ((delegate* unmanaged[Stdcall]<IWICBitmapFrameEncode*, IWICBitmapSource*, WICRect*, int>)(lpVtbl[11]))((IWICBitmapFrameEncode*)Unsafe.AsPointer(ref this), pIBitmapSource, prc);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public HRESULT Commit()
    {
        return ((delegate* unmanaged[Stdcall]<IWICBitmapFrameEncode*, int>)(lpVtbl[12]))((IWICBitmapFrameEncode*)Unsafe.AsPointer(ref this));
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public HRESULT GetMetadataQueryWriter(IWICMetadataQueryWriter** ppIMetadataQueryWriter)
    {
        return ((delegate* unmanaged[Stdcall]<IWICBitmapFrameEncode*, IWICMetadataQueryWriter**, int>)(lpVtbl[13]))((IWICBitmapFrameEncode*)Unsafe.AsPointer(ref this), ppIMetadataQueryWriter);
    }

    public interface Interface : IUnknown.Interface
    {
        [VtblIndex(3)]
        HRESULT Initialize(IPropertyBag2* pIEncoderOptions);

        [VtblIndex(4)]
        HRESULT SetSize(uint uiWidth, uint uiHeight);

        [VtblIndex(5)]
        HRESULT SetResolution(double dpiX, double dpiY);

        [VtblIndex(6)]
        HRESULT SetPixelFormat([NativeTypeName("WICPixelFormatGUID *")] Guid* pPixelFormat);

        [VtblIndex(7)]
        HRESULT SetColorContexts(uint cCount, IWICColorContext** ppIColorContext);

        [VtblIndex(8)]
        HRESULT SetPalette(IWICPalette* pIPalette);

        [VtblIndex(9)]
        HRESULT SetThumbnail(IWICBitmapSource* pIThumbnail);

        [VtblIndex(10)]
        HRESULT WritePixels(uint lineCount, uint cbStride, uint cbBufferSize, byte* pbPixels);

        [VtblIndex(11)]
        HRESULT WriteSource(IWICBitmapSource* pIBitmapSource, WICRect* prc);

        [VtblIndex(12)]
        HRESULT Commit();

        [VtblIndex(13)]
        HRESULT GetMetadataQueryWriter(IWICMetadataQueryWriter** ppIMetadataQueryWriter);
    }
}
