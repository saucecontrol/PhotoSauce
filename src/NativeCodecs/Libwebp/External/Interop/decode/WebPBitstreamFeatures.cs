// Copyright © Clinton Ingram and Contributors
// SPDX-License-Identifier: MIT

// Ported from libweb headers (decode.h)
// Original source Copyright 2012 Google Inc. All Rights Reserved.
// See third-party-notices in the repository root for more information.

namespace PhotoSauce.Interop.Libwebp;

internal unsafe partial struct WebPBitstreamFeatures
{
    public int width;

    public int height;

    public int has_alpha;

    public int has_animation;

    public int format;

    [NativeTypeName("uint32_t[5]")]
    private fixed uint pad[5];
}
