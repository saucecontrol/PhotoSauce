// Copyright © Clinton Ingram and Contributors. Licensed under the MIT License (MIT).

// Ported from libjxl headers (memory_manager.h)
// Original source Copyright (c) the JPEG XL Project Authors. All rights reserved.
// See third-party-notices in the repository root for more information.

namespace PhotoSauce.Interop.Libjxl;

internal unsafe partial struct JxlMemoryManagerStruct
{
    public void* opaque;

    [NativeTypeName("jpegxl_alloc_func")]
    public delegate* unmanaged[Cdecl]<void*, nuint, void*> alloc;

    [NativeTypeName("jpegxl_free_func")]
    public delegate* unmanaged[Cdecl]<void*, void*, void> free;
}
