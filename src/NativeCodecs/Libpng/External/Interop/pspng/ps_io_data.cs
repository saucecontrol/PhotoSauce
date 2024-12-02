// Copyright © Clinton Ingram and Contributors
// SPDX-License-Identifier: MIT

// Ported from pspng.h
// This software is based in part on the work of the libpng authors.
// See third-party-notices in the repository root for more information.

namespace PhotoSauce.Interop.Libpng;

internal unsafe partial struct ps_io_data
{
    [NativeTypeName("intptr_t")]
    public nint stream_handle;

    [NativeTypeName("size_t (*)(intptr_t, png_bytep, size_t)")]
    public delegate* unmanaged[Cdecl]<nint, byte*, nuint, nuint> write_callback;

    [NativeTypeName("size_t (*)(intptr_t, png_bytep, size_t)")]
    public delegate* unmanaged[Cdecl]<nint, byte*, nuint, nuint> read_callback;
}
