// Copyright © Clinton Ingram and Contributors.  Licensed under the MIT License.

namespace PhotoSauce.MagicScaler.Transforms;

/// <summary>Applies a <a href="https://en.wikipedia.org/wiki/Gaussian_blur">Gaussian blur</a> to an image.</summary>
public sealed class GaussianBlurTransform : PixelTransformInternalBase
{
	private readonly double radius;

	/// <summary>Constructs a new <see cref="GaussianBlurTransform" /> with the specified <paramref name="radius" />.</summary>
	/// <param name="radius">The blur radius (sigma value).</param>
	public GaussianBlurTransform(double radius) => this.radius = radius;

	internal override void Init(PipelineContext ctx)
	{
		MagicTransforms.AddInternalFormatConverter(ctx, allow96bppFloat: true);

		Source = ctx.Source = ctx.AddProfiler(ConvolutionTransform.CreateBlur(ctx.Source, radius));
	}
}
