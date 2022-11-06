// Copyright © Clinton Ingram and Contributors.  Licensed under the MIT License.

using System.Security;

namespace PhotoSauce.Interop.Libjxl;

[SuppressUnmanagedCodeSecurity]
internal static partial class Libjxl
{
	public const uint BoxTypeExif = 'E' | 'x' << 8 | 'i' << 16 | 'f' << 24;
}
