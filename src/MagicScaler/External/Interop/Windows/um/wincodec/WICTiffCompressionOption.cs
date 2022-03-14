// Copyright © Tanner Gooding and Contributors. Licensed under the MIT License (MIT).
// See third-party-notices in the repository root for more information.

// Ported from um/wincodec.h in the Windows SDK for Windows 10.0.22000.0
// Original source is Copyright © Microsoft. All rights reserved.

namespace TerraFX.Interop.Windows;

internal enum WICTiffCompressionOption
{
    WICTiffCompressionDontCare = 0,
    WICTiffCompressionNone = 0x1,
    WICTiffCompressionCCITT3 = 0x2,
    WICTiffCompressionCCITT4 = 0x3,
    WICTiffCompressionLZW = 0x4,
    WICTiffCompressionRLE = 0x5,
    WICTiffCompressionZIP = 0x6,
    WICTiffCompressionLZWHDifferencing = 0x7,
    WICTIFFCOMPRESSIONOPTION_FORCE_DWORD = 0x7fffffff,
}
