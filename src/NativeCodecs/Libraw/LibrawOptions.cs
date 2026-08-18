// Copyright © Clinton Ingram and Contributors
// SPDX-License-Identifier: MIT

using PhotoSauce.MagicScaler;

namespace PhotoSauce.NativeCodecs.Libraw;

/// <summary>LibRaw decoder options.</summary>
/// <param name="UseCameraWhiteBalance">Use the camera-provided white balance when available.</param>
/// <param name="AutoBrightness">Apply LibRaw's automatic brightness adjustment.</param>
/// <param name="HalfSize">Decode one output pixel from each 2x2 RAW sensor block for a faster, lower-resolution result.</param>
public readonly record struct LibrawDecoderOptions(bool UseCameraWhiteBalance, bool AutoBrightness, bool HalfSize) : IDecoderOptions
{
	/// <summary>Default LibRaw decoder options.</summary>
	public static LibrawDecoderOptions Default => new(true, true, false);
}
