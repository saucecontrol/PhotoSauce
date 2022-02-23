// Copyright © Clinton Ingram and Contributors.  Licensed under the MIT License.

using System;
using System.IO;
using System.Diagnostics;
using System.Collections.Generic;

using PhotoSauce.MagicScaler.Transforms;

namespace PhotoSauce.MagicScaler
{
	internal interface IProfileSource
	{
		public IProfiler Profiler { get; }
	}

	internal interface IProfiler
	{
		public void PauseTiming();
		public void ResumeTiming();
		public void ResumeTiming(in PixelArea prc);
	}

	internal sealed class NoopProfiler : IProfiler
	{
		public static readonly IProfiler Instance = new NoopProfiler();

		public void PauseTiming() { }
		public void ResumeTiming() { }
		public void ResumeTiming(in PixelArea prc) { }
	}

	internal sealed class ProcessingProfiler : IProfiler
	{
		private int callCount;
		private long pixelCount;
		private readonly Stopwatch timer = new();
		private readonly object processor;

		public ProcessingProfiler(object proc) => processor = proc;

		public PixelSourceStats Stats => new(processor.ToString()!, callCount, pixelCount, (double)timer.ElapsedTicks / Stopwatch.Frequency * 1000);

		public void PauseTiming() => timer.Stop();
		public void ResumeTiming() => timer.Start();
		public void ResumeTiming(in PixelArea prc)
		{
			callCount++;
			pixelCount += prc.Width * prc.Height;

			timer.Start();
		}
	}

	internal static class StatsManager
	{
		public const string SwitchName = $"{nameof(PhotoSauce)}.{nameof(MagicScaler)}.EnablePixelSourceStats";

		public static readonly bool ProfilingEnabled = AppContext.TryGetSwitch(SwitchName, out bool enabled) && enabled;

		public static IProfiler GetProfiler(object src) => ProfilingEnabled ? new ProcessingProfiler(src) : NoopProfiler.Instance;
	}

	/// <summary>Represents basic instrumentation information for a single pipeline step.</summary>
	public sealed class PixelSourceStats
	{
		internal PixelSourceStats(string sourceName, int callCount, long pixelCount, double processingTime)
		{
			SourceName = sourceName;
			CallCount = callCount;
			PixelCount = pixelCount;
			ProcessingTime = processingTime;
		}

		/// <summary>A friendly name for the <see cref="IPixelSource" />.</summary>
		public string SourceName { get; }

		/// <summary>The number of times <see cref="IPixelSource.CopyPixels" /> was invoked.</summary>
		public int CallCount { get; }

		/// <summary>The total number of pixels retrieved from the <see cref="IPixelSource" />.</summary>
		public long PixelCount { get; }

		/// <summary>The total processing time of the <see cref="IPixelSource" /> in milliseconds.  Note that WIC-based pixel sources will report times inclusive of upstream sources.</summary>
		public double ProcessingTime { get; }

		/// <inheritdoc />
		public override string ToString() => $"{SourceName}: Calls={CallCount}, Pixels={PixelCount}, Time={ProcessingTime:f2}ms";
	}

	/// <summary>Represents the results of a completed pipeline operation.</summary>
	public sealed class ProcessImageResult
	{
		internal ProcessImageResult(ProcessImageSettings settings, IEnumerable<PixelSourceStats> stats)
		{
			Settings = settings;
			Stats = stats;
		}

		/// <summary>The settings used for the operation.  Any default or auto properties will reflect their final calculated values.</summary>
		public ProcessImageSettings Settings { get; }

		/// <summary>Basic instrumentation for the operation.  There will be one <see cref="PixelSourceStats" /> instance for each pipeline step.</summary>
		/// <remarks>This collection will be empty unless the PhotoSauce.MagicScaler.EnablePixelSourceStats <see cref="AppContext" /> switch is set to <see langword="true" />.</remarks>
		public IEnumerable<PixelSourceStats> Stats { get; }
	}

	/// <summary>Represents an image processing pipeline from which computed pixels can be retrieved.</summary>
	public sealed class ProcessingPipeline : IDisposable
	{
		internal readonly PipelineContext Context;

		private readonly Lazy<IPixelSource> source;

		internal ProcessingPipeline(PipelineContext ctx)
		{
			Context = ctx;
			source = new Lazy<IPixelSource>(() => {
				MagicTransforms.AddExternalFormatConverter(Context);
				WicTransforms.AddPixelFormatConverter(Context, false);

				return Context.Source;
			});
		}

		/// <summary>The source for retrieving calculated pixels from the pipeline.</summary>
		public IPixelSource PixelSource => source.Value;

		/// <summary>The settings used to construct the pipeline.  Any default or auto properties will reflect their final calculated values.</summary>
		public ProcessImageSettings Settings => Context.UsedSettings;

		/// <inheritdoc cref="ProcessImageResult.Stats" />
		public IEnumerable<PixelSourceStats> Stats => Context.Stats;

		/// <summary>Adds a new transform filter to the pipeline.  Because a filter may alter dimensions or pixel format of an image, filters may not be added once the <see cref="PixelSource" /> has been retrieved.</summary>
		/// <param name="transform">The <see cref="IPixelTransform" /> that implements the filter.</param>
		public ProcessingPipeline AddTransform(IPixelTransform transform!!)
		{
			if (source.IsValueCreated)
				throw new NotSupportedException("A Transform cannot be added once the Pipeline Source is materialized");

			if (transform is PixelTransformInternalBase tint)
			{
				tint.Init(Context);
				return this;
			}

			MagicTransforms.AddExternalFormatConverter(Context);

			transform.Init(Context.Source);
			Context.Source = transform.AsPixelSource();
			return this;
		}

		/// <summary>Completes processing of the pipeline, writing the output image to <paramref name="outStream" />.</summary>
		/// <param name="outStream">The stream to which the output image will be written. The stream must allow Seek and Write.</param>
		/// <returns>A <see cref="ProcessImageResult" /> containing the settings used and basic instrumentation for the pipeline.</returns>
		public ProcessImageResult WriteOutput(Stream outStream!!)
		{
			outStream.EnsureValidForOutput();

			return MagicImageProcessor.WriteOutput(Context, outStream);
		}

		/// <inheritdoc />
		public void Dispose() => Context.Dispose();
	}
}