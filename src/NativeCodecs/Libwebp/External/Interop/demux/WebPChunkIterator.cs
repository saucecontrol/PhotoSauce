// Copyright © Clinton Ingram and Contributors
// SPDX-License-Identifier: MIT

// Ported from libweb headers (demux.h)
// Original source Copyright 2012 Google Inc. All Rights Reserved.
// See third-party-notices in the repository root for more information.

namespace PhotoSauce.Interop.Libwebp;

internal unsafe partial struct WebPChunkIterator
{
    public int chunk_num;

    public int num_chunks;

    public WebPData chunk;

    [NativeTypeName("uint32_t[6]")]
    private fixed uint pad[6];

    private void* private_;
}
