// Copyright © Clinton Ingram and Contributors. Licensed under the MIT License (MIT).

// Ported from libjxl headers (codestream_header.h)
// Original source Copyright (c) the JPEG XL Project Authors. All rights reserved.
// See third-party-notices in the repository root for more information.

namespace PhotoSauce.Interop.Libjxl;

internal partial struct JxlAnimationHeader
{
    [NativeTypeName("uint32_t")]
    public uint tps_numerator;

    [NativeTypeName("uint32_t")]
    public uint tps_denominator;

    [NativeTypeName("uint32_t")]
    public uint num_loops;

    public int have_timecodes;
}
