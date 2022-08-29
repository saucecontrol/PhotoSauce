// Copyright © Clinton Ingram and Contributors. Licensed under the MIT License (MIT).

// Ported from libjpeg headers (jpeglib.h)
// This software is based in part on the work of the Independent JPEG Group.
// Original source copyright (C) 1991-1998, Thomas G. Lane. All Rights Reserved
// See third-party-notices in the repository root for more information.

namespace PhotoSauce.Interop.Libjpeg;

internal unsafe partial struct JQUANT_TBL
{
    [NativeTypeName("UINT16[64]")]
    public fixed ushort quantval[64];

    [NativeTypeName("boolean")]
    public int sent_table;
}
