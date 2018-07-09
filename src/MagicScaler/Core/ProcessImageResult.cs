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
		internal readonly WicProcessingContext Context;

		private readonly Lazy<IPixelSource> source;

		internal ProcessingPipeline(WicProcessingContext ctx)
		{
			Context = ctx;
			source = new Lazy<IPixelSource>(() => {
				MagicTransforms.AddExternalFormatConverter(Context);

				return Context.Source.AsIPixelSource();
			});
		}

		public IPixelSource PixelSource => source.Value;
		public ProcessImageSettings Settings => Context.UsedSettings;
		public IEnumerable<PixelSourceStats> Stats => Context.Stats;

		public void AddTransform(IPixelTransform transform)
		{
			if (source.IsValueCreated)
				throw new NotSupportedException("A Transform cannot be added once the Pipeline Source is materialized");

			if (transform is IPixelTransformInternal tint)
			{
				tint.Init(Context);
				return;
			}

			MagicTransforms.AddExternalFormatConverter(Context);

			transform.Init(Context.Source.AsIPixelSource());
			Context.Source = transform.AsPixelSource();
		}

		public void Dispose() => Context.Dispose();
	}
}