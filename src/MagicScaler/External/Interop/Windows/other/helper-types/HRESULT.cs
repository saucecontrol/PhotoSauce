// Copyright © Tanner Gooding and Contributors. Licensed under the MIT License (MIT).
// See third-party-notices in the repository root for more information.

namespace TerraFX.Interop.Windows;

internal unsafe readonly partial record struct HRESULT(int Value)
{
    public static implicit operator HRESULT(int value) => new HRESULT(value);

    public static implicit operator int(HRESULT value) => value.Value;

    public static explicit operator HRESULT(uint value) => new HRESULT((int)(value));

    public static explicit operator uint(HRESULT value) => (uint)(value.Value);
}
