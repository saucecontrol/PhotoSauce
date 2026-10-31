// Copyright © Clinton Ingram and Contributors
// SPDX-License-Identifier: MIT

using System;
using System.Runtime.InteropServices;
using System.Security;

namespace PhotoSauce.NativeCodecs.JxlRs;

[StructLayout(LayoutKind.Sequential)]
internal unsafe struct JxlRsImageInfo
{
	public uint Width;
	public uint Height;
	public uint FrameCount;
	public uint Channels;
	public uint AlphaAssociated;
	public uint Orientation;
	public uint LoopCount;
	public nuint IccLength;
}

[StructLayout(LayoutKind.Sequential)]
internal unsafe struct JxlRsFrameInfo
{
	public double DurationMilliseconds;
}

[StructLayout(LayoutKind.Sequential)]
internal struct JxlRsStream
{
	public nint Context;
	public nint Read;
	public nint Seek;
}

[SuppressUnmanagedCodeSecurity]
internal static unsafe class JxlRsNative
{
	public const string LibraryName = "jxlrs";

	[DllImport(LibraryName, ExactSpelling = true, CallingConvention = CallingConvention.Cdecl)]
	public static extern uint jxlrs_version();

	[DllImport(LibraryName, ExactSpelling = true, CallingConvention = CallingConvention.Cdecl)]
	public static extern nint jxlrs_inspect(byte* data, nuint length, JxlRsImageInfo* info);

	[DllImport(LibraryName, ExactSpelling = true, CallingConvention = CallingConvention.Cdecl)]
	public static extern nint jxlrs_inspect_with_options(byte* data, nuint length, JxlRsImageInfo* info, uint parallelism);

	[DllImport(LibraryName, ExactSpelling = true, CallingConvention = CallingConvention.Cdecl)]
	public static extern nint jxlrs_inspect_stream(JxlRsStream* stream, JxlRsImageInfo* info);

	[DllImport(LibraryName, ExactSpelling = true, CallingConvention = CallingConvention.Cdecl)]
	public static extern nint jxlrs_inspect_stream_with_options(JxlRsStream* stream, JxlRsImageInfo* info, uint parallelism);

	[DllImport(LibraryName, ExactSpelling = true, CallingConvention = CallingConvention.Cdecl)]
	[return: MarshalAs(UnmanagedType.I1)]
	public static extern bool jxlrs_get_frame(nint handle, uint index, JxlRsFrameInfo* info);

	[DllImport(LibraryName, ExactSpelling = true, CallingConvention = CallingConvention.Cdecl)]
	[return: MarshalAs(UnmanagedType.I1)]
	public static extern bool jxlrs_decode_frame(nint handle, uint index, byte* data, nuint length, byte* output, nuint outputLength, nuint outputStride);

	[DllImport(LibraryName, ExactSpelling = true, CallingConvention = CallingConvention.Cdecl)]
	[return: MarshalAs(UnmanagedType.I1)]
	public static extern bool jxlrs_decode_frame_stream(nint handle, uint index, JxlRsStream* stream, byte* output, nuint outputLength, nuint outputStride);

	[DllImport(LibraryName, ExactSpelling = true, CallingConvention = CallingConvention.Cdecl)]
	public static extern byte* jxlrs_get_icc(nint handle);

	[DllImport(LibraryName, ExactSpelling = true, CallingConvention = CallingConvention.Cdecl)]
	public static extern void jxlrs_destroy(nint handle);

	[DllImport(LibraryName, ExactSpelling = true, CallingConvention = CallingConvention.Cdecl)]
	public static extern nint jxlrs_last_error();
}
