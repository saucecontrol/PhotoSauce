// Copyright © Clinton Ingram and Contributors
// SPDX-License-Identifier: MIT

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
	/// <returns>An <see cref="IColorProfileHandle"/> for use with other experimental APIs.</returns>
	public static IColorProfileHandle Create(ReadOnlySpan<byte> profile) => new Impl(ColorProfile.Cache.GetOrAdd(profile));

	private sealed class Impl(ColorProfile profile) : IColorProfileHandle
	{
		public ColorProfile ColorProfile { get; } = profile;
	}
}
