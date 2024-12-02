// Copyright © Clinton Ingram and Contributors
// SPDX-License-Identifier: MIT

// Ported from libheif headers (heif.h)
// Original source Copyright (c) struktur AG, Dirk Farin
// See third-party-notices in the repository root for more information.

namespace PhotoSauce.Interop.Libheif;

internal partial struct heif_color_profile_nclx
{
    [NativeTypeName("uint8_t")]
    public byte version;

    [NativeTypeName("enum heif_color_primaries")]
    public heif_color_primaries color_primaries;

    [NativeTypeName("enum heif_transfer_characteristics")]
    public heif_transfer_characteristics transfer_characteristics;

    [NativeTypeName("enum heif_matrix_coefficients")]
    public heif_matrix_coefficients matrix_coefficients;

    [NativeTypeName("uint8_t")]
    public byte full_range_flag;

    public float color_primary_red_x;

    public float color_primary_red_y;

    public float color_primary_green_x;

    public float color_primary_green_y;

    public float color_primary_blue_x;

    public float color_primary_blue_y;

    public float color_primary_white_x;

    public float color_primary_white_y;
}
