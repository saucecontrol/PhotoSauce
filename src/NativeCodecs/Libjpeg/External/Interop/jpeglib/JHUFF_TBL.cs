// Copyright © Clinton Ingram and Contributors
// SPDX-License-Identifier: MIT

// Ported from libjpeg headers (jpeglib.h)
// This software is based in part on the work of the Independent JPEG Group.
// Original source copyright (C) 1991-1998, Thomas G. Lane. All Rights Reserved
// See third-party-notices in the repository root for more information.

namespace PhotoSauce.Interop.Libjpeg;

internal unsafe partial struct JHUFF_TBL
{
    [NativeTypeName("UINT8[17]")]
    public fixed byte bits[17];

    [NativeTypeName("UINT8[256]")]
    public fixed byte huffval[256];

    [NativeTypeName("boolean")]
    public int sent_table;
}
