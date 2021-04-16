// Copyright © Clinton Ingram and Contributors.  Licensed under the MIT License.

using System;
using System.Linq;
using System.Collections.Generic;

namespace PhotoSauce.MagicScaler
{
	internal sealed class PipelineContext : IDisposable
	{
		internal class PlanarPipelineContext
		{
			public PixelSource SourceY;
			public PixelSource SourceCb;
			public PixelSource SourceCr;
			public ChromaSubsampleMode ChromaSubsampling;

			public PlanarPipelineContext(PixelSource sourceY, PixelSource sourceCb, PixelSource sourceCr)
			{
				if (sourceY.Format != PixelFormat.Y8) throw new ArgumentException("Invalid pixel format", nameof(sourceY));
				if (sourceCb.Format != PixelFormat.Cb8) throw new ArgumentException("Invalid pixel format", nameof(sourceCb));
				if (sourceCr.Format != PixelFormat.Cr8) throw new ArgumentException("Invalid pixel format", nameof(sourceCr));

				SourceY = sourceY;
				SourceCb = sourceCb;
				SourceCr = sourceCr;

				ChromaSubsampling =
					sourceCb.Width < sourceY.Width && sourceCb.Height < sourceY.Height ? ChromaSubsampleMode.Subsample420 :
					sourceCb.Width < sourceY.Width ? ChromaSubsampleMode.Subsample422 :
					sourceCb.Height < sourceY.Height ? ChromaSubsampleMode.Subsample440 :
					ChromaSubsampleMode.Subsample444;
			}
		}

		private readonly Stack<IDisposable> disposeHandles = new(capacity: 16);

		private PixelSource? source;
		private List<IProfiler>? profilers;
		private WicPipelineContext? wicContext;

		public ProcessImageSettings Settings { get; }
		public ProcessImageSettings UsedSettings { get; private set; }
		public Orientation Orientation { get; set; }

		public IImageContainer ImageContainer { get; set; }
		public IImageFrame ImageFrame { get; set; }

		public PlanarPipelineContext? PlanarContext { get; set; }

		public ColorProfile? SourceColorProfile { get; set; }
		public ColorProfile? DestColorProfile { get; set; }

		public IEnumerable<PixelSourceStats> Stats => profilers?.OfType<ProcessingProfiler>().Select(p => p.Stats!) ?? Enumerable.Empty<PixelSourceStats>();

		public WicPipelineContext WicContext => wicContext ??= AddDispose(new WicPipelineContext());

		public PixelSource Source
		{
			get => source ?? NoopPixelSource.Instance;
			set
			{
				source = value;

				if (MagicImageProcessor.EnablePixelSourceStats)
				{
					profilers ??= new List<IProfiler>(capacity: 8);
					if (!profilers.Contains(source.Profiler))
						profilers.Add(source.Profiler);
				}
			}
		}

		public bool IsAnimatedGifPipeline =>
			ImageContainer is WicGifContainer &&
			(Settings.SaveFormat == FileFormat.Gif || Settings.SaveFormat == FileFormat.Auto) &&
			Settings.FrameIndex == 0 && ImageContainer.FrameCount > 1;

		public PipelineContext(ProcessImageSettings settings)
		{
			Settings = settings.Clone();

			// https://github.com/dotnet/runtime/issues/31877
			UsedSettings = null!;
			ImageContainer = null!;
			ImageFrame = null!;
		}

		public T AddDispose<T>(T disposeHandle) where T : IDisposable
		{
			disposeHandles.Push(disposeHandle);

			return disposeHandle;
		}

		public ProcessingProfiler AddProfiler(ProcessingProfiler prof)
		{
			(profilers ??= new()).Add(prof);

			return prof;
		}

		public void AddFrameDisposer() => AddDispose(new FrameDisposer(this));

		public void FinalizeSettings()
		{
			Orientation = Settings.OrientationMode == OrientationMode.Normalize ? ImageFrame.ExifOrientation : Orientation.Normal;

			if (!Settings.IsNormalized)
			{
				Settings.Fixup(Source.Width, Source.Height, Orientation.SwapsDimensions());

				if (Settings.SaveFormat == FileFormat.Auto)
					Settings.SetSaveFormat(ImageContainer.ContainerFormat, Source.Format.AlphaRepresentation != PixelAlphaRepresentation.None);

				if (Settings.ColorProfileMode <= ColorProfileMode.NormalizeAndEmbed && (Settings.SaveFormat == FileFormat.Bmp || Settings.SaveFormat == FileFormat.Gif))
					Settings.ColorProfileMode = ColorProfileMode.ConvertToSrgb;
			}

			UsedSettings = Settings.Clone();
		}

		public void Dispose()
		{
			while (disposeHandles.Count > 0)
				disposeHandles.Pop().Dispose();
		}

		private struct FrameDisposer : IDisposable
		{
			private readonly PipelineContext ctx;

			public FrameDisposer(PipelineContext owner) => ctx = owner;

			public void Dispose() => ctx.ImageFrame?.Dispose();
		}
	}
}
