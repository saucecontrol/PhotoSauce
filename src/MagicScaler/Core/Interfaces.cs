// Copyright © Clinton Ingram and Contributors.  Licensed under the MIT License.

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
		/// <summary>The horizontal resolution of the image frame, in dots/pixels per inch.</summary>
		/// <remarks>Implementation note: If the frame source has no resolution information, a default value of 72 or 96 is suitable.</remarks>
		double DpiX { get; }

		/// <summary>The vertical resolution of the image frame, in dots/pixels per inch.</summary>
		/// <remarks>Implementation note: If the frame source has no resolution information, a default value of 72 or 96 is suitable.</remarks>
		double DpiY { get; }

		/// <summary>The <see cref="Orientation"/> of the image frame.</summary>
		/// <remarks>Implementation note: If the frame source has no orientation information, a default value of <see cref="Orientation.Normal"/> is suitable.</remarks>
		Orientation ExifOrientation { get; }

		/// <summary>The <a href="https://en.wikipedia.org/wiki/ICC_profile">ICC color profile</a> that describes the color space of the image frame.</summary>
		/// <remarks>If this value is <see cref="ReadOnlySpan{T}.Empty" />, colors will be interpreted as <a href="https://en.wikipedia.org/wiki/SRGB">sRGB or sYCC</a>.</remarks>
		ReadOnlySpan<byte> IccProfile { get; }

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

	/// <summary>A container defining global metadata for a sequence of <see cref="IAnimationFrame" /> instances.</summary>
	internal interface IAnimationContainer
	{
		/// <summary>The width of the animation's logical screen.</summary>
		public int ScreenWidth { get; }

		/// <summary>The height of the animation's logical screen.</summary>
		public int ScreenHeight { get; }

		/// <summary>The number of times to loop the animation.  Values less than 1 imply inifinte looping.</summary>
		public int LoopCount { get; }

		/// <summary>The background color to restore when a frame's disposal method is RestoreBackground.</summary>
		public Color BackgroundColor { get; }

		/// <summary>True if this animation requires a persistent screen buffer onto which frames are rendered, otherwise false.</summary>
		public bool RequiresScreenBuffer { get; }
	}

	/// <summary>Defines metadata for a single frame within an animated image sequence.</summary>
	internal interface IAnimationFrame
	{
		/// <summary>The origin point (offset) of the frame's content, relative to the logical screen size.</summary>
		public Point Origin { get; }

		/// <summary>The size of the frame's content to be rendered to the logical screen.</summary>
		public Size Size { get; }

		/// <summary>The amount of time, in seconds, the frame should be displayed.</summary>
		/// <remarks>For animated GIF output, the denominator will be normalized to <c>100</c>.</remarks>
		public Rational Duration { get; }

		/// <summary>The disposition of the frame.</summary>
		public FrameDisposalMethod Disposal { get; }

		/// <summary>True to indicate the frame contains transparent pixels, otherwise false.</summary>
		public bool HasAlpha { get; }
	}

	/// <summary>A <a href="https://en.wikipedia.org/wiki/Rational_number">rational number</a>, as defined by an integer numerator and denominator.</summary>
	internal readonly struct Rational : IEquatable<Rational>
	{
		/// <summary>The numerator of the rational number.</summary>
		public readonly int Numerator;
		/// <summary>The denominator of the rational number.</summary>
		public readonly int Denominator;

		/// <summary>Constructs a new <see cref="Rational" /> with the specified <see cref="Numerator" /> and <see cref="Denominator" />.</summary>
		/// <param name="numerator">The numerator.</param>
		/// <param name="denominator">The denominator.</param>
		public Rational(int numerator, int denominator) => (Numerator, Denominator) = (numerator, denominator);

		/// <inheritdoc />
		public bool Equals(Rational other) => Numerator == other.Numerator && Denominator == other.Denominator;

		/// <inheritdoc />
		public override bool Equals(object? obj) => obj is Rational other && Equals(other);
		/// <inheritdoc />
		public override int GetHashCode() => (Numerator, Denominator).GetHashCode();
		/// <inheritdoc />
		public override string ToString() => $"{Numerator}/{Denominator}";

		/// <inheritdoc cref="double.op_Equality" />
		public static bool operator ==(Rational left, Rational right) => left.Equals(right);

		/// <inheritdoc cref="double.op_Equality" />
		public static bool operator !=(Rational left, Rational right) => !(left == right);
	}
}
