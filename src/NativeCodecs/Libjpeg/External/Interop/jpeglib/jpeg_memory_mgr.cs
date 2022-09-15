// Copyright © Clinton Ingram and Contributors. Licensed under the MIT License (MIT).

// Ported from libjpeg headers (jpeglib.h)
// This software is based in part on the work of the Independent JPEG Group.
// Original source copyright (C) 1991-1998, Thomas G. Lane. All Rights Reserved
// See third-party-notices in the repository root for more information.

namespace PhotoSauce.Interop.Libjpeg;

internal unsafe partial struct jpeg_memory_mgr
{
    [NativeTypeName("void *(*)(j_common_ptr, int, size_t)")]
    public delegate* unmanaged[Cdecl]<jpeg_common_struct*, int, nuint, void*> alloc_small;

    [NativeTypeName("void *(*)(j_common_ptr, int, size_t)")]
    public delegate* unmanaged[Cdecl]<jpeg_common_struct*, int, nuint, void*> alloc_large;

    [NativeTypeName("JSAMPARRAY (*)(j_common_ptr, int, JDIMENSION, JDIMENSION)")]
    public delegate* unmanaged[Cdecl]<jpeg_common_struct*, int, uint, uint, byte**> alloc_sarray;

    [NativeTypeName("JBLOCKARRAY (*)(j_common_ptr, int, JDIMENSION, JDIMENSION)")]
    public delegate* unmanaged[Cdecl]<jpeg_common_struct*, int, uint, uint, short**> alloc_barray;

    [NativeTypeName("jvirt_sarray_ptr (*)(j_common_ptr, int, boolean, JDIMENSION, JDIMENSION, JDIMENSION)")]
    public delegate* unmanaged[Cdecl]<jpeg_common_struct*, int, int, uint, uint, uint, void*> request_virt_sarray;

    [NativeTypeName("jvirt_barray_ptr (*)(j_common_ptr, int, boolean, JDIMENSION, JDIMENSION, JDIMENSION)")]
    public delegate* unmanaged[Cdecl]<jpeg_common_struct*, int, int, uint, uint, uint, void*> request_virt_barray;

    [NativeTypeName("void (*)(j_common_ptr)")]
    public delegate* unmanaged[Cdecl]<jpeg_common_struct*, void> realize_virt_arrays;

    [NativeTypeName("JSAMPARRAY (*)(j_common_ptr, jvirt_sarray_ptr, JDIMENSION, JDIMENSION, boolean)")]
    public delegate* unmanaged[Cdecl]<jpeg_common_struct*, void*, uint, uint, int, byte**> access_virt_sarray;

    [NativeTypeName("JBLOCKARRAY (*)(j_common_ptr, jvirt_barray_ptr, JDIMENSION, JDIMENSION, boolean)")]
    public delegate* unmanaged[Cdecl]<jpeg_common_struct*, void*, uint, uint, int, short**> access_virt_barray;

    [NativeTypeName("void (*)(j_common_ptr, int)")]
    public delegate* unmanaged[Cdecl]<jpeg_common_struct*, int, void> free_pool;

    [NativeTypeName("void (*)(j_common_ptr)")]
    public delegate* unmanaged[Cdecl]<jpeg_common_struct*, void> self_destruct;

    [NativeTypeName("long")]
    public int max_memory_to_use;

    [NativeTypeName("long")]
    public int max_alloc_chunk;
}
