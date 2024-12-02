// Copyright © Clinton Ingram and Contributors
// SPDX-License-Identifier: MIT

// Ported from libjxl headers (types.h)
// Original source Copyright (c) the JPEG XL Project Authors. All rights reserved.
// See third-party-notices in the repository root for more information.

namespace PhotoSauce.Interop.Libjxl;

internal partial struct JxlPixelFormat
{
    [NativeTypeName("uint32_t")]
    public uint num_channels;

    public JxlDataType data_type;

    public JxlEndianness endianness;

    [NativeTypeName("size_t")]
    public nuint align;
}
