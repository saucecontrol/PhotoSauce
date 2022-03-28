// Copyright © Clinton Ingram and Contributors. Licensed under the MIT License (MIT).

// Ported from libheif headers (heif.h)
// Original source Copyright (c) struktur AG, Dirk Farin <farin@struktur.de>
// See third-party-notices in the repository root for more information.

namespace PhotoSauce.Interop.Libheif;

internal enum heif_reader_grow_status
{
    heif_reader_grow_status_size_reached,
    heif_reader_grow_status_timeout,
    heif_reader_grow_status_size_beyond_eof,
}
