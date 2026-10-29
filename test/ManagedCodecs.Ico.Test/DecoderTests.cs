// Copyright © Clinton Ingram and Contributors
// SPDX-License-Identifier: MIT

using System;
using System.IO;
using System.Linq;
using System.Drawing;
using System.Buffers.Binary;
using System.Threading.Tasks;
using System.Collections.Generic;

using Xunit;

using PhotoSauce.MagicScaler;

using static PhotoSauce.ManagedCodecs.Ico.Test.TestUtil;

namespace PhotoSauce.ManagedCodecs.Ico.Test;

public class DecoderTests
{
	public DecoderTests() => EnsureCodecs();

	public static TheoryData<int, int, bool> IndexedCases()
	{
		var data = new TheoryData<int, int, bool>();
		foreach (int bpp in new[] { 1, 4, 8 })
			foreach (int width in new[] { 1, 3, 13, 17, 256 })
				foreach (bool topDown in new[] { false, true })
					data.Add(bpp, width, topDown);

		return data;
	}

	public static TheoryData<int, bool> WidthCases()
	{
		var data = new TheoryData<int, bool>();
		foreach (int width in new[] { 1, 3, 13, 17, 256 })
			foreach (bool topDown in new[] { false, true })
				data.Add(width, topDown);

		return data;
	}

	[Theory, MemberData(nameof(IndexedCases))]
	public void Indexed(int bpp, int width, bool topDown)
	{
		var rnd = new Random(bpp * 1000 + width);
		int height = width == 256 ? 2 : 5;
		int colors = 1 << bpp;

		// The reserved byte of each color table entry holds garbage, as it does in real files.
		var palette = Enumerable.Range(0, colors).Select(_ => (uint)rnd.Next() | 0xab000000).ToArray();
		var dib = new Dib {
			Width = width, Height = height, Bpp = bpp, TopDown = topDown, Palette = palette,
			Pixels = Enumerable.Range(0, width * height).Select(_ => (uint)rnd.Next(colors)).ToArray(),
			Mask = Enumerable.Range(0, width * height).Select(_ => rnd.Next(4) == 0).ToArray()
		};

		var expected = dib.Pixels.Select((p, i) => palette[p] & 0x00ffffff | (dib.Mask[i] ? 0u : 0xff000000)).ToArray();

		AssertPixels(expected, decodeSingle(dib));
	}

	[Fact]
	public void IndexedShortColorTable()
	{
		var rnd = new Random(8);
		var palette = Enumerable.Range(0, 200).Select(_ => (uint)rnd.Next() & 0x00ffffff).ToArray();
		var dib = new Dib {
			Width = 7, Height = 7, Bpp = 8, ColorsUsed = 200, Palette = palette,
			Pixels = Enumerable.Range(0, 49).Select(_ => (uint)rnd.Next(200)).ToArray()
		};

		AssertPixels(dib.Pixels.Select(p => palette[p] | 0xff000000).ToArray(), decodeSingle(dib));
	}

	[Theory, MemberData(nameof(WidthCases))]
	public void Rgb24(int width, bool topDown)
	{
		var rnd = new Random(width);
		int height = width == 256 ? 2 : 5;
		var dib = new Dib {
			Width = width, Height = height, Bpp = 24, TopDown = topDown,
			Pixels = Enumerable.Range(0, width * height).Select(_ => (uint)rnd.Next() & 0x00ffffff).ToArray(),
			Mask = Enumerable.Range(0, width * height).Select(_ => rnd.Next(4) == 0).ToArray()
		};

		var expected = dib.Pixels.Select((p, i) => p | (dib.Mask[i] ? 0u : 0xff000000)).ToArray();

		AssertPixels(expected, decodeSingle(dib));
	}

	[Theory, MemberData(nameof(WidthCases))]
	public void Rgba32(int width, bool topDown)
	{
		var rnd = new Random(width);
		int height = width == 256 ? 2 : 5;
		var dib = new Dib {
			Width = width, Height = height, Bpp = 32, TopDown = topDown,
			Pixels = Enumerable.Range(0, width * height).Select(_ => (uint)rnd.Next() | (uint)rnd.Next(2) << 31).ToArray(),
			Mask = Enumerable.Range(0, width * height).Select(_ => true).ToArray()
		};

		// With a populated alpha channel, the AND mask is ignored.
		AssertPixels(dib.Pixels, decodeSingle(dib));
	}

	[Fact]
	public void Rgba32WithZeroAlphaUsesMask()
	{
		var rnd = new Random(32);
		var dib = new Dib {
			Width = 9, Height = 4, Bpp = 32,
			Pixels = Enumerable.Range(0, 36).Select(_ => (uint)rnd.Next() & 0x00ffffff).ToArray(),
			Mask = Enumerable.Range(0, 36).Select(_ => rnd.Next(3) == 0).ToArray()
		};

		var expected = dib.Pixels.Select((p, i) => p | (dib.Mask[i] ? 0u : 0xff000000)).ToArray();

		AssertPixels(expected, decodeSingle(dib));
	}

	// Bit depth, compression, header size, channel masks (R, G, B, A), and a filter applied to the random pixels.
	private static readonly Dictionary<string, (int Bpp, uint Compression, int HeaderSize, uint[] Masks, uint Filter)> maskedLayouts = new() {
		["16-bit 555"]                   = (16, 0, 40,  [ 0x7c00, 0x03e0, 0x001f, 0 ], 0xffff),
		["16-bit 565 bitfields"]         = (16, 3, 40,  [ 0xf800, 0x07e0, 0x001f ], 0xffff),
		["16-bit 4444 alphabitfields"]   = (16, 6, 40,  [ 0x0f00, 0x00f0, 0x000f, 0xf000 ], 0xffff),
		["16-bit 4444 zero alpha"]       = (16, 6, 40,  [ 0x0f00, 0x00f0, 0x000f, 0xf000 ], 0x0fff),
		["16-bit 565 v2 header"]         = (16, 3, 52,  [ 0xf800, 0x07e0, 0x001f ], 0xffff),
		["16-bit 565 v3 header"]         = (16, 3, 56,  [ 0xf800, 0x07e0, 0x001f ], 0xffff),
		["16-bit zero green mask"]       = (16, 3, 40,  [ 0xf800, 0, 0x001f ], 0xffff),
		["16-bit red mask above bit 15"] = (16, 3, 40,  [ 0xf0000, 0x07e0, 0x001f ], 0xffff),
		["32-bit RGBx bitfields"]        = (32, 3, 40,  [ 0x000000ff, 0x0000ff00, 0x00ff0000 ], 0xffffffff),
		["32-bit 10-bit bitfields"]      = (32, 3, 40,  [ 0x3ff00000, 0x000ffc00, 0x000003ff ], 0xffffffff),
		["32-bit RGBA v4 header"]        = (32, 3, 108, [ 0x000000ff, 0x0000ff00, 0x00ff0000, 0xff000000 ], 0xffffffff),
		["32-bit RGBA v5 header"]        = (32, 3, 124, [ 0x000000ff, 0x0000ff00, 0x00ff0000, 0xff000000 ], 0xffffffff),
	};

	public static TheoryData<string, int, bool> MaskedCases()
	{
		var data = new TheoryData<string, int, bool>();
		foreach (string layout in maskedLayouts.Keys)
			foreach (int width in new[] { 1, 3, 13, 17, 256 })
				foreach (bool topDown in new[] { false, true })
					data.Add(layout, width, topDown);

		return data;
	}

	[Theory, MemberData(nameof(MaskedCases))]
	public void Masked(string layout, int width, bool topDown)
	{
		var (bpp, compression, headerSize, masks, filter) = maskedLayouts[layout];
		var rnd = new Random(layout.Length * 1000 + width);
		int height = width == 256 ? 2 : 5;

		var dib = new Dib {
			Width = width, Height = height, Bpp = bpp, Compression = compression, HeaderSize = headerSize, TopDown = topDown,
			Masks = compression == 0 ? null : masks,
			Pixels = Enumerable.Range(0, width * height).Select(_ => (uint)rnd.Next() & filter).ToArray(),
			Mask = Enumerable.Range(0, width * height).Select(_ => rnd.Next(4) == 0).ToArray()
		};

		uint amask = masks.Length > 3 ? masks[3] : 0;
		bool useAlpha = amask != 0 && dib.Pixels.Any(p => (p & amask) != 0);

		var expected = dib.Pixels.Select((p, i) => Bgra(
			channel(p, masks[2], bpp),
			channel(p, masks[1], bpp),
			channel(p, masks[0], bpp),
			useAlpha ? channel(p, amask, bpp) : dib.Mask![i] ? (byte)0 : byte.MaxValue
		)).ToArray();

		AssertPixels(expected, decodeSingle(dib));

		// Collects the masked bits one at a time, from the lowest.  Bits beyond the pixel size read as 0.
		static byte channel(uint px, uint mask, int bpp)
		{
			uint value = 0;
			int bits = 0;
			for (int bit = 0; bit < 32; bit++)
			{
				if ((mask & 1u << bit) == 0)
					continue;

				if (bit < bpp)
					value |= (px >> bit & 1) << bits;

				bits++;
			}

			return bits == 0 ? (byte)0 : Scale(value, bits);
		}
	}

	[Fact]
	public void GappedMask()
	{
		// A mask with gaps has no defined meaning.  It must decode without error, and the AND mask still applies.
		var rnd = new Random(15);
		var dib = new Dib {
			Width = 7, Height = 3, Bpp = 16, Compression = 3, Masks = [ 0x0f0f, 0x00f0, 0xf000 ],
			Pixels = Enumerable.Range(0, 21).Select(_ => (uint)rnd.Next() & 0xffff).ToArray(),
			Mask = Enumerable.Range(0, 21).Select(_ => rnd.Next(2) == 0).ToArray()
		};

		var actual = decodeSingle(dib);
		for (int i = 0; i < dib.Pixels.Length; i++)
			Assert.Equal(dib.Mask[i] ? 0 : byte.MaxValue, actual[i * 4 + 3]);
	}

	public static TheoryData<string, bool, string> CropCases()
	{
		var data = new TheoryData<string, bool, string>();
		foreach (string layout in new[] { "1-bit", "4-bit", "8-bit", "16-bit 565", "24-bit", "32-bit", "32-bit bitfields" })
			foreach (bool topDown in new[] { false, true })
				foreach (string cut in new[] { "complete", "cut in mask", "cut in color data" })
					data.Add(layout, topDown, cut);

		return data;
	}

	[Theory, MemberData(nameof(CropCases))]
	public void Crop(string layout, bool topDown, string cut)
	{
		// The pipeline reads in strips and asks for sub-areas when cropping.  Each area must match the same pixels
		// of a full decode, including across the boundaries of missing color and mask rows in a truncated frame.
		const int width = 37, height = 5;
		var rnd = new Random(layout.Length);
		var pixels = Enumerable.Range(0, width * height).Select(_ => (uint)rnd.Next()).ToArray();
		var palette = Enumerable.Range(0, 256).Select(_ => (uint)rnd.Next() & 0x00ffffff).ToArray();

		// Runs of opaque pixels give whole zero mask bytes, which the decoder skips 8 at a time.
		var mask = Enumerable.Range(0, width * height).Select(i => i % width >= 16 && rnd.Next(3) == 0).ToArray();

		var dib = layout switch {
			"1-bit"            => new Dib { Width = width, Height = height, Bpp = 1, Palette = palette.Take(2).ToArray(), Pixels = pixels.Select(p => p & 1).ToArray(), Mask = mask },
			"4-bit"            => new Dib { Width = width, Height = height, Bpp = 4, Palette = palette.Take(16).ToArray(), Pixels = pixels.Select(p => p & 0xf).ToArray(), Mask = mask },
			"8-bit"            => new Dib { Width = width, Height = height, Bpp = 8, Palette = palette, Pixels = pixels.Select(p => p & 0xff).ToArray(), Mask = mask },
			"16-bit 565"       => new Dib { Width = width, Height = height, Bpp = 16, Compression = 3, Masks = [ 0xf800, 0x07e0, 0x001f ], Pixels = pixels.Select(p => p & 0xffff).ToArray(), Mask = mask },
			"24-bit"           => new Dib { Width = width, Height = height, Bpp = 24, Pixels = pixels.Select(p => p & 0xffffff).ToArray(), Mask = mask },
			"32-bit"           => new Dib { Width = width, Height = height, Bpp = 32, Pixels = pixels.Select(p => p & 0xffffff).ToArray(), Mask = mask },
			_                  => new Dib { Width = width, Height = height, Bpp = 32, Compression = 3, Masks = [ 0xff, 0xff00, 0xff0000 ], Pixels = pixels, Mask = mask },
		};

		dib = new Dib {
			Width = dib.Width, Height = dib.Height, Bpp = dib.Bpp, Compression = dib.Compression, Masks = dib.Masks,
			Palette = dib.Palette, Pixels = dib.Pixels, Mask = dib.Mask, TopDown = topDown
		};

		// Keep two AND mask rows, or three color rows and no mask.
		var ico = IcoBuilder.Build(dib);
		ico = cut switch {
			"cut in mask"       => ico.Prefix(ico.Length - dib.MaskStride * (height - 2)),
			"cut in color data" => ico.Prefix(ico.Length - dib.MaskStride * height - dib.XorStride * (height - 3)),
			_                   => ico
		};

		using var cnt = Load(ico);
		using var frame = cnt.GetFrame(0);
		var full = Decode(frame);

		foreach (var (y0, h) in new[] { (0, height), (1, height - 1), (2, 1), (height - 1, 1) })
		{
			foreach (int x in new[] { 0, 1, 3, 8, 13 })
			{
				foreach (int w in new[] { 1, 8, 16, width - x })
				{
					if (x + w > width)
						continue;

					var crop = DirtyBuffer(w * h * 4);
					frame.PixelSource.CopyPixels(new Rectangle(x, y0, w, h), w * 4, crop);

					for (int y = 0; y < h; y++)
						Assert.Equal(full.AsSpan(((y0 + y) * width + x) * 4, w * 4).ToArray(), crop.AsSpan(y * w * 4, w * 4).ToArray());
				}
			}
		}
	}

	[Fact]
	public void PngFrame()
	{
		var rnd = new Random(1);
		var bgra = new byte[16 * 16 * 4];
		rnd.NextBytes(bgra);
		for (int i = 3; i < bgra.Length; i += 4)
			bgra[i] = byte.MaxValue;

		var png = EncodePng(bgra, 16, 16);
		using var cnt = Load(IcoBuilder.Build((png, 16, 16)));
		using var frame = cnt.GetFrame(0);

		Assert.Equal(bgra, Decode(frame));
	}

	[Fact]
	public void PngAndBitmapFramesShareTheStream()
	{
		// Random noise does not compress, so the PNG data is far larger than libpng's 8 KB read size, and the
		// first CopyPixels below stops in the middle of it.
		const int size = 128;
		var rnd = new Random(2);
		var bgra = new byte[size * size * 4];
		rnd.NextBytes(bgra);
		for (int i = 3; i < bgra.Length; i += 4)
			bgra[i] = byte.MaxValue;

		var dib = new Dib {
			Width = 16, Height = 16, Bpp = 8, Palette = Enumerable.Range(0, 256).Select(i => (uint)i * 0x010101).ToArray(),
			Pixels = Enumerable.Range(0, 256).Select(i => (uint)i).ToArray()
		};

		var png = EncodePng(bgra, size, size);
		Assert.True(png.Length > 4 * 8192);

		var ico = IcoBuilder.Build((png, size, size), (dib.ToBytes(), 16, 16));
		using var cnt = Load(ico);
		using var pngFrame = cnt.GetFrame(0);
		using var bmpFrame = cnt.GetFrame(1);

		// Read the PNG frame in two parts, decoding the bitmap frame in between.  The PNG decoder reads lazily,
		// so this fails if the frames do not each keep their own stream position.
		var src = pngFrame.PixelSource;
		var actual = new byte[bgra.Length];
		src.CopyPixels(new Rectangle(0, 0, size, 10), size * 4, actual);
		var bmp = Decode(bmpFrame);
		src.CopyPixels(new Rectangle(0, 10, size, size - 10), size * 4, actual.AsSpan(10 * size * 4));

		Assert.Equal(bgra, actual);
		AssertPixels(dib.Pixels.Select(p => p * 0x010101 | 0xff000000).ToArray(), bmp);
	}

	[Fact]
	public void CursorContainer()
	{
		var dib = new Dib { Width = 3, Height = 3, Bpp = 1, Palette = [ 0, 0xffffff ], Pixels = [ 0, 1, 0, 1, 0, 1, 0, 1, 0 ] };
		using var cnt = IcoContainer.TryLoad(new MemoryStream(IcoBuilder.Build(2, (dib.ToBytes(), 3, 3))), null);

		Assert.NotNull(cnt);
		using var frame = cnt.GetFrame(0);
		AssertPixels(dib.Pixels.Select(p => p == 0 ? 0xff000000 : 0xffffffff).ToArray(), Decode(frame));
	}

	[Fact]
	public void PublicApi()
	{
		var dib = new Dib { Width = 4, Height = 4, Bpp = 24, Pixels = new uint[16] };
		var big = new Dib { Width = 8, Height = 8, Bpp = 32, Pixels = Enumerable.Repeat(0xff336699u, 64).ToArray() };
		var ico = IcoBuilder.Build(dib, big);

		var info = ImageFileInfo.Load(new MemoryStream(ico));
		Assert.Equal("image/x-icon", info.MimeType);
		Assert.Equal(new[] { (4, 4), (8, 8) }, info.Frames.Select(f => (f.Width, f.Height)));

		using var output = new MemoryStream();
		var settings = new ProcessImageSettings { DecoderOptions = new IcoDecoderOptions(1..2) };
		Assert.True(settings.TrySetEncoderFormat(ImageMimeTypes.Png));
		var result = MagicImageProcessor.ProcessImage(new MemoryStream(ico), output, settings);

		Assert.Equal(8, result.Settings.Width);
		Assert.Equal(8, result.Settings.Height);

		// The selected frame comes through the whole pipeline unchanged.
		using var png = Load(IcoBuilder.Build((output.ToArray(), 8, 8)));
		using var frame = png.GetFrame(0);
		AssertPixels(big.Pixels, Decode(frame));
	}

	[Fact]
	public void NotIcoTga()
	{
		// An uncompressed true-color TGA with no ID field or color map starts with the CUR signature.
		var tga = new byte[] { 0, 0, 2, 0, 0, 0, 0, 0, 0, 0, 0, 0, 2, 0, 2, 0, 24, 0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12 };

		var stream = new MemoryStream(tga);
		Assert.Null(IcoContainer.TryLoad(stream, null));
		Assert.Equal(0, stream.Position);
	}

	[Fact]
	public void FirstImageInsideDirectory()
	{
		var ico = IcoBuilder.Build(new Dib { Width = 1, Height = 1, Bpp = 32, Pixels = [ 0 ] });
		ico[18] = 6;

		Assert.Null(IcoContainer.TryLoad(new MemoryStream(ico), null));
	}

	[Theory]
	[InlineData(new byte[] { 1, 0, 1, 0 })]
	[InlineData(new byte[] { 0, 0, 3, 0 })]
	public void NotIco(byte[] header)
	{
		var ico = IcoBuilder.Build(new Dib { Width = 1, Height = 1, Bpp = 32, Pixels = [ 0 ] });
		header.CopyTo(ico, 0);

		var stream = new MemoryStream(ico);
		Assert.Null(IcoContainer.TryLoad(stream, null));
		Assert.Equal(0, stream.Position);
	}

	[Fact]
	public void NoImages()
	{
		// Declined rather than rejected, because other formats share the signature.
		var stream = new MemoryStream([ 0, 0, 1, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0 ]);
		Assert.Null(IcoContainer.TryLoad(stream, null));
		Assert.Equal(0, stream.Position);
	}

	[Fact]
	public void FrameIndexOutOfRange()
	{
		using var cnt = Load(IcoBuilder.Build(new Dib { Width = 1, Height = 1, Bpp = 32, Pixels = [ 0 ] }));

		Assert.Throws<ArgumentOutOfRangeException>(() => cnt.GetFrame(1));
		Assert.Throws<ArgumentOutOfRangeException>(() => cnt.GetFrame(-1));
	}

	[Fact]
	public void TruncatedDirectory()
	{
		var dib = new Dib { Width = 1, Height = 1, Bpp = 32, Pixels = [ 0 ] };
		var ico = IcoBuilder.Build(dib, dib, dib);

		using var cnt = Load(ico.Prefix(6 + 16 + 10));
		Assert.Equal(1, cnt.FrameCount);
	}

	public static TheoryData<Dib> CorruptCases() => [
		new Dib { Name = "oversized color table", Width = 2, Height = 2, Bpp = 8, ColorsUsed = 300, Pixels = new uint[4] },
		new Dib { Name = "BITMAPCOREHEADER", Width = 2, Height = 2, Bpp = 8, HeaderSize = 12, Pixels = new uint[4] },
		new Dib { Name = "RLE8", Width = 2, Height = 2, Bpp = 8, Compression = 1, Pixels = new uint[4] },
		new Dib { Name = "2-bit", Width = 2, Height = 2, Bpp = 2, Pixels = new uint[4] },
		new Dib { Name = "bitfields at 24-bit", Width = 2, Height = 2, Bpp = 24, Compression = 3, Masks = [ 0xff, 0xff00, 0xff0000 ], Pixels = new uint[4] },
		new Dib { Name = "zero width", Width = 0, Height = 2, Bpp = 32, Pixels = [ ] },
		new Dib { Name = "zero height", Width = 2, Height = 0, Bpp = 32, Pixels = [ ] },
		new Dib { Name = "too wide", Width = BmpPixelSource.MaxDimension + 1, Height = 1, Bpp = 1, Palette = [ 0, 0 ], Pixels = new uint[BmpPixelSource.MaxDimension + 1] },
	];

	[Theory, MemberData(nameof(CorruptCases))]
	public void CorruptHeader(Dib dib)
	{
		using var cnt = Load(IcoBuilder.Build(dib));

		Assert.Throws<InvalidDataException>(() => cnt.GetFrame(0));
	}

	[Fact]
	public void ColorTableSkippedForTrueColor()
	{
		// Any color table size is allowed with 24-bit pixels.  The table is unused and must be skipped.
		var rnd = new Random(24);
		var dib = new Dib {
			Width = 5, Height = 3, Bpp = 24, ColorsUsed = 300, Palette = Enumerable.Range(0, 300).Select(_ => (uint)rnd.Next()).ToArray(),
			Pixels = Enumerable.Range(0, 15).Select(_ => (uint)rnd.Next() & 0x00ffffff).ToArray()
		};

		AssertPixels(dib.Pixels.Select(p => p | 0xff000000).ToArray(), decodeSingle(dib));
	}

	[Fact]
	public void HugeHeaderWithoutData()
	{
		// The header claims 64 MB of pixel data, and the stream has none.  The frame decodes as transparent black,
		// without allocating for the claimed size.
		var dib = new Dib { Width = BmpPixelSource.MaxDimension, Height = BmpPixelSource.MaxDimension, Bpp = 32, Pixels = [ ] };
		var ico = IcoBuilder.Build((dib.HeaderBytes(), dib.Width, dib.Height));

		var line = DirtyBuffer(BmpPixelSource.MaxDimension * 4);
#if NET
		long before = GC.GetAllocatedBytesForCurrentThread();
#endif

		using var cnt = Load(ico);
		using var frame = cnt.GetFrame(0);
		var src = frame.PixelSource;
		src.CopyPixels(new Rectangle(0, src.Height - 1, src.Width, 1), line.Length, line);

#if NET
		Assert.InRange(GC.GetAllocatedBytesForCurrentThread() - before, 0, 1 << 20);
#endif
		Assert.Equal(BmpPixelSource.MaxDimension, src.Width);
		Assert.All(line, static b => Assert.Equal(0, b));
	}

	[Fact]
	public void LoadRetriedAfterFailure()
	{
		// A read error while loading the pixel data must not be remembered as an empty frame.
		var rnd = new Random(7);
		var dib = new Dib { Width = 4, Height = 4, Bpp = 32, Pixels = Enumerable.Range(0, 16).Select(_ => (uint)rnd.Next() | 0xff000000).ToArray() };
		var ico = IcoBuilder.Build(dib);

		var stream = new FailingStream(ico, failAfter: ico.Length - 8);
		using var cnt = IcoContainer.TryLoad(stream, null)!;
		using var frame = cnt.GetFrame(0);
		var buff = new byte[4 * 4 * 4];

		Assert.Throws<IOException>(() => frame.PixelSource.CopyPixels(new Rectangle(0, 0, 4, 4), 16, buff));

		stream.Failing = false;
		frame.PixelSource.CopyPixels(new Rectangle(0, 0, 4, 4), 16, buff);
		AssertPixels(dib.Pixels, buff);
	}

	[Fact]
	public void PngStreamFailure()
	{
		// A stream error inside a PNG frame must surface, not be mistaken for truncated data.  On Windows a stream
		// exception passes straight through libpng, but elsewhere libpng swallows it and only reports a failed open,
		// after which the decoder reads the stream again to classify the failure.  This reproduces that second read
		// on every platform: libpng rejects the IHDR (invalid bit depth) without an I/O error, and the stream fails
		// right after IHDR, where the classification reads next.
		const int pngStart = 6 + 16, ihdrType = 8 + 4, ihdrData = ihdrType + 4, ihdrLength = 13;
		var png = EncodePng(new byte[4 * 4 * 4], 4, 4);
		png[ihdrData + 8] = 3;
		BinaryPrimitives.WriteUInt32BigEndian(png.AsSpan(ihdrData + ihdrLength), Crc32(png.AsSpan(ihdrType, 4 + ihdrLength)));
		var ico = IcoBuilder.Build((png, 4, 4));

		var stream = new FailingStream(ico, failAfter: pngStart + ihdrData + ihdrLength + 4);
		using var cnt = IcoContainer.TryLoad(stream, null)!;

		Assert.Throws<IOException>(() => cnt.GetFrame(0));
	}

	[Fact]
	public void CorruptCompletePng()
	{
		// A complete PNG with an invalid bit depth (3) in its IHDR cannot be opened by the codec, and it is not
		// missing data.  The chunk checksum is kept valid, so the header itself is what the codec rejects.
		const int ihdrType = 8 + 4, ihdrData = ihdrType + 4, ihdrLength = 13;
		var png = EncodePng(new byte[4 * 4 * 4], 4, 4);
		png[ihdrData + 8] = 3;
		BinaryPrimitives.WriteUInt32BigEndian(png.AsSpan(ihdrData + ihdrLength), Crc32(png.AsSpan(ihdrType, 4 + ihdrLength)));

		using var cnt = Load(IcoBuilder.Build((png, 4, 4)));
		var ex = Assert.Throws<InvalidDataException>(() => cnt.GetFrame(0));
		Assert.NotNull(ex.InnerException);
	}

	[Theory]
	[InlineData(0xfffffff4u, "IHDR")]
	[InlineData(0xffffffffu, "IHDR")]
	[InlineData(0x0d00000du, "IHDR")]
	[InlineData(0xfffffff4u, "IDAT")]
	[InlineData(0x80000000u, "IDAT")]
	public async Task CorruptPngChunkLength(uint length, string chunk)
	{
		// A chunk length outside the PNG limit, or an IHDR of the wrong size, is damage, not truncation.  In IDAT,
		// past the IHDR check, the length limit decides; 0xfffffff4 would wrap a 32-bit chunk walk back onto
		// itself, so the walk must neither loop forever nor read it as data running past the end.
		var png = EncodePng(new byte[4 * 4 * 4], 4, 4);
		int type = png.AsSpan().IndexOf(System.Text.Encoding.ASCII.GetBytes(chunk));
		BinaryPrimitives.WriteUInt32BigEndian(png.AsSpan(type - 4), length);

		var ct = TestContext.Current.CancellationToken;
		using var cnt = Load(IcoBuilder.Build((png, 4, 4)));
		var open = Task.Run(() => Assert.Throws<InvalidDataException>(() => cnt.GetFrame(0)), ct);
		var done = await Task.WhenAny(open, Task.Delay(TimeSpan.FromSeconds(30), ct));

		Assert.True(done == open, "Opening the frame did not finish.");
		_ = await open;
	}

	[Fact]
	public void TruncatedPngFrame()
	{
		// Cut inside IHDR: the codec can't open it, and it ends early, so the frame is empty at the directory size.
		var png = EncodePng(new byte[4 * 4 * 4], 4, 4);
		using (var cnt = Load(IcoBuilder.Build((png.Prefix(20), 5, 6))))
		using (var frame = cnt.GetFrame(0))
		{
			Assert.Equal((5, 6), (frame.PixelSource.Width, frame.PixelSource.Height));
			Assert.All(Decode(frame), static b => Assert.Equal(0, b));
		}

		// Cut inside the image data: libpng opens it, and every row is either decoded or cleared.  The libpng plugin
		// clears every row of a PNG cut inside a single IDAT chunk (the same happens to a standalone PNG), so only
		// the rows that are present can be checked, not how many of them there are.
		const int size = 128;
		var rnd = new Random(8);
		var noise = new byte[size * size * 4];
		rnd.NextBytes(noise);
		for (int i = 3; i < noise.Length; i += 4)
			noise[i] = byte.MaxValue;

		png = EncodePng(noise, size, size);
		using (var cnt = Load(IcoBuilder.Build((png.Prefix(png.Length / 2), size, size))))
		using (var frame = cnt.GetFrame(0))
		{
			Assert.IsNotType<BmpPixelSource>(frame.PixelSource);
			var actual = Decode(frame);

			int rowBytes = size * 4;
			int present = Enumerable.Range(0, size).TakeWhile(y => actual.AsSpan(y * rowBytes, rowBytes).SequenceEqual(noise.AsSpan(y * rowBytes, rowBytes))).Count();
			Assert.InRange(present, 0, size - 1);

			// Missing rows are cleared.  If the codec reports a format without alpha, the test helper fills in 255.
			for (int y = present; y < size; y++)
				for (int x = 0; x < size; x++)
					Assert.Equal([ 0, 0, 0 ], actual.AsSpan((y * size + x) * 4, 3).ToArray());
		}
	}

	[Fact]
	public void PngFrameMetadata()
	{
		var bgra = new byte[4 * 4 * 4];
		var png = WithGamma(EncodePng(bgra, 4, 4));
		var dib = new Dib { Width = 4, Height = 4, Bpp = 32, Pixels = new uint[16] };

		using var cnt = Load(IcoBuilder.Build((png, 4, 4), (dib.ToBytes(), 4, 4)));
		using var pngFrame = cnt.GetFrame(0);
		using var bmpFrame = cnt.GetFrame(1);

		// A gAMA chunk makes the PNG codec report a color profile, which the ICO frame passes through.
		Assert.True(((IMetadataSource)pngFrame).TryGetMetadata<IIccProfileSource>(out _));
		Assert.False(((IMetadataSource)bmpFrame).TryGetMetadata<IIccProfileSource>(out _));
	}

	[Fact]
	public void PngStreamFailureInChunkBody()
	{
		// A stream error in the body of a chunk before the image data.  Outside Windows libpng swallows it, and
		// the chunk walk reads only chunk headers, so the error must come from what the sub-stream recorded.
		// On Windows the exception passes through libpng directly, so this test can't fail there.
		const int pngStart = 6 + 16, gamaBody = pngStart + 8 + 25 + 8;
		var png = WithGamma(EncodePng(new byte[4 * 4 * 4], 4, 4));
		var stream = new FailingStream(IcoBuilder.Build((png, 4, 4)), failAfter: gamaBody, failUntil: gamaBody + 4);

		using var cnt = IcoContainer.TryLoad(stream, null)!;
		Assert.Throws<IOException>(() => cnt.GetFrame(0));
	}

	[Fact]
	public void SubStreamRecordsFault()
	{
		var data = Enumerable.Range(0, 40).Select(static i => (byte)i).ToArray();
		var sub = new IcoSubStream(new FailingStream(data, failAfter: 20), 10, 30);

		var buff = new byte[8];
		Assert.Equal(8, sub.Read(buff, 0, 8));
		Assert.Null(sub.Fault);

		sub.Position = 8;
		var ex = Assert.Throws<IOException>(() => sub.Read(buff, 0, 8));
		Assert.Same(ex, sub.Fault?.SourceException);

		// Only the first fault is kept.
		sub.Position = 12;
		_ = Assert.Throws<IOException>(() => sub.ReadByte());
		Assert.Same(ex, sub.Fault?.SourceException);
	}

	[Fact]
	public void BitfieldMasksCutOff()
	{
		// A 40-byte BI_BITFIELDS header whose channel masks are cut off takes its size from the directory.
		var dib = new Dib { Width = 4, Height = 4, Bpp = 16, Compression = 3, Masks = [ 0xf800, 0x07e0, 0x001f ], Pixels = new uint[16] };
		var ico = IcoBuilder.Build((dib.ToBytes(), 9, 7));

		using var cnt = Load(ico.Prefix(6 + 16 + 40 + 6));
		using var frame = cnt.GetFrame(0);

		Assert.Equal((9, 7), (frame.PixelSource.Width, frame.PixelSource.Height));
		Assert.All(Decode(frame), static b => Assert.Equal(0, b));
	}

	[Fact]
	public void DisposedObjects()
	{
		var rnd = new Random(6);
		var bgra = new byte[64 * 64 * 4];
		rnd.NextBytes(bgra);
		var dib = new Dib { Width = 4, Height = 4, Bpp = 32, Pixels = new uint[16] };

		var cnt = Load(IcoBuilder.Build((EncodePng(bgra, 64, 64), 64, 64), (dib.ToBytes(), 4, 4)));

		// A repeated Dispose must not throw.  Whether it releases the PNG codec's buffers only once can't be
		// observed from here; the use-after-dispose checks below can.
		var pngFrame = cnt.GetFrame(0);
		pngFrame.PixelSource.CopyPixels(new Rectangle(0, 0, 10, 2), 40, new byte[80]);
		pngFrame.Dispose();
		pngFrame.Dispose();

		var bmpFrame = cnt.GetFrame(1);
		var src = bmpFrame.PixelSource;
		bmpFrame.Dispose();
		bmpFrame.Dispose();
		Assert.Throws<ObjectDisposedException>(() => src.CopyPixels(new Rectangle(0, 0, 4, 4), 16, new byte[64]));

		cnt.Dispose();
		Assert.Throws<ObjectDisposedException>(() => cnt.GetFrame(0));
	}

	[Fact]
	public void SubStream()
	{
		var data = Enumerable.Range(0, 20).Select(i => (byte)i).ToArray();
		var stream = new IcoSubStream(new MemoryStream(data), 5, 10);

		Assert.Equal(10, stream.Length);
		Assert.Equal(5, stream.ReadByte());
		Assert.Equal(1, stream.Position);

		Assert.Equal(12, stream.Seek(2, SeekOrigin.End));
		Assert.Equal(-1, stream.ReadByte());
		Assert.Equal(0, stream.Read(new byte[4], 0, 4));

		stream.Position = 8;
		var buff = new byte[4];
		Assert.Equal(2, stream.Read(buff, 0, 4));
		Assert.Equal(new byte[] { 13, 14, 0, 0 }, buff);

		Assert.Throws<IOException>(() => stream.Seek(-1, SeekOrigin.Begin));
		Assert.Throws<ArgumentOutOfRangeException>(() => stream.Position = -1);
	}

	[Fact]
	public void FrameRange()
	{
		var dibs = new[] { 1, 2, 3 }.Select(s => new Dib { Width = s, Height = s, Bpp = 32, Pixels = new uint[s * s] }).ToArray();
		var ico = IcoBuilder.Build(dibs);

		using var one = Load(ico, new IcoDecoderOptions(1..2));
		Assert.Equal(1, one.FrameCount);
		using (var frame = one.GetFrame(0))
			Assert.Equal(2, frame.PixelSource.Width);

		// An out-of-range selection falls back to all frames, as in the other multi-frame codecs.
		using var all = Load(ico, new IcoDecoderOptions(5..6));
		Assert.Equal(3, all.FrameCount);
	}

	[Fact]
	public void TruncatedAtEveryLength()
	{
		var rnd = new Random(3);
		var bgra = new byte[12 * 12 * 4];
		rnd.NextBytes(bgra);

		var dibs = new[] {
			new Dib { Width = 5, Height = 4, Bpp = 32, Pixels = Enumerable.Range(0, 20).Select(_ => (uint)rnd.Next()).ToArray() },
			new Dib { Width = 3, Height = 3, Bpp = 8, Palette = [ 0x123456, 0x654321 ], ColorsUsed = 2, Pixels = [ 0, 1, 0, 1, 0, 1, 0, 1, 1 ] },
			new Dib { Width = 9, Height = 2, Bpp = 16, Compression = 3, Masks = [ 0xf800, 0x07e0, 0x001f ], Pixels = new uint[18] },
		};
		var ico = IcoBuilder.Build([ .. dibs.Select(static d => (d.ToBytes(), d.Width, d.Height)), (EncodePng(bgra, 12, 12), 12, 12) ]);

		for (int len = 0; len < ico.Length; len++)
		{
			IcoContainer? cnt;
			try
			{
				cnt = IcoContainer.TryLoad(new MemoryStream(ico.Prefix(len)), null);
			}
			catch (InvalidDataException)
			{
				// Only a header too short to hold a single directory entry may fail.
				Assert.InRange(len, 6, 6 + 16 - 1);
				continue;
			}

			if (cnt is null)
			{
				Assert.InRange(len, 0, 5);
				continue;
			}

			using (cnt)
			{
				// A cut PNG frame either decodes as far as libpng allows or, when libpng can't open it, comes out empty.
				for (int i = 0; i < cnt.FrameCount; i++)
				{
					using var frame = cnt.GetFrame(i);
					_ = Decode(frame);
				}
			}
		}
	}

	[Fact]
	public void TruncatedPixelData()
	{
		var rnd = new Random(4);
		var dib = new Dib {
			Width = 4, Height = 4, Bpp = 8, Palette = Enumerable.Range(0, 256).Select(_ => (uint)rnd.Next() & 0x00ffffff).ToArray(),
			Pixels = Enumerable.Range(0, 16).Select(_ => (uint)rnd.Next(256)).ToArray(),
			Mask = Enumerable.Range(0, 16).Select(_ => true).ToArray()
		};

		var ico = IcoBuilder.Build(dib);
		var full = Decode(Load(ico).GetFrame(0));

		// Cut halfway through the third of the four XOR rows.  Rows are stored bottom-up, so the bottom two survive
		// and the partial row counts as missing.  The AND mask is gone, which leaves the surviving pixels opaque.
		var cut = ico.Prefix(ico.Length - (dib.XorStride + dib.XorStride / 2 + dib.MaskStride * 4));
		var part = Decode(Load(cut).GetFrame(0));

		for (int y = 0; y < 4; y++)
		{
			for (int x = 0; x < 4; x++)
			{
				int o = (y * 4 + x) * 4;
				uint act = BinaryPrimitives.ReadUInt32LittleEndian(part.AsSpan(o));
				uint exp = y < 2 ? 0 : BinaryPrimitives.ReadUInt32LittleEndian(full.AsSpan(o)) | 0xff000000;
				Assert.Equal(exp, act);
			}
		}
	}

	[Fact]
	public void TruncatedMaskWithAlphaChannel()
	{
		// A frame that takes its alpha from its alpha channel keeps it on rows whose AND mask is missing.
		var rnd = new Random(9);
		var dib = new Dib {
			Width = 4, Height = 4, Bpp = 32,
			Pixels = Enumerable.Range(0, 16).Select(_ => (uint)rnd.Next() | (uint)rnd.Next(1, 255) << 24).ToArray(),
			Mask = Enumerable.Range(0, 16).Select(_ => true).ToArray()
		};

		var ico = IcoBuilder.Build(dib);
		var part = Decode(Load(ico.Prefix(ico.Length - dib.MaskStride * 3)).GetFrame(0));

		AssertPixels(dib.Pixels, part);
	}

	[Fact]
	public void FrameOffsetPastEnd()
	{
		var ico = IcoBuilder.Build(new Dib { Width = 20, Height = 20, Bpp = 32, Pixels = new uint[400] });
		using var cnt = Load(ico.Prefix(6 + 16));
		using var frame = cnt.GetFrame(0);

		// With no data at all, the frame takes its size from the directory and decodes as transparent black.
		Assert.Equal(20, frame.PixelSource.Width);
		Assert.All(Decode(frame), static b => Assert.Equal(0, b));
	}

	private static byte[] decodeSingle(Dib dib)
	{
		using var cnt = Load(IcoBuilder.Build(dib));
		using var frame = cnt.GetFrame(0);

		Assert.Equal(PixelFormats.Bgra32bpp, frame.PixelSource.Format);
		Assert.Equal(dib.Width, frame.PixelSource.Width);
		Assert.Equal(dib.Height, frame.PixelSource.Height);

		return Decode(frame);
	}
}
