// Copyright © Clinton Ingram and Contributors
// SPDX-License-Identifier: MIT

// Ported from libheif headers (heif_properties.h)
// Original source Copyright (c) struktur AG, Dirk Farin
// See third-party-notices in the repository root for more information.

namespace PhotoSauce.Interop.Libheif;

internal unsafe partial struct heif_property_user_description
{
    public int version;

    [NativeTypeName("const char *")]
    public sbyte* lang;

    [NativeTypeName("const char *")]
    public sbyte* name;

    [NativeTypeName("const char *")]
    public sbyte* description;

    [NativeTypeName("const char *")]
    public sbyte* tags;
}
