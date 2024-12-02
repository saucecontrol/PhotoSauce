// Copyright © Clinton Ingram and Contributors
// SPDX-License-Identifier: MIT

using System;
using System.Security;
using System.Runtime.InteropServices;
using System.Runtime.ExceptionServices;
#if NET5_0_OR_GREATER
using System.Runtime.CompilerServices;
#endif

using PhotoSauce.MagicScaler;

namespace PhotoSauce.Interop.Giflib;

[SuppressUnmanagedCodeSecurity]
internal static unsafe partial class Giflib
{
	public static ReadOnlySpan<byte> Animexts1_0 => "ANIMEXTS1.0"u8;
	public static ReadOnlySpan<byte> Netscape2_0 => "NETSCAPE2.0"u8;
	public static ReadOnlySpan<byte> IccExtBlock => "ICCRGBG1012"u8;
}

internal unsafe partial struct GifFileType
{
	public readonly StreamWrapper* Stream => (StreamWrapper*)UserData;
}

#if NET5_0_OR_GREATER
static
#endif
internal unsafe class GifCallbacks
{
#if !NET5_0_OR_GREATER
	[UnmanagedFunctionPointer(CallingConvention.Cdecl)] private delegate int ReadDelegate(GifFileType* pinst, byte* buff, int cb);
	[UnmanagedFunctionPointer(CallingConvention.Cdecl)] private delegate int WriteDelegate(GifFileType* pinst, byte* buff, int cb);

	private static readonly ReadDelegate delRead = typeof(GifCallbacks).CreateMethodDelegate<ReadDelegate>(nameof(read));
	private static readonly WriteDelegate delWrite = typeof(GifCallbacks).CreateMethodDelegate<WriteDelegate>(nameof(write));
#endif

	public static readonly delegate* unmanaged[Cdecl]<GifFileType*, byte*, int, int> Read =
#if NET5_0_OR_GREATER
		&read;
#else
		(delegate* unmanaged[Cdecl]<GifFileType*, byte*, int, int>)Marshal.GetFunctionPointerForDelegate(delRead);
#endif

	public static readonly delegate* unmanaged[Cdecl]<GifFileType*, byte*, int, int> Write =
#if NET5_0_OR_GREATER
		&write;
#else
		(delegate* unmanaged[Cdecl]<GifFileType*, byte*, int, int>)Marshal.GetFunctionPointerForDelegate(delWrite);
#endif

#if NET5_0_OR_GREATER
	[UnmanagedCallersOnly(CallConvs = [typeof(CallConvCdecl)])]
	static
#endif
	private int read(GifFileType* pinst, byte* buff, int cb)
	{
		try
		{
			return (int)pinst->Stream->Read(buff, (uint)cb);
		}
		catch (Exception ex) when (StreamWrapper.CaptureExceptions)
		{
			pinst->Stream->SetException(ExceptionDispatchInfo.Capture(ex));
			return 0;
		}
	}

#if NET5_0_OR_GREATER
	[UnmanagedCallersOnly(CallConvs = [typeof(CallConvCdecl)])]
	static
#endif
	private int write(GifFileType* pinst, byte* buff, int cb)
	{
		// encoder may make a call to write trailer byte after we've yanked the stream from under it
		if (pinst->Stream is null)
			return 0;

		try
		{
			pinst->Stream->Write(buff, (uint)cb);
			return cb;
		}
		catch (Exception ex) when (StreamWrapper.CaptureExceptions)
		{
			pinst->Stream->SetException(ExceptionDispatchInfo.Capture(ex));
			return 0;
		}
	}
}
