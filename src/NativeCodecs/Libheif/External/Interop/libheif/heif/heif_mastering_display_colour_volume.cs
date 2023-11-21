// Copyright © Clinton Ingram and Contributors. Licensed under the MIT License (MIT).

// Ported from libheif headers (heif.h)
// Original source Copyright (c) struktur AG, Dirk Farin
// See third-party-notices in the repository root for more information.

namespace PhotoSauce.Interop.Libheif;

internal unsafe partial struct heif_mastering_display_colour_volume
{
    [NativeTypeName("uint16_t[3]")]
    public fixed ushort display_primaries_x[3];

    [NativeTypeName("uint16_t[3]")]
    public fixed ushort display_primaries_y[3];

    [NativeTypeName("uint16_t")]
    public ushort white_point_x;

    [NativeTypeName("uint16_t")]
    public ushort white_point_y;

    [NativeTypeName("uint32_t")]
    public uint max_display_mastering_luminance;

    [NativeTypeName("uint32_t")]
    public uint min_display_mastering_luminance;
}
