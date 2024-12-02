// Copyright © Clinton Ingram and Contributors
// SPDX-License-Identifier: MIT

// Ported from libheif headers (heif.h)
// Original source Copyright (c) struktur AG, Dirk Farin
// See third-party-notices in the repository root for more information.

namespace PhotoSauce.Interop.Libheif;

internal partial struct heif_camera_intrinsic_matrix
{
    public double focal_length_x;

    public double focal_length_y;

    public double principal_point_x;

    public double principal_point_y;

    public double skew;
}
