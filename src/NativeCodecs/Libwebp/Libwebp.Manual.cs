// Copyright © Clinton Ingram and Contributors.  Licensed under the MIT License.

using System.Security;

namespace PhotoSauce.Interop.Libwebp;

[SuppressUnmanagedCodeSecurity]
internal static partial class Libwebp
{
}

internal enum WebPPlane
{
	Bgra,
	Bgr,
	Y,
	U,
	V
}

internal static unsafe class WebPExtensions
{
	public static bool IsAllocated(this WebPDecBuffer b) => b.u.RGBA.rgba is not null;
}