// Copyright © Clinton Ingram and Contributors
// SPDX-License-Identifier: MIT

// Ported from libweb headers (encode.h)
// Original source Copyright 2012 Google Inc. All Rights Reserved.
// See third-party-notices in the repository root for more information.

namespace PhotoSauce.Interop.Libwebp;

internal unsafe partial struct WebPMemoryWriter
{
    [NativeTypeName("uint8_t *")]
    public byte* mem;

    [NativeTypeName("size_t")]
    public nuint size;

    [NativeTypeName("size_t")]
    public nuint max_size;

    [NativeTypeName("uint32_t[1]")]
    private fixed uint pad[1];
}
