// Copyright © Clinton Ingram and Contributors.  Licensed under the MIT License.

using System;
using System.Buffers;
using System.Drawing;
using System.Runtime.CompilerServices;

namespace PhotoSauce.MagicScaler
{
	/// <summary>A sample <see cref="IPixelSource" /> implementation.  Creates a test pattern of stair-stepped color or grey bars.</summary>
	/// <remarks>This pixel source is useful when a simple recognizable pattern is desired.  It produces output at <c>memcpy</c> speed.</remarks>
	public sealed class TestPatternPixelSource : IPixelSource, IDisposable
	{
		private static readonly Guid[] formats = new[] { default, PixelFormats.Grey8bpp, default, PixelFormats.Bgr24bpp, PixelFormats.Bgra32bpp };

		private readonly int channels;
		private readonly int cols;
		private readonly int rows;
		private readonly int stride;
		private readonly double rheight;

		private byte[]? pixels;

		/// <inheritdoc />
		public Guid Format => formats[channels];
		/// <inheritdoc />
		public int Width { get; }
		/// <inheritdoc />
		public int Height { get; }

		/// <summary>Constructs a new <see cref="TestPatternPixelSource" /> using the specified settings.</summary>
		/// <param name="width">The image width in pixels.</param>
		/// <param name="height">The image height in pixels.</param>
		/// <param name="pixelFormat">The pixel format of the image.  Must be a member of <see cref="PixelFormats" />.</param>
		/// <remarks><paramref name="width" /> and <paramref name="height" /> values up to 65535 are allowed, although not all encoders support imgages of that size.</remarks>
		public TestPatternPixelSource(int width, int height, Guid pixelFormat)
		{
			channels = Array.IndexOf(formats, pixelFormat);
			if (channels < 1 || channels == 2) throw new ArgumentException("Unsupported pixel format", nameof(pixelFormat));
			if (width < 1 || width > ushort.MaxValue) throw new ArgumentOutOfRangeException(nameof(width), $"Value must be between 1 and {ushort.MaxValue}");
			if (height < 1 || height > ushort.MaxValue) throw new ArgumentOutOfRangeException(nameof(height), $"Value must be between 1 and {ushort.MaxValue}");

			Width = width;
			Height = height;
			cols = Math.Min(width, 8);
			rows = Math.Min(height, cols);
			rheight = (double)rows / height;

			stride = MathUtil.PowerOfTwoCeiling(width * channels, IntPtr.Size);
		}

		private unsafe byte[] getPattern()
		{
			const uint mask = 0xf0f0f0f0u; // limits the max intensity of color values

			var buff = ArrayPool<byte>.Shared.Rent(stride * rows);
			fixed (byte* buffStart = &buff[0])
			{
				uint barVal = mask;
				double cwidth = (double)Width / cols;

				for (int i = 0; i < cols; i++)
				{
					if (channels == 1)
						barVal = (byte)((cols - i - 1) * byte.MaxValue / Math.Max(cols - 1, 1));
					else if ((i & 1) != 0)
						barVal &= ~0xffu;
					else if (i == 2)
						barVal >>= 16;
					else if (i == 4)
						barVal = ~barVal & mask;
					else if (i == 6)
						barVal >>= 24;

					int barStart = (int)Math.Round(i * cwidth);
					int barWidth = Math.Min((int)Math.Round((i + 1) * cwidth), Width) - barStart;
					byte* bp = buffStart + barStart * channels;

					switch (channels)
					{
						case 1:
							new Span<byte>(bp, barWidth).Fill((byte)barVal);
							break;
						case 3:
							new Span<triple>(bp, barWidth).Fill((triple)barVal);
							break;
						case 4:
							new Span<uint>(bp, barWidth).Fill((uint)byte.MaxValue << 24 | barVal);
							break;
					}
				}

				byte blankVal = channels > 3 ? byte.MinValue : (byte)0xa0;
				for (int i = 1; i < rows; i++)
				{
					int rowOffs = i * stride;
					int colOffs = (int)Math.Round((cols - i) * cwidth) * channels;

					Unsafe.CopyBlockUnaligned(buffStart + rowOffs, buffStart, (uint)colOffs);
					Unsafe.InitBlockUnaligned(buffStart + rowOffs + colOffs, blankVal, (uint)(stride - colOffs));
				}
			}

			return buff;
		}

		/// <inheritdoc />
		public void CopyPixels(Rectangle sourceArea, int cbStride, Span<byte> buffer)
		{
			var (rx, ry, rw, rh) = (sourceArea.X, sourceArea.Y, sourceArea.Width, sourceArea.Height);
			int cb = rw * channels;

			if (rx < 0 || ry < 0 || rw < 0 || rh < 0 || rx + rw > Width || ry + rh > Height)
				throw new ArgumentOutOfRangeException(nameof(sourceArea), "Requested area does not fall within the image bounds");

			if (cb > cbStride)
				throw new ArgumentOutOfRangeException(nameof(cbStride), "Stride is too small for the requested area");

			if ((rh - 1) * cbStride + cb > buffer.Length)
				throw new ArgumentOutOfRangeException(nameof(buffer), "Buffer is too small for the requested area");

			pixels ??= getPattern();
			for (int y = 0; y < rh; y++)
			{
				int row = Math.Min((int)((ry + y) * rheight), rows - 1);
				Unsafe.CopyBlockUnaligned(ref buffer[y * cbStride], ref pixels[row * stride + rx * channels], (uint)cb);
			}
		}

		/// <inheritdoc />
		public void Dispose()
		{
			if (pixels is null)
				return;

			ArrayPool<byte>.Shared.Return(pixels);
			pixels = default;
		}

		/// <inheritdoc />
		public override string ToString() => nameof(TestPatternPixelSource);
	}
}
