// Copyright © Clinton Ingram and Contributors. Licensed under the MIT License (MIT).

// Ported from libjpeg headers (jpeglib.h)
// This software is based in part on the work of the Independent JPEG Group.
// Original source copyright (C) 1991-1998, Thomas G. Lane. All Rights Reserved
// See third-party-notices in the repository root for more information.

namespace PhotoSauce.Interop.Libjpeg;

internal unsafe partial struct jpeg_destination_mgr
{
    [NativeTypeName("JOCTET *")]
    public byte* next_output_byte;

    [NativeTypeName("size_t")]
    public nuint free_in_buffer;

    [NativeTypeName("void (*)(j_compress_ptr)")]
    public delegate* unmanaged[Cdecl]<jpeg_compress_struct*, void> init_destination;

    [NativeTypeName("boolean (*)(j_compress_ptr)")]
    public delegate* unmanaged[Cdecl]<jpeg_compress_struct*, int> empty_output_buffer;

    [NativeTypeName("void (*)(j_compress_ptr)")]
    public delegate* unmanaged[Cdecl]<jpeg_compress_struct*, void> term_destination;
}
