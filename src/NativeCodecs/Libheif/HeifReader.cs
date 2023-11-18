// Copyright © Clinton Ingram and Contributors.  Licensed under the MIT License.

using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Runtime.ExceptionServices;
#if NET5_0_OR_GREATER
using System.Runtime.CompilerServices;
#endif

using PhotoSauce.MagicScaler;

namespace PhotoSauce.Interop.Libheif;

internal readonly unsafe struct HeifReader
{
#if NET5_0_OR_GREATER
	[UnmanagedCallersOnly(CallConvs = [ typeof(CallConvCdecl) ])]
	static
#endif
	private long getPosition(void* pinst)
	{
		var stm = (StreamWrapper*)pinst;
		try
		{
			return stm->Seek(0, SeekOrigin.Current);
		}
		catch (Exception ex) when (StreamWrapper.CaptureExceptions)
		{
			stm->SetException(ExceptionDispatchInfo.Capture(ex));
			return -1;
		}
	}

#if NET5_0_OR_GREATER
	[UnmanagedCallersOnly(CallConvs = [ typeof(CallConvCdecl) ])]
	static
#endif
	private int read(void* pv, nuint cb, void* pinst)
	{
		var stm = (StreamWrapper*)pinst;
		try
		{
			uint read = stm->Read(pv, checked((uint)cb));
			return read == cb ? 0 : 1;
		}
		catch (Exception ex) when (StreamWrapper.CaptureExceptions)
		{
			stm->SetException(ExceptionDispatchInfo.Capture(ex));
			return -1;
		}
	}

#if NET5_0_OR_GREATER
	[UnmanagedCallersOnly(CallConvs = [ typeof(CallConvCdecl) ])]
	static
#endif
	private int seek(long npos, void* pinst)
	{
		var stm = (StreamWrapper*)pinst;
		try
		{
			long cpos = stm->Seek(npos, SeekOrigin.Begin);
			return cpos == npos ? 0 : 1;
		}
		catch (Exception ex) when (StreamWrapper.CaptureExceptions)
		{
			stm->SetException(ExceptionDispatchInfo.Capture(ex));
			return -1;
		}
	}

#if NET5_0_OR_GREATER
	[UnmanagedCallersOnly(CallConvs = [ typeof(CallConvCdecl) ])]
	static
#endif
	private heif_reader_grow_status waitForLength(long len, void* pinst)
	{
		var stm = (StreamWrapper*)pinst;
		try
		{
			return stm->IsPastEnd(len)
				? heif_reader_grow_status.heif_reader_grow_status_size_beyond_eof
				: heif_reader_grow_status.heif_reader_grow_status_size_reached;
		}
		catch (Exception ex) when (StreamWrapper.CaptureExceptions)
		{
			stm->SetException(ExceptionDispatchInfo.Capture(ex));
			return (heif_reader_grow_status)(-1);
		}
	}

#if !NET5_0_OR_GREATER
	[UnmanagedFunctionPointer(CallingConvention.Cdecl)] private delegate long GetPosition(void* pinst);
	[UnmanagedFunctionPointer(CallingConvention.Cdecl)] private delegate int Read(void* pv, nuint cb, void* pinst);
	[UnmanagedFunctionPointer(CallingConvention.Cdecl)] private delegate int Seek(long npos, void* pinst);
	[UnmanagedFunctionPointer(CallingConvention.Cdecl)] private delegate heif_reader_grow_status WaitForLength(long len, void* pinst);

	private static readonly GetPosition delGetPosition = typeof(HeifReader).CreateMethodDelegate<GetPosition>(nameof(getPosition));
	private static readonly Read delRead = typeof(HeifReader).CreateMethodDelegate<Read>(nameof(read));
	private static readonly Seek delSeek = typeof(HeifReader).CreateMethodDelegate<Seek>(nameof(seek));
	private static readonly WaitForLength delWaitForLength = typeof(HeifReader).CreateMethodDelegate<WaitForLength>(nameof(waitForLength));
#endif

	public static readonly heif_reader* Impl = createImpl();

	private static heif_reader* createImpl()
	{
		var impl = (heif_reader*)UnsafeUtil.AllocateTypeAssociatedMemory(typeof(HeifReader), sizeof(heif_reader));
		impl->reader_api_version = 1;
#if NET5_0_OR_GREATER
		impl->get_position = &getPosition;
		impl->read = &read;
		impl->seek = &seek;
		impl->wait_for_file_size = &waitForLength;
#else
		impl->get_position = (delegate* unmanaged[Cdecl]<void*, long>)Marshal.GetFunctionPointerForDelegate(delGetPosition);
		impl->read = (delegate* unmanaged[Cdecl]<void*, nuint, void*, int>)Marshal.GetFunctionPointerForDelegate(delRead);
		impl->seek = (delegate* unmanaged[Cdecl]<long, void*, int>)Marshal.GetFunctionPointerForDelegate(delSeek);
		impl->wait_for_file_size = (delegate* unmanaged[Cdecl]<long, void*, heif_reader_grow_status>)Marshal.GetFunctionPointerForDelegate(delWaitForLength);
#endif

		return impl;
	}
}