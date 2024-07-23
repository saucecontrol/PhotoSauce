// Copyright © Clinton Ingram and Contributors.  Licensed under the MIT License.

using System;

namespace PhotoSauce.MagicScaler.Experimental;

/// <summary>
/// TODO: Comment goes here
/// </summary>
public interface IColorProfileHandle
{
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
	public static IColorProfileHandle GetOrCreate(ReadOnlySpan<byte> profile)
	{
		return ColorProfile.Cache.GetOrAdd(profile);
	}
}
