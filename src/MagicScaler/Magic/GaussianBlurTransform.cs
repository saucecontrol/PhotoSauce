namespace PhotoSauce.MagicScaler.Transforms
{
	/// <summary>Applies a <a href="https://en.wikipedia.org/wiki/Gaussian_blur">Gaussian blur</a> to an image.</summary>
	public sealed class GaussianBlurTransform : PixelTransformInternalBase, IPixelTransformInternal
	{
		private readonly double radius;

		/// <summary>Constructs a new <see cref="GaussianBlurTransform" /> with the specified <paramref name="radius" />.</summary>
		/// <param name="radius">The blur radius (sigma value).</param>
		public GaussianBlurTransform(double radius) => this.radius = radius;

		void IPixelTransformInternal.Init(PipelineContext ctx)
		{
			MagicTransforms.AddInternalFormatConverter(ctx, allow96bppFloat: true);

			var fmt = ctx.Source.Format;
			if (fmt.NumericRepresentation == PixelNumericRepresentation.Float)
				ctx.Source = ctx.AddDispose(ConvolutionTransform<float, float>.CreateBlur(ctx.Source, radius));
			else if (fmt.NumericRepresentation == PixelNumericRepresentation.Fixed)
				ctx.Source = ctx.AddDispose(ConvolutionTransform<ushort, int>.CreateBlur(ctx.Source, radius));
			else
				ctx.Source = ctx.AddDispose(ConvolutionTransform<byte, int>.CreateBlur(ctx.Source, radius));

			Source = ctx.Source;
		}
	}
}
