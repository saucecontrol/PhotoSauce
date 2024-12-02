// Copyright © Clinton Ingram and Contributors
// SPDX-License-Identifier: MIT

// Ported from libjxl headers (memory_manager.h)
// Original source Copyright (c) the JPEG XL Project Authors. All rights reserved.
// See third-party-notices in the repository root for more information.

namespace PhotoSauce.Interop.Libjxl;

internal unsafe partial struct JxlMemoryManagerStruct
{
    public void* opaque;

    [NativeTypeName("jpegxl_alloc_func")]
#if NET5_0_OR_GREATER
    public delegate* unmanaged[Cdecl]<void*, nuint, void*> alloc;
#else
    public void* _alloc;

    public delegate* unmanaged[Cdecl]<void*, nuint, void*> alloc
    {
        get => (delegate* unmanaged[Cdecl]<void*, nuint, void*>)_alloc;
        set => _alloc = value;
    }
#endif

    [NativeTypeName("jpegxl_free_func")]
#if NET5_0_OR_GREATER
    public delegate* unmanaged[Cdecl]<void*, void*, void> free;
#else
    public void* _free;

    public delegate* unmanaged[Cdecl]<void*, void*, void> free
    {
        get => (delegate* unmanaged[Cdecl]<void*, void*, void>)_free;
        set => _free = value;
    }
#endif
}
