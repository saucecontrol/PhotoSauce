// Copyright © Clinton Ingram and Contributors.  Licensed under the MIT License.

using System;

using PhotoSauce.MagicScaler;

namespace PhotoSauce.NativeCodecs.Libjxl;

/// <summary>Base interface for JPEG XL encoder options.</summary>
public interface IJxlEncoderOptions : IEncoderOptions
{
	/// <inheritdoc cref="JxlEncodeSpeed" />
	JxlEncodeSpeed EncodeSpeed { get; }
	/// <inheritdoc cref="JxlDecodeSpeed" />
	JxlDecodeSpeed DecodeSpeed { get; }
}

/// <summary>Lossless JPEG XL encoder options.</summary>
public readonly record struct JxlLosslessEncoderOptions(JxlEncodeSpeed EncodeSpeed, JxlDecodeSpeed DecodeSpeed) : IJxlEncoderOptions
{
	/// <summary>Default lossless JPEG XL encoder options.</summary>
	public static JxlLosslessEncoderOptions Default => new(JxlEncodeSpeed.Squirrel, JxlDecodeSpeed.Slowest);
}

/// <summary>Lossy JPEG XL encoder options.</summary>
public readonly record struct JxlLossyEncoderOptions(float Distance, JxlEncodeSpeed EncodeSpeed, JxlDecodeSpeed DecodeSpeed) : IJxlEncoderOptions, ILossyEncoderOptions
{
	/// <summary>Calculates Butteraugli distance from a JPEG-normalized (0-100) quality value.</summary>
	/// <remarks>Heuristic is taken from the <a href="https://github.com/libjxl/libjxl/blob/v0.6.1/tools/cjxl.cc#L594">cjxl encoder</a>.</remarks>
	/// <param name="quality">The target quality value.</param>
	/// <returns>The calculated Butteraugli distance.</returns>
	public static float DistanceFromQuality(int quality)
	{
		if (quality < 0 || quality > 100) throw new ArgumentOutOfRangeException(nameof(quality), "Value must be between 0 and 100");

		if (quality < 30)
			return (float)(6.4 + Math.Pow(2.5, (30.0 - quality) / 5.0) / 6.25);

		return 0.1f + (100f - quality) * 0.09f;
	}

	/// <summary>Calculates JPEG-normalized (0-100) quality value from Butteraugli distance.</summary>
	/// <param name="distance">The Butteraugli distance value.</param>
	/// <returns>The calculated quality value.</returns>
	public static int QualityFromDistance(float distance)
	{
		if (distance > 6.56f)
			return (int)(-5.456783 * Math.Log(0.0256 * distance - 0.16384));

		return (int)(-11.11111f * distance + 101.11111f);
	}

	/// <summary>Default lossy JPEG XL encoder options.</summary>
	public static JxlLossyEncoderOptions Default => new(default, JxlEncodeSpeed.Cheetah, JxlDecodeSpeed.Slowest);

	int ILossyEncoderOptions.Quality => QualityFromDistance(Distance);
}

/// <summary>Determines speed/effort of the JPEG XL encoder.</summary>
public enum JxlEncodeSpeed
{
	/// <summary>Useful only for losless encoding.</summary>
	Lightning = 1,
	/// <summary>Useful only for losless encoding.</summary>
	Thunder = 2,
	/// <summary>Disables all advanced lossy encoding options.</summary>
	Falcon = 3,
	/// <summary>Enables coefficient reordering, context clustering, and heuristics for selecting DCT sizes and quantization steps.</summary>
	Cheetah = 4,
	/// <summary>Enables Gaborish filtering, chroma from luma, and an initial estimate of quantization steps.</summary>
	Hare = 5,
	/// <summary>Enables error diffusion quantization and full DCT size selection heuristics.</summary>
	Wombat = 6,
	/// <summary>Enables dots, patches, and spline detection, and full context clustering. (libjxl default)</summary>
	Squirrel = 7,
	/// <summary>Optimizes the adaptive quantization for a psychovisual metric.</summary>
	Kitten = 8,
	/// <summary>Enables a more thorough adaptive quantization search.</summary>
	Tortoise = 9
}

/// <summary>Determines the speed or effort required to decode the image.</summary>
/// <remarks>libjxl does not assign cutesy names to these values.  Values greater than 1 are not recommended for lossless encoding.</remarks>
public enum JxlDecodeSpeed
{
	/// <summary>Speed value 0.</summary>
	Speed0 = 0,
	/// <summary>Speed value 1.</summary>
	Speed1 = 1,
	/// <summary>Speed value 2.</summary>
	Speed2 = 2,
	/// <summary>Speed value 3.</summary>
	Speed3 = 3,
	/// <summary>Speed value 4.</summary>
	Speed4 = 4,
	/// <summary>The slowest decoding speed.  Equivalent to <see cref="Speed0"/>.</summary>
	Slowest = 0,
	/// <summary>The fastest decoding speed.  Equivalent to <see cref="Speed4"/>.</summary>
	Fastest = 4
}
