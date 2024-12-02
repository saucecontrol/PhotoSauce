// Copyright © Clinton Ingram and Contributors
// SPDX-License-Identifier: MIT

namespace PhotoSauce.MagicScaler;

internal static class ExifConstants
{
	public const uint MarkerII = 42 << 16 | 'I' << 8 | 'I';
	public const uint MarkerMM = 42 << 24 | 'M' << 8 | 'M';

	public const int TiffHeadLength = sizeof(uint) * 2;
	public const int ExifHeadLength = TiffHeadLength + sizeof(ushort);
	public const int MinTagLength = sizeof(ushort) * 2 + sizeof(uint) * 2;
	public const int MinDirLength = sizeof(ushort) + MinTagLength;
	public const int MinExifLength = ExifHeadLength + MinTagLength;
}

internal enum ExifType : ushort
{
	Invalid = 0,
	// Exif 2.0
	Byte = 1,
	Ascii = 2,
	Short = 3,
	Long = 4,
	Rational = 5,
	Undefined = 7,
	SLong = 9,
	SRational = 10,
	// TIFF types not included in Exif spec
	SByte = 6,
	SShort = 8,
	Float = 11,
	Double = 12,
	IFD = 13,
	// BigTIFF
	Long8 = 16,
	SLong8 = 17,
	IFD8 = 18,
	// Exif 3.0
	Utf8 = 129
}

internal static partial class EnumExtensions
{
	public static int GetElementSize(this ExifType t) => t switch {
		ExifType.Byte or ExifType.SByte or ExifType.Ascii or ExifType.Utf8 or ExifType.Undefined => sizeof(byte),
		ExifType.Short or ExifType.SShort => sizeof(ushort),
		ExifType.Long or ExifType.SLong or ExifType.Float or ExifType.IFD => sizeof(uint),
		ExifType.Rational or ExifType.SRational or ExifType.Double or ExifType.Long8 or ExifType.SLong8 or ExifType.IFD8 => sizeof(ulong),
		_ => default
	};

	public static bool IsSigned(this ExifType t) => t switch {
		ExifType.SByte or ExifType.SShort or ExifType.SLong or ExifType.SLong8 => true,
		_ => default
	};

	public static bool IsFloating(this ExifType t) => t switch {
		ExifType.Float or ExifType.Double => true,
		_ => default
	};
}
