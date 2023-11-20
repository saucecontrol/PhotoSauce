// Copyright © Clinton Ingram and Contributors. Licensed under the MIT License (MIT).

// Ported from libjpeg headers (jhconfig.h)
// This software is based in part on the work of the Independent JPEG Group.
// Original source copyright (C) 1991-1998, Thomas G. Lane. All Rights Reserved
// See third-party-notices in the repository root for more information.

namespace PhotoSauce.Interop.Libjpeg;

internal static partial class Libjpeg
{
    [NativeTypeName("#define JPEG_LIB_VERSION 62")]
    public const int JPEG_LIB_VERSION = 62;

    [NativeTypeName("#define LIBJPEG_TURBO_VERSION_NUMBER 3000001")]
    public const int LIBJPEG_TURBO_VERSION_NUMBER = 3000001;

    [NativeTypeName("#define BITS_IN_JSAMPLE 8")]
    public const int BITS_IN_JSAMPLE = 8;
}
