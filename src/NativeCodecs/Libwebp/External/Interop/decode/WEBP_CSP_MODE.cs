// Copyright © Clinton Ingram and Contributors
// SPDX-License-Identifier: MIT

// Ported from libweb headers (decode.h)
// Original source Copyright 2012 Google Inc. All Rights Reserved.
// See third-party-notices in the repository root for more information.

namespace PhotoSauce.Interop.Libwebp;

internal enum WEBP_CSP_MODE
{
    MODE_RGB = 0,
    MODE_RGBA = 1,
    MODE_BGR = 2,
    MODE_BGRA = 3,
    MODE_ARGB = 4,
    MODE_RGBA_4444 = 5,
    MODE_RGB_565 = 6,
    MODE_rgbA = 7,
    MODE_bgrA = 8,
    MODE_Argb = 9,
    MODE_rgbA_4444 = 10,
    MODE_YUV = 11,
    MODE_YUVA = 12,
    MODE_LAST = 13,
}
