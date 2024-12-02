// Copyright © Clinton Ingram and Contributors
// SPDX-License-Identifier: MIT

// Ported from GIFLIB headers (gif_lib.h)
// Original source Copyright (c) 1997  Eric S. Raymond.
// See third-party-notices in the repository root for more information.

namespace PhotoSauce.Interop.Giflib;

internal unsafe partial struct GifFileType
{
    [NativeTypeName("GifWord")]
    public int SWidth;

    [NativeTypeName("GifWord")]
    public int SHeight;

    [NativeTypeName("GifWord")]
    public int SColorResolution;

    [NativeTypeName("GifWord")]
    public int SBackGroundColor;

    [NativeTypeName("GifByteType")]
    public byte AspectByte;

    public ColorMapObject* SColorMap;

    public int ImageCount;

    public GifImageDesc Image;

    public SavedImage* SavedImages;

    public int ExtensionBlockCount;

    public ExtensionBlock* ExtensionBlocks;

    public int Error;

    public void* UserData;

    private void* Private;
}
