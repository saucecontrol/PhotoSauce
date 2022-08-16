// Copyright © Clinton Ingram and Contributors.  Licensed under the MIT License.

using System;
using System.ComponentModel;

namespace PhotoSauce.MagicScaler;

/// <summary>Defines the horizontal and vertical anchor positions for auto cropping.</summary>
[Flags]
public enum CropAnchor : byte
{
	/// <summary>Crop to the image center.</summary>
	Center = 0,
	/// <summary>Preserve the top edge of the image.</summary>
	Top = 1,
	/// <summary>Preserve the bottom edge of the image.</summary>
	Bottom = 2,
	/// <summary>Preserve the left edge of the image.</summary>
	Left = 4,
	/// <summary>Preserve the right edge of the image.</summary>
	Right = 8
}

/// <summary>Defines the modes for auto cropping and scaling.</summary>
public enum CropScaleMode : byte
{
	/// <summary>Preserve the aspect ratio of the input image.  Crop if necessary to fit the output dimensions.</summary>
	Crop,
	/// <summary>Preserve the aspect ratio of the input image.  Reduce one of the output dimensions if necessary to preserve the ratio.</summary>
	Contain,
	/// <summary>Stretch the image on one axis if necessary to fill the output dimensions.</summary>
	Stretch,
	/// <summary>Preserve the aspect ratio of the input image.  Fill any undefined pixels with the <see cref="ProcessImageSettings.MatteColor" />.</summary>
	Pad,
	/// <summary>Preserve the aspect ratio of the input image.  Reduce one or both of the output dimensions if necessary to preserve the ratio but never enlarge.</summary>
	Max
}

/// <summary>Defines the modes that control speed vs. quality trade-offs for high-ratio scaling operations.</summary>
public enum HybridScaleMode : byte
{
	/// <summary>Allow lower-quality downscaling to a size at least 3x the target size.  Use high-quality scaling to reach the final size.</summary>
	FavorQuality,
	/// <summary>Allow lower-quality downscaling to a size at least 2x the target size.  Use high-quality scaling to reach the final size.</summary>
	FavorSpeed,
	/// <summary>Allow lower-quality downscaling to the nearest power of 2 to the target size.  Use high-quality scaling to reach the final size.</summary>
	Turbo,
	/// <summary>Do not allow hybrid scaling.  Use the high-quality scaler exclusively.</summary>
	Off
}

/// <summary>Defines the modes that control <a href="http://blog.johnnovak.net/2016/09/21/what-every-coder-should-know-about-gamma/">gamma correction</a> in pixel blending.</summary>
public enum GammaMode : byte
{
	/// <summary>Convert values to linear RGB before blending.  This is more mathematically correct and more visually pleasing in most cases.</summary>
	Linear,
	/// <summary>Blend gamma-companded R'G'B' values directly.  This is usually a poor choice but may be used for compatibility with other software or where speed is more important than image quality.</summary>
	Companded
}

/// <summary>Use ImageMimeTypes instead.</summary>
[Obsolete($"Use {nameof(ImageMimeTypes)} instead.")]
public enum FileFormat : byte
{
	/// <summary>Set output container format automatically based on input format and image contents.  The container format will be a web-friendly format (JPEG, PNG, or GIF).</summary>
	Auto,
	/// <summary>A JPEG container.</summary>
	Jpeg,
	/// <summary>A PNG container.</summary>
	Png,
	/// <summary>A PNG container using an 8-bit indexed pixel format.</summary>
	Png8,
	/// <summary>A GIF container.</summary>
	Gif,
	/// <summary>A BMP container.</summary>
	Bmp,
	/// <summary>A TIFF container.</summary>
	Tiff,
	/// <summary>An unrecognized but still decodable image container.</summary>
	Unknown = Auto
}

/// <summary>Defines the modes that control <a href="https://en.wikipedia.org/wiki/ICC_profile">ICC Color Profile</a> handling.</summary>
public enum ColorProfileMode : byte
{
	/// <summary>Convert the input image to a well-known RGB color space during processing.  A minimal compatible color profile will be embedded unless the output image is in the the sRGB color space.</summary>
	/// <include file='Docs/Remarks.xml' path='doc/member[@name="ColorProfileMode.Normalize"]/*'/>
	Normalize,
	/// <summary>Convert the input image to a well-known RGB color space during processing.  A minimal compatible color profile will be embedded for the output color space, including sRGB.</summary>
	/// <include file='Docs/Remarks.xml' path='doc/member[@name="ColorProfileMode.Normalize"]/*'/>
	NormalizeAndEmbed,
	/// <summary>Preserve the input image color space during processing and embed the source image's ICC profile in the output image.  CMYK images will be converted to Adobe RGB.</summary>
	/// <include file='Docs/Remarks.xml' path='doc/member[@name="ColorProfileMode.Preserve"]/*'/>
	Preserve,
	/// <summary>Convert the input image to the <a href="https://en.wikipedia.org/wiki/SRGB">sRGB color space</a> during processing.  Output an untagged sRGB image.</summary>
	ConvertToSrgb,
	/// <summary>Ignore any embedded profiles and treat the image as <a href="https://en.wikipedia.org/wiki/SRGB">sRGB</a> data.  Do not tag the output image.</summary>
	Ignore = byte.MaxValue
}

/// <summary>Represents orientation correction to be applied to an image.  The values in this enumeration match the values defined in the <a href="https://en.wikipedia.org/wiki/Exif">Exif</a> specification.</summary>
public enum Orientation
{
	/// <summary>No orientation correction is required.</summary>
	Normal = 1,
	/// <summary>The image should be flipped along the X axis prior to display.</summary>
	FlipHorizontal = 2,
	/// <summary>The image should be rotated 180 degrees prior to display.</summary>
	Rotate180 = 3,
	/// <summary>The image should be flipped along the Y axis prior to display.</summary>
	FlipVertical = 4,
	/// <summary>The image should be flipped along the diagonal axis from top left to bottom right prior to display.</summary>
	Transpose = 5,
	/// <summary>The image should be rotated 90 degrees prior to display.</summary>
	Rotate90 = 6,
	/// <summary>The image should be flipped along the diagonal axis from bottom left to top right prior to display.</summary>
	Transverse = 7,
	/// <summary>The image should be rotated 270 degrees prior to display.</summary>
	Rotate270 = 8
}

/// <summary>Defines the modes that control <a href="https://magnushoff.com/articles/jpeg-orientation/">Exif Orientation</a> correction.</summary>
public enum OrientationMode : byte
{
	/// <summary>Correct the image orientation according to the Exif tag on load.  Save the output in normal orientation.  This option ensures maximum compatibility with viewer software.</summary>
	Normalize,
	/// <summary>Preserve the orientation of the input image and tag the output image to reflect the orientation.  If the output format does not support orientation tagging, it will be discarded.</summary>
	Preserve,
	/// <summary>Ignore any orientation tag and treat the image as if its stored orientation is normal.  Do not tag the output image.  This option should only be used if the Exif orientation of the input image is known to be incorrect.</summary>
	Ignore = byte.MaxValue
}

/// <summary>Defines the positioning of chroma components relative to their associated luma components when chroma planes are subsampled.</summary>
[Flags]
public enum ChromaPosition
{
	/// <summary>Chroma components are aligned with luma columns.</summary>
	[EditorBrowsable(EditorBrowsableState.Never)]
	CositedHorizontal = 1,
	/// <summary>Chroma components are aligned with luma rows.</summary>
	[EditorBrowsable(EditorBrowsableState.Never)]
	CositedVertical = 2,
	/// <summary>Chroma components are offset between luma columns.</summary>
	[EditorBrowsable(EditorBrowsableState.Never)]
	InterstitialHorizontal = 0,
	/// <summary>Chroma components are offset between luma rows.</summary>
	[EditorBrowsable(EditorBrowsableState.Never)]
	InterstitalVertical = 0,
	/// <summary>Chroma components are offset between luma rows and columns, as in JPEG images.</summary>
	Jpeg = Center,
	/// <summary>Chroma components are aligned with luma columns and offset between luma rows, as in most modern video formats.</summary>
	Video = Left,
	/// <summary>Chroma components are offset between luma rows/columns.</summary>
	Center = 0,
	/// <summary>Chroma components are aligned with even luma columns.</summary>
	Left = 1,
	/// <summary>Chroma components are aligned with even luma rows.</summary>
	Top = 2,
	/// <summary>Chroma components are aligned with odd luma rows.</summary>
	Bottom = 4
}

/// <summary>Defines the modes that control <a href="https://en.wikipedia.org/wiki/Chroma_subsampling">chroma subsampling</a> for output image formats that support it.</summary>
public enum ChromaSubsampleMode
{
	/// <summary>Configure subsampling automatically based on output format and quality settings.</summary>
	Default = 0,
	/// <summary>Use 4:2:0 Y'CbCr subsampling.</summary>
	Subsample420 = 1,
	/// <summary>Use 4:2:2 Y'CbCr subsampling.</summary>
	Subsample422 = 2,
	/// <summary>Do not use chroma subsampling (4:4:4).</summary>
	Subsample444 = 3,
	/// <summary>4:4:0 Y'CbCr subsampling.  Not supported by the JPEG encoder.</summary>
	[EditorBrowsable(EditorBrowsableState.Never)]
	Subsample440 = 4
}

/// <summary>Defines the modes that control disposal of image frames in an animation.</summary>
internal enum FrameDisposalMethod : byte
{
	/// <summary>Disposal is not defined.  This has the same behavior as <see cref="Preserve" />.</summary>
	Unspecified = 0,
	/// <summary>The frame should not be disposed.  The display buffer will preserve the current frame's content.</summary>
	Preserve = 1,
	/// <summary>The display buffer should be cleared to the background color.</summary>
	RestoreBackground = 2,
	/// <summary>The display buffer should revert to the state preceding display of the current frame.</summary>
	RestorePrevious = 3
}

internal enum ResolutionUnit
{
	Virtual,
	Inch,
	Centimeter,
	Meter
}

/// <summary>Defines the codec vendors that are permitted for use in the pipeline.</summary>
public enum WicCodecPolicy
{
	/// <summary>Include only codecs that are built in to Windows.</summary>
	BuiltIn,
	/// <summary>Include only codecs that are built in to Windows, plus installed codecs authored by Microsoft.</summary>
	/// <remarks>Microsoft installed codecs include those installed from Microsoft Store, such as the RAW Image Extension.</remarks>
	Microsoft,
	/// <summary>Include any codecs that are registered with WIC, inlcuding those installed by third party vendors.</summary>
	/// <remarks>Using third party codecs in server environments can be dangerous. Include them only if necessary and if properly tested.</remarks>
	All
}

internal static partial class EnumExtensions
{
	public static FrameDisposalMethod Clamp(this FrameDisposalMethod m) => m < FrameDisposalMethod.Preserve || m > FrameDisposalMethod.RestorePrevious ? FrameDisposalMethod.Preserve : m;

	public static Orientation Clamp(this Orientation o) => o < Orientation.Normal ? Orientation.Normal : o > Orientation.Rotate270 ? Orientation.Rotate270 : o;

	public static bool SwapsDimensions(this Orientation o) => o > Orientation.FlipVertical;

	public static bool RequiresCache(this Orientation o) => o > Orientation.FlipHorizontal;

	public static bool FlipsX(this Orientation o) => o is Orientation.FlipHorizontal or Orientation.Rotate180 or Orientation.Rotate270 or Orientation.Transverse;

	public static bool FlipsY(this Orientation o) => o is Orientation.FlipVertical or Orientation.Rotate180 or Orientation.Rotate90 or Orientation.Transverse;

	public static Orientation Invert(this Orientation o) => o is Orientation.Rotate270 ? Orientation.Rotate90 : o is Orientation.Rotate90 ? Orientation.Rotate270 : o;

	public static bool IsSubsampledX(this ChromaSubsampleMode m) => m is ChromaSubsampleMode.Subsample420 or ChromaSubsampleMode.Subsample422;

	public static bool IsSubsampledY(this ChromaSubsampleMode m) => m is ChromaSubsampleMode.Subsample420 or ChromaSubsampleMode.Subsample440;

	public static int SubsampleRatioX(this ChromaSubsampleMode m) => m is ChromaSubsampleMode.Subsample420 or ChromaSubsampleMode.Subsample422 ? 2 : 1;

	public static int SubsampleRatioY(this ChromaSubsampleMode m) => m is ChromaSubsampleMode.Subsample420 or ChromaSubsampleMode.Subsample440 ? 2 : 1;

	public static float OffsetX(this ChromaPosition p) => p.HasFlag(ChromaPosition.Left) ? -0.5f : default;

	public static float OffsetY(this ChromaPosition p) => p.HasFlag(ChromaPosition.Top) ? -0.5f : p.HasFlag(ChromaPosition.Bottom) ? 0.5f : default;

	public static string? ToMimeType(this FileFormat fmt) => fmt switch {
		FileFormat.Bmp  => ImageMimeTypes.Bmp,
		FileFormat.Gif  => ImageMimeTypes.Gif,
		FileFormat.Png  => ImageMimeTypes.Png,
		FileFormat.Png8 => ImageMimeTypes.Png,
		FileFormat.Jpeg => ImageMimeTypes.Jpeg,
		FileFormat.Tiff => ImageMimeTypes.Tiff,
		_               => default
	};
}
