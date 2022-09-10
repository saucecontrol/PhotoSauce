// Copyright © Clinton Ingram and Contributors. Licensed under the MIT License (MIT).

// Ported from libpng headers (png.h)
// Original source Copyright (c) 1995-2022 The PNG Reference Library Authors.
// See third-party-notices in the repository root for more information.

namespace PhotoSauce.Interop.Libpng;

internal partial struct png_color_struct
{
    [NativeTypeName("png_byte")]
    public byte red;

    [NativeTypeName("png_byte")]
    public byte green;

    [NativeTypeName("png_byte")]
    public byte blue;
}
