// Copyright © Clinton Ingram and Contributors
// SPDX-License-Identifier: MIT

// Ported from libheif headers (heif.h)
// Original source Copyright (c) struktur AG, Dirk Farin
// See third-party-notices in the repository root for more information.

namespace PhotoSauce.Interop.Libheif;

internal enum heif_chroma_downsampling_algorithm
{
    heif_chroma_downsampling_nearest_neighbor = 1,
    heif_chroma_downsampling_average = 2,
    heif_chroma_downsampling_sharp_yuv = 3,
}
