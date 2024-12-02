// Copyright © Clinton Ingram and Contributors
// SPDX-License-Identifier: MIT

// Ported from libjxl headers (codestream_header.h)
// Original source Copyright (c) the JPEG XL Project Authors. All rights reserved.
// See third-party-notices in the repository root for more information.

namespace PhotoSauce.Interop.Libjxl;

internal enum JxlOrientation
{
    JXL_ORIENT_IDENTITY = 1,
    JXL_ORIENT_FLIP_HORIZONTAL = 2,
    JXL_ORIENT_ROTATE_180 = 3,
    JXL_ORIENT_FLIP_VERTICAL = 4,
    JXL_ORIENT_TRANSPOSE = 5,
    JXL_ORIENT_ROTATE_90_CW = 6,
    JXL_ORIENT_ANTI_TRANSPOSE = 7,
    JXL_ORIENT_ROTATE_90_CCW = 8,
}
