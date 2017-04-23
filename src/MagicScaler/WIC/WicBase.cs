using System;
using System.Diagnostics;
using System.Collections.Generic;
using System.Runtime.InteropServices;

using PhotoSauce.MagicScaler.Interop;

namespace PhotoSauce.MagicScaler
{
	internal abstract class WicBase : IDisposable
	{
		protected static readonly IWICImagingFactory Wic = new WICImagingFactory2() as IWICImagingFactory;

		private readonly Stack<object> comHandles = new Stack<object>();

		protected T AddRef<T>(T comHandle) where T : class
		{
			Debug.Assert(Marshal.IsComObject(comHandle), "Not a COM object");

			comHandles.Push(comHandle);

			return comHandle;
		}

		private void PopRelease()
		{
			var h = comHandles.Pop();
			if (h != null && Marshal.IsComObject(h))
				Marshal.ReleaseComObject(h);
		}

		protected void Release<T>(T comHandle) where T : class
		{
			Debug.Assert(ReferenceEquals(comHandles.Peek(), comHandle), "Release() should only be called on the last handle passed to AddRef()");

			PopRelease();
		}

		protected T AddOwnRef<T>(T comHandle) where T : class
		{
			if (comHandle == null)
				return null;

			var punk = Marshal.GetIUnknownForObject(comHandle);
			var newHandle = (T)Marshal.GetUniqueObjectForIUnknown(punk);
			Marshal.Release(punk);

			return AddRef(newHandle);
		}

		public virtual void Dispose()
		{
			while (comHandles.Count > 0)
				PopRelease();
		}
	}

	internal class WicProcessingContext : WicBase
	{
		private IWICColorContext sourceColorContext;
		private IWICColorContext destColorContext;
		private IWICPalette destPalette;

		public ProcessImageSettings Settings;
		public Guid ContainerFormat;
		public uint ContainerFrameCount;
		public Guid PixelFormat;
		public uint Width;
		public uint Height;
		public WICBitmapTransformOptions TransformOptions;
		public IDictionary<string, PropVariant> Metadata;
		public uint[] CustomPalette;
		public bool SupportsPlanar;
		public bool IsSubsampled;
		public bool HasAlpha;
		public bool IsGreyscale;
		public bool IsCmyk;
		public bool NeedsCache;

		public bool IsRotated90 => TransformOptions.HasFlag(WICBitmapTransformOptions.WICBitmapTransformRotate90);

		public IWICColorContext SourceColorContext { get => sourceColorContext; set => sourceColorContext = AddOwnRef(value); }
		public IWICColorContext DestColorContext { get => destColorContext; set => destColorContext = AddOwnRef(value); }
		public IWICPalette DestPalette { get => destPalette; set => destPalette = AddOwnRef(value); }

		public WicProcessingContext(ProcessImageSettings settings)
		{
			Settings = settings.Clone();
		}
	}
}
