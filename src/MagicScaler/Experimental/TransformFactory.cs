// Copyright © Clinton Ingram and Contributors.  Licensed under the MIT License.

using System;

using PhotoSauce.MagicScaler.Transforms;

namespace PhotoSauce.MagicScaler.Experimental;

/// <summary>Contains methods for creating <see cref="IPixelSource" /> transforms. These may be changed or removed in future releases.</summary>
public static class TransformFactory
{
	/// <summary>Creates a transform that converts an <see cref="IPixelSource" /> to the given pixel format, optionally using the given ICC color profiles for gamma correction.</summary>
	/// <param name="source">The <see cref="IPixelSource" /> to retrieve pixels from.</param>
	/// <param name="destFormat">The binary representation of the resulting pixel data.  Must be one of the values from <see cref="PixelFormats" />.</param>
	/// <param name="sourceProfile">The ICC color profile to use for the <paramref name="source" />. If null, sRGB will be used.</param>
	/// <param name="destProfile">The ICC color profile to use for the resulting pixel data. If null, sRGB will be used.</param>
	/// <returns>An <see cref="IPixelSource" /> that provides the resulting pixel data.</returns>
	public static IPixelSource CreateConversionTransform(IPixelSource source, Guid destFormat, IColorProfileHandle? sourceProfile = null, IColorProfileHandle? destProfile = null)
	{
		return new ConversionTransform(source.AsPixelSource(), PixelFormat.FromGuid(destFormat), sourceProfile?.Get(), destProfile?.Get());
	}

	/// <summary>Creates a transform that resizes an <see cref="IPixelSource" /> to the given size, optionally using a specific interpolator.</summary>
	/// <param name="source">The <see cref="IPixelSource" /> to retrieve pixels from.</param>
	/// <param name="newWidth">The width of the resulting <see cref="IPixelSource" />.</param>
	/// <param name="newHeight">The height of the resulting <see cref="IPixelSource" />.</param>
	/// <param name="settings">The interpolation settings to use. If <c>default</c>, then the default high-quality scaler will be used.</param>
	/// <returns>An <see cref="IPixelSource" /> that provides the resulting pixel data.</returns>
	public static IPixelSource CreateScaleTransform(IPixelSource source, int newWidth, int newHeight, InterpolationSettings settings = default)
	{
		if (settings.WeightingFunction is null)
		{
			double scaleRatio = Math.Min(
				source.Width > 0 ? (double)newWidth / source.Width : 0, 
				source.Height > 0 ? (double)newHeight / source.Height : 0);
			settings = SettingsUtil.GetDefaultInterpolation(scaleRatio);
		}

		return ConvolutionTransform.CreateResample(source.AsPixelSource(), newWidth, newHeight, settings, settings);
	}
}
