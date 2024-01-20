// Copyright © Clinton Ingram and Contributors. Licensed under the MIT License (MIT).

// Ported from libjxl headers (cms_interface.h)
// Original source Copyright (c) the JPEG XL Project Authors. All rights reserved.
// See third-party-notices in the repository root for more information.

namespace PhotoSauce.Interop.Libjxl;

internal partial struct JxlColorProfile
{
    [NativeTypeName("struct (anonymous struct at cms_interface.h:47:3)")]
    public _icc_e__Struct icc;

    public JxlColorEncoding color_encoding;

    [NativeTypeName("size_t")]
    public nuint num_channels;

    internal unsafe partial struct _icc_e__Struct
    {
        [NativeTypeName("const uint8_t *")]
        public byte* data;

        [NativeTypeName("size_t")]
        public nuint size;
    }
}
