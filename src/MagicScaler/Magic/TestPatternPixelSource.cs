using System;
using System.Buffers;
using System.Drawing;
using System.Runtime.CompilerServices;

namespace PhotoSauce.MagicScaler
{
	/// <summary>An <see cref="IPixelSource" /> implementation that creates a test pattern.</summary>
	public class TestPatternPixelSource : IPixelSource, IDisposable
	{
		private static readonly Guid[] formats = new[] { Guid.Empty, PixelFormats.Grey8bpp, Guid.Empty, PixelFormats.Bgr24bpp, PixelFormats.Bgra32bpp };

		private readonly Lazy<IMemoryOwner<byte>> pixels;
		private readonly int chans;
		private readonly int width;
		private readonly int height;
		private readonly int cols;
		private readonly int rows;
		private readonly int stride;
		private readonly double rheight;

		/// <inheritdoc />
		public Guid Format => formats[chans];
		/// <inheritdoc />
		public int Width => width;
		/// <inheritdoc />
		public int Height => height;

		/// <summary>Constructs a new <see cref="TestPatternPixelSource" /> using the specified settings.</summary>
		/// <param name="width">The image width in pixels.  Values up to 65535 are supported.</param>
		/// <param name="height">The image height in pixels.  Values up to 65535 are supported.</param>
		/// <param name="pixelFormat">The pixel format of the image.  Must be a member of <see cref="PixelFormats" />.</param>
		public TestPatternPixelSource(int width, int height, Guid pixelFormat)
		{
			chans = Array.IndexOf(formats, pixelFormat);
			if (width < 1 || width > 65535) throw new ArgumentOutOfRangeException(nameof(width), "Value must be between 1 and 65535");
			if (height < 1 || height > 65535) throw new ArgumentOutOfRangeException(nameof(height), "Value must be between 1 and 65535");
			if (chans < 1 || chans == 2) throw new ArgumentException("Unsupported pixel format", nameof(pixelFormat));

			this.width = width;
			this.height = height;
			cols = Math.Min(width, 8);
			rows = Math.Min(height, cols);
			rheight = ((double)rows / height);

			stride = MathUtil.PowerOfTwoCeiling(width * chans, IntPtr.Size);
			pixels = new Lazy<IMemoryOwner<byte>>(() => {
				var buff = MemoryPool<byte>.Shared.Rent(stride * rows);
				drawPattern(buff.Memory.Span);
				return buff;
			});
		}

		unsafe private void drawPattern(Span<byte> buff)
		{
			fixed (byte* bstart = buff)
			{
				int chan = chans;
				uint cv = ~0u;
				double cw = (double)width / cols;

				for (int i = 0; i < cols; i++)
				{
					if (chan == 1)
						cv = (byte)((cols - i - 1) * byte.MaxValue / Math.Max(cols - 1, 1));
					else if ((i & 1) == 1)
						cv &= ~0xffu;
					else if (i == 2)
						cv >>= 16;
					else if (i == 4)
						cv = ~cv;
					else if (i == 6)
						cv >>= 24;

					byte pv0 = (byte)cv;
					byte pv1 = (byte)(cv >> 8);
					byte pv2 = (byte)(cv >> 16);

					int cs = (int)Math.Round(i * cw);
					int ce = Math.Min((int)Math.Round(cs + cw), width - 1);

					byte* bp = bstart + cs * chan;
					byte* be = bstart + ce * chan;
					while (bp <= be)
					{
						bp[0] = pv0;
						if (chan > 1)
						{
							bp[1] = pv1;
							bp[2] = pv2;
							if (chan > 3)
								bp[3] = byte.MaxValue;
						}

						bp += chan;
					}
				}

				byte bcv = chan > 3 ? (byte)0 : (byte)0xa0;
				for (int i = 1; i < rows; i++)
				{
					Unsafe.CopyBlock(bstart + i * stride, bstart, (uint)stride);

					int cs = (int)Math.Round((cols - i) * cw);
					int os = i * stride + cs * chan;
					int cb = (i + 1) * stride - os;
					Unsafe.InitBlockUnaligned(bstart + os, bcv, (uint)cb);
				}
			}
		}

		/// <inheritdoc />
		public void CopyPixels(Rectangle sourceArea, int cbStride, Span<byte> buffer)
		{
			int cb = sourceArea.Width * chans;

			if (buffer == default)
				throw new ArgumentNullException(nameof(buffer));

			if (sourceArea.X < 0 || sourceArea.Y < 0 || sourceArea.Width < 0 || sourceArea.Height < 0 || sourceArea.X + sourceArea.Width > width || sourceArea.Y + sourceArea.Height > height)
				throw new ArgumentOutOfRangeException(nameof(sourceArea), "Requested area does not fall within the image bounds");

			if (cb > cbStride)
				throw new ArgumentOutOfRangeException(nameof(cbStride), "Stride is too small for the requested area");

			if ((sourceArea.Height - 1) * cbStride + cb > buffer.Length)
				throw new ArgumentOutOfRangeException(nameof(buffer), "Buffer is too small for the requested area");

			var pixspan = pixels.Value.Memory.Span;
			for (int y = 0; y < sourceArea.Height; y++)
			{
				int row = Math.Min((int)((sourceArea.Y + y) * rheight), rows - 1);
				Unsafe.CopyBlockUnaligned(ref buffer[y * cbStride], ref pixspan[row * stride + sourceArea.X * chans], (uint)cb);
			}
		}

		/// <inheritdoc />
		public void Dispose()
		{
			if (pixels.IsValueCreated)
				pixels.Value.Dispose();
		}
	}
}
