// Copyright © Clinton Ingram and Contributors. Licensed under the MIT License (MIT).

// Ported from libjpeg headers (jpeglib.h)
// This software is based in part on the work of the Independent JPEG Group.
// Original source copyright (C) 1991-1998, Thomas G. Lane. All Rights Reserved
// See third-party-notices in the repository root for more information.

namespace PhotoSauce.Interop.Libjpeg;

internal unsafe partial struct jpeg_marker_struct
{
    [NativeTypeName("jpeg_saved_marker_ptr")]
    public jpeg_marker_struct* next;

    [NativeTypeName("UINT8")]
    public byte marker;

    [NativeTypeName("unsigned int")]
    public uint original_length;

    [NativeTypeName("unsigned int")]
    public uint data_length;

    [NativeTypeName("JOCTET *")]
    public byte* data;
}
