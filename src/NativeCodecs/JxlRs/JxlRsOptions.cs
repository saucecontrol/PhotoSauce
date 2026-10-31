// Copyright © Clinton Ingram and Contributors
// SPDX-License-Identifier: MIT

using System;

using PhotoSauce.MagicScaler;

namespace PhotoSauce.NativeCodecs.JxlRs;

/// <summary>JPEG XL decoder options for the jxl-rs codec.</summary>
/// <param name="FrameRange"><inheritdoc cref="IMultiFrameDecoderOptions.FrameRange" path="/summary/node()" /></param>
/// <param name="MaxDegreeOfParallelism">
/// The maximum number of threads used to decode one image. Use 0 to select the available
/// processor count automatically or 1 to disable parallel decoding.
/// </param>
public readonly record struct JxlRsDecoderOptions(Range FrameRange, int MaxDegreeOfParallelism = 1) : IMultiFrameDecoderOptions
{
	/// <summary>Default jxl-rs decoder options.</summary>
	public static JxlRsDecoderOptions Default => new(Range.All, 1);
}
