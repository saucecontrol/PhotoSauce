// Copyright © Tanner Gooding and Contributors. Licensed under the MIT License (MIT).
// See third-party-notices in the repository root for more information.

// Ported from um/wincodec.h in the Windows SDK for Windows 10.0.22000.0
// Original source is Copyright © Microsoft. All rights reserved.

using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace TerraFX.Interop.Windows;

[Guid("00000040-A8F2-4877-BA0A-FD2B6645FB94")]
[NativeTypeName("struct IWICPalette : IUnknown")]
[NativeInheritance("IUnknown")]
internal unsafe partial struct IWICPalette : IWICPalette.Interface
{
    public void** lpVtbl;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public HRESULT QueryInterface([NativeTypeName("const IID &")] Guid* riid, void** ppvObject)
    {
        return ((delegate* unmanaged[Stdcall]<IWICPalette*, Guid*, void**, int>)(lpVtbl[0]))((IWICPalette*)Unsafe.AsPointer(ref this), riid, ppvObject);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [return: NativeTypeName("ULONG")]
    public uint AddRef()
    {
        return ((delegate* unmanaged[Stdcall]<IWICPalette*, uint>)(lpVtbl[1]))((IWICPalette*)Unsafe.AsPointer(ref this));
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [return: NativeTypeName("ULONG")]
    public uint Release()
    {
        return ((delegate* unmanaged[Stdcall]<IWICPalette*, uint>)(lpVtbl[2]))((IWICPalette*)Unsafe.AsPointer(ref this));
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public HRESULT InitializePredefined(WICBitmapPaletteType ePaletteType, BOOL fAddTransparentColor)
    {
        return ((delegate* unmanaged[Stdcall]<IWICPalette*, WICBitmapPaletteType, BOOL, int>)(lpVtbl[3]))((IWICPalette*)Unsafe.AsPointer(ref this), ePaletteType, fAddTransparentColor);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public HRESULT InitializeCustom([NativeTypeName("WICColor *")] uint* pColors, uint cCount)
    {
        return ((delegate* unmanaged[Stdcall]<IWICPalette*, uint*, uint, int>)(lpVtbl[4]))((IWICPalette*)Unsafe.AsPointer(ref this), pColors, cCount);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public HRESULT InitializeFromBitmap(IWICBitmapSource* pISurface, uint cCount, BOOL fAddTransparentColor)
    {
        return ((delegate* unmanaged[Stdcall]<IWICPalette*, IWICBitmapSource*, uint, BOOL, int>)(lpVtbl[5]))((IWICPalette*)Unsafe.AsPointer(ref this), pISurface, cCount, fAddTransparentColor);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public HRESULT InitializeFromPalette(IWICPalette* pIPalette)
    {
        return ((delegate* unmanaged[Stdcall]<IWICPalette*, IWICPalette*, int>)(lpVtbl[6]))((IWICPalette*)Unsafe.AsPointer(ref this), pIPalette);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public HRESULT GetType(WICBitmapPaletteType* pePaletteType)
    {
        return ((delegate* unmanaged[Stdcall]<IWICPalette*, WICBitmapPaletteType*, int>)(lpVtbl[7]))((IWICPalette*)Unsafe.AsPointer(ref this), pePaletteType);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public HRESULT GetColorCount(uint* pcCount)
    {
        return ((delegate* unmanaged[Stdcall]<IWICPalette*, uint*, int>)(lpVtbl[8]))((IWICPalette*)Unsafe.AsPointer(ref this), pcCount);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public HRESULT GetColors(uint cCount, [NativeTypeName("WICColor *")] uint* pColors, uint* pcActualColors)
    {
        return ((delegate* unmanaged[Stdcall]<IWICPalette*, uint, uint*, uint*, int>)(lpVtbl[9]))((IWICPalette*)Unsafe.AsPointer(ref this), cCount, pColors, pcActualColors);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public HRESULT IsBlackWhite(BOOL* pfIsBlackWhite)
    {
        return ((delegate* unmanaged[Stdcall]<IWICPalette*, BOOL*, int>)(lpVtbl[10]))((IWICPalette*)Unsafe.AsPointer(ref this), pfIsBlackWhite);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public HRESULT IsGrayscale(BOOL* pfIsGrayscale)
    {
        return ((delegate* unmanaged[Stdcall]<IWICPalette*, BOOL*, int>)(lpVtbl[11]))((IWICPalette*)Unsafe.AsPointer(ref this), pfIsGrayscale);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public HRESULT HasAlpha(BOOL* pfHasAlpha)
    {
        return ((delegate* unmanaged[Stdcall]<IWICPalette*, BOOL*, int>)(lpVtbl[12]))((IWICPalette*)Unsafe.AsPointer(ref this), pfHasAlpha);
    }

    public interface Interface : IUnknown.Interface
    {
        [VtblIndex(3)]
        HRESULT InitializePredefined(WICBitmapPaletteType ePaletteType, BOOL fAddTransparentColor);

        [VtblIndex(4)]
        HRESULT InitializeCustom([NativeTypeName("WICColor *")] uint* pColors, uint cCount);

        [VtblIndex(5)]
        HRESULT InitializeFromBitmap(IWICBitmapSource* pISurface, uint cCount, BOOL fAddTransparentColor);

        [VtblIndex(6)]
        HRESULT InitializeFromPalette(IWICPalette* pIPalette);

        [VtblIndex(7)]
        HRESULT GetType(WICBitmapPaletteType* pePaletteType);

        [VtblIndex(8)]
        HRESULT GetColorCount(uint* pcCount);

        [VtblIndex(9)]
        HRESULT GetColors(uint cCount, [NativeTypeName("WICColor *")] uint* pColors, uint* pcActualColors);

        [VtblIndex(10)]
        HRESULT IsBlackWhite(BOOL* pfIsBlackWhite);

        [VtblIndex(11)]
        HRESULT IsGrayscale(BOOL* pfIsGrayscale);

        [VtblIndex(12)]
        HRESULT HasAlpha(BOOL* pfHasAlpha);
    }
}
