// Copyright © Clinton Ingram and Contributors
// SPDX-License-Identifier: MIT

// Ported from libheif headers (heif.h)
// Original source Copyright (c) struktur AG, Dirk Farin
// See third-party-notices in the repository root for more information.

namespace PhotoSauce.Interop.Libheif;

internal unsafe partial struct heif_entity_group
{
    [NativeTypeName("heif_entity_group_id")]
    public uint entity_group_id;

    [NativeTypeName("uint32_t")]
    public uint entity_group_type;

    [NativeTypeName("heif_item_id *")]
    public uint* entities;

    [NativeTypeName("uint32_t")]
    public uint num_entities;
}
