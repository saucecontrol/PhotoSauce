// Copyright © Clinton Ingram and Contributors
// SPDX-License-Identifier: MIT

using System;
using System.IO;
using System.Linq;
using System.Buffers.Binary;
using System.Runtime.InteropServices;

using Xunit;

using PhotoSauce.MagicScaler;

using static PhotoSauce.ManagedCodecs.Ico.Test.TestUtil;

namespace PhotoSauce.ManagedCodecs.Ico.Test;

// Bitmap variants that are valid in ICO files but rare in real ones (16-bit, BI_BITFIELDS, newer headers,
// top-down rows), taken from bmpsuite (public domain, see Images/bmpsuite/readme.md).  Each BMP is rewritten
// as an ICO frame and compared with the Windows BMP decoder's output for the original file.
public class BmpSuiteTests
{
	private const int fileHeaderSize = 14;

	private static readonly string imageDir = Path.Combine(AppContext.BaseDirectory, "Images", "bmpsuite");

	public BmpSuiteTests() => EnsureCodecs();

	public static TheoryData<string> Files() => new(Directory.EnumerateFiles(imageDir, "*.bmp").Select(static f => Path.GetFileName(f)).OrderBy(static f => f, StringComparer.Ordinal));

	[Theory, MemberData(nameof(Files))]
	public void MatchesWindowsDecoder(string name)
	{
		if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
			Assert.Skip("The reference decoder is the Windows BMP codec.");

		var bmp = File.ReadAllBytes(Path.Combine(imageDir, name));

		// Only a file the Windows codec refuses to open is skipped; any other failure is a test failure.
		ProcessingPipeline pipeline;
		try
		{
			pipeline = MagicImageProcessor.BuildPipeline(new MemoryStream(bmp), ProcessImageSettings.Default);
		}
		catch (InvalidDataException ex)
		{
			Assert.Skip($"The Windows BMP codec does not decode this file: {ex.Message}");
			return;
		}

		byte[] expected;
		bool refHasAlpha;
		using (pipeline)
		{
			refHasAlpha = pipeline.PixelSource.Format == PixelFormats.Bgra32bpp;
			expected = Decode(pipeline.PixelSource);
		}

		var (dib, width, height) = toIcoFrame(bmp);
		using var cnt = Load(IcoBuilder.Build((dib, width, height)));
		using var frame = cnt.GetFrame(0);
		var actual = Decode(frame);

		Assert.Equal(expected.Length, actual.Length);
		for (int i = 0; i < actual.Length; i += 4)
		{
			// Windows ignores the alpha channel of most bitmap variants, so compare color only, unless it kept it.
			uint mask = refHasAlpha ? 0xffffffff : 0x00ffffff;
			uint a = BinaryPrimitives.ReadUInt32LittleEndian(actual.AsSpan(i)) & mask, e = BinaryPrimitives.ReadUInt32LittleEndian(expected.AsSpan(i)) & mask;
			if (a != e)
				Assert.Fail($"Pixel {i / 4}: expected 0x{e:x8}, actual 0x{a:x8}.");
		}
	}

	// Drop the file header, double the height to cover an AND mask, and append an all-opaque mask.
	private static (byte[] Dib, int Width, int Height) toIcoFrame(byte[] bmp)
	{
		int dataOffset = BinaryPrimitives.ReadInt32LittleEndian(bmp.AsSpan(10));
		var info = bmp.AsSpan(fileHeaderSize);
		int width = BinaryPrimitives.ReadInt32LittleEndian(info[4..]);
		int height = BinaryPrimitives.ReadInt32LittleEndian(info[8..]);
		int bpp = BinaryPrimitives.ReadUInt16LittleEndian(info[14..]);

		int rows = Math.Abs(height);
		int cbXor = (width * bpp + 31) / 32 * 4 * rows;
		int cbMask = (width + 31) / 32 * 4 * rows;
		int cbHead = dataOffset - fileHeaderSize;

		var dib = new byte[cbHead + cbXor + cbMask];
		bmp.AsSpan(fileHeaderSize, cbHead).CopyTo(dib);
		bmp.AsSpan(dataOffset, cbXor).CopyTo(dib.AsSpan(cbHead));
		BinaryPrimitives.WriteInt32LittleEndian(dib.AsSpan(8), height * 2);

		return (dib, width, rows);
	}
}
