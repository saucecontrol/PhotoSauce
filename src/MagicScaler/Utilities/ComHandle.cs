using System;
using System.Runtime.InteropServices;

namespace PhotoSauce.MagicScaler.Interop
{
	internal readonly struct ComHandle<T> : IDisposable where T : class
	{
		public T ComObject { get; }

		public ComHandle(object obj)
		{
			if (!Marshal.IsComObject(obj)) throw new ArgumentException("Must be a COM object", nameof(obj));
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
