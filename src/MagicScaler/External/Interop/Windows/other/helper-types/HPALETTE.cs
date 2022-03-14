// Copyright © Tanner Gooding and Contributors. Licensed under the MIT License (MIT).
// See third-party-notices in the repository root for more information.

namespace TerraFX.Interop.Windows;

internal unsafe readonly partial record struct HPALETTE(nint Value)
{
    public static HPALETTE INVALID_VALUE => new HPALETTE((nint)(-1));

    public static HPALETTE NULL => default;

    public static explicit operator HPALETTE(void* value) => new HPALETTE((nint)(value));

    public static implicit operator void*(HPALETTE value) => (void*)(value.Value);

    public static explicit operator HPALETTE(HANDLE value) => new HPALETTE(value);

    public static implicit operator HANDLE(HPALETTE value) => new HANDLE(value.Value);

    public static explicit operator HPALETTE(nint value) => new HPALETTE(value);

    public static implicit operator nint(HPALETTE value) => value.Value;

    public static explicit operator HPALETTE(nuint value) => new HPALETTE((nint)(value));

    public static implicit operator nuint(HPALETTE value) => (nuint)(value.Value);
}
