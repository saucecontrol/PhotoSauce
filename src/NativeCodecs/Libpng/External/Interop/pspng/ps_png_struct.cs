// Copyright © Clinton Ingram and Contributors
// SPDX-License-Identifier: MIT

// Ported from pspng.h
// This software is based in part on the work of the libpng authors.
// See third-party-notices in the repository root for more information.

namespace PhotoSauce.Interop.Libpng;

internal unsafe partial struct ps_png_struct
{
    [NativeTypeName("png_structp")]
    public void* png_ptr;

    [NativeTypeName("png_infop")]
    public void* info_ptr;

    public ps_io_data* io_ptr;
}
