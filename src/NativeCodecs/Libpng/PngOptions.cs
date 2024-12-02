// Copyright © Clinton Ingram and Contributors
// SPDX-License-Identifier: MIT

using System;

using PhotoSauce.MagicScaler;

namespace PhotoSauce.NativeCodecs.Libpng;

/// <summary>PNG decoder options.</summary>
/// <param name="FrameRange"><inheritdoc cref="IMultiFrameDecoderOptions.FrameRange" path="/summary/node()" /></param>
public readonly record struct PngDecoderOptions(Range FrameRange) : IMultiFrameDecoderOptions
{
	/// <summary>Default PNG decoder options.</summary>
	public static PngDecoderOptions Default => new(..);
}
