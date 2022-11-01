// Copyright © Clinton Ingram and Contributors.  Licensed under the MIT License.

using System;
using System.Security;

namespace PhotoSauce.Interop.Giflib;

[SuppressUnmanagedCodeSecurity]
internal static unsafe partial class Giflib
{
	public static ReadOnlySpan<byte> Animexts1_0 => "ANIMEXTS1.0"u8;
	public static ReadOnlySpan<byte> Netscape2_0 => "NETSCAPE2.0"u8;
}
