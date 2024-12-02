// Copyright © Clinton Ingram and Contributors
// SPDX-License-Identifier: MIT

// Ported from libjxl headers (codestream_header.h)
// Original source Copyright (c) the JPEG XL Project Authors. All rights reserved.
// See third-party-notices in the repository root for more information.

namespace PhotoSauce.Interop.Libjxl;

internal enum JxlBlendMode
{
    JXL_BLEND_REPLACE = 0,
    JXL_BLEND_ADD = 1,
    JXL_BLEND_BLEND = 2,
    JXL_BLEND_MULADD = 3,
    JXL_BLEND_MUL = 4,
}
