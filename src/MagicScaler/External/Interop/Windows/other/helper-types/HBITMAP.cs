// Copyright © Tanner Gooding and Contributors. Licensed under the MIT License (MIT).
// See third-party-notices in the repository root for more information.

namespace TerraFX.Interop.Windows;

internal unsafe readonly partial record struct HBITMAP(nint Value)
{
    public static HBITMAP INVALID_VALUE => new HBITMAP((nint)(-1));

    public static HBITMAP NULL => default;

    public static explicit operator HBITMAP(void* value) => new HBITMAP((nint)(value));

    public static implicit operator void*(HBITMAP value) => (void*)(value.Value);

    public static explicit operator HBITMAP(HANDLE value) => new HBITMAP(value);

    public static implicit operator HANDLE(HBITMAP value) => new HANDLE(value.Value);

    public static explicit operator HBITMAP(nint value) => new HBITMAP(value);

    public static implicit operator nint(HBITMAP value) => value.Value;

    public static explicit operator HBITMAP(nuint value) => new HBITMAP((nint)(value));

    public static implicit operator nuint(HBITMAP value) => (nuint)(value.Value);
}
