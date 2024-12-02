// Copyright © Clinton Ingram and Contributors
// SPDX-License-Identifier: MIT

namespace PhotoSauce.MagicScaler.Transforms;

/// <summary>Applies a <a href="https://en.wikipedia.org/wiki/Gaussian_blur">Gaussian blur</a> to an image.</summary>
/// <param name="radius">The blur radius (sigma value).</param>
public sealed class GaussianBlurTransform(double radius) : PixelTransformInternalBase
{
	private readonly double radius = radius;

	internal override void Init(PipelineContext ctx)
	{
		MagicTransforms.AddInternalFormatConverter(ctx, allow96bppFloat: true);

		Source = ctx.Source = ctx.AddProfiler(ConvolutionTransform.CreateBlur(ctx.Source, radius));
	}
}
