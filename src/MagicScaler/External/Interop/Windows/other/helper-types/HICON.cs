// Copyright © Tanner Gooding and Contributors. Licensed under the MIT License (MIT).
// See third-party-notices in the repository root for more information.

namespace TerraFX.Interop.Windows;

internal unsafe readonly partial record struct HICON(nint Value)
{
    public static HICON INVALID_VALUE => new HICON((nint)(-1));

    public static HICON NULL => default;

    public static explicit operator HICON(void* value) => new HICON((nint)(value));

    public static implicit operator void*(HICON value) => (void*)(value.Value);

    public static explicit operator HICON(HANDLE value) => new HICON(value);

    public static implicit operator HANDLE(HICON value) => new HANDLE(value.Value);

    public static explicit operator HICON(nint value) => new HICON(value);

    public static implicit operator nint(HICON value) => value.Value;

    public static explicit operator HICON(nuint value) => new HICON((nint)(value));

    public static implicit operator nuint(HICON value) => (nuint)(value.Value);
}
