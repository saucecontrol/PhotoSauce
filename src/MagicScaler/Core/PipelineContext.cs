// Copyright © Clinton Ingram and Contributors
// SPDX-License-Identifier: MIT

using System;
using System.Linq;
using System.Collections.Generic;

namespace PhotoSauce.MagicScaler;

internal sealed class PipelineContext(ProcessImageSettings settings, IImageContainer cont, bool ownCont = true) : IDisposable
{
	private readonly bool ownContainer = ownCont;

	private List<IProfiler>? profilers;
	private Stack<IDisposable>? disposables;
	private WicPipelineContext? wicContext;

	public ProcessImageSettings Settings { get; } = settings.Clone();
	public ProcessImageSettings UsedSettings { get; private set; } = null!;
	public Orientation Orientation { get; set; }

	public IImageContainer ImageContainer { get; set; } = cont;
	public IImageFrame ImageFrame { get; set; } = null!;
	public PixelSource Source { get; set; } = NoopPixelSource.Instance;
	public IMetadataSource Metadata { get; set; } = NoopMetadataSource.Instance;

	public AnimationPipelineContext? AnimationContext { get; set; }

	public ColorProfile? SourceColorProfile { get; set; }
	public ColorProfile? DestColorProfile { get; set; }

	public IEnumerable<PixelSourceStats> Stats => profilers?.OfType<ProcessingProfiler>().Select(static p => p.Stats) ?? [ ];

	public WicPipelineContext WicContext => wicContext ??= new();

	public bool IsAnimationPipeline =>
		Settings.EncoderInfo!.SupportsAnimation && ImageContainer.FrameCount > 1 && ImageContainer is IMetadataSource meta && meta.TryGetMetadata<AnimationContainer>(out _);

	public T AddDispose<T>(T disposeHandle) where T : IDisposable
	{
		(disposables ??= new()).Push(disposeHandle);

		return disposeHandle;
	}

	public IProfiler AddProfiler(string name)
	{
		if (!StatsManager.ProfilingEnabled)
			return NoopProfiler.Instance;

		var prof = new ProcessingProfiler(name);
		(profilers ??= new(capacity: 8)).Add(prof);

		return prof;
	}

	public T AddProfiler<T>(T source) where T : IProfileSource
	{
		if (StatsManager.ProfilingEnabled)
		{
			profilers ??= new(capacity: 8);
			if (!profilers.Contains(source.Profiler))
				profilers.Add(source.Profiler);
		}

		return source;
	}

	public bool TryGetAnimationMetadata(out AnimationContainer anicnt, out AnimationFrame anifrm)
	{
		anicnt = default;
		anifrm = default;

		bool nocntmeta = ImageContainer is not IMetadataSource cmsrc || !cmsrc.TryGetMetadata(out anicnt);
		bool nofrmmeta = ImageFrame is not IMetadataSource fmsrc || !fmsrc.TryGetMetadata(out anifrm);
		if (nocntmeta && nofrmmeta)
			return false;

		if (nocntmeta || anicnt.ScreenWidth is 0 || anicnt.ScreenHeight is 0)
			anicnt = new(Source.Width, Source.Height, ImageContainer.FrameCount);
		if (nofrmmeta)
			anifrm = AnimationFrame.Default;

		return true;
	}

	public void FinalizeSettings()
	{
		Orientation = Settings.OrientationMode == OrientationMode.Normalize ? ImageFrame.GetOrientation() : Orientation.Normal;

		if (!Settings.IsNormalized)
		{
			Settings.Fixup(Source.Width, Source.Height, Orientation.SwapsDimensions());

			if (Settings.EncoderInfo is null)
				Settings.SetEncoder(ImageContainer.MimeType, Source.Format.AlphaRepresentation != PixelAlphaRepresentation.None);

			if (Settings.ColorProfileMode <= ColorProfileMode.NormalizeAndEmbed && !Settings.EncoderInfo.SupportsColorProfile)
				Settings.ColorProfileMode = ColorProfileMode.ConvertToSrgb;
		}

		if (Settings.EncoderOptions is null)
			Settings.EncoderOptions = Settings.EncoderInfo!.DefaultOptions;

		UsedSettings = Settings.Clone();
	}

	public void Dispose()
	{
		wicContext?.Dispose();
		Source.Dispose();
		AnimationContext?.Dispose();
		ImageFrame?.Dispose();

		if (ownContainer)
			ImageContainer?.Dispose();

		while (disposables?.Count > 0)
			disposables.Pop().Dispose();
	}
}
