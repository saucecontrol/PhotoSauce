// Copyright © Clinton Ingram and Contributors. Licensed under the MIT License (MIT).

// Ported from libjpeg headers (jpeglib.h)
// This software is based in part on the work of the Independent JPEG Group.
// Original source copyright (C) 1991-1998, Thomas G. Lane. All Rights Reserved
// See third-party-notices in the repository root for more information.

namespace PhotoSauce.Interop.Libjpeg;

internal unsafe partial struct jpeg_progress_mgr
{
    [NativeTypeName("void (*)(j_common_ptr)")]
    public delegate* unmanaged[Cdecl]<jpeg_common_struct*, void> progress_monitor;

    [NativeTypeName("long")]
    public int pass_counter;

    [NativeTypeName("long")]
    public int pass_limit;

    public int completed_passes;

    public int total_passes;
}
