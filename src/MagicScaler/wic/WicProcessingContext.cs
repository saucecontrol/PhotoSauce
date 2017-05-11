using System;
using System.Diagnostics;
using System.Collections.Generic;
using System.Runtime.InteropServices;

using PhotoSauce.MagicScaler.Interop;

namespace PhotoSauce.MagicScaler
{
	internal class WicProcessingContext : IDisposable
	{
		private readonly Stack<object> comHandles = new Stack<object>();
		private readonly Stack<IDisposable> disposeHandles = new Stack<IDisposable>();

		private IWICColorContext sourceColorContext;
		private IWICColorContext destColorContext;
		private IWICPalette destPalette;

		public ProcessImageSettings Settings { get; private set; }
		public WicDecoder Decoder { get; set; }
		public WicFrameReader DecoderFrame { get; set; }
		public PixelSource Source { get; set; }
		public PixelSource PlanarLumaSource { get; set; }
		public PixelSource PlanarChromaSource { get; set; }

		public IWICColorContext SourceColorContext { get => sourceColorContext; set => sourceColorContext = AddOwnRef(value); }
		public IWICColorContext DestColorContext { get => destColorContext; set => destColorContext = AddOwnRef(value); }
		public IWICPalette DestPalette { get => destPalette; set => destPalette = AddOwnRef(value); }

		public WicProcessingContext(ProcessImageSettings settings)
		{
			Settings = settings.Clone();
		}

		public T AddDispose<T>(T disposeHandle) where T : IDisposable
		{
			disposeHandles.Push(disposeHandle);

			return disposeHandle;
		}

		public T AddRef<T>(T comHandle) where T : class
		{
			Debug.Assert(Marshal.IsComObject(comHandle), "Not a COM object");

			comHandles.Push(comHandle);

			return comHandle;
		}

		public T AddOwnRef<T>(T comHandle) where T : class
		{
			if (comHandle == null)
				return null;

			var punk = Marshal.GetIUnknownForObject(comHandle);
			var newHandle = (T)Marshal.GetUniqueObjectForIUnknown(punk);
			Marshal.Release(punk);

			return AddRef(newHandle);
		}

		public void FinalizeSettings()
		{
			if (!Settings.Normalized)
			{
				Settings.Fixup((int)Source.Width, (int)Source.Height, DecoderFrame.SwapDimensions);

				if (Settings.SaveFormat == FileFormat.Auto)
					Settings.SetSaveFormat(Decoder.ContainerFormat, Source.Format.AlphaRepresentation != PixelAlphaRepresentation.None);
			}
		}

		public void SwitchPlanarSource(WicPlane plane)
		{
			if (plane == WicPlane.Chroma)
			{
				PlanarLumaSource = Source;
				Source = PlanarChromaSource;
			}
			else
			{
				PlanarChromaSource = Source;
				Source = PlanarLumaSource;
			}
		}

		public void Dispose()
		{
			while (comHandles.Count > 0)
			{
				var h = comHandles.Pop();
				if (h != null && Marshal.IsComObject(h))
					Marshal.ReleaseComObject(h);
			}

			while (disposeHandles.Count > 0)
				disposeHandles.Pop()?.Dispose();
		}
	}
}
