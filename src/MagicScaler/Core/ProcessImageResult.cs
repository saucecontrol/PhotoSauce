using System;
using System.Collections.Generic;

namespace PhotoSauce.MagicScaler
{
	public sealed class PixelSourceStats
	{
		public string SourceName { get; internal set; }
		public int CallCount { get; internal set; }
		public int PixelCount { get; internal set; }
		public double ProcessingTime { get; internal set; }

		public override string ToString() => $"{SourceName}: Calls={CallCount}, Pixels={PixelCount}, Time={ProcessingTime:f2}ms";
	}

	public sealed class ProcessImageResult
	{
		public ProcessImageSettings Settings { get; internal set; }
		public IEnumerable<PixelSourceStats> Stats { get; internal set; }
	}

	public sealed class ProcessingPipeline : IDisposable
	{
		private WicProcessingContext context;
		private IPixelSource source;

		internal ProcessingPipeline(WicProcessingContext ctx)
		{
			context = ctx;
			source = ctx.Source.AsIPixelSource();
		}

		public IPixelSource PixelSource => source;
		public ProcessImageSettings Settings => context.Settings;
		public IEnumerable<PixelSourceStats> Stats => context.Stats;

		public void Dispose() => context.Dispose();
	}
}