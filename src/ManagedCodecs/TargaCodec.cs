// Copyright © Clinton Ingram and Contributors.  Licensed under the MIT License.

using System;
using System.IO;
using System.Runtime.CompilerServices;

using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

using PhotoSauce.MagicScaler;

namespace PhotoSauce.ManagedCodecs
{
	/// <summary>Represents a TARGA image file.</summary>
	public sealed class TargaContainer : IImageContainer, IDisposable
	{
		private readonly Image decodedImage;

		private TargaContainer(Image image) => decodedImage = image;

		/// <inheritdoc />
		public FileFormat ContainerFormat => FileFormat.Unknown;

		/// <inheritdoc />
		public bool IsAnimation => false;

		int IImageContainer.FrameCount => 1;

		IImageFrame IImageContainer.GetFrame(int index) => index == 0 ? new TargaFrame(decodedImage.Frames.RootFrame) : throw new IndexOutOfRangeException("Invalid frame index");

		/// <summary>Loads a TGA image from the specified path.</summary>
		/// <param name="imgPath">The path to the image file.</param>
		/// <returns>A <see cref="TargaContainer"/> encapsulating the image.</returns>
		public static TargaContainer Load(string imgPath)
		{
			var info = Image.Identify(imgPath, out var fmt);
			if (fmt.Name != "TGA")
				throw new InvalidDataException("Not a TARGA image");

			return info.PixelType.BitsPerPixel switch
			{
				 8 => new TargaContainer(Image.Load<L8>(imgPath)),
				24 => new TargaContainer(Image.Load<Bgr24>(imgPath)),
				 _ => new TargaContainer(Image.Load<Bgra32>(imgPath))
			};
		}

		/// <inheritdoc />
		public void Dispose() => decodedImage.Dispose();
	}

	/// <summary>Gives access to the pixels in a TGA file.</summary>
	public sealed class TargaFrame : IImageFrame
	{
		private readonly ImageFrame decodedFrame;

		internal TargaFrame(ImageFrame frame) => decodedFrame = frame;

		double IImageFrame.DpiX => 96;

		double IImageFrame.DpiY => 96;

		Orientation IImageFrame.ExifOrientation => Orientation.Normal;

		ReadOnlySpan<byte> IImageFrame.IccProfile => default;

		/// <inheritdoc />
		public IPixelSource PixelSource => new ImagePixelSource(decodedFrame);

		/// <inheritdoc />
		public void Dispose() => decodedFrame.Dispose();
	}

	internal sealed class ImagePixelSource : IPixelSource
	{
		private readonly ImageFrame decodedFrame;

		public ImagePixelSource(ImageFrame frame) => decodedFrame = frame;

		private int bytesPerPixel => decodedFrame switch {
			ImageFrame<L8>    _ => 1,
			ImageFrame<Bgr24> _ => 3,
			                  _ => 4
		};

		public Guid Format => decodedFrame switch {
			ImageFrame<L8>    _ => PixelFormats.Grey8bpp,
			ImageFrame<Bgr24> _ => PixelFormats.Bgr24bpp,
			                  _ => PixelFormats.Bgra32bpp
		};

		public int Width => decodedFrame.Width;

		public int Height => decodedFrame.Height;

		public void CopyPixels(System.Drawing.Rectangle sourceArea, int cbStride, Span<byte> buffer)
		{
			var (rx, ry, rw, rh) = (sourceArea.X, sourceArea.Y, sourceArea.Width, sourceArea.Height);
			int cb = rw * bytesPerPixel;

			if (rx < 0 || ry < 0 || rw < 0 || rh < 0 || rx + rw > Width || ry + rh > Height)
				throw new ArgumentOutOfRangeException(nameof(sourceArea), "Requested area does not fall within the image bounds");

			if (cb > cbStride)
				throw new ArgumentOutOfRangeException(nameof(cbStride), "Stride is too small for the requested area");

			if ((rh - 1) * cbStride + cb > buffer.Length)
				throw new ArgumentOutOfRangeException(nameof(buffer), "Buffer is too small for the requested area");

			if (decodedFrame is ImageFrame<L8> img8)
				copyPixels(img8, (rx, ry, rw, rh), buffer, cbStride, cb);
			else if (decodedFrame is ImageFrame<Bgr24> img24)
				copyPixels(img24, (rx, ry, rw, rh), buffer, cbStride, cb);
			else if (decodedFrame is ImageFrame<Bgra32> img32)
				copyPixels(img32, (rx, ry, rw, rh), buffer, cbStride, cb);
			else throw new NotSupportedException("Image format not supported");
		}

		private static void copyPixels<TPixel>(ImageFrame<TPixel> frame, (int x, int y, int w, int h) area, Span<byte> dest, int stride, int cb) where TPixel : unmanaged, IPixel<TPixel>
		{
			for (int y = 0; y < area.h; y++)
			{
				ref byte src = ref Unsafe.As<TPixel, byte>(ref frame.GetPixelRowSpan(area.y + y)[area.x]);
				Unsafe.CopyBlockUnaligned(ref dest[y * stride], ref src, (uint)cb);
			}
		}
	}
}
