// Copyright © Clinton Ingram and Contributors.  Licensed under the MIT License.

using System;

namespace PhotoSauce.MagicScaler;

/// <summary>Describes a lossy codec in terms of JPEG-normalized (0-100) quality.</summary>
public interface ILossyEncoderOptions : IEncoderOptions
{
	/// <summary>The desired image quality in the range of 0-100, where 0 is minimum quality and 100 is maximum quality.</summary>
	int Quality { get; }
}

/// <summary>Describes a codec that encodes chroma values separate from luma and is capable of encoding chroma values at a lower resolution.</summary>
public interface IPlanarEncoderOptions : IEncoderOptions
{
	/// <summary>The type of chroma subsampling to use when encoding.</summary>
	ChromaSubsampleMode Subsample { get; }
}

/// <summary>Describes a codec that uses a palette or color map.</summary>
public interface IIndexedEncoderOptions : IEncoderOptions
{
	/// <summary>The maximum number of entries in the target palette. Actual palette may have fewer entries if the image contains fewer colors.</summary>
	int MaxPaletteSize { get; }
	/// <summary>The palette to use when quantizing the image.  If <see langword="null" />, a custom palette will be created from the image.</summary>
	int[]? PredefinedPalette { get; }
	/// <summary>True to apply error diffusion dithering to the image.</summary>
	DitherMode Dither { get; }
}

/// <summary>Common encoder options for PNG.</summary>
public interface IPngEncoderOptions : IEncoderOptions
{
	/// <summary>The <see cref="PngFilterMode" /> to apply to the image before compression.</summary>
	PngFilterMode Filter { get; }
	/// <summary>True to enable interlaced PNG output, otherwise false.</summary>
	/// <remarks>Interlaced PNGs are larger and slower to decode than non-interlaced and have more limited decoder support.</remarks>
	bool Interlace { get; }
}

/// <summary>JPEG encoder options.</summary>
/// <param name="Quality"><inheritdoc cref="ILossyEncoderOptions.Quality" path="/summary/node()" /></param>
/// <param name="Subsample"><inheritdoc cref="IPlanarEncoderOptions.Subsample" path="/summary/node()" /></param>
/// <param name="SuppressApp0">True to skip writing the JFIF APP0 metadata header, false to include it.</param>
public readonly record struct JpegEncoderOptions(int Quality, ChromaSubsampleMode Subsample, bool SuppressApp0) : ILossyEncoderOptions, IPlanarEncoderOptions
{
	/// <summary>Default JPEG encoder options.</summary>
	public static JpegEncoderOptions Default => default;
}

/// <summary>True-color/greyscale PNG encoder options.</summary>
/// <param name="Filter"><inheritdoc cref="IPngEncoderOptions.Filter" path="/summary/node()" /></param>
/// <param name="Interlace"><inheritdoc cref="IPngEncoderOptions.Interlace" path="/summary/node()" /></param>
public readonly record struct PngEncoderOptions(PngFilterMode Filter, bool Interlace) : IPngEncoderOptions
{
	/// <summary>Default PNG encoder options.</summary>
	public static PngEncoderOptions Default => default;
}

/// <summary>Indexed color PNG encoder options.</summary>
/// <param name="MaxPaletteSize"><inheritdoc cref="IIndexedEncoderOptions.MaxPaletteSize" path="/summary/node()" /></param>
/// <param name="PredefinedPalette"><inheritdoc cref="IIndexedEncoderOptions.PredefinedPalette" path="/summary/node()" /></param>
/// <param name="Dither"><inheritdoc cref="IIndexedEncoderOptions.Dither" path="/summary/node()" /></param>
/// <param name="Filter"><inheritdoc cref="IPngEncoderOptions.Filter" path="/summary/node()" /></param>
/// <param name="Interlace"><inheritdoc cref="IPngEncoderOptions.Interlace" path="/summary/node()" /></param>
public readonly record struct PngIndexedEncoderOptions(int MaxPaletteSize, int[]? PredefinedPalette, DitherMode Dither, PngFilterMode Filter, bool Interlace) : IPngEncoderOptions, IIndexedEncoderOptions
{
	/// <summary>Default indexed color PNG encoder options.</summary>
	public static PngIndexedEncoderOptions Default => new(256, default, default, PngFilterMode.None, default);
}

/// <summary>GIF encoder options.</summary>
/// <param name="MaxPaletteSize"><inheritdoc cref="IIndexedEncoderOptions.MaxPaletteSize" path="/summary/node()" /></param>
/// <param name="PredefinedPalette"><inheritdoc cref="IIndexedEncoderOptions.PredefinedPalette" path="/summary/node()" /></param>
/// <param name="Dither"><inheritdoc cref="IIndexedEncoderOptions.Dither" path="/summary/node()" /></param>
public readonly record struct GifEncoderOptions(int MaxPaletteSize, int[]? PredefinedPalette, DitherMode Dither) : IIndexedEncoderOptions
{
	/// <summary>Default GIF encoder options.</summary>
	public static GifEncoderOptions Default => new(256, default, default);
}

/// <summary>TIFF encoder options.</summary>
/// <param name="Compression">The compression method to be used by the lossless TIFF encoder.</param>
public readonly record struct TiffEncoderOptions(TiffCompressionMode Compression) : IEncoderOptions
{
	/// <summary>Default TIFF encoder options.</summary>
	public static TiffEncoderOptions Default => new(TiffCompressionMode.None);
}

/// <summary>Decoder options for codecs that store images natively in Y'CbCr planar formats.</summary>
public interface IPlanarDecoderOptions : IDecoderOptions
{
	/// <summary>True to allow the decoder to expose the raw planar form of the image, false to force conversion to RGB/BGR on decode.</summary>
	bool AllowPlanar { get; }
}

/// <summary>Describes a decoder that supports multiple image frames in a container.</summary>
public interface IMultiFrameDecoderOptions : IDecoderOptions
{
	/// <summary>The <see cref="Range" /> of frames to decode.</summary>
	/// <remarks>Some built-in codecs only support processing a single frame at a time.</remarks>
	Range FrameRange { get; }
}

/// <summary>JPEG decoder options.</summary>
/// <param name="AllowPlanar"><inheritdoc cref="IPlanarDecoderOptions.AllowPlanar" path="/summary/node()" /></param>
public readonly record struct JpegDecoderOptions(bool AllowPlanar) : IPlanarDecoderOptions
{
	/// <summary>Default JPEG decoder options.</summary>
	public static JpegDecoderOptions Default => new(true);
}

/// <summary>GIF decoder options.</summary>
/// <param name="FrameRange"><inheritdoc cref="IMultiFrameDecoderOptions.FrameRange" path="/summary/node()" /></param>
public readonly record struct GifDecoderOptions(Range FrameRange) : IMultiFrameDecoderOptions
{
	/// <summary>Default GIF decoder options.</summary>
	public static GifDecoderOptions Default => new(..);
}

/// <summary>TIFF decoder options.</summary>
/// <param name="FrameRange"><inheritdoc cref="IMultiFrameDecoderOptions.FrameRange" path="/summary/node()" /></param>
public readonly record struct TiffDecoderOptions(Range FrameRange) : IMultiFrameDecoderOptions
{
	/// <summary>Default TIFF decoder options.</summary>
	public static TiffDecoderOptions Default => new(..);
}

internal readonly record struct MultiFrameDecoderOptions(Range FrameRange) : IMultiFrameDecoderOptions { }

/// <summary>Represents the PNG <a href="https://www.w3.org/TR/PNG-Filters.html">prediction filter</a> applied to image lines before compression.</summary>
public enum PngFilterMode
{
	/// <summary>The encoder will choose the filter.</summary>
	/// <remarks>The Windows encoder uses <see cref="Adaptive" /> filtering by default.</remarks>
	Unspecified = 0,
	/// <summary>No prediction filter will be used.</summary>
	None = 1,
	/// <summary>The pixel to the left is used as the predicted value.</summary>
	Sub = 2,
	/// <summary>The pixel above is used as the predicted value.</summary>
	Up = 3,
	/// <summary>The average of pixels to the left and above is used as the predicted value.</summary>
	Average = 4,
	/// <summary>The predicted value is derived from pixels to the left, above, and above-left.</summary>
	Paeth = 5,
	/// <summary>The encoder may evaluate and choose a different filter per image line.</summary>
	Adaptive = 6
}

/// <summary>Represents the <a href="https://en.wikipedia.org/wiki/TIFF#TIFF_Compression_Tag">TIFF compression</a> method used when encoding an image.</summary>
public enum TiffCompressionMode
{
	/// <summary>The encoder will choose the compression algorithm based on the image and pixel format.</summary>
	Unspecified = 0,
	/// <summary>No compression.</summary>
	None = 1,
	/// <summary>CCITT Group 3 fax compression.</summary>
	/// <remarks>This algorithm is only valid for 1bpp pixel formats.</remarks>
	CCITT3 = 2,
	/// <summary>CCITT Group 4 fax compression.</summary>
	/// <remarks>This algorithm is only valid for 1bpp pixel formats.</remarks>
	CCITT4 = 3,
	/// <summary>LZW compression.</summary>
	LZW = 4,
	/// <summary>RLE compression.</summary>
	/// <remarks>This algorithm is only valid for 1bpp pixel formats.</remarks>
	RLE = 5,
	/// <summary>Deflate (zlib) compression.</summary>
	ZIP = 6,
	/// <summary>LZWH differencing algorithm.</summary>
	LZWHDifferencing = 7
}

/// <summary>Represents dithering options for indexed color images.</summary>
public enum DitherMode
{
	/// <summary>The quantizer will choose whether to dither or not based on the palette size and image colors.</summary>
	Auto,
	/// <summary>No dithering will be applied.</summary>
	None,
	/// <summary>Error diffusion dithering will be applied.</summary>
	/// <remarks>The algorithm used is a modified version of <a href="https://en.wikipedia.org/wiki/Floyd%E2%80%93Steinberg_dithering">Floyd-Steinberg</a>, which propagates 7/8 of the error value.</remarks>
	ErrorDiffusion
}

