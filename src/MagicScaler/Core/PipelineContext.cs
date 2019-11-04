using System;
using System.Linq;
using System.Collections.Generic;

namespace PhotoSauce.MagicScaler
{
	internal class PipelineContext : IDisposable
	{
		private readonly Stack<IDisposable> disposeHandles = new Stack<IDisposable>();
		private readonly List<PixelSource> allSources = new List<PixelSource>();

		private PixelSource? source;

		public WicPipelineContext WicContext { get; }
		public ProcessImageSettings Settings { get; }
		public ProcessImageSettings UsedSettings { get; private set; }

		public IImageContainer ImageContainer { get; set; }
		public WicFrameReader DecoderFrame { get; set; }

		public PixelSource? PlanarSourceY { get; set; }
		public PixelSource? PlanarSourceCbCr { get; set; }

		public ColorProfile? SourceColorProfile { get; set; }
		public ColorProfile? DestColorProfile { get; set; }

		public IEnumerable<PixelSourceStats> Stats => MagicImageProcessor.EnablePixelSourceStats ? allSources.Select(s => s.Stats!) : Enumerable.Empty<PixelSourceStats>();

		public PixelSource Source
		{
			get => source ?? NoopPixelSource.Instance;
			set
			{
				source = value;
				if (!allSources.Contains(source))
					allSources.Add(source);
			}
		}

		public PipelineContext(ProcessImageSettings settings)
		{
			Settings = settings.Clone();
			WicContext = AddDispose(new WicPipelineContext());

			// HACK this quiets the nullable warnings for now but needs refactoring
			UsedSettings = null!;
			ImageContainer = null!;
			DecoderFrame = null!;
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
				Settings.Fixup(Source.Width, Source.Height, DecoderFrame.ExifOrientation.SwapsDimensions());

				if (Settings.SaveFormat == FileFormat.Auto)
					Settings.SetSaveFormat(ImageContainer.ContainerFormat, Source.Format.AlphaRepresentation != PixelAlphaRepresentation.None);
			}

			UsedSettings = Settings.Clone();
		}

		public void Dispose()
		{
			while (disposeHandles.Count > 0)
				disposeHandles.Pop()?.Dispose();
		}
	}
}
