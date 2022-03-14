// Copyright © Tanner Gooding and Contributors. Licensed under the MIT License (MIT).
// See third-party-notices in the repository root for more information.

namespace TerraFX.Interop.Windows;

internal unsafe readonly partial record struct HANDLE(nint Value)
{
    public static HANDLE INVALID_VALUE => new HANDLE((nint)(-1));

    public static HANDLE NULL => default;

    public static explicit operator HANDLE(void* value) => new HANDLE((nint)(value));

    public static implicit operator void*(HANDLE value) => (void*)(value.Value);

    public static explicit operator HANDLE(nint value) => new HANDLE(value);

    public static implicit operator nint(HANDLE value) => value.Value;

    public static explicit operator HANDLE(nuint value) => new HANDLE((nint)(value));

    public static implicit operator nuint(HANDLE value) => (nuint)(value.Value);
}
