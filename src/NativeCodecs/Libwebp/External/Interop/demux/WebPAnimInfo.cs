// Copyright © Clinton Ingram and Contributors
// SPDX-License-Identifier: MIT

// Ported from libweb headers (demux.h)
// Original source Copyright 2012 Google Inc. All Rights Reserved.
// See third-party-notices in the repository root for more information.

namespace PhotoSauce.Interop.Libwebp;

internal unsafe partial struct WebPAnimInfo
{
    [NativeTypeName("uint32_t")]
    public uint canvas_width;

    [NativeTypeName("uint32_t")]
    public uint canvas_height;

    [NativeTypeName("uint32_t")]
    public uint loop_count;

    [NativeTypeName("uint32_t")]
    public uint bgcolor;

    [NativeTypeName("uint32_t")]
    public uint frame_count;

    [NativeTypeName("uint32_t[4]")]
    private fixed uint pad[4];
}
