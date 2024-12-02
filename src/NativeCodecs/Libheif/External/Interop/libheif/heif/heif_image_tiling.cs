// Copyright © Clinton Ingram and Contributors
// SPDX-License-Identifier: MIT

// Ported from libheif headers (heif.h)
// Original source Copyright (c) struktur AG, Dirk Farin
// See third-party-notices in the repository root for more information.

namespace PhotoSauce.Interop.Libheif;

internal unsafe partial struct heif_image_tiling
{
    public int version;

    [NativeTypeName("uint32_t")]
    public uint num_columns;

    [NativeTypeName("uint32_t")]
    public uint num_rows;

    [NativeTypeName("uint32_t")]
    public uint tile_width;

    [NativeTypeName("uint32_t")]
    public uint tile_height;

    [NativeTypeName("uint32_t")]
    public uint image_width;

    [NativeTypeName("uint32_t")]
    public uint image_height;

    [NativeTypeName("uint32_t")]
    public uint top_offset;

    [NativeTypeName("uint32_t")]
    public uint left_offset;

    [NativeTypeName("uint8_t")]
    public byte number_of_extra_dimensions;

    [NativeTypeName("uint32_t[8]")]
    public fixed uint extra_dimension_size[8];
}
