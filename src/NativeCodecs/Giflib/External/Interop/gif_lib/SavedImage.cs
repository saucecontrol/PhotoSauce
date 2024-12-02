// Copyright © Clinton Ingram and Contributors
// SPDX-License-Identifier: MIT

// Ported from GIFLIB headers (gif_lib.h)
// Original source Copyright (c) 1997  Eric S. Raymond.
// See third-party-notices in the repository root for more information.

namespace PhotoSauce.Interop.Giflib;

internal unsafe partial struct SavedImage
{
    public GifImageDesc ImageDesc;

    [NativeTypeName("GifByteType *")]
    public byte* RasterBits;

    public int ExtensionBlockCount;

    public ExtensionBlock* ExtensionBlocks;
}
