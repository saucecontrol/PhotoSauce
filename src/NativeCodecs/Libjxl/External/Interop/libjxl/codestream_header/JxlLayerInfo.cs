// Copyright © Clinton Ingram and Contributors
// SPDX-License-Identifier: MIT

// Ported from libjxl headers (codestream_header.h)
// Original source Copyright (c) the JPEG XL Project Authors. All rights reserved.
// See third-party-notices in the repository root for more information.

namespace PhotoSauce.Interop.Libjxl;

internal partial struct JxlLayerInfo
{
    public int have_crop;

    [NativeTypeName("int32_t")]
    public int crop_x0;

    [NativeTypeName("int32_t")]
    public int crop_y0;

    [NativeTypeName("uint32_t")]
    public uint xsize;

    [NativeTypeName("uint32_t")]
    public uint ysize;

    public JxlBlendInfo blend_info;

    [NativeTypeName("uint32_t")]
    public uint save_as_reference;
}
