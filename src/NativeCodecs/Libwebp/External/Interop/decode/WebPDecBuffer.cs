// Copyright © Clinton Ingram and Contributors. Licensed under the MIT License (MIT).

// Ported from libweb headers (decode.h)
// Original source Copyright 2012 Google Inc. All Rights Reserved.
// See third-party-notices in the repository root for more information.

using System.Runtime.InteropServices;

namespace PhotoSauce.Interop.Libwebp;

internal unsafe partial struct WebPDecBuffer
{
    public WEBP_CSP_MODE colorspace;

    public int width;

    public int height;

    public int is_external_memory;

    [NativeTypeName("union (anonymous union at decode.h:207:3)")]
    public _u_e__Union u;

    [NativeTypeName("uint32_t[4]")]
    private fixed uint pad[4];

    [NativeTypeName("uint8_t *")]
    private byte* private_memory;

    [StructLayout(LayoutKind.Explicit)]
    public partial struct _u_e__Union
    {
        [FieldOffset(0)]
        public WebPRGBABuffer RGBA;

        [FieldOffset(0)]
        public WebPYUVABuffer YUVA;
    }
}
