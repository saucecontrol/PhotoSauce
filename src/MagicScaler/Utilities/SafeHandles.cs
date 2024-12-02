// Copyright © Clinton Ingram and Contributors
// SPDX-License-Identifier: MIT

using System;
using System.Runtime.InteropServices;

namespace PhotoSauce.MagicScaler;

internal sealed unsafe class SafeNativeMemoryHandle : SafeHandle
{
	private readonly nuint count;

	public override bool IsInvalid => handle == IntPtr.Zero;

	public SafeNativeMemoryHandle(nuint cb) : base(IntPtr.Zero, true)
	{
		SetHandle((nint)UnsafeUtil.NativeAlloc(cb));

		if (!IsInvalid)
			GC.AddMemoryPressure((long)(count = cb));
	}

	protected override bool ReleaseHandle()
	{
		if (IsInvalid)
			return false;

		UnsafeUtil.NativeFree((void*)handle);
		SetHandle(IntPtr.Zero);
		GC.RemoveMemoryPressure((long)count);

		return true;
	}
}

internal ref struct SafeHandleReleaser(SafeHandle h)
{
	private SafeHandle? handle = h;

	public readonly SafeHandle? Handle => handle;

	public SafeHandle Attach(SafeHandle h) => handle = h;

	public SafeHandle? Detach()
	{
		var h = handle;
		handle = null;

		return h;
	}

	public void Dispose() => handle?.Dispose();
}
