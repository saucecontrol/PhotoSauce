// Copyright © Clinton Ingram and Contributors. Licensed under the MIT License (MIT).

// Ported from psjpeg.h
// This software is based in part on the work of the Independent JPEG Group.
// See third-party-notices in the repository root for more information.

namespace PhotoSauce.Interop.Libjpeg;

internal unsafe partial struct ps_client_data
{
    [NativeTypeName("intptr_t")]
    public nint stream_handle;

    [NativeTypeName("size_t (*)(intptr_t, JOCTET *, size_t)")]
    public delegate* unmanaged[Cdecl]<nint, byte*, nuint, nuint> write_callback;

    [NativeTypeName("size_t (*)(intptr_t, JOCTET *, size_t)")]
    public delegate* unmanaged[Cdecl]<nint, byte*, nuint, nuint> read_callback;

    [NativeTypeName("size_t (*)(intptr_t, size_t)")]
    public delegate* unmanaged[Cdecl]<nint, nuint, nuint> seek_callback;
}
