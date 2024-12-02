// Copyright © Clinton Ingram and Contributors
// SPDX-License-Identifier: MIT

// Ported from libjpeg headers (jpeglib.h)
// This software is based in part on the work of the Independent JPEG Group.
// Original source copyright (C) 1991-1998, Thomas G. Lane. All Rights Reserved
// See third-party-notices in the repository root for more information.

namespace PhotoSauce.Interop.Libjpeg;

internal unsafe partial struct jpeg_common_struct
{
    [NativeTypeName("struct jpeg_error_mgr *")]
    public jpeg_error_mgr* err;

    [NativeTypeName("struct jpeg_memory_mgr *")]
    public jpeg_memory_mgr* mem;

    [NativeTypeName("struct jpeg_progress_mgr *")]
    public jpeg_progress_mgr* progress;

    public void* client_data;

    [NativeTypeName("boolean")]
    public int is_decompressor;

    public int global_state;
}
