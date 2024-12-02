// Copyright © Clinton Ingram and Contributors
// SPDX-License-Identifier: MIT

// Ported from libjxl headers (types.h)
// Original source Copyright (c) the JPEG XL Project Authors. All rights reserved.
// See third-party-notices in the repository root for more information.

namespace PhotoSauce.Interop.Libjxl;

internal enum JxlBitDepthType
{
    JXL_BIT_DEPTH_FROM_PIXEL_FORMAT = 0,
    JXL_BIT_DEPTH_FROM_CODESTREAM = 1,
    JXL_BIT_DEPTH_CUSTOM = 2,
}
