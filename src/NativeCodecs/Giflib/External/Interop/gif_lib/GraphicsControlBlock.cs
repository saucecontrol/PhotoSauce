// Copyright © Clinton Ingram and Contributors. Licensed under the MIT License (MIT).

// Ported from GIFLIB headers (gif_lib.h)
// Original source Copyright (c) 1997  Eric S. Raymond.
// See third-party-notices in the repository root for more information.

namespace PhotoSauce.Interop.Giflib;

internal partial struct GraphicsControlBlock
{
    public int DisposalMode;

    [NativeTypeName("bool")]
    public byte UserInputFlag;

    public int DelayTime;

    public int TransparentColor;
}
