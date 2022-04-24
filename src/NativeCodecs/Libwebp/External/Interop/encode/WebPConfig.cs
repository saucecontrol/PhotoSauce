// Copyright © Clinton Ingram and Contributors. Licensed under the MIT License (MIT).

// Ported from libweb headers (encode.h)
// Original source Copyright 2012 Google Inc. All Rights Reserved.
// See third-party-notices in the repository root for more information.

namespace PhotoSauce.Interop.Libwebp;

internal partial struct WebPConfig
{
    public int lossless;

    public float quality;

    public int method;

    public WebPImageHint image_hint;

    public int target_size;

    public float target_PSNR;

    public int segments;

    public int sns_strength;

    public int filter_strength;

    public int filter_sharpness;

    public int filter_type;

    public int autofilter;

    public int alpha_compression;

    public int alpha_filtering;

    public int alpha_quality;

    public int pass;

    public int show_compressed;

    public int preprocessing;

    public int partitions;

    public int partition_limit;

    public int emulate_jpeg_size;

    public int thread_level;

    public int low_memory;

    public int near_lossless;

    public int exact;

    public int use_delta_palette;

    public int use_sharp_yuv;

    public int qmin;

    public int qmax;
}
