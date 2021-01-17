// Copyright © Clinton Ingram and Contributors.  Licensed under the MIT License.

using System;
using System.Runtime.InteropServices;
using System.Runtime.CompilerServices;

namespace PhotoSauce.MagicScaler
{
	internal sealed unsafe class SafeComCallable<T> : SafeHandle where T : unmanaged
	{
		public override bool IsInvalid => handle == IntPtr.Zero;

		public SafeComCallable(in T cc) : base(IntPtr.Zero, true)
		{
			SetHandle(Marshal.AllocHGlobal(sizeof(T)));
			Unsafe.CopyBlockUnaligned(ref *(byte*)handle, ref Unsafe.As<T, byte>(ref Unsafe.AsRef(cc)), (uint)sizeof(T));
		}

		protected override bool ReleaseHandle()
		{
			if (IsInvalid)
				return false;

			GCHandle.FromIntPtr(((nint*)handle)[1]).Free();
			Unsafe.InitBlockUnaligned((byte*)handle, 0, (uint)sizeof(T));
			Marshal.FreeHGlobal(handle);
			handle = IntPtr.Zero;

			return true;
		}
	}

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

	internal ref struct WeakGCHandle
	{
		public GCHandle Handle { get; }

		public WeakGCHandle(object target) => Handle = GCHandle.Alloc(target, GCHandleType.Weak);

		public void Dispose() => Handle.Free();
	}
}
