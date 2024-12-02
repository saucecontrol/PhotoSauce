// Copyright © Clinton Ingram and Contributors
// SPDX-License-Identifier: MIT

// Ported from libjxl headers (encode.h)
// Original source Copyright (c) the JPEG XL Project Authors. All rights reserved.
// See third-party-notices in the repository root for more information.

namespace PhotoSauce.Interop.Libjxl;

internal unsafe partial struct JxlEncoderOutputProcessor
{
    public void* opaque;

    [NativeTypeName("void *(*)(void *, size_t *)")]
#if NET5_0_OR_GREATER
    public delegate* unmanaged[Cdecl]<void*, nuint*, void*> get_buffer;
#else
    public void* _get_buffer;

    public delegate* unmanaged[Cdecl]<void*, nuint*, void*> get_buffer
    {
        get => (delegate* unmanaged[Cdecl]<void*, nuint*, void*>)_get_buffer;
        set => _get_buffer = value;
    }
#endif

    [NativeTypeName("void (*)(void *, size_t)")]
#if NET5_0_OR_GREATER
    public delegate* unmanaged[Cdecl]<void*, nuint, void> release_buffer;
#else
    public void* _release_buffer;

    public delegate* unmanaged[Cdecl]<void*, nuint, void> release_buffer
    {
        get => (delegate* unmanaged[Cdecl]<void*, nuint, void>)_release_buffer;
        set => _release_buffer = value;
    }
#endif

    [NativeTypeName("void (*)(void *, uint64_t)")]
#if NET5_0_OR_GREATER
    public delegate* unmanaged[Cdecl]<void*, ulong, void> seek;
#else
    public void* _seek;

    public delegate* unmanaged[Cdecl]<void*, ulong, void> seek
    {
        get => (delegate* unmanaged[Cdecl]<void*, ulong, void>)_seek;
        set => _seek = value;
    }
#endif

    [NativeTypeName("void (*)(void *, uint64_t)")]
#if NET5_0_OR_GREATER
    public delegate* unmanaged[Cdecl]<void*, ulong, void> set_finalized_position;
#else
    public void* _set_finalized_position;

    public delegate* unmanaged[Cdecl]<void*, ulong, void> set_finalized_position
    {
        get => (delegate* unmanaged[Cdecl]<void*, ulong, void>)_set_finalized_position;
        set => _set_finalized_position = value;
    }
#endif
}
