// Copyright © Clinton Ingram and Contributors
// SPDX-License-Identifier: MIT

// Ported from libheif headers (heif_properties.h)
// Original source Copyright (c) struktur AG, Dirk Farin
// See third-party-notices in the repository root for more information.

namespace PhotoSauce.Interop.Libheif;

internal enum heif_item_property_type
{
    heif_item_property_type_invalid = 0,
    heif_item_property_type_user_description = (('u' << 24) | ('d' << 16) | ('e' << 8) | 's'),
    heif_item_property_type_transform_mirror = (('i' << 24) | ('m' << 16) | ('i' << 8) | 'r'),
    heif_item_property_type_transform_rotation = (('i' << 24) | ('r' << 16) | ('o' << 8) | 't'),
    heif_item_property_type_transform_crop = (('c' << 24) | ('l' << 16) | ('a' << 8) | 'p'),
    heif_item_property_type_image_size = (('i' << 24) | ('s' << 16) | ('p' << 8) | 'e'),
    heif_item_property_type_uuid = (('u' << 24) | ('u' << 16) | ('i' << 8) | 'd'),
    heif_item_property_type_tai_clock_info = (('t' << 24) | ('a' << 16) | ('i' << 8) | 'c'),
    heif_item_property_type_tai_timestamp = (('i' << 24) | ('t' << 16) | ('a' << 8) | 'i'),
}
