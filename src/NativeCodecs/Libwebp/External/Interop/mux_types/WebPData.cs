// Copyright © Clinton Ingram and Contributors
// SPDX-License-Identifier: MIT

// Ported from libweb headers (mux_types.h)
// Original source Copyright 2012 Google Inc. All Rights Reserved.
// See third-party-notices in the repository root for more information.

namespace PhotoSauce.Interop.Libwebp;

internal unsafe partial struct WebPData
{
    [NativeTypeName("const uint8_t *")]
    public byte* bytes;

    [NativeTypeName("size_t")]
    public nuint size;
}
