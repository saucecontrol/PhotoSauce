// Copyright © Clinton Ingram and Contributors.  Licensed under the MIT License.

using System;
using System.Security;

using PhotoSauce.MagicScaler;

namespace PhotoSauce.Interop.Libjpeg;

[SuppressUnmanagedCodeSecurity]
internal static unsafe partial class Libjpeg
{
	public const int DSTATE_READY = 202;

	public static ReadOnlySpan<byte> ExifIdentifier => "Exif\0\0"u8;
	public static ReadOnlySpan<byte> IccpIdentifier => "ICC_PROFILE\0"u8;

	public static bool IsValidImage(this ref jpeg_decompress_struct handle) =>
		handle.jpeg_color_space is not J_COLOR_SPACE.JCS_UNKNOWN &&
		handle.image_width is > 0 and <= JPEG_MAX_DIMENSION &&
		handle.image_height is > 0 and <= JPEG_MAX_DIMENSION;

	public static bool IsPlanarSupported(this ref jpeg_decompress_struct handle) =>
		handle.jpeg_color_space is J_COLOR_SPACE.JCS_YCbCr &&
		handle.num_components is 3 &&
		handle.comp_info[0].h_samp_factor is 1 or 2 &&
		handle.comp_info[0].v_samp_factor is 1 or 2 &&
		handle.comp_info[1].h_samp_factor is 1 &&
		handle.comp_info[1].v_samp_factor is 1 &&
		handle.comp_info[2].h_samp_factor is 1 &&
		handle.comp_info[2].v_samp_factor is 1;

	public static bool IsExifMarker(this ref jpeg_marker_struct marker) =>
		marker.marker is JPEG_APP0 + 1 &&
		marker.data_length >= ExifIdentifier.Length + ExifConstants.MinExifLength &&
		new ReadOnlySpan<byte>(marker.data, ExifIdentifier.Length - 1).SequenceEqual(ExifIdentifier[..^1]);

	public static bool IsIccMarker(this ref jpeg_marker_struct marker) =>
		marker.marker is JPEG_APP0 + 2 &&
		marker.data_length >= IccpIdentifier.Length + ColorProfile.MinProfileLength &&
		new ReadOnlySpan<byte>(marker.data, IccpIdentifier.Length).SequenceEqual(IccpIdentifier);
}
