// Copyright © Clinton Ingram and Contributors.  Licensed under the MIT License.

using System;
using System.IO;
using System.Drawing;
using System.Numerics;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;

namespace PhotoSauce.MagicScaler;

/// <summary>Provides a mechanism for accessing raw pixel data from an image.</summary>
public interface IPixelSource
{
	/// <summary>The binary representation of the pixel data.  Must be one of the values from <see cref="PixelFormats" />.</summary>
	Guid Format { get; }

	/// <summary>The width of the image in pixels</summary>
	int Width { get; }

	/// <summary>The height of the image in pixels</summary>
	int Height { get; }

	/// <summary>Copies the image pixels bounded by <paramref name="sourceArea" /> to the provided <paramref name="buffer" />.</summary>
	/// <param name="sourceArea">A <see cref="Rectangle" /> that bounds the area of interest.</param>
	/// <param name="cbStride">The number of bytes between pixels in the same image column within the buffer.</param>
	/// <param name="buffer">A target memory buffer that will receive the pixel data.</param>
	void CopyPixels(Rectangle sourceArea, int cbStride, Span<byte> buffer);
}

/// <summary>A single image frame within an <see cref="IImageContainer" /></summary>
public interface IImageFrame : IDisposable
{
	/// <summary>The <see cref="IPixelSource" /> to retrieve pixels from this image frame.</summary>
	IPixelSource PixelSource { get; }
}

/// <summary>An image frame within an <see cref="IImageContainer" />.  The frame exposes 3 <see cref="IPixelSource" /> values, representing the Y', Cb, and Cr planes.</summary>
/// <remarks>Implementation note: The <see cref="IImageFrame.PixelSource" /> property should return the Y' (luma) plane.</remarks>
public interface IYccImageFrame : IImageFrame
{
	/// <summary>The position of subsampled chroma components relative to their associated luma components.</summary>
	ChromaPosition ChromaPosition { get; }

	/// <summary>A 3x3 matrix containing the coefficients for converting from R'G'B' to the Y'CbCr format of this image frame.</summary>
	/// <remarks>The fourth row and column will be ignored.  See <see cref="YccMatrix" /> for standard values.</remarks>
	Matrix4x4 RgbYccMatrix { get; }

	/// <summary>True if the encoding uses the full 0-255 range for pixel values, false if it uses video range (16-235 luma and 16-240 chroma).</summary>
	bool IsFullRange { get; }

	/// <summary>The <see cref="IPixelSource" /> to retrieve pixels from the Cb (blue-yellow) chroma plane.</summary>
	IPixelSource PixelSourceCb { get; }

	/// <summary>The <see cref="IPixelSource" /> to retrieve pixels from the Cr (red-green) chroma plane.</summary>
	IPixelSource PixelSourceCr { get; }
}

/// <summary>An image container (file), made up of one or more <see cref="IImageFrame" /> instances.</summary>
public interface IImageContainer : IDisposable
{
	/// <summary>The <a href="https://en.wikipedia.org/wiki/Media_type">MIME type</a> of the image container, if applicable.</summary>
	string? MimeType { get; }

	/// <summary>The total number of image frames in this container.</summary>
	int FrameCount { get; }

	/// <summary>Retrieves an individual frame from within this container.</summary>
	/// <param name="index">The zero-based index of the desired frame.</param>
	/// <returns>The <see cref="IImageFrame" /> at the requested index.</returns>
	IImageFrame GetFrame(int index);
}

/// <summary>Base interface for metadata types.</summary>
public interface IMetadata { }

/// <summary>Provides a mechanism for accessing metadata from an image.</summary>
public interface IMetadataSource
{
	/// <summary>Attempt to retrieve metadata of type <typeparamref name="T"/> from this source.</summary>
	/// <typeparam name="T">The type of metadata to retrieve.</typeparam>
	/// <param name="metadata">The value of the metadata, if available.</param>
	/// <returns>True if the metadata was available, otherwise false.</returns>
	bool TryGetMetadata<T>([NotNullWhen(true)] out T? metadata) where T : IMetadata;
}

/// <summary>Base interface for decoder configuration options.</summary>
public interface IDecoderOptions { }

/// <summary>Base interface for encoder configuration options.</summary>
public interface IEncoderOptions { }

/// <summary>An encoder capable of writing image data.</summary>
public interface IImageEncoder : IDisposable
{
	/// <summary>Writes a new image frame to the encoder output, including the pixels and metadata provided.</summary>
	/// <param name="source">The source of pixels for the new image frame.</param>
	/// <param name="metadata">The source of metadata for the new image frame.</param>
	/// <param name="region">The region of interest to take from thie pixel source.  If this value is <see cref="Rectangle.Empty" />, the entire source will be written.</param>
	void WriteFrame(IPixelSource source, IMetadataSource metadata, Rectangle region);

	/// <summary>Finish writing the image file, and disallow writing any more frames. For most single-frame formats, this method is a no-op.</summary>
	void Commit();
}

/// <summary>Describes an image codec.</summary>
public interface IImageCodecInfo
{
	/// <summary>The friendly name of the codec.</summary>
	string Name { get; }
	/// <summary>A list of <a href="https://en.wikipedia.org/wiki/Media_type">MIME types</a> supported by the codec.</summary>
	IEnumerable<string> MimeTypes { get; }
	/// <summary>A list of file extensions supported by the codec.</summary>
	/// <remarks>Extensions should include a leading dot ('.').</remarks>
	IEnumerable<string> FileExtensions { get; }
}

/// <summary>Describes an image decoder.</summary>
public interface IImageDecoderInfo : IImageCodecInfo
{
	/// <summary>A list of "magic byte" patterns that may match files this decoder can read.</summary>
	/// <remarks>No attempt will be made to decode an image with this decoder if its header does not match one of the <see cref="ContainerPattern" />s.</remarks>
	IEnumerable<ContainerPattern> Patterns { get; }

	/// <summary>Default codec options to be used for this decoder in the absence of per-instance overrides.</summary>
	IDecoderOptions? DefaultOptions { get; }

	/// <summary>A delegate capable of creating an instance of this decoder over a given <see cref="Stream" /> data source.</summary>
	Func<Stream, IDecoderOptions?, IImageContainer?> Factory { get; }
}

/// <summary>Describes an image encoder.</summary>
public interface IImageEncoderInfo : IImageCodecInfo
{
	/// <summary>A list of the pixel format GUIDs supported by this encoder.</summary>
	IEnumerable<Guid> PixelFormats { get; }

	/// <summary>Default codec options to be used for this encoder in the absence of per-instance overrides.</summary>
	IEncoderOptions? DefaultOptions { get; }

	/// <summary>A delegate capable of creating an instance of this encoder to write to a given <see cref="Stream" /> data target.</summary>
	Func<Stream, IEncoderOptions?, IImageEncoder> Factory { get; }

	/// <summary>True if the codec supports transparency, otherwise false.</summary>
	bool SupportsMultiFrame { get; }
	/// <summary>True if the codec supports animation sequences, otherwise false.</summary>
	bool SupportsAnimation { get; }
	/// <summary>True if the codec supports ICC color profiles, otherwise false.</summary>
	bool SupportsColorProfile { get; }
}

internal interface IFramePixelSource : IPixelSource
{
	IImageFrame Frame { get; }
}

internal interface IIndexedPixelSource : IPixelSource
{
	ReadOnlySpan<uint> Palette { get; }
}

internal interface IPlanarDecoder : IImageFrame
{
	bool TryGetYccFrame([NotNullWhen(true)] out IYccImageFrame? frame);
}

internal interface ICroppedDecoder
{
	void SetDecodeCrop(PixelArea crop);
}

internal interface IScaledDecoder : IImageFrame
{
	(int width, int height) SetDecodeScale(int ratio);
}

internal interface IAnimatedImageEncoder : IImageEncoder
{
	void WriteAnimationMetadata(IMetadataSource metadata);
}

internal interface IPlanarImageEncoderInfo : IImageEncoderInfo
{
	ChromaSubsampleMode[] SubsampleModes { get; }
	ChromaPosition ChromaPosition { get; }
	Matrix4x4 DefaultMatrix { get; }
	bool SupportsCustomMatrix { get; }
}
