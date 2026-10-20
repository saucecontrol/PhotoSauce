// Copyright © Clinton Ingram and Contributors
// SPDX-License-Identifier: MIT

using System;

using PhotoSauce.MagicScaler;

namespace PhotoSauce.ManagedCodecs.Ico;

/// <summary>ICO decoder options.</summary>
/// <param name="FrameRange"><inheritdoc cref="IMultiFrameDecoderOptions.FrameRange" path="/summary/node()" /></param>
/// <remarks>An ICO or CUR file is a collection of images, typically the same picture at different sizes and color depths.  Each one is exposed as a frame.</remarks>
public readonly record struct IcoDecoderOptions(Range FrameRange) : IMultiFrameDecoderOptions
{
	/// <summary>Default ICO decoder options.</summary>
	public static IcoDecoderOptions Default => new(..);
}
