using System;
using System.Diagnostics;
using System.Collections.Generic;
using System.Runtime.InteropServices;

using PhotoSauce.MagicScaler.Interop;

namespace PhotoSauce.MagicScaler
{
	internal class WicPipelineContext : IDisposable
	{
		private readonly Stack<object> comHandles = new Stack<object>();

		public WicPlanarCache? PlanarCache { get; set; }

		public IWICColorContext? SourceColorContext { get; set; }
		public IWICColorContext? DestColorContext { get; set; }
		public IWICPalette? DestPalette { get; set; }

		public T AddRef<T>(T comHandle) where T : class
		{
			Debug.Assert(Marshal.IsComObject(comHandle), "Not a COM object");

			comHandles.Push(comHandle);

			return comHandle;
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
}
