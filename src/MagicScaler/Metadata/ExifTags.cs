// Copyright © Clinton Ingram and Contributors.  Licensed under the MIT License.

namespace PhotoSauce.MagicScaler;

// https://www.cipa.jp/std/documents/e/DC-X008-Translation-2019-E.pdf
internal static class ExifTags
{
	public static class Tiff
	{
		public const ushort Orientation = 0x0112;
		public const ushort ResolutionX = 0x011a;
		public const ushort ResolutionY = 0x011b;
		public const ushort ResolutionUnit = 0x0128;

		public const ushort ExifIFD = 0x8769;
		public const ushort GpsIFD = 0x8825;
	}

	public static class Exif
	{
		public const ushort ColorSpace = 0xa001;
		public const ushort InteropIFD = 0xa005;
	}

	public static class Interop
	{
		public const ushort InteropIndex = 0x0001;
		public const ushort InteropVersion = 0x0002;
	}
}

internal enum ExifColorSpace : ushort
{
	sRGB = 0x0001,
	AdobeRGB = 0x0002,
	Uncalibrated = 0xffff
}
