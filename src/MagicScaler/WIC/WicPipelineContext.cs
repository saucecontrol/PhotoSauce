using System;
using System.Diagnostics;
using System.Collections.Generic;
using System.Runtime.InteropServices;

using PhotoSauce.Interop.Wic;

namespace PhotoSauce.MagicScaler
{
	internal class WicPipelineContext : IDisposable
	{
		private readonly ComHandleCollection comHandles = new ComHandleCollection(8);

		public IWICColorContext? SourceColorContext { get; set; }
		public IWICColorContext? DestColorContext { get; set; }
		public IWICPalette? DestPalette { get; set; }

		public T AddRef<T>(T comHandle) where T : class => comHandles.AddRef(comHandle);

		public void Dispose() => comHandles.Dispose();
	}

	internal class ComHandleCollection : IDisposable
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
				if (h != null && Marshal.IsComObject(h))
					Marshal.ReleaseComObject(h);
			}
		}
	}

	internal readonly struct ComHandle<T> : IDisposable where T : class
	{
		public T ComObject { get; }

		public ComHandle(object obj)
		{
			Debug.Assert(Marshal.IsComObject(obj), "Not a COM object");
			if (!(obj is T com)) throw new ArgumentException("Interface not supported: " + typeof(T).Name, nameof(obj));

			ComObject = com;
		}

		public void Dispose()
		{
			if (!(ComObject is null) && Marshal.IsComObject(ComObject))
				Marshal.ReleaseComObject(ComObject);
		}
	}
}
