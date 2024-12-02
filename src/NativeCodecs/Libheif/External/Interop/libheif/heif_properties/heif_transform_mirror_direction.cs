// Copyright © Clinton Ingram and Contributors
// SPDX-License-Identifier: MIT

// Ported from libheif headers (heif_properties.h)
// Original source Copyright (c) struktur AG, Dirk Farin
// See third-party-notices in the repository root for more information.

namespace PhotoSauce.Interop.Libheif;

internal enum heif_transform_mirror_direction
{
    heif_transform_mirror_direction_invalid = -1,
    heif_transform_mirror_direction_vertical = 0,
    heif_transform_mirror_direction_horizontal = 1,
}
