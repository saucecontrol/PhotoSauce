// Copyright © Clinton Ingram and Contributors
// SPDX-License-Identifier: MIT

// Ported from libheif headers (heif.h)
// Original source Copyright (c) struktur AG, Dirk Farin
// See third-party-notices in the repository root for more information.

namespace PhotoSauce.Interop.Libheif;

internal unsafe partial struct heif_decoded_mastering_display_colour_volume
{
    [NativeTypeName("float[3]")]
    public fixed float display_primaries_x[3];

    [NativeTypeName("float[3]")]
    public fixed float display_primaries_y[3];

    public float white_point_x;

    public float white_point_y;

    public double max_display_mastering_luminance;

    public double min_display_mastering_luminance;
}
