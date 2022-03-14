// Copyright © Tanner Gooding and Contributors. Licensed under the MIT License (MIT).
// See third-party-notices in the repository root for more information.

// Ported from um/wincodec.h in the Windows SDK for Windows 10.0.22000.0
// Original source is Copyright © Microsoft. All rights reserved.

using System;

namespace TerraFX.Interop.Windows;

internal partial struct WICBitmapPlaneDescription
{
    [NativeTypeName("WICPixelFormatGUID")]
    public Guid Format;

    public uint Width;

    public uint Height;
}
