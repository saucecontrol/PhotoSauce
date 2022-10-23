// Copyright © Clinton Ingram and Contributors.  Licensed under the MIT License.

using System.Security;
using System.Runtime.CompilerServices;

namespace PhotoSauce.Interop.Libpng;

[SuppressUnmanagedCodeSecurity]
internal static unsafe partial class Libpng
{
	public static bool HasChunk(this ref ps_png_struct png, uint chunk) =>
		PngGetValid((ps_png_struct*)Unsafe.AsPointer(ref png), chunk) != FALSE;
}
