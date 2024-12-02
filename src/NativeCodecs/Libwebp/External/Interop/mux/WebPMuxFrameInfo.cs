// Copyright © Clinton Ingram and Contributors
// SPDX-License-Identifier: MIT

// Ported from libweb headers (mux.h)
// Original source Copyright 2012 Google Inc. All Rights Reserved.
// See third-party-notices in the repository root for more information.

namespace PhotoSauce.Interop.Libwebp;

internal unsafe partial struct WebPMuxFrameInfo
{
    public WebPData bitstream;

    public int x_offset;

    public int y_offset;

    public int duration;

    public WebPChunkId id;

    public WebPMuxAnimDispose dispose_method;

    public WebPMuxAnimBlend blend_method;

    [NativeTypeName("uint32_t[1]")]
    private fixed uint pad[1];
}
