// Copyright © Clinton Ingram and Contributors. Licensed under the MIT License (MIT).

// Ported from libweb headers (decode.h)
// Original source Copyright 2012 Google Inc. All Rights Reserved.
// See third-party-notices in the repository root for more information.

namespace PhotoSauce.Interop.Libwebp;

internal unsafe partial struct WebPRGBABuffer
{
    [NativeTypeName("uint8_t *")]
    public byte* rgba;

    public int stride;

    [NativeTypeName("size_t")]
    public nuint size;
}
