// Copyright © Clinton Ingram and Contributors
// SPDX-License-Identifier: MIT

// Ported from libjxl headers (codestream_header.h)
// Original source Copyright (c) the JPEG XL Project Authors. All rights reserved.
// See third-party-notices in the repository root for more information.

namespace PhotoSauce.Interop.Libjxl;

internal unsafe partial struct JxlBasicInfo
{
    public int have_container;

    [NativeTypeName("uint32_t")]
    public uint xsize;

    [NativeTypeName("uint32_t")]
    public uint ysize;

    [NativeTypeName("uint32_t")]
    public uint bits_per_sample;

    [NativeTypeName("uint32_t")]
    public uint exponent_bits_per_sample;

    public float intensity_target;

    public float min_nits;

    public int relative_to_max_display;

    public float linear_below;

    public int uses_original_profile;

    public int have_preview;

    public int have_animation;

    public JxlOrientation orientation;

    [NativeTypeName("uint32_t")]
    public uint num_color_channels;

    [NativeTypeName("uint32_t")]
    public uint num_extra_channels;

    [NativeTypeName("uint32_t")]
    public uint alpha_bits;

    [NativeTypeName("uint32_t")]
    public uint alpha_exponent_bits;

    public int alpha_premultiplied;

    public JxlPreviewHeader preview;

    public JxlAnimationHeader animation;

    [NativeTypeName("uint32_t")]
    public uint intrinsic_xsize;

    [NativeTypeName("uint32_t")]
    public uint intrinsic_ysize;

    [NativeTypeName("uint8_t[100]")]
    public fixed byte padding[100];
}
