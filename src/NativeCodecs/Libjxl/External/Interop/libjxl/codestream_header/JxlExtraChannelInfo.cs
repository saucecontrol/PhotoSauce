// Copyright © Clinton Ingram and Contributors
// SPDX-License-Identifier: MIT

// Ported from libjxl headers (codestream_header.h)
// Original source Copyright (c) the JPEG XL Project Authors. All rights reserved.
// See third-party-notices in the repository root for more information.

namespace PhotoSauce.Interop.Libjxl;

internal unsafe partial struct JxlExtraChannelInfo
{
    public JxlExtraChannelType type;

    [NativeTypeName("uint32_t")]
    public uint bits_per_sample;

    [NativeTypeName("uint32_t")]
    public uint exponent_bits_per_sample;

    [NativeTypeName("uint32_t")]
    public uint dim_shift;

    [NativeTypeName("uint32_t")]
    public uint name_length;

    public int alpha_premultiplied;

    [NativeTypeName("float[4]")]
    public fixed float spot_color[4];

    [NativeTypeName("uint32_t")]
    public uint cfa_channel;
}
