// Copyright © Clinton Ingram and Contributors.  Licensed under the MIT License.

using System;

namespace PhotoSauce.MagicScaler.Experimental;

/// <summary>
/// TODO: Comment goes here
/// </summary>
public interface IColorProfileHandle
{
	internal ColorProfile Get();
}

/// <summary>
/// TODO: Comment goes here
/// </summary>
public static class ColorProfileHandle
{
	/// <summary>
	/// TODO: Comment goes here
	/// </summary>
	/// <param name="profile">TODO: Comment goes here</param>
	/// <returns>TODO: Comment goes here</returns>
	public static IColorProfileHandle Create(ReadOnlySpan<byte> profile)
	{
		ColorProfile colorProfile = ColorProfile.Cache.GetOrAdd(profile);
		return new Impl(colorProfile);
	}

	private sealed class Impl
		: IColorProfileHandle
	{
		private readonly ColorProfile colorProfile;

		public Impl(ColorProfile colorProfile)
		{
			this.colorProfile = colorProfile;
		}

		public ColorProfile Get()
		{
			return colorProfile;
		}
	}
}
