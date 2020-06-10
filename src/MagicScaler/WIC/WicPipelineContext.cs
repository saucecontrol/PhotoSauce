using System;
using System.Diagnostics;
using System.Collections.Generic;
using System.Runtime.InteropServices;

using PhotoSauce.Interop.Wic;

namespace PhotoSauce.MagicScaler
{
	internal sealed class WicPipelineContext : IDisposable
	{
		private readonly ComHandleCollection comHandles = new ComHandleCollection(4);
		private SafeHandle? unmanagedMemory = null;

		public IWICColorContext? SourceColorContext { get; set; }
		public IWICColorContext? DestColorContext { get; set; }
		public IWICPalette? DestPalette { get; set; }

		public T AddRef<T>(T comHandle) where T : class => comHandles.AddRef(comHandle);

		public SafeHandle AddUnmanagedMemory(int cb)
		{
			if (unmanagedMemory is not null) throw new InvalidOperationException("Memory already allocated");

			return unmanagedMemory = new SafeHGlobalHandle(Marshal.AllocHGlobal(cb), cb);
		}

		public void Dispose()
		{
			comHandles.Dispose();
			unmanagedMemory?.Dispose();
		}

		private sealed class SafeHGlobalHandle : SafeHandle
		{
			private readonly int count;

			public override bool IsInvalid => handle == IntPtr.Zero;

			public SafeHGlobalHandle(IntPtr handle, int cb) : base(IntPtr.Zero, true)
			{
				SetHandle(handle);
				GC.AddMemoryPressure(cb);
				count = cb;
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
	}

	internal sealed class ComHandleCollection : IDisposable
	{
		private readonly Stack<object> comHandles;

		public ComHandleCollection(int size) => comHandles = new Stack<object>(size);

		public T AddRef<T>(T comHandle) where T : class
		{
			Debug.Assert(Marshal.IsComObject(comHandle), "Not a COM object");

			comHandles.Push(comHandle);

			return comHandle;
		}

		public T AddOwnRef<T>(T comHandle) where T : class
		{

			Debug.Assert(Marshal.IsComObject(comHandle), "Not a COM object");

			var punk = Marshal.GetIUnknownForObject(comHandle);
			var newHandle = (T)Marshal.GetUniqueObjectForIUnknown(punk);
			Marshal.Release(punk);

			return AddRef(newHandle);
		}

		public void Dispose()
		{
			while (comHandles.Count > 0)
			{
				object h = comHandles.Pop();
				if (h is not null && Marshal.IsComObject(h))
					Marshal.ReleaseComObject(h);
			}
		}
	}

	internal static class ComHandle
	{
		internal readonly struct ComDisposer<T> : IDisposable where T : class
		{
			public T ComObject { get; }

			public ComDisposer(object obj)
			{
				Debug.Assert(Marshal.IsComObject(obj), "Not a COM object");
				if (!(obj is T com)) throw new ArgumentException("Interface not supported: " + typeof(T).Name, nameof(obj));

				ComObject = com;
			}

			public void Dispose()
			{
				if (ComObject is not null && Marshal.IsComObject(ComObject))
					Marshal.ReleaseComObject(ComObject);
			}
		}

		public static ComDisposer<T> Wrap<T>(T obj) where T : class => new ComDisposer<T>(obj);

		public static ComDisposer<T> QueryInterface<T>(object obj) where T : class => new ComDisposer<T>(obj);
	}
}
