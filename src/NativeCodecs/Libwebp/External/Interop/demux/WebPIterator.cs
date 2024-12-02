// Copyright © Clinton Ingram and Contributors
// SPDX-License-Identifier: MIT

// Ported from libweb headers (demux.h)
// Original source Copyright 2012 Google Inc. All Rights Reserved.
// See third-party-notices in the repository root for more information.

namespace PhotoSauce.Interop.Libwebp;

internal unsafe partial struct WebPIterator
{
    public int frame_num;

    public int num_frames;

    public int x_offset;

    public int y_offset;

    public int width;

    public int height;

    public int duration;

    public WebPMuxAnimDispose dispose_method;

    public int complete;

    public WebPData fragment;

    public int has_alpha;

    public WebPMuxAnimBlend blend_method;

    [NativeTypeName("uint32_t[2]")]
    private fixed uint pad[2];

    private void* private_;
}
