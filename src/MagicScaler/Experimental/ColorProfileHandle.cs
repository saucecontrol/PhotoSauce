// Copyright © Clinton Ingram and Contributors.  Licensed under the MIT License.

using System;

namespace PhotoSauce.MagicScaler.Experimental;

/// <summary>A handle to a color profile for use with other experimental APIs.</summary>
public interface IColorProfileHandle
{
	internal ColorProfile ColorProfile { get; }
}

/// <summary>Provides factory methods for <see cref="IColorProfileHandle"/>.</summary>
public static class ColorProfileHandle
{
	/// <summary>Given an ICC profile, creates an <see cref="IColorProfileHandle"/>.</summary>
	/// <param name="profile">The ICC color profile.</param>
	/// <returns>An <see cref="IColorProfileHandle"/> for use with other experimental APIs such as <see cref="TransformFactory.CreateConversionTransform(IPixelSource, Guid, IColorProfileHandle?, IColorProfileHandle?)"/>.</returns>
	public static IColorProfileHandle Create(ReadOnlySpan<byte> profile)
	{
		ColorProfile colorProfile = ColorProfile.Cache.GetOrAdd(profile);
		return new Impl(colorProfile);
	}

	private sealed class Impl(ColorProfile colorProfile)
		: IColorProfileHandle
	{
		public ColorProfile ColorProfile { get; init; } = colorProfile;
	}
}
