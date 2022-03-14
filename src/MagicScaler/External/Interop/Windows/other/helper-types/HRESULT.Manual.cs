// Copyright © Tanner Gooding and Contributors. Licensed under the MIT License (MIT).
// See third-party-notices in the repository root for more information.

namespace TerraFX.Interop.Windows;

internal unsafe readonly partial record struct HRESULT
{
    public bool FAILED => Windows.FAILED(Value);

    public bool SUCCEEDED => Windows.SUCCEEDED(Value);
}
