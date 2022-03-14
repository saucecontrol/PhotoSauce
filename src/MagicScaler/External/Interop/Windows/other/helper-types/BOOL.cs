// Copyright © Tanner Gooding and Contributors. Licensed under the MIT License (MIT).
// See third-party-notices in the repository root for more information.

namespace TerraFX.Interop.Windows;

internal readonly partial record struct BOOL(int Value)
{
    public static BOOL FALSE => new BOOL(0);

    public static BOOL TRUE => new BOOL(1);

    public static implicit operator bool(BOOL value) => value.Value != 0;

    public static implicit operator BOOL(bool value) => new BOOL(value ? 1 : 0);

    public static bool operator false(BOOL value) => value.Value == 0;

    public static bool operator true(BOOL value) => value.Value != 0;

    public static implicit operator BOOL(int value) => new BOOL(value);

    public static implicit operator int(BOOL value) => value.Value;

    public static explicit operator BOOL(uint value) => new BOOL((int)(value));

    public static explicit operator uint(BOOL value) => (uint)(value.Value);
}
