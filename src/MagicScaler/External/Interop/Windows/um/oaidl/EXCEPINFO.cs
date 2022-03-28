// Copyright © Tanner Gooding and Contributors. Licensed under the MIT License (MIT).
// See third-party-notices in the repository root for more information.

// Ported from um/oaidl.h in the Windows SDK for Windows 10.0.22000.0
// Original source is Copyright © Microsoft. All rights reserved.

namespace TerraFX.Interop.Windows;

internal unsafe partial struct EXCEPINFO
{
    [NativeTypeName("WORD")]
    public ushort wCode;

    [NativeTypeName("WORD")]
    public ushort wReserved;

    [NativeTypeName("BSTR")]
    public ushort* bstrSource;

    [NativeTypeName("BSTR")]
    public ushort* bstrDescription;

    [NativeTypeName("BSTR")]
    public ushort* bstrHelpFile;

    [NativeTypeName("DWORD")]
    public uint dwHelpContext;

    [NativeTypeName("PVOID")]
    public void* pvReserved;

    [NativeTypeName("HRESULT (*)(struct tagEXCEPINFO *) __attribute__((stdcall))")]
#if NET5_0_OR_GREATER
    public delegate* unmanaged<EXCEPINFO*, int> pfnDeferredFillIn;
#else
    public void* _pfnDeferredFillIn;

    public delegate* unmanaged[Stdcall]<EXCEPINFO*, int> pfnDeferredFillIn
    {
        get => (delegate* unmanaged[Stdcall]<EXCEPINFO*, int>)_pfnDeferredFillIn;
        set => _pfnDeferredFillIn = value;
    }
#endif

    [NativeTypeName("SCODE")]
    public int scode;
}
