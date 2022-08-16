// Copyright © Clinton Ingram and Contributors.  Licensed under the MIT License.

using System;
using System.Runtime.InteropServices;

namespace PhotoSauce.MagicScaler;

internal sealed class SafeHGlobalHandle : SafeHandle
{
	private readonly int count;

	public override bool IsInvalid => handle == IntPtr.Zero;

	public SafeHGlobalHandle(int cb) : base(IntPtr.Zero, true)
	{
		SetHandle(Marshal.AllocHGlobal(cb));
		GC.AddMemoryPressure(count = cb);
	}

	protected override bool ReleaseHandle()
	{
		if (IsInvalid)
			return false;

		Marshal.FreeHGlobal(handle);
		GC.RemoveMemoryPressure(count);
		handle = IntPtr.Zero;

		return true;
	}
}

internal ref struct SafeHandleReleaser
{
	private SafeHandle? handle;

	public SafeHandle? Handle => handle;

	public SafeHandleReleaser(SafeHandle h) => handle = h;

	public SafeHandle Attach(SafeHandle h) => handle = h;

	public SafeHandle? Detach()
	{
		var h = handle;
		handle = null;

		return h;
	}

	public void Dispose() => handle?.Dispose();
}
