using PhotoSauce.MagicScaler;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.IO;

namespace PhotoSauce.ManagedCodecs.Ico;

internal sealed class IcoContainer : IImageContainer
{
	/// <summary>
	/// Size of ICONDIRENTRY structure.
	/// </summary>
	private const int ICONDIRENTRY_SIZE = 16;
	/// <summary>
	/// First 4 bytes of PNG file.
	/// </summary>
	private const int PNG_SIGNATURE = 0x47_4E_50_89;
	/// <summary>
	/// BITMAPINFOHEADER size.
	/// </summary>
	private const int BMP_HEADER_SIZE = 0x28;
	/// <summary>
	/// No-compression BMP bitmap compression mode.
	/// </summary>
	private const int BI_RGB = 0;


	public const string DecoderDisplayName = "ICO Container Managed Decoder";
	public const string DefaultMimeType = "image/x-icon";

	public static readonly string[] Extensions = [".ico"];
	public static readonly string[] MimeTypes = [DefaultMimeType, "image/ico", "image/vnd.microsoft.icon"];

	private readonly Stream _image;
	private readonly long[] _frameOffsets;
	private readonly IcoFrame?[] _frames;

	string? IImageContainer.MimeType => DefaultMimeType;

	int IImageContainer.FrameCount => _frames.Length;

	internal static IcoContainer? TryLoad(Stream image, IDecoderOptions? options)
	{
		return TryLoadHeader(image, options is IMultiFrameDecoderOptions multiFrameDecoderOptions ? multiFrameDecoderOptions.FrameRange : Range.All, out var frameOffsets)
			? new IcoContainer(image, frameOffsets)
			: null;
	}

	private IcoContainer(Stream image, long[] frameOffsets)
	{
		_image = image;
		_frameOffsets = frameOffsets;
		_frames = new IcoFrame[frameOffsets.Length];
	}

	void IDisposable.Dispose()
	{
	}

	IImageFrame IImageContainer.GetFrame(int index)
	{
		return index >= 0 && index < _frames.Length
			? _frames[index] ??= new IcoFrame(_image, _frameOffsets[index])
			: throw new ArgumentOutOfRangeException(nameof(index), $"Invalid frame index {index}. Expected range: [0, {_frames.Length}).");
	}

	private static bool TryLoadHeader(Stream image, Range frameRange, [NotNullWhen(true)] out long[]? frameOffsets)
	{
		/*
		 * Notes:
		 * 1. All data stored in little-indian format
		 * 2. Paddings in images data counted from start of image entry (specified in image descriptor)
		 * 3. Each image entry in container is either PNG file (whole) or BMP (without header)
		 * 4. for more details on ICO format you can use https://en.wikipedia.org/wiki/ICO_(file_format), which contains more or less correct information
		 *
		 * Format
		 *
		 * 1. Header : 6 bytes
		 *      reserved      : 2 bytes. Always 0
		 *      container type: 2 bytes. 0x1 for ICO, 0x2 for CUR (not handled by current decoder)
		 *      images count  : 2 bytes. Could be 0 for empty container
		 *
		 * 2. Image Descriptors: image count * 16 (descriptor size).
		 * Most of fields in descriptor are not used by decoder and sometimes even could contain wrong information. All required data for decoding we get from image itself.
		 * 
		 *      width in pixels.                             1 byte.
		 *      height in pixels.                            1 byte.
		 *      number of colors in color palette.           1 byte.
		 *      reserved.                                    1 byte.
		 *      color planes count.                          2 bytes.
		 *      bits per pixel.                              2 bytes.
		 *      size of image in bytes.                      4 bytes.
		 *      offset to image data from start of ICO file. 4 bytes. This is the only field that contain important information for decoder
		 */

		// check first 4 bytes of fixed header signature
		if (ReadUInt32(image) != 0x00010000)
		{
			frameOffsets = null;
			return false;
		}

		var start = image.Position - 4;

		var imageCount = ReadUInt16(image);

		// apply frame range
		var (startFrame, frameCount) = frameRange.GetOffsetAndLength(imageCount);
		frameOffsets = new long[frameCount];

		// move to first selected frame descriptor
		image.Position += ICONDIRENTRY_SIZE * startFrame;

		// read frames
		for (var i = 0; i < frameCount; i++)
		{
			// ignore descriptor data except offset field
			image.Position += 12;
			frameOffsets[i] = start + ReadUInt32(image);
		}

		return true;
	}

	sealed class IcoFrame(Stream stream, long Position) : IImageFrame
	{
		IPixelSource IImageFrame.PixelSource
		{
			get
			{
				stream.Position = Position;
				var signature = ReadUInt32(stream);
				stream.Position = Position;

				// PNG
				if (signature == PNG_SIGNATURE)
				{
					// easiest case: image is PNG, so we can just delegate decode to PNG codec
					// potentially there is a gap in functionality if it is APNG, but I doubt such PNG supported in ICO
					return MagicImageProcessor.BuildPipeline(stream, ProcessImageSettings.Default).PixelSource;
				}
				// BMP
				else if (signature == BMP_HEADER_SIZE)
				{
					// Decode BMP
					return ReadBitmap(stream);
				}

				throw new InvalidOperationException($"Unknown ICO image. First 4 bytes are 0x{signature:X8}.");
			}
		}

		void IDisposable.Dispose()
		{
		}

		private static IPixelSource ReadBitmap(Stream image)
		{
			/*
			 * ICO use limited set of BMP functionality and use following layout (BITMAPFILEHEADER is missing):
			 * 1. image starts with BITMAPINFOHEADER (other header types are not supported)
			 * 2. Color table (only for images with BPP=1,4,8)
			 * 3. Bitmap in BPP-specific format
			 * 4. 1-bit opacity mask
			 * 
			 * Implementation notes:
			 * 1. supported BPP (bit-per-pixel) values: 1, 4, 8, 24, 32
			 *   - some docs/ibraries also mention/support BPP=2, but it is not actually a valid value for BMP and I didn't see any such images before.
			 *     Still we can easily enable it if requested.
			 *   - 16 (actually 15) BPP support is not implemented as I didn't seen any ICO files yet with such BPP
			 * 2. In places, where DWORD alignment required, it is calculated from frame start position, not from file start
			 * 3. Opacity mask is ignored for BPP=32 as this format already provide 8-bit opacity information
			 * 4. Reserved 4th byte from color in color table ignored despite rumors that it could be used for opacity as all ICO files with this byte filled
			 *    contained only random garbage
			 *
			 * More or less correct additional information could be also found here https://en.wikipedia.org/wiki/BMP_file_format
			 */

			var start = image.Position;

			// read required data from BITMAPINFOHEADER

			// skip header size
			image.Position += 4;

			// read real bitmap dimensions
			var width = checked((int)ReadUInt32(image));
			var height = checked((int)ReadUInt32(image));

			// skip color planes count
			image.Position += 2;

			var bpp = ReadUInt16(image);
			var compression = ReadUInt32(image);

			// skip size and PPM fields
			image.Position += 12;

			var paletteSize = ReadUInt32(image);

			// position to next byte after header
			image.Position = start + 40;

			// validate/normalize data
			// height contains x2 value as it also count opacity mask height
			if (height % 2 != 0)
			{
				throw new InvalidDataException($"Expected bitmap height must be power of 2 but was {height}.");
			}
			height /= 2;

			if (bpp is not 1 and not 4 and not 8 and not 24 and not 32)
			{
				throw new InvalidDataException($"Bitmaps with BPP={bpp} currently not supported.");
			}

			if (compression != BI_RGB)
			{
				throw new InvalidDataException($"Bitmaps with compression method {compression} currently not supported.");
			}

			// allocate bitmap
			var body = new byte[width * height * 4];

			if (bpp is 1 or 4 or 8)
			{
				// indexed bitmap:
				// - color table
				// - indexed bitmap
				// - opacity mask

				// color table stored as RGBx DWORD with x being reserved(alignment) byte
				// while in theory reserved byte could be used to store opacity, in practice it contains
				// either 0 or (in rare cases) garbage
				var colors = paletteSize != 0 ? (int)paletteSize : (1 << bpp);
				var colorTable = new byte[4 * colors];
				ReadSpan(image, colorTable);

				// read indexed bitmap
				// bitmap stored in rows in upside down order with each row padded to DWORD alignment
				var y = height - 1;
				var x = 0;

				foreach (var idx in GetColorIndexes(image, body.Length / 4, bpp))
				{
					body[y * width * 4 + x * 4] = colorTable[idx * 4];
					body[y * width * 4 + x * 4 + 1] = colorTable[idx * 4 + 1];
					body[y * width * 4 + x * 4 + 2] = colorTable[idx * 4 + 2];
					// opacity byte filled below
					x++;

					// append alignment if needed
					if (x == width)
					{
						x = 0;
						y--;
						image.Position += (4 - ((image.Position - start) % 4)) % 4;
					}
				}

				// read and apply 1-bit opacity mask
				ApplyOpacityMask(image, start, body, width, height);
			}
			else if (bpp == 24)
			{
				// 24-bit RGB bitmap:
				// - bitmap
				// - opacity mask

				// bitmap stored upside down without padding
				for (var y = height - 1; y >= 0; y--)
				{
					var idx = y * width * 4;
					for (var x = 0; x < width; x++)
					{
						body[idx] = ReadUInt8(image);
						body[idx + 1] = ReadUInt8(image);
						body[idx + 2] = ReadUInt8(image);
						idx += 4;
					}
				}

				// read and apply 1-bit opacity mask
				ApplyOpacityMask(image, start, body, width, height);
			}
			else if (bpp == 32)
			{
				// 32-bit RGBA bitmap

				// birmap rows stored bottom-up without padding
				var hasOpacity = false;
				for (var y = height - 1; y >= 0; y--)
				{
					var idx = y * width * 4;
					for (var x = 0; x < width; x++)
					{
						body[idx] = ReadUInt8(image);
						body[idx + 1] = ReadUInt8(image);
						body[idx + 2] = ReadUInt8(image);
						body[idx + 3] = ReadUInt8(image);
						hasOpacity = hasOpacity || body[idx + 3] != 0;
						idx += 4;
					}
				}

				// as we already have 8-bit opacity, no need to read lower-quality 1-bit mask
				// except case when opacity is not actually specified in 4-th byte
				// (this actually happends in the wild)
				if (!hasOpacity)
				{
					ApplyOpacityMask(image, start, body, width, height);
				}
			}
			else
			{
				throw new InvalidOperationException($"Unexpected bitmap BPP value ({bpp})");
			}

			return new IcoBitmapPixelSource(body, width, height);
		}

		private static void ApplyOpacityMask(Stream image, long start, byte[] body, int width, int height)
		{
			// opacity mask stored as 1-bit mask in same upside-down row order with each row padded to DWORD
			var idx = body.Length - 1 - width * 4 + 4;
			var x = 0;
			foreach (var opaque in GetOpacities(image, start, width, height))
			{
				if (opaque)
				{
					body[idx] = 0xFF;
				}

				idx += 4;
				x++;

				if (x % width == 0)
				{
					idx -= width * 4 * 2;
				}
			}
		}

		private static IEnumerable<bool> GetOpacities(Stream image, long start, int width, int height)
		{
			for (var y = 0; y < height; y++)
			{
				if (y > 0)
				{
					image.Position += (4 - ((image.Position - start) % 4)) % 4;
				}

				for (var i = 0; i < width;)
				{
					var pixelsToRead = width - i;

					var b = ReadUInt8(image);

					if (pixelsToRead > 0) yield return 0 == ((b & 0x80) >> 7);
					if (pixelsToRead > 1) yield return 0 == ((b & 0x40) >> 6);
					if (pixelsToRead > 2) yield return 0 == ((b & 0x20) >> 5);
					if (pixelsToRead > 3) yield return 0 == ((b & 0x10) >> 4);
					if (pixelsToRead > 4) yield return 0 == ((b & 0x08) >> 3);
					if (pixelsToRead > 5) yield return 0 == ((b & 0x04) >> 2);
					if (pixelsToRead > 6) yield return 0 == ((b & 0x02) >> 1);
					if (pixelsToRead > 7) yield return 0 == (b & 0x01);

					i += pixelsToRead >= 8 ? 8 : pixelsToRead;
				}
			}
		}

		private static IEnumerable<int> GetColorIndexes(Stream image, int sizeInPixels, ushort bpp)
		{
			var currentPixel = 0;
			while (currentPixel < sizeInPixels)
			{
				var b = ReadUInt8(image);

				// color indexes are packed into byte with length based on image bpp

				if (bpp == 1)
				{
					if (currentPixel == sizeInPixels) yield break;
					yield return (b & 0x80) >> 7;
					currentPixel++;

					if (currentPixel == sizeInPixels) yield break;
					yield return (b & 0x40) >> 6;
					currentPixel++;

					if (currentPixel == sizeInPixels) yield break;
					yield return (b & 0x20) >> 5;
					currentPixel++;

					if (currentPixel == sizeInPixels) yield break;
					yield return (b & 0x10) >> 4;
					currentPixel++;

					if (currentPixel == sizeInPixels) yield break;
					yield return (b & 0x08) >> 3;
					currentPixel++;

					if (currentPixel == sizeInPixels) yield break;
					yield return (b & 0x04) >> 2;
					currentPixel++;

					if (currentPixel == sizeInPixels) yield break;
					yield return (b & 0x02) >> 1;
					currentPixel++;

					yield return b & 0x01;
					currentPixel++;
				}
				else if (bpp == 4)
				{
					yield return b >> 4;
					currentPixel++;

					if (currentPixel < sizeInPixels) yield return b & 0x0F;
					currentPixel++;
				}
				else if (bpp == 8)
				{
					yield return b;
					currentPixel++;
				}
				else
				{
					throw new InvalidOperationException($"Unexpected indexed bitmap BPP value ({bpp})");
				}
			}
		}
	}

	private sealed class IcoBitmapPixelSource(byte[] image, int width, int height)
		: BitmapPixelSource(PixelFormats.Bgra32bpp, width, height, width * 4)
	{
		protected override ReadOnlySpan<byte> Span => image;
	}

	private static byte ReadUInt8(Stream stream)
	{
		var b = stream.ReadByte();
		if (b == -1)
		{
			throw new InvalidDataException($"Not enough data to decode image");
		}

		return (byte)b;
	}

	private static uint ReadUInt32(Stream stream)
	{
		var b1 = stream.ReadByte();
		if (b1 == -1)
		{
			throw new InvalidDataException($"Not enough data to decode image");
		}

		var b2 = stream.ReadByte();
		if (b1 == -1)
		{
			throw new InvalidDataException($"Not enough data to decode image");
		}

		var b3 = stream.ReadByte();
		if (b1 == -1)
		{
			throw new InvalidDataException($"Not enough data to decode image");
		}

		var b4 = stream.ReadByte();
		if (b1 == -1)
		{
			throw new InvalidDataException($"Not enough data to decode image");
		}

		return (uint)(b1 | (b2 << 8) | (b3 << 16) | (b4 << 24));
	}

	private static ushort ReadUInt16(Stream stream)
	{
		var b1 = stream.ReadByte();
		if (b1 == -1)
		{
			throw new InvalidDataException($"Not enough data to decode image");
		}

		var b2 = stream.ReadByte();
		if (b1 == -1)
		{
			throw new InvalidDataException($"Not enough data to decode image");
		}

		return (ushort)(b1 | (b2 << 8));
	}

	private static void ReadSpan(Stream stream, byte[] memory)
	{
		var size = 0;

		while (size != memory.Length)
		{
			var rd = stream.Read(memory, size, memory.Length - size);
			if (rd == 0)
			{
				throw new InvalidDataException($"Not enough data to decode image");
			}

			size += rd;
		}
	}
}
