// Copyright © Clinton Ingram and Contributors
// SPDX-License-Identifier: MIT

using System;
using System.IO;
using System.Linq;
using System.Buffers.Binary;

namespace PhotoSauce.ManagedCodecs.Ico.Test;

// Writes a DIB as stored in an ICO frame: header, optional channel masks and color table, XOR rows, AND mask rows.
public sealed class Dib
{
	public required int Width { get; init; }
	public required int Height { get; init; }
	public required int Bpp { get; init; }

	// Raw pixel values, row-major from the top: a palette index, a packed 16/32-bit value, or 0x00RRGGBB for 24-bit.
	public required uint[] Pixels { get; init; }

	// AND mask bits, row-major from the top.  True means transparent.
	public bool[]? Mask { get; init; }

	public uint[]? Palette { get; init; }
	public uint ColorsUsed { get; init; }
	public uint Compression { get; init; }
	public uint[]? Masks { get; init; }
	public int HeaderSize { get; init; } = 40;
	public bool TopDown { get; init; }

	// Shown as the theory case name.
	public string Name { get; init; } = "";

	public int XorStride => (Width * Bpp + 31) / 32 * 4;
	public int MaskStride => (Width + 31) / 32 * 4;

	public override string ToString() => Name;

	public byte[] HeaderBytes()
	{
		using var ms = new MemoryStream();
		using var bw = new BinaryWriter(ms);

		writeHeader(bw);

		bw.Flush();
		return ms.ToArray();
	}

	public byte[] ToBytes()
	{
		using var ms = new MemoryStream();
		using var bw = new BinaryWriter(ms);

		writeHeader(bw);
		writePixels(bw);

		bw.Flush();
		return ms.ToArray();
	}

	private void writeHeader(BinaryWriter bw)
	{
		bw.Write(HeaderSize);
		bw.Write(Width);
		bw.Write(Height * 2 * (TopDown ? -1 : 1));
		bw.Write((ushort)1);
		bw.Write((ushort)Bpp);
		bw.Write(Compression);
		bw.Write(0u);
		bw.Write(0);
		bw.Write(0);
		bw.Write(ColorsUsed);
		bw.Write(0u);

		// V3+ headers carry the masks in the header itself.  The rest of a V4/V5 header is left zeroed.
		var masks = Masks ?? [ ];
		if (HeaderSize > 40)
		{
			for (int i = 0; i < 4; i++)
				if ((HeaderSize - 40) / 4 > i)
					bw.Write(i < masks.Length ? masks[i] : 0u);

			bw.Write(new byte[Math.Max(0, HeaderSize - 56)]);
		}
		else
		{
			foreach (uint m in masks)
				bw.Write(m);
		}

		foreach (uint c in Palette ?? [ ])
			bw.Write(c);
	}

	private void writePixels(BinaryWriter bw)
	{
		var row = new byte[XorStride];
		for (int s = 0; s < Height; s++)
		{
			int y = TopDown ? s : Height - 1 - s;
			Array.Clear(row, 0, row.Length);
			for (int x = 0; x < Width; x++)
			{
				uint v = Pixels[y * Width + x];
				switch (Bpp)
				{
					case 1:
					case 4:
					case 8:
						int bit = x * Bpp;
						row[bit / 8] |= (byte)(v << (8 - Bpp - bit % 8));
						break;
					case 16:
						BinaryPrimitives.WriteUInt16LittleEndian(row.AsSpan(x * 2), (ushort)v);
						break;
					case 24:
						row[x * 3] = (byte)v;
						row[x * 3 + 1] = (byte)(v >> 8);
						row[x * 3 + 2] = (byte)(v >> 16);
						break;
					case 32:
						BinaryPrimitives.WriteUInt32LittleEndian(row.AsSpan(x * 4), v);
						break;
				}
			}

			bw.Write(row);
		}

		var mrow = new byte[MaskStride];
		for (int s = 0; s < Height; s++)
		{
			int y = TopDown ? s : Height - 1 - s;
			Array.Clear(mrow, 0, mrow.Length);
			for (int x = 0; x < Width; x++)
				if (Mask?[y * Width + x] ?? false)
					mrow[x / 8] |= (byte)(0x80 >> (x % 8));

			bw.Write(mrow);
		}
	}
}

internal static class IcoBuilder
{
	public static byte[] Build(params (byte[] Data, int Width, int Height)[] frames) => Build(1, frames);

	public static byte[] Build(ushort type, params (byte[] Data, int Width, int Height)[] frames)
	{
		using var ms = new MemoryStream();
		using var bw = new BinaryWriter(ms);

		bw.Write((ushort)0);
		bw.Write(type);
		bw.Write((ushort)frames.Length);

		int offset = 6 + 16 * frames.Length;
		foreach (var (data, width, height) in frames)
		{
			bw.Write((byte)(width >= 256 ? 0 : width));
			bw.Write((byte)(height >= 256 ? 0 : height));
			bw.Write((byte)0);
			bw.Write((byte)0);
			bw.Write((ushort)1);
			bw.Write((ushort)32);
			bw.Write((uint)data.Length);
			bw.Write((uint)offset);
			offset += data.Length;
		}

		foreach (var frame in frames)
			bw.Write(frame.Data);

		bw.Flush();
		return ms.ToArray();
	}

	public static byte[] Build(params Dib[] dibs) => Build(dibs.Select(static d => (d.ToBytes(), d.Width, d.Height)).ToArray());
}
