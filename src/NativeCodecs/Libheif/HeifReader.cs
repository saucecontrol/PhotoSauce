// Copyright © Clinton Ingram and Contributors.  Licensed under the MIT License.

using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Runtime.CompilerServices;

#if !NET5_0_OR_GREATER
using PhotoSauce.MagicScaler;
#endif

namespace PhotoSauce.Interop.Libheif;

internal unsafe ref struct HeifReader
{
	private static readonly bool isWindows = RuntimeInformation.IsOSPlatform(OSPlatform.Windows);

	private readonly GCHandle source;
	private readonly uint offset;

	private HeifReader(Stream managedSource, uint offs = 0)
	{
		source = GCHandle.Alloc(managedSource, GCHandleType.Weak);
		offset = offs;
	}

	public static HeifReader* Wrap(Stream managedSource, uint offs = 0)
	{
#if NET6_0_OR_GREATER
		var ptr = (HeifReader*)NativeMemory.Alloc((nuint)sizeof(HeifReader));
#else
		var ptr = (HeifReader*)Marshal.AllocHGlobal(sizeof(HeifReader));
#endif
		*ptr = new(managedSource, offs);

		return ptr;
	}

	public static void Free(HeifReader* pinst)
	{
		pinst->source.Free();
		*pinst = default;
#if NET6_0_OR_GREATER
		NativeMemory.Free(pinst);
#else
		Marshal.FreeHGlobal((IntPtr)pinst);
#endif
	}

#if NET5_0_OR_GREATER
	[UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvCdecl) })]
	static
#endif
	private long getPosition(void* pinst)
	{
		try
		{
			var rdr = (HeifReader*)pinst;
			var stm = Unsafe.As<Stream>(rdr->source.Target!);

			return stm.Position - rdr->offset;
		}
		catch when (!isWindows)
		{
			return -1;
		}
	}

#if NET5_0_OR_GREATER
	[UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvCdecl) })]
	static
#endif
	private int read(void* pv, nuint cb, void* pinst)
	{
		try
		{
			var rdr = (HeifReader*)pinst;
			var stm = Unsafe.As<Stream>(rdr->source.Target!);
			nuint read = 0;

			if (cb == 1)
			{
				int res = stm.ReadByte();
				if (res >= 0)
				{
					*(byte*)pv = (byte)res;
					read = cb;
				}
			}
			else
			{
				var buff = new Span<byte>(pv, (int)cb);

				int rb;
				do
				{
					rb = stm.Read(buff);
					buff = buff[rb..];
				}
				while (rb != 0 && buff.Length != 0);

				read = cb - (uint)buff.Length;
			}

			return read == cb ? 0 : 1;
		}
		catch when (!isWindows)
		{
			return -1;
		}
	}

#if NET5_0_OR_GREATER
	[UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvCdecl) })]
	static
#endif
	private int seek(long npos, void* pinst)
	{
		try
		{
			var rdr = (HeifReader*)pinst;
			var stm = Unsafe.As<Stream>(rdr->source.Target!);
			long cpos = stm.Position - rdr->offset;

			if (cpos != npos)
			{
				npos += rdr->offset;
				cpos = stm.Seek(npos, SeekOrigin.Begin);
			}

			return cpos == npos ? 0 : 1;
		}
		catch when (!isWindows)
		{
			return -1;
		}
	}

#if NET5_0_OR_GREATER
	[UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvCdecl) })]
	static
#endif
	private heif_reader_grow_status waitForLength(long len, void* pinst)
	{
		try
		{
			var rdr = (HeifReader*)pinst;
			var stm = Unsafe.As<Stream>(rdr->source.Target!);

			return len > stm.Length - rdr->offset
				? heif_reader_grow_status.heif_reader_grow_status_size_beyond_eof
				: heif_reader_grow_status.heif_reader_grow_status_size_reached;
		}
		catch when (!isWindows)
		{
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
#if NET5_0_OR_GREATER
		var impl = (heif_reader*)RuntimeHelpers.AllocateTypeAssociatedMemory(typeof(HeifReader), sizeof(heif_reader));
		impl->reader_api_version = 1;
		impl->get_position = &getPosition;
		impl->read = &read;
		impl->seek = &seek;
		impl->wait_for_file_size = &waitForLength;
#else
		var impl = (heif_reader*)Marshal.AllocHGlobal(sizeof(heif_reader));
		impl->reader_api_version = 1;
		impl->get_position = (delegate* unmanaged[Cdecl]<void*, long>)Marshal.GetFunctionPointerForDelegate(delGetPosition);
		impl->read = (delegate* unmanaged[Cdecl]<void*, nuint, void*, int>)Marshal.GetFunctionPointerForDelegate(delRead);
		impl->seek = (delegate* unmanaged[Cdecl]<long, void*, int>)Marshal.GetFunctionPointerForDelegate(delSeek);
		impl->wait_for_file_size = (delegate* unmanaged[Cdecl]<long, void*, heif_reader_grow_status>)Marshal.GetFunctionPointerForDelegate(delWaitForLength);
#endif

		return impl;
	}
}