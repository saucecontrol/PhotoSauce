// Copyright © Clinton Ingram and Contributors. Licensed under the MIT License (MIT).

// Ported from libweb headers (decode.h)
// Original source Copyright 2012 Google Inc. All Rights Reserved.
// See third-party-notices in the repository root for more information.

namespace PhotoSauce.Interop.Libwebp;

internal unsafe partial struct WebPYUVABuffer
{
    [NativeTypeName("uint8_t *")]
    public byte* y;

    [NativeTypeName("uint8_t *")]
    public byte* u;

    [NativeTypeName("uint8_t *")]
    public byte* v;

    [NativeTypeName("uint8_t *")]
    public byte* a;

    public int y_stride;

    public int u_stride;

    public int v_stride;

    public int a_stride;

    [NativeTypeName("size_t")]
    public nuint y_size;

    [NativeTypeName("size_t")]
    public nuint u_size;

    [NativeTypeName("size_t")]
    public nuint v_size;

    [NativeTypeName("size_t")]
    public nuint a_size;
}
