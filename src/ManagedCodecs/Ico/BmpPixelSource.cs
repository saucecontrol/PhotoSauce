// Copyright © Clinton Ingram and Contributors
// SPDX-License-Identifier: MIT

using System;
using System.IO;
using System.Buffers.Binary;
using System.Runtime.InteropServices;
using System.Runtime.CompilerServices;

using PhotoSauce.MagicScaler;
using PhotoSauce.MagicScaler.Converters;

namespace PhotoSauce.ManagedCodecs.Ico;

// Decodes the device-independent bitmap (DIB) stored in an ICO/CUR frame.  This is a BMP file without
// its BITMAPFILEHEADER, with the height doubled to cover a 1-bit AND (transparency) mask that follows
// the color (XOR) data.  Rows of both are bottom-up unless the height is negative, and padded to 4 bytes.
internal sealed unsafe class BmpPixelSource : PixelSource
{
	public const int MaxDimension = 4096;

	private const int bitmapInfoHeaderSize = 40, bitmapV2HeaderSize = 52, bitmapV3HeaderSize = 56, bitmapV4HeaderSize = 108, bitmapV5HeaderSize = 124;
	private const uint biRgb = 0, biBitfields = 3, biAlphaBitfields = 6;
	private const int tableSize = 256;

	private readonly Stream? stream;
	private readonly long dataOffset;
	private readonly int width, height, bpp, cbXorStride, cbMaskStride;
	private readonly bool topDown, directBgra;
	private readonly ChannelMask red, green, blue, alpha;

	// Indexed formats: the color table, as 256 BGRA entries.  16/32-bit formats: four 256-entry tables
	// (blue, green, red, alpha) that widen each channel to 8 bits.
	private RentedBuffer<byte> tables;
	private RentedBuffer<byte> data;
	private ChannelMask alphaInUse;
	private int xorRows, maskRows;
	private bool loaded, disposed, useAlphaChannel;

	private BmpPixelSource(int w, int h) => (width, height) = (w, h);

	private BmpPixelSource(Stream stm, long offset, in BmpHeader hdr, RentedBuffer<byte> palette, ReadOnlySpan<uint> masks)
	{
		(stream, dataOffset, tables) = (stm, offset, palette);
		(width, height, bpp, topDown) = (hdr.Width, hdr.Height, hdr.BitsPerPixel, hdr.TopDown);

		cbXorStride = (width * bpp + 31) / 32 * 4;
		cbMaskStride = (width + 31) / 32 * 4;

		(red, green, blue, alpha) = (new(masks[0]), new(masks[1]), new(masks[2]), new(masks[3]));
		directBgra = bpp == 32 && masks[0] == 0x00ff0000 && masks[1] == 0x0000ff00 && masks[2] == 0x000000ff && masks[3] is 0xff000000 or 0;
	}

	public override PixelFormat Format => PixelFormat.Bgra32;
	public override int Width => width;
	public override int Height => height;

	public static bool IsValidHeaderSize(uint size) => size is bitmapInfoHeaderSize or bitmapV2HeaderSize or bitmapV3HeaderSize or bitmapV4HeaderSize or bitmapV5HeaderSize;

	// A frame whose data is missing from a truncated stream.  It decodes as transparent black.
	public static BmpPixelSource CreateEmpty(int width, int height) => new(width, height);

	public static BmpPixelSource Create(Stream stream, long offset, int dirWidth, int dirHeight)
	{
		var buff = (Span<byte>)stackalloc byte[bitmapV5HeaderSize + sizeof(uint) * 4];

		stream.Position = offset;
		if (stream.TryFillBuffer(buff[..sizeof(uint)]) != sizeof(uint))
			return CreateEmpty(dirWidth, dirHeight);

		// The caller has already checked the size with IsValidHeaderSize to tell a bitmap from other frame data.
		int cbHeader = (int)BinaryPrimitives.ReadUInt32LittleEndian(buff);
		if (stream.TryFillBuffer(buff[sizeof(uint)..cbHeader]) != cbHeader - sizeof(uint))
			return CreateEmpty(dirWidth, dirHeight);

		var hdr = parseHeader(buff[..cbHeader]);

		// Channel masks.  BI_BITFIELDS with a 40-byte header stores the RGB masks (and with BI_ALPHABITFIELDS,
		// the alpha mask) after the header.  Later header versions include them.
		var masks = (Span<uint>)stackalloc uint[4];
		if (hdr.BitsPerPixel == 16)
			(masks[0], masks[1], masks[2], masks[3]) = (0x7c00, 0x03e0, 0x001f, 0);
		else if (hdr.BitsPerPixel == 32)
			(masks[0], masks[1], masks[2], masks[3]) = (0x00ff0000, 0x0000ff00, 0x000000ff, 0xff000000);
		else
			masks.Clear();

		if (hdr.Compression is biBitfields or biAlphaBitfields)
		{
			int cbMasks = 0;
			if (cbHeader == bitmapInfoHeaderSize)
			{
				cbMasks = sizeof(uint) * (hdr.Compression == biAlphaBitfields ? 4 : 3);
				if (stream.TryFillBuffer(buff.Slice(cbHeader, cbMasks)) != cbMasks)
					return CreateEmpty(dirWidth, dirHeight);
			}

			var mbuff = buff[bitmapInfoHeaderSize..];
			int cbAvail = Math.Max(cbHeader - bitmapInfoHeaderSize, cbMasks);
			for (int i = 0; i < masks.Length; i++)
				masks[i] = i * sizeof(uint) < cbAvail ? BinaryPrimitives.ReadUInt32LittleEndian(mbuff[(i * sizeof(uint))..]) : 0;
		}

		// A color table is required for indexed formats.  It is optional for the others, which skip it.
		var palette = default(RentedBuffer<byte>);
		long dataOffset = stream.Position + (long)hdr.ColorsUsed * sizeof(uint);
		if (hdr.BitsPerPixel <= 8)
		{
			// Always 256 entries, so any index in the pixel data is in range.  Unused entries are black.
			palette = BufferPool.Rent<byte>(tableSize * sizeof(uint), true);
			long colors = hdr.ColorsUsed == 0 ? 1 << hdr.BitsPerPixel : hdr.ColorsUsed;
			dataOffset = stream.Position + colors * sizeof(uint);

			// A short read leaves the remaining entries black.  The pixel data is missing in that case anyway.
			var pbytes = palette.Span;
			try
			{
				_ = stream.TryFillBuffer(pbytes[..(int)(colors * sizeof(uint))]);
			}
			catch
			{
				palette.Dispose();
				throw;
			}

			// The 4th byte of each entry is reserved.  Files in the wild leave garbage there, so it is not alpha.
			for (int i = 3; i < pbytes.Length; i += sizeof(uint))
				pbytes[i] = byte.MaxValue;
		}

		return new BmpPixelSource(stream, dataOffset, hdr, palette, masks);
	}

	protected override void CopyPixelsInternal(in PixelArea prc, int cbStride, int cbBufferSize, byte* pbBuffer)
	{
		if (disposed)
			ThrowHelper.ThrowObjectDisposed(nameof(BmpPixelSource));

		ensureLoaded();

		var span = data.Span;
		int cbXor = cbXorStride * height;
		int cbLine = prc.Width * sizeof(uint);

		for (int y = 0; y < prc.Height; y++)
		{
			byte* pout = pbBuffer + y * cbStride;

			int row = prc.Y + y;
			if (!topDown)
				row = height - 1 - row;

			if (row >= xorRows)
			{
				new Span<byte>(pout, cbLine).Clear();
				continue;
			}

			expandLine(span.Slice(row * cbXorStride, cbXorStride), prc.X, prc.Width, pout);

			if (!useAlphaChannel && row < maskRows)
				applyMask(span.Slice(cbXor + row * cbMaskStride, cbMaskStride), prc.X, prc.Width, pout);
		}
	}

	protected override void Dispose(bool disposing)
	{
		if (disposing)
		{
			disposed = true;

			data.Dispose();
			data = default;

			tables.Dispose();
			tables = default;
		}

		base.Dispose(disposing);
	}

	public override string ToString() => nameof(BmpPixelSource);

	// Marked loaded only once everything is in place, so a failed read is retried on the next call rather
	// than remembered as an empty frame.
	private void ensureLoaded()
	{
		if (loaded)
			return;

		if (stream is not null)
			load(stream);

		loaded = true;
	}

	private void load(Stream stm)
	{
		// Size the buffer from what the stream holds, not from what the header claims.  A truncated stream is
		// not an error once the header is read: rows with no color data decode as transparent black, and rows
		// with color data but no AND mask are opaque, unless the frame takes its alpha from its alpha channel.
		int cbXor = cbXorStride * height;
		int cbMask = cbMaskStride * height;
		int cbData = (int)Math.Min(cbXor + cbMask, Math.Max(stm.Length - dataOffset, 0));
		if (cbData == 0)
			return;

		var buff = BufferPool.Rent<byte>(cbData);
		var lut = default(RentedBuffer<byte>);
		try
		{
			stm.Position = dataOffset;
			int cb = stm.TryFillBuffer(buff.Span);

			int rows = Math.Min(cb, cbXor) / cbXorStride;
			bool useAlpha = false;
			if (bpp is 16 or 32)
			{
				useAlpha = alpha.Mask != 0 && hasAlpha(buff.Span, rows);
				if (!directBgra)
					lut = buildTables(useAlpha);
			}

			if (!lut.IsEmpty)
				tables = lut;

			(data, xorRows, maskRows) = (buff, rows, Math.Max(cb - cbXor, 0) / cbMaskStride);
			(useAlphaChannel, alphaInUse) = (useAlpha, useAlpha ? alpha : default);
		}
		catch
		{
			lut.Dispose();
			buff.Dispose();
			throw;
		}
	}

	// 32-bit frames normally carry alpha, but some files leave it all zero and rely on the AND mask instead.
	private bool hasAlpha(ReadOnlySpan<byte> xor, int rows)
	{
		uint mask = alpha.Mask;

		for (int row = 0; row < rows; row++)
		{
			var line = xor.Slice(row * cbXorStride, cbXorStride);
			if (bpp == 16)
			{
				for (int x = 0; x < width; x++)
					if ((BinaryPrimitives.ReadUInt16LittleEndian(line[(x * 2)..]) & mask) != 0)
						return true;
			}
			else
			{
				for (int x = 0; x < width; x++)
					if ((BinaryPrimitives.ReadUInt32LittleEndian(line[(x * 4)..]) & mask) != 0)
						return true;
			}
		}

		return false;
	}

	// With no alpha in use, every pixel maps to alpha table entry 0, which is opaque.
	private RentedBuffer<byte> buildTables(bool useAlpha)
	{
		var lut = BufferPool.Rent<byte>(tableSize * 4);
		var span = lut.Span;

		blue.FillTable(span[..tableSize]);
		green.FillTable(span.Slice(tableSize, tableSize));
		red.FillTable(span.Slice(tableSize * 2, tableSize));

		if (useAlpha)
			alpha.FillTable(span[(tableSize * 3)..]);
		else
			span[(tableSize * 3)..].Fill(byte.MaxValue);

		return lut;
	}

	private void expandLine(ReadOnlySpan<byte> line, int x0, int count, byte* pout)
	{
		switch (bpp)
		{
			case 1:
			{
				var pal = MemoryMarshal.Cast<byte, uint>(tables.Span);
				for (int i = 0, x = x0; i < count; i++, x++)
					Unsafe.WriteUnaligned(pout + i * 4, pal[line[x >> 3] >> (7 - (x & 7)) & 1]);
				break;
			}
			case 4:
			{
				var pal = MemoryMarshal.Cast<byte, uint>(tables.Span);
				for (int i = 0, x = x0; i < count; i++, x++)
					Unsafe.WriteUnaligned(pout + i * 4, pal[line[x >> 1] >> ((~x & 1) * 4) & 0xf]);
				break;
			}
			case 8:
			{
				var pal = MemoryMarshal.Cast<byte, uint>(tables.Span);
				for (int i = 0, x = x0; i < count; i++, x++)
					Unsafe.WriteUnaligned(pout + i * 4, pal[line[x]]);
				break;
			}
			case 16:
				expandMasked16(line.Slice(x0 * 2, count * 2), count, pout);
				break;
			case 24:
				fixed (byte* pline = line)
					Swizzlers<byte>.GetConverter(3, 4).ConvertLine(pline + x0 * 3, pout, count * 3);
				break;
			case 32 when directBgra:
				line.Slice(x0 * 4, count * 4).CopyTo(new Span<byte>(pout, count * 4));
				if (!useAlphaChannel)
					for (int i = 0; i < count; i++)
						pout[i * 4 + 3] = byte.MaxValue;
				break;
			case 32:
				expandMasked32(line.Slice(x0 * 4, count * 4), count, pout);
				break;
			default:
				ThrowHelper.ThrowUnreachable();
				break;
		}
	}

	private void expandMasked16(ReadOnlySpan<byte> line, int count, byte* pout)
	{
		var tbl = (ReadOnlySpan<byte>)tables.Span;
		var tb = tbl[..tableSize];
		var tg = tbl.Slice(tableSize, tableSize);
		var tr = tbl.Slice(tableSize * 2, tableSize);
		var ta = tbl[(tableSize * 3)..];
		var (b, g, r, a) = (blue, green, red, alphaInUse);

		for (int i = 0; i < count; i++)
		{
			uint px = BinaryPrimitives.ReadUInt16LittleEndian(line[(i * 2)..]);
			byte* p = pout + i * 4;
			p[0] = tb[b.Index(px)];
			p[1] = tg[g.Index(px)];
			p[2] = tr[r.Index(px)];
			p[3] = ta[a.Index(px)];
		}
	}

	private void expandMasked32(ReadOnlySpan<byte> line, int count, byte* pout)
	{
		var tbl = (ReadOnlySpan<byte>)tables.Span;
		var tb = tbl[..tableSize];
		var tg = tbl.Slice(tableSize, tableSize);
		var tr = tbl.Slice(tableSize * 2, tableSize);
		var ta = tbl[(tableSize * 3)..];
		var (b, g, r, a) = (blue, green, red, alphaInUse);

		for (int i = 0; i < count; i++)
		{
			uint px = BinaryPrimitives.ReadUInt32LittleEndian(line[(i * 4)..]);
			byte* p = pout + i * 4;
			p[0] = tb[b.Index(px)];
			p[1] = tg[g.Index(px)];
			p[2] = tr[r.Index(px)];
			p[3] = ta[a.Index(px)];
		}
	}

	// A set mask bit makes the pixel transparent.  The color is kept: a set mask bit with a non-black color
	// means "invert the screen", which has no meaning outside the icon/cursor renderer.
	private static void applyMask(ReadOnlySpan<byte> mask, int x0, int count, byte* pout)
	{
		for (int i = 0, x = x0; i < count;)
		{
			// A whole zero byte is 8 opaque pixels, the common case.
			if ((x & 7) == 0 && count - i >= 8 && mask[x >> 3] == 0)
			{
				i += 8;
				x += 8;
				continue;
			}

			if ((mask[x >> 3] >> (7 - (x & 7)) & 1) != 0)
				pout[i * 4 + 3] = 0;

			i++;
			x++;
		}
	}

	private static BmpHeader parseHeader(ReadOnlySpan<byte> hdr)
	{
		int width = BinaryPrimitives.ReadInt32LittleEndian(hdr[4..]);
		int height = BinaryPrimitives.ReadInt32LittleEndian(hdr[8..]);
		int bpp = BinaryPrimitives.ReadUInt16LittleEndian(hdr[14..]);
		uint compression = BinaryPrimitives.ReadUInt32LittleEndian(hdr[16..]);
		uint colors = BinaryPrimitives.ReadUInt32LittleEndian(hdr[32..]);

		// The height covers both the color data and the AND mask that follows it.
		bool topDown = height < 0;
		height = (int)(Math.Abs((long)height) / 2);

		if (width is <= 0 or > MaxDimension || height is <= 0 or > MaxDimension)
			throw new InvalidDataException($"ICO frame bitmap has invalid dimensions ({width}x{height}).");

		if (bpp is not (1 or 4 or 8 or 16 or 24 or 32))
			throw new InvalidDataException($"ICO frame bitmap has an unsupported bit depth ({bpp}).");

		if (compression is not biRgb && (compression is not (biBitfields or biAlphaBitfields) || bpp is not (16 or 32)))
			throw new InvalidDataException($"ICO frame bitmap has an unsupported compression type ({compression}).");

		if (bpp <= 8 && colors > 1u << bpp)
			throw new InvalidDataException($"ICO frame bitmap has an invalid color table size ({colors}).");

		return new(width, height, bpp, compression, colors, topDown);
	}

	// Maps a channel of a 16/32-bit pixel to an index into a 256-entry table.  Channels wider than 8 bits
	// are truncated to their top 8 bits by the shift; narrower ones are widened by the table.
	private readonly struct ChannelMask
	{
		public readonly uint Mask;
		private readonly int shift, bits;

		public ChannelMask(uint mask)
		{
			Mask = mask;
			if (mask == 0)
				return;

			while ((mask & 1) == 0)
			{
				mask >>= 1;
				shift++;
			}

			// Use the highest set bit rather than the bit count, so a mask with gaps cannot index past the table.
			int span = 0;
			while (mask != 0)
			{
				mask >>= 1;
				span++;
			}

			if (span > 8)
			{
				shift += span - 8;
				span = 8;
			}

			bits = span;
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public int Index(uint px) => (int)((px & Mask) >> shift);

		// Widen by bit replication (e.g. 5-bit abcde -> abcdeabc), as Windows does.
		public void FillTable(Span<byte> table)
		{
			table.Clear();
			if (bits == 0)
				return;

			for (uint v = 0; v < 1u << bits; v++)
			{
				uint val = v << (8 - bits);
				for (int n = bits; n < 8; n *= 2)
					val |= val >> n;

				table[(int)v] = (byte)val;
			}
		}
	}

	private readonly record struct BmpHeader(int Width, int Height, int BitsPerPixel, uint Compression, uint ColorsUsed, bool TopDown);
}
