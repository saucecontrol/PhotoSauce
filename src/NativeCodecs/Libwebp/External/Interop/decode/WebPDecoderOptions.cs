// Copyright © Clinton Ingram and Contributors. Licensed under the MIT License (MIT).

// Ported from libweb headers (decode.h)
// Original source Copyright 2012 Google Inc. All Rights Reserved.
// See third-party-notices in the repository root for more information.

namespace PhotoSauce.Interop.Libwebp;

internal unsafe partial struct WebPDecoderOptions
{
    public int bypass_filtering;

    public int no_fancy_upsampling;

    public int use_cropping;

    public int crop_left;

    public int crop_top;

    public int crop_width;

    public int crop_height;

    public int use_scaling;

    public int scaled_width;

    public int scaled_height;

    public int use_threads;

    public int dithering_strength;

    public int flip;

    public int alpha_dithering_strength;

    [NativeTypeName("uint32_t[5]")]
    private fixed uint pad[5];
}
