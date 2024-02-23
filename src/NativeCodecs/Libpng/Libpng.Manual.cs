// Copyright © Clinton Ingram and Contributors.  Licensed under the MIT License.

using System;
using System.Security;
using System.Runtime.InteropServices;
using System.Runtime.CompilerServices;
using System.Runtime.ExceptionServices;

using PhotoSauce.MagicScaler;

namespace PhotoSauce.Interop.Libpng;

[SuppressUnmanagedCodeSecurity]
internal static unsafe partial class Libpng
{
	public static bool HasChunk(this ref ps_png_struct png, uint chunk) =>
		PngGetValid((ps_png_struct*)Unsafe.AsPointer(ref png), chunk) != FALSE;
}

internal unsafe partial struct ps_io_data
{
	public readonly StreamWrapper* Stream => (StreamWrapper*)stream_handle;
}

#if NET5_0_OR_GREATER
static
#endif
internal unsafe class PngCallbacks
{
#if !NET5_0_OR_GREATER
	[UnmanagedFunctionPointer(CallingConvention.Cdecl)] private delegate nuint ReadDelegate(nint pinst, byte* buff, nuint cb);
	[UnmanagedFunctionPointer(CallingConvention.Cdecl)] private delegate nuint WriteDelegate(nint pinst, byte* buff, nuint cb);

	private static readonly ReadDelegate delRead = typeof(PngCallbacks).CreateMethodDelegate<ReadDelegate>(nameof(read));
	private static readonly WriteDelegate delWrite = typeof(PngCallbacks).CreateMethodDelegate<WriteDelegate>(nameof(write));
#endif

	public static readonly delegate* unmanaged[Cdecl]<nint, byte*, nuint, nuint> Read =
#if NET5_0_OR_GREATER
		&read;
#else
		(delegate* unmanaged[Cdecl]<nint, byte*, nuint, nuint>)Marshal.GetFunctionPointerForDelegate(delRead);
#endif

	public static readonly delegate* unmanaged[Cdecl]<nint, byte*, nuint, nuint> Write =
#if NET5_0_OR_GREATER
		&write;
#else
		(delegate* unmanaged[Cdecl]<nint, byte*, nuint, nuint>)Marshal.GetFunctionPointerForDelegate(delWrite);
#endif

#if NET5_0_OR_GREATER
	[UnmanagedCallersOnly(CallConvs = [typeof(CallConvCdecl)])]
	static
#endif
	private nuint read(nint pinst, byte* buff, nuint cb)
	{
		var stm = (StreamWrapper*)pinst;
		try
		{
			return stm->Read(buff, checked((uint)cb));
		}
		catch (Exception ex) when (StreamWrapper.CaptureExceptions)
		{
			stm->SetException(ExceptionDispatchInfo.Capture(ex));
			return unchecked((nuint)~0ul);
		}
	}

#if NET5_0_OR_GREATER
	[UnmanagedCallersOnly(CallConvs = [typeof(CallConvCdecl)])]
	static
#endif
	private nuint write(nint pinst, byte* buff, nuint cb)
	{
		var stm = (StreamWrapper*)pinst;
		try
		{
			stm->Write(buff, checked((uint)cb));
			return cb;
		}
		catch (Exception ex) when (StreamWrapper.CaptureExceptions)
		{
			stm->SetException(ExceptionDispatchInfo.Capture(ex));
			return 0;
		}
	}
}
