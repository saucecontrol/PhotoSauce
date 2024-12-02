// Copyright © Clinton Ingram and Contributors
// SPDX-License-Identifier: MIT

// Ported from libjpeg headers (jpeglib.h)
// This software is based in part on the work of the Independent JPEG Group.
// Original source copyright (C) 1991-1998, Thomas G. Lane. All Rights Reserved
// See third-party-notices in the repository root for more information.

namespace PhotoSauce.Interop.Libjpeg;

internal unsafe partial struct jpeg_source_mgr
{
    [NativeTypeName("const JOCTET *")]
    public byte* next_input_byte;

    [NativeTypeName("size_t")]
    public nuint bytes_in_buffer;

    [NativeTypeName("void (*)(j_decompress_ptr)")]
    public delegate* unmanaged[Cdecl]<jpeg_decompress_struct*, void> init_source;

    [NativeTypeName("boolean (*)(j_decompress_ptr)")]
    public delegate* unmanaged[Cdecl]<jpeg_decompress_struct*, int> fill_input_buffer;

    [NativeTypeName("void (*)(j_decompress_ptr, long)")]
    public delegate* unmanaged[Cdecl]<jpeg_decompress_struct*, int, void> skip_input_data;

    [NativeTypeName("boolean (*)(j_decompress_ptr, int)")]
    public delegate* unmanaged[Cdecl]<jpeg_decompress_struct*, int, int> resync_to_restart;

    [NativeTypeName("void (*)(j_decompress_ptr)")]
    public delegate* unmanaged[Cdecl]<jpeg_decompress_struct*, void> term_source;
}
