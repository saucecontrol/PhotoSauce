// Copyright © Clinton Ingram and Contributors.  Licensed under the MIT License.

using System;

using PhotoSauce.MagicScaler;

namespace PhotoSauce.NativeCodecs.Libwebp;

/// <summary>WebP decoder options.</summary>
/// <param name="FrameRange"><inheritdoc cref="IMultiFrameDecoderOptions.FrameRange" path="/summary/node()" /></param>
/// <param name="AllowPlanar"><inheritdoc cref="IPlanarDecoderOptions.AllowPlanar" path="/summary/node()" /></param>
public readonly record struct WebpDecoderOptions(Range FrameRange, bool AllowPlanar) : IMultiFrameDecoderOptions, IPlanarDecoderOptions
{
	/// <summary>Default WebP decoder options.</summary>
	public static WebpDecoderOptions Default => new(.., true);
}