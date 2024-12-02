// Copyright © Clinton Ingram and Contributors
// SPDX-License-Identifier: MIT

// Ported from libweb headers (demux.h)
// Original source Copyright 2012 Google Inc. All Rights Reserved.
// See third-party-notices in the repository root for more information.

namespace PhotoSauce.Interop.Libwebp;

internal unsafe partial struct WebPAnimDecoderOptions
{
    public WEBP_CSP_MODE color_mode;

    public int use_threads;

    [NativeTypeName("uint32_t[7]")]
    private fixed uint padding[7];
}
