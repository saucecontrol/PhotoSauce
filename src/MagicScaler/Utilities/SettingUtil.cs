// Copyright © Clinton Ingram and Contributors.  Licensed under the MIT License.

namespace PhotoSauce.MagicScaler;

internal static class SettingsUtil
{
	public static int GetDefaultQuality(int size) => size switch {
		<= 160  => 95,
		<= 320  => 93,
		<= 480  => 91,
		<= 640  => 89,
		<= 1280 => 87,
		<= 1920 => 85,
		_       => 83
	};

	public static ChromaSubsampleMode GetDefaultSubsampling(int quality) => quality switch {
		>= 95 => ChromaSubsampleMode.Subsample444,
		>= 90 => ChromaSubsampleMode.Subsample422,
		_     => ChromaSubsampleMode.Subsample420
	};

	public static InterpolationSettings GetDefaultInterpolation(double ratio) => ratio switch {
		   1.0 => InterpolationSettings.Linear,
		<  0.5 => InterpolationSettings.Lanczos,
		> 16.0 => InterpolationSettings.Quadratic,
		>  4.0 => InterpolationSettings.CatmullRom,
		_      => InterpolationSettings.Spline36
	};

	public static UnsharpMaskSettings GetDefaultUnsharpMask(double ratio) => ratio switch {
		   1.0 => UnsharpMaskSettings.None,
		<  0.5 => new UnsharpMaskSettings(40, 1.5, 0),
		<  1.0 => new UnsharpMaskSettings(30, 1.0, 0),
		<  2.0 => new UnsharpMaskSettings(30, 0.75, 4),
		<  4.0 => new UnsharpMaskSettings(75, 0.50, 2),
		<  6.0 => new UnsharpMaskSettings(50, 0.75, 2),
		<  8.0 => new UnsharpMaskSettings(100, 0.6, 1),
		< 10.0 => new UnsharpMaskSettings(125, 0.5, 0),
		_      => new UnsharpMaskSettings(150, 0.5, 0)
	};
}
