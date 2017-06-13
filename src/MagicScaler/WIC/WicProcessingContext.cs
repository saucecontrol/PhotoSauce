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

		private PixelSource source;

		public ProcessImageSettings Settings { get; private set; }
		public ProcessImageSettings UsedSettings { get; private set; }
		public IList<PixelSourceStats> Stats { get; private set; }

		public WicDecoder Decoder { get; set; }
		public WicFrameReader DecoderFrame { get; set; }
		public PixelSource PlanarLumaSource { get; set; }
		public PixelSource PlanarChromaSource { get; set; }

		public IWICColorContext SourceColorContext { get; set; }
		public IWICColorContext DestColorContext { get; set; }
		public IWICPalette DestPalette { get; set; }

		public PixelSource Source
		{
			get => source;
			set
			{
				source = value;
				Stats.Add(source.Stats);
			}
		}

		public WicProcessingContext(ProcessImageSettings settings)
		{
			Settings = settings.Clone();
			Stats = new List<PixelSourceStats>();
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

		public void FinalizeSettings()
		{
			if (!Settings.Normalized)
			{
				Settings.Fixup((int)Source.Width, (int)Source.Height, DecoderFrame.SwapDimensions);

				if (Settings.SaveFormat == FileFormat.Auto)
					Settings.SetSaveFormat(Decoder.ContainerFormat, Source.Format.AlphaRepresentation != PixelAlphaRepresentation.None);
			}

			UsedSettings = Settings.Clone();
		}

		public void SwitchPlanarSource(WicPlane plane)
		{
			if (plane == WicPlane.Chroma)
			{
				PlanarLumaSource = source;
				source = PlanarChromaSource;
			}
			else
			{
				PlanarChromaSource = source;
				source = PlanarLumaSource;
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
