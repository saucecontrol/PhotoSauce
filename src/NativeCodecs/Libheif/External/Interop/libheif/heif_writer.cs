// Copyright © Clinton Ingram and Contributors. Licensed under the MIT License (MIT).

// Ported from libheif headers (heif.h)
// Original source Copyright (c) struktur AG, Dirk Farin <farin@struktur.de>
// See third-party-notices in the repository root for more information.

using System;

namespace PhotoSauce.Interop.Libheif;

internal unsafe partial struct heif_writer
{
    public int writer_api_version;

    [NativeTypeName("struct heif_error (*)(struct heif_context *, const void *, size_t, void *)")]
#if NET5_0_OR_GREATER
    public delegate* unmanaged[Cdecl]<IntPtr, void*, nuint, void*, heif_error> write;
#else
    public void* _write;

    public delegate* unmanaged[Cdecl]<IntPtr, void*, nuint, void*, heif_error> write
    {
        get => (delegate* unmanaged[Cdecl]<IntPtr, void*, nuint, void*, heif_error>)_write;
        set => _write = value;
    }
#endif
}
