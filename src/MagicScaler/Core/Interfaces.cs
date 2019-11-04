using System;
using System.Drawing;

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
	public interface IImageFrame
	{
		/// <summary>The <see cref="IPixelSource" /> to retrieve pixels from this image frame.</summary>
		IPixelSource PixelSource { get; }
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
