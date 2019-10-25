using System;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace PhotoSauce.Interop.Wic
{
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
