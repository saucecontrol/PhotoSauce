using System;
using System.Drawing;
using System.Numerics;

namespace PhotoSauce.MagicScaler
{
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
		/// <summary>The horizontal resolution of the image frame, in dots per inch.  If the frame source has no resolution information, a default value of 72 or 96 is suitable.</summary>
		double DpiX { get; }

		/// <summary>The vertical resolution of the image frame, in dots per inch.  If the frame source has no resolution information, a default value of 72 or 96 is suitable.</summary>
		double DpiY { get; }

		/// <summary>The <see cref="Orientation"/> of the image frame.  If the frame source has no orientation information, a default value of <see cref="Orientation.Normal"/> is suitable.</summary>
		Orientation ExifOrientation { get; }

		/// <summary>The <a href="https://en.wikipedia.org/wiki/ICC_profile">ICC color profile</a> that describes the color space of the image frame.  If this value is <see cref="ReadOnlySpan{T}.Empty" />, colors will be interpreted as <a href="https://en.wikipedia.org/wiki/SRGB">sRGB or sYCC</a>.</summary>
		ReadOnlySpan<byte> IccProfile { get; }

		/// <summary>The <see cref="IPixelSource" /> to retrieve pixels from this image frame.</summary>
		IPixelSource PixelSource { get; }
	}

	/// <summary>An image frame within an <see cref="IImageContainer" />.  The frame exposes 3 <see cref="IPixelSource" /> values, representing the Y', Cb, and Cr planes.</summary>
	public interface IYccImageFrame : IImageFrame
	{
		/// <summary>The position of subsampled chroma components relative to their associated luma components.</summary>
		ChromaPosition ChromaPosition { get; }

		/// <summary>A 3x3 matrix containing the coefficients for converting the image frame from Y'CbCr format to R'G'B'.  The fourth row and column will be ignored.  See <see cref="YccRgbMatrix" /> for standard values.</summary>
		Matrix4x4 YccToRgbMatrix { get; }

		/// <summary>True if the encoding uses the full 0-255 range for pixel values, false if the encoding uses video range (16-235 luma and 16-240 chroma).</summary>
		bool IsFullRange { get; }

		/// <summary>The <see cref="IPixelSource" /> to retrieve pixels from the Cb (blue-yellow) chroma plane.</summary>
		IPixelSource PixelSourceCb { get; }

		/// <summary>The <see cref="IPixelSource" /> to retrieve pixels from the Cr (red-green) chroma plane.</summary>
		IPixelSource PixelSourceCr { get; }
	}

	/// <summary>An image container (file), made up of one or more <see cref="IImageFrame" />instances.</summary>
	public interface IImageContainer
	{
		/// <summary>The <see cref="FileFormat" /> (codec) of the image container.</summary>
		FileFormat ContainerFormat { get; }

		/// <summary>The total number of image frames in this container.</summary>
		int FrameCount { get; }

		/// <summary>Retrieves an individual frame from within this container.</summary>
		/// <param name="index">The zero-based index of the desired frame.</param>
		/// <returns>The <see cref="IImageFrame" /> at the requested index.</returns>
		IImageFrame GetFrame(int index);
	}
}
