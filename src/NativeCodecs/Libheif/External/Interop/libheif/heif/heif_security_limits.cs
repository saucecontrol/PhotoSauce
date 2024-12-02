// Copyright © Clinton Ingram and Contributors
// SPDX-License-Identifier: MIT

// Ported from libheif headers (heif.h)
// Original source Copyright (c) struktur AG, Dirk Farin
// See third-party-notices in the repository root for more information.

namespace PhotoSauce.Interop.Libheif;

internal partial struct heif_security_limits
{
    [NativeTypeName("uint8_t")]
    public byte version;

    [NativeTypeName("uint64_t")]
    public ulong max_image_size_pixels;

    [NativeTypeName("uint64_t")]
    public ulong max_number_of_tiles;

    [NativeTypeName("uint32_t")]
    public uint max_bayer_pattern_pixels;

    [NativeTypeName("uint32_t")]
    public uint max_items;

    [NativeTypeName("uint32_t")]
    public uint max_color_profile_size;

    [NativeTypeName("uint64_t")]
    public ulong max_memory_block_size;

    [NativeTypeName("uint32_t")]
    public uint max_components;

    [NativeTypeName("uint32_t")]
    public uint max_iloc_extents_per_item;

    [NativeTypeName("uint32_t")]
    public uint max_size_entity_group;

    [NativeTypeName("uint32_t")]
    public uint max_children_per_box;
}
