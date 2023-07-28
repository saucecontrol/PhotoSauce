// Copyright © Clinton Ingram and Contributors. Licensed under the MIT License (MIT).

// Ported from libheif headers (heif.h)
// Original source Copyright (c) struktur AG, Dirk Farin <farin@struktur.de>
// See third-party-notices in the repository root for more information.

namespace PhotoSauce.Interop.Libheif;

internal enum heif_region_type
{
    heif_region_type_point = 0,
    heif_region_type_rectangle = 1,
    heif_region_type_ellipse = 2,
    heif_region_type_polygon = 3,
    heif_region_type_referenced_mask = 4,
    heif_region_type_inline_mask = 5,
    heif_region_type_polyline = 6,
}
