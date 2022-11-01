// Copyright © Clinton Ingram and Contributors. Licensed under the MIT License (MIT).

// Ported from GIFLIB headers (gif_lib.h)
// Original source Copyright (c) 1997  Eric S. Raymond.
// See third-party-notices in the repository root for more information.

namespace PhotoSauce.Interop.Giflib;

internal unsafe partial struct ColorMapObject
{
    public int ColorCount;

    public int BitsPerPixel;

    [NativeTypeName("bool")]
    public byte SortFlag;

    public GifColorType* Colors;
}
