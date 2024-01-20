// Copyright © Clinton Ingram and Contributors. Licensed under the MIT License (MIT).

// Ported from libjxl headers (decode.h)
// Original source Copyright (c) the JPEG XL Project Authors. All rights reserved.
// See third-party-notices in the repository root for more information.

namespace PhotoSauce.Interop.Libjxl;

internal enum JxlProgressiveDetail
{
    kFrames = 0,
    kDC = 1,
    kLastPasses = 2,
    kPasses = 3,
    kDCProgressive = 4,
    kDCGroups = 5,
    kGroups = 6,
}
