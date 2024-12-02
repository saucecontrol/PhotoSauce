// Copyright © Clinton Ingram and Contributors
// SPDX-License-Identifier: MIT

using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Runtime.CompilerServices;
using System.Runtime.ExceptionServices;

namespace PhotoSauce.MagicScaler;

internal unsafe struct StreamWrapper
{
	public static readonly bool CaptureExceptions = !RuntimeInformation.IsOSPlatform(OSPlatform.Windows);

	private readonly long offset;
	private readonly GCHandle source;
	private GCHandle exception;

	private StreamWrapper(Stream managedSource)
	{
		offset = managedSource.Position;
		source = GCHandle.Alloc(managedSource);
	}

	public static StreamWrapper* Wrap(Stream managedSource)
	{
		var pinst = UnsafeUtil.NativeAlloc<StreamWrapper>();
		if (pinst is null)
			return null;

		*pinst = new(managedSource);
		return pinst;
	}

	public static void Free(StreamWrapper* pinst)
	{
		pinst->source.Free();

		if (pinst->exception.IsAllocated)
			pinst->exception.Free();

		*pinst = default;
		UnsafeUtil.NativeFree(pinst);
	}

	public readonly uint Read(void* pb, uint cb)
	{
		var stm = Unsafe.As<Stream>(source.Target!);

		uint read = 0;
		if (cb == 1)
		{
			int res = stm.ReadByte();
			if (res >= 0)
			{
				*(byte*)pb = (byte)res;
				read = cb;
			}

			return read;
		}

		var buff = new Span<byte>(pb, checked((int)cb));
		return (uint)stm.TryFillBuffer(buff);
	}

	public readonly void Write(void* pb, uint cb)
	{
		var stm = Unsafe.As<Stream>(source.Target!);
		if (cb == 1)
			stm.WriteByte(*(byte*)pb);
		else
			stm.Write(new ReadOnlySpan<byte>(pb, checked((int)cb)));
	}

	public readonly long Seek(long npos, SeekOrigin origin)
	{
		var stm = Unsafe.As<Stream>(source.Target!);

		if (origin == SeekOrigin.Begin)
			npos += offset;

		long cpos = stm.Position - offset;
		if (!(origin == SeekOrigin.Current && npos == 0) && !(origin == SeekOrigin.Begin && npos == cpos))
			cpos = stm.Seek(npos, origin) - offset;

		return cpos;
	}

	public readonly bool IsEof()
	{
		var stm = Unsafe.As<Stream>(source.Target!);

		return stm.Position == stm.Length;
	}

	public readonly bool IsPastEnd(long len)
	{
		var stm = Unsafe.As<Stream>(source.Target!);

		return len > stm.Length - offset;
	}

	public void SetException(ExceptionDispatchInfo edi) => exception = GCHandle.Alloc(edi);

	public readonly void ThrowIfExceptional()
	{
		if (exception.IsAllocated)
			Unsafe.As<ExceptionDispatchInfo>(exception.Target!).Throw();
	}
}
