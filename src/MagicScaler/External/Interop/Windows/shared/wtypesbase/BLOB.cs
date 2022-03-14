// Copyright © Tanner Gooding and Contributors. Licensed under the MIT License (MIT).
// See third-party-notices in the repository root for more information.

// Ported from shared/wtypesbase.h in the Windows SDK for Windows 10.0.22000.0
// Original source is Copyright © Microsoft. All rights reserved.

namespace TerraFX.Interop.Windows;

internal unsafe partial struct BLOB
{
    [NativeTypeName("ULONG")]
    public uint cbSize;

    public byte* pBlobData;
}
