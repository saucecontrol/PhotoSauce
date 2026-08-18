// Copyright © Clinton Ingram and Contributors
// SPDX-License-Identifier: MIT

using System.Runtime.InteropServices;
using System.Security;

namespace PhotoSauce.Interop.Libraw;

[StructLayout(LayoutKind.Sequential)]
internal struct psraw_options
{
	public int use_camera_white_balance;
	public int auto_brightness;
	public int half_size;
}

[StructLayout(LayoutKind.Sequential)]
internal struct psraw_image_info
{
	public int width;
	public int height;
	public int channels;
}

[SuppressUnmanagedCodeSecurity]
internal static unsafe class Libraw
{
	internal const int LIBRAW_SUCCESS = 0;
	internal const int LIBRAW_FILE_UNSUPPORTED = -2;
	internal const int LIBRAW_UNSUFFICIENT_MEMORY = -100007;

	[DllImport("psraw", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
	internal static extern int PsRawVersion();

	[DllImport("psraw", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
	internal static extern sbyte* PsRawStrError(int error_code);

	[DllImport("psraw", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
	internal static extern void* PsRawCreate(void* data, nuint length, psraw_options* options, psraw_image_info* info, int* error_code);

	[DllImport("psraw", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
	internal static extern int PsRawCopyPixels(void* decoder, int x, int y, int width, int height, int stride, byte* destination);

	[DllImport("psraw", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
	internal static extern void PsRawDestroy(void* decoder);
}
