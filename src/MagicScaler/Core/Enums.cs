using System;
using System.ComponentModel;

namespace PhotoSauce.MagicScaler
{
	/// <summary>Defines the horizontal and vertical anchor positions for auto cropping.</summary>
	[Flags]
	public enum CropAnchor
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
	public enum CropScaleMode
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
	public enum HybridScaleMode
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
	public enum GammaMode
	{
		/// <summary>Convert values to linear RGB before blending.  This is more mathematically correct and more visually pleasing in most cases.</summary>
		Linear,
		/// <summary>Blend gamma-companded R'G'B' values directly.  This is usually a poor choice but may be used for compatibility with other software or where speed is more important than image quality.</summary>
		Companded
	}

	/// <summary>Defines known image container formats for auto-detection and output configuration.</summary>
	public enum FileFormat
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
	public enum ColorProfileMode
	{
		/// <summary>Convert the input image to a well-known RGB color space during processing.  A minimal compatible color profile will be embedded unless the output image is in the the sRGB color space.</summary>
		/// <include file='Docs/Remarks.xml' path='doc/member[@name="ColorProfileMode"]/*'/>
		Normalize,
		/// <summary>Convert the input image to a well-known RGB color space during processing.  A minimal compatible color profile will be embedded for the output color space, including sRGB.</summary>
		/// <include file='Docs/Remarks.xml' path='doc/member[@name="ColorProfileMode"]/*'/>
		NormalizeAndEmbed,
		/// <summary>Preserve the input image color space during processing.  Embed the source image's ICC profile in the output image.  If the output format does not support embedded profiles, it will be discarded.</summary>
		/// <ramarks>Be aware that the embedded profile may be very large -- in the case of thumbnails, often larger than the thumbnail image itself.</ramarks>
		Preserve,
		/// <summary>Convert the input image to the <a href="https://en.wikipedia.org/wiki/SRGB">sRGB color space</a> during processing.  Output an untagged sRGB image.</summary>
		ConvertToSrgb,
		/// <summary>Ignore any embedded profiles and treat the image as <a href="https://en.wikipedia.org/wiki/SRGB">sRGB</a> data.  Do not tag the output image.</summary>
		Ignore = 0xff
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
	public enum OrientationMode
	{
		/// <summary>Correct the image orientation according to the Exif tag on load.  Save the output in normal orientation.  This option ensures maximum compatibility with viewer software.</summary>
		Normalize,
		/// <summary>Preserve the orientation of the input image and tag the output image to reflect the orientation.  If the output format does not support orientation tagging, it will be discarded.</summary>
		Preserve,
		/// <summary>Ignore any orientation tag and treat the image as if its stored orientation is normal.  Do not tag the output image.  This option should only be used if the Exif orientation of the input image is known to be incorrect.</summary>
		Ignore = 0xff
	}

	/// <summary>Defines the positioning of chroma components relative to their associated luma components when chroma planes are subsampled.</summary>
	[Flags]
	public enum ChromaPosition
	{
		/// <summary>Chroma components are aligned with luma columns.</summary>
		CositedHorizontal = 1,
		/// <summary>Chroma components are aligned with luma rows.</summary>
		CositedVertical = 2,
		/// <summary>Chroma components are offset between luma columns.</summary>
		InterstitialHorizontal = 0,
		/// <summary>Chroma components are offset between luma rows.</summary>
		InterstitalVertical = 0,
		/// <summary>Chroma components are offset between luma rows and columns, as in JPEG images.</summary>
		Jpeg = InterstitialHorizontal | InterstitalVertical,
		/// <summary>Chroma components are aligned with luma columns and offset between luma rows, as in most modern video formats.</summary>
		Video = CositedHorizontal | InterstitalVertical
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

	internal enum GifDisposalMethod
	{
		Undefined = 0,
		Preserve = 1,
		RestoreBackground = 2,
		RestorePrevious = 3
	}
}
