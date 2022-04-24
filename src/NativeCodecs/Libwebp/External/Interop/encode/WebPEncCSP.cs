// Copyright © Clinton Ingram and Contributors. Licensed under the MIT License (MIT).

// Ported from libweb headers (encode.h)
// Original source Copyright 2012 Google Inc. All Rights Reserved.
// See third-party-notices in the repository root for more information.

namespace PhotoSauce.Interop.Libwebp;

internal enum WebPEncCSP
{
    WEBP_YUV420 = 0,
    WEBP_YUV420A = 4,
    WEBP_CSP_UV_MASK = 3,
    WEBP_CSP_ALPHA_BIT = 4,
}
