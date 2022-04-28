// Copyright © Clinton Ingram and Contributors. Licensed under the MIT License (MIT).

// Ported from libweb headers (encode.h)
// Original source Copyright 2012 Google Inc. All Rights Reserved.
// See third-party-notices in the repository root for more information.

namespace PhotoSauce.Interop.Libwebp;

internal unsafe partial struct WebPAuxStats
{
    public int coded_size;

    [NativeTypeName("float[5]")]
    public fixed float PSNR[5];

    [NativeTypeName("int[3]")]
    public fixed int block_count[3];

    [NativeTypeName("int[2]")]
    public fixed int header_bytes[2];

    [NativeTypeName("int[3][4]")]
    public fixed int residual_bytes[3 * 4];

    [NativeTypeName("int[4]")]
    public fixed int segment_size[4];

    [NativeTypeName("int[4]")]
    public fixed int segment_quant[4];

    [NativeTypeName("int[4]")]
    public fixed int segment_level[4];

    public int alpha_data_size;

    public int layer_data_size;

    [NativeTypeName("uint32_t")]
    public uint lossless_features;

    public int histogram_bits;

    public int transform_bits;

    public int cache_bits;

    public int palette_size;

    public int lossless_size;

    public int lossless_hdr_size;

    public int lossless_data_size;

    [NativeTypeName("uint32_t[2]")]
    private fixed uint pad[2];
}
