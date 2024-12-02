// Copyright © Clinton Ingram and Contributors
// SPDX-License-Identifier: MIT

// Ported from libjpeg headers (jmorecfg.h)
// This software is based in part on the work of the Independent JPEG Group.
// Original source copyright (C) 1991-1998, Thomas G. Lane. All Rights Reserved
// See third-party-notices in the repository root for more information.

namespace PhotoSauce.Interop.Libjpeg;

internal static partial class Libjpeg
{
    [NativeTypeName("#define MAX_COMPONENTS 4")]
    public const int MAX_COMPONENTS = 4;

    [NativeTypeName("#define JPEG_MAX_DIMENSION 65500L")]
    public const int JPEG_MAX_DIMENSION = 65500;

    [NativeTypeName("#define FALSE 0")]
    public const int FALSE = 0;

    [NativeTypeName("#define TRUE 1")]
    public const int TRUE = 1;
}
