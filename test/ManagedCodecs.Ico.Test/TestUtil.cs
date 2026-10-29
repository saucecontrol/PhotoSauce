// Copyright © Clinton Ingram and Contributors
// SPDX-License-Identifier: MIT

using System;
using System.IO;
using System.Drawing;
using System.Buffers.Binary;

using Xunit;

using PhotoSauce.MagicScaler;
using PhotoSauce.NativeCodecs.Libpng;

namespace PhotoSauce.ManagedCodecs.Ico.Test;

internal static class TestUtil
{
	private static readonly object sync = new();
	private static bool configured;

	// The codec registry is process-wide, so every test configures it the same way.
	public static void EnsureCodecs()
	{
		lock (sync)
		{
			if (configured)
				return;

			CodecManager.Configure(static codecs => {
				codecs.UseLibpng();
				codecs.UseIco();
			});
			configured = true;
		}
	}

	public static IcoContainer Load(byte[] ico, IDecoderOptions? options = null) =>
		IcoContainer.TryLoad(new MemoryStream(ico), options) ?? throw new InvalidOperationException("Not recognized as ICO.");

	public static byte[] Decode(IImageFrame frame) => Decode(frame.PixelSource);

	// Fills a destination buffer with a value the decoder never produces by accident, so a row it fails to write
	// shows up as a difference.  The pipeline hands decoders reused, uncleared buffers.
	public static byte[] DirtyBuffer(int length)
	{
		var buff = new byte[length];
		buff.AsSpan().Fill(0xcd);
		return buff;
	}

	public static byte[] Decode(IPixelSource src)
	{
		var buff = DirtyBuffer(src.Width * src.Height * 4);

		if (src.Format == PixelFormats.Bgra32bpp)
		{
			src.CopyPixels(new Rectangle(0, 0, src.Width, src.Height), src.Width * 4, buff);
			return buff;
		}

		// Frames decoded by another codec (embedded PNG) may use a narrower format.
		int bpp = src.Format == PixelFormats.Bgr24bpp ? 3 : src.Format == PixelFormats.Grey8bpp ? 1 : throw new NotSupportedException(src.Format.ToString());
		var line = DirtyBuffer(src.Width * bpp);
		for (int y = 0; y < src.Height; y++)
		{
			src.CopyPixels(new Rectangle(0, y, src.Width, 1), line.Length, line);
			for (int x = 0; x < src.Width; x++)
			{
				int o = (y * src.Width + x) * 4;
				buff[o] = line[x * bpp];
				buff[o + 1] = line[x * bpp + (bpp == 3 ? 1 : 0)];
				buff[o + 2] = line[x * bpp + (bpp == 3 ? 2 : 0)];
				buff[o + 3] = byte.MaxValue;
			}
		}

		return buff;
	}

	public static byte[] EncodePng(byte[] bgra, int width, int height)
	{
		EnsureCodecs();

		using var ms = new MemoryStream();
		var settings = new ProcessImageSettings();
		_ = settings.TrySetEncoderFormat(ImageMimeTypes.Png);
		_ = MagicImageProcessor.ProcessImage(new ArrayPixelSource(bgra, width, height), ms, settings);

		return ms.ToArray();
	}

	public static uint Bgra(byte b, byte g, byte r, byte a) => (uint)(b | g << 8 | r << 16 | a << 24);

	// Widen a channel value to 8 bits by repeating its bit pattern, written independently of the decoder's shifts.
	public static byte Scale(uint value, int bits)
	{
		if (bits >= 8)
			return (byte)(value >> (bits - 8));

		int res = 0;
		for (int i = 0; i < 8; i++)
			res = res << 1 | (int)(value >> (bits - 1 - i % bits) & 1);

		return (byte)res;
	}

	// The first bytes of a file, as if the rest had been cut off.
	public static byte[] Prefix(this byte[] data, int length) => data.AsSpan(0, length).ToArray();

	// CRC-32 as used by PNG chunks.
	public static uint Crc32(ReadOnlySpan<byte> data)
	{
		uint crc = 0xffffffff;
		foreach (byte b in data)
		{
			crc ^= b;
			for (int k = 0; k < 8; k++)
				crc = (crc & 1) != 0 ? crc >> 1 ^ 0xedb88320 : crc >> 1;
		}

		return ~crc;
	}

	public static void AssertPixels(uint[] expected, byte[] actual)
	{
		Assert.Equal(expected.Length * 4, actual.Length);
		for (int i = 0; i < expected.Length; i++)
		{
			uint act = BinaryPrimitives.ReadUInt32LittleEndian(actual.AsSpan(i * 4));
			if (act != expected[i])
				Assert.Fail($"Pixel {i}: expected 0x{expected[i]:x8}, actual 0x{act:x8}.");
		}
	}

	// Inserts a gAMA chunk (gamma 1/2.2) after IHDR.
	public static byte[] WithGamma(byte[] png)
	{
		const int ihdrEnd = 8 + 4 + 4 + 13 + 4;
		var chunk = new byte[4 + 4 + 4 + 4];
		BinaryPrimitives.WriteInt32BigEndian(chunk, 4);
		"gAMA"u8.CopyTo(chunk.AsSpan(4));
		BinaryPrimitives.WriteInt32BigEndian(chunk.AsSpan(8), 45455);
		BinaryPrimitives.WriteUInt32BigEndian(chunk.AsSpan(12), Crc32(chunk.AsSpan(4, 8)));

		return [ .. png.AsSpan(0, ihdrEnd), .. chunk, .. png.AsSpan(ihdrEnd) ];
	}

	// Throws IOException on any read that touches positions from failAfter up to (not including) failUntil,
	// until Failing is cleared.
	public sealed class FailingStream(byte[] data, int failAfter, int failUntil = int.MaxValue) : Stream
	{
		private readonly MemoryStream inner = new(data);

		public bool Failing { get; set; } = true;

		public override bool CanRead => true;
		public override bool CanSeek => true;
		public override bool CanWrite => false;
		public override long Length => inner.Length;
		public override long Position { get => inner.Position; set => inner.Position = value; }

		public override int Read(byte[] buffer, int offset, int count)
		{
			if (Failing && count > 0 && inner.Position < failUntil && inner.Position + count > failAfter)
				throw new IOException("Simulated read failure.");

			return inner.Read(buffer, offset, count);
		}

		public override long Seek(long offset, SeekOrigin origin) => inner.Seek(offset, origin);
		public override void Flush() { }
		public override void SetLength(long value) => throw new NotSupportedException();
		public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
	}

	private sealed class ArrayPixelSource(byte[] pixels, int width, int height) : BitmapPixelSource(PixelFormats.Bgra32bpp, width, height, width * 4)
	{
		protected override ReadOnlySpan<byte> Span => pixels;
	}
}
