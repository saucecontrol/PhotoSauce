// Copyright © Clinton Ingram and Contributors
// SPDX-License-Identifier: MIT

// Ported from libheif headers (heif.h)
// Original source Copyright (c) struktur AG, Dirk Farin
// See third-party-notices in the repository root for more information.

namespace PhotoSauce.Interop.Libheif;

internal unsafe partial struct heif_plugin_info
{
    public int version;

    [NativeTypeName("enum heif_plugin_type")]
    public heif_plugin_type type;

    [NativeTypeName("const void *")]
    public void* plugin;

    public void* internal_handle;
}
