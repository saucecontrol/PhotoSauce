using System;
using System.Collections.Generic;

namespace PhotoSauce.MagicScaler
{
	internal class PipelineContext : IDisposable
	{
		private readonly Stack<IDisposable> disposeHandles = new Stack<IDisposable>();
		private readonly HashSet<PixelSourceStats> stats;

		private PixelSource source;

		public WicPipelineContext WicContext { get; }
		public ProcessImageSettings Settings { get; }
		public ProcessImageSettings UsedSettings { get; private set; }

		public WicDecoder Decoder { get; set; }
		public WicFrameReader DecoderFrame { get; set; }
		public PixelSource PlanarLumaSource { get; set; }
		public PixelSource PlanarChromaSource { get; set; }

		public ColorProfile SourceColorProfile { get; set; }
		public ColorProfile DestColorProfile { get; set; }

		public IReadOnlyCollection<PixelSourceStats> Stats => stats;

		public PixelSource Source
		{
			get => source;
			set
			{
				source = value;
				if (!stats.Contains(source.Stats))
					stats.Add(source.Stats);
			}
		}

		public PipelineContext(ProcessImageSettings settings)
		{
			Settings = settings.Clone();
			WicContext = new WicPipelineContext();
			stats = new HashSet<PixelSourceStats>();
		}

		public T AddDispose<T>(T disposeHandle) where T : IDisposable
		{
			disposeHandles.Push(disposeHandle);

			return disposeHandle;
		}

		public void FinalizeSettings()
		{
			if (!Settings.Normalized)
			{
				Settings.Fixup((int)Source.Width, (int)Source.Height, DecoderFrame.ExifOrientation.RequiresDimensionSwap());

				if (Settings.SaveFormat == FileFormat.Auto)
					Settings.SetSaveFormat(Decoder.ContainerFormat, Source.Format.AlphaRepresentation != PixelAlphaRepresentation.None);
			}

			UsedSettings = Settings.Clone();
		}

		public void Dispose()
		{
			while (disposeHandles.Count > 0)
				disposeHandles.Pop()?.Dispose();

			WicContext.Dispose();
		}
	}
}
