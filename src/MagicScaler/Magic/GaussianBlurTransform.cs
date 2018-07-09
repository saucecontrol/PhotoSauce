namespace PhotoSauce.MagicScaler
{
	public sealed class GaussianBlurTransform : PixelTransform, IPixelTransformInternal
	{
		private readonly double radius;

		public GaussianBlurTransform(double radius) => this.radius = radius;

		void IPixelTransformInternal.Init(WicProcessingContext ctx)
		{
			MagicTransforms.AddInternalFormatConverter(ctx, allow96bppFloat: true);

			var fmt = ctx.Source.Format;
			if (fmt.NumericRepresentation == PixelNumericRepresentation.Float)
			{
				var mx = ctx.AddDispose(KernelMap<float>.MakeBlurMap(ctx.Source.Width, radius, fmt.ColorChannelCount, fmt.AlphaRepresentation != PixelAlphaRepresentation.None, true));
				var my = ctx.AddDispose(KernelMap<float>.MakeBlurMap(ctx.Source.Height, radius, fmt.ChannelCount == 3 ? 4 : fmt.ColorChannelCount, fmt.AlphaRepresentation != PixelAlphaRepresentation.None, true));

				ctx.Source = ctx.AddDispose(new ConvolutionTransform<float, float>(ctx.Source, mx, my));
			}
			else
			{
				var mx = ctx.AddDispose(KernelMap<int>.MakeBlurMap(ctx.Source.Width, radius, 1, false, false));
				var my = ctx.AddDispose(KernelMap<int>.MakeBlurMap(ctx.Source.Height, radius, 1, false, false));

				if (fmt.NumericRepresentation == PixelNumericRepresentation.Fixed)
					ctx.Source = ctx.AddDispose(new ConvolutionTransform<ushort, int>(ctx.Source, mx, my));
				else
					ctx.Source = ctx.AddDispose(new ConvolutionTransform<byte, int>(ctx.Source, mx, my));
			}

			Source = ctx.Source;
		}
	}
}
