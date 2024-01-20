// Copyright © Clinton Ingram and Contributors. Licensed under the MIT License (MIT).

// Ported from libheif headers (heif_version.h)
// Original source Copyright (c) struktur AG, Dirk Farin
// See third-party-notices in the repository root for more information.

using System;

namespace PhotoSauce.Interop.Libheif;

internal static partial class Libheif
{
    [NativeTypeName("#define LIBHEIF_NUMERIC_VERSION ((1<<24) | (17<<16) | (6<<8) | 0)")]
    public const int LIBHEIF_NUMERIC_VERSION = ((1 << 24) | (17 << 16) | (6 << 8) | 0);

    [NativeTypeName("#define LIBHEIF_VERSION \"1.17.6\"")]
    public const string LIBHEIF_VERSION = "1.17.6";
}
