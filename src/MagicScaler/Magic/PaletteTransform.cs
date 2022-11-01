// Copyright © Clinton Ingram and Contributors.  Licensed under the MIT License.

using System;

namespace PhotoSauce.MagicScaler.Transforms;

internal sealed class PaletteTransform : ChainedPixelSource
{
	private RentedBuffer<uint> palette;

	public override PixelFormat Format { get; }

	public override bool Passthrough => false;

	public PaletteTransform(PixelSource source, PixelFormat destFormat) : base(source)
	{
		var srcFormat = source.Format;
		Format = destFormat;

		if (srcFormat != PixelFormat.Indexed8 || (destFormat != PixelFormat.Grey8 && destFormat != PixelFormat.Bgr24 && destFormat != PixelFormat.Bgra32))
			throw new NotSupportedException("Pixel format not supported.");

		setPalette(((IIndexedPixelSource)source).Palette);
	}

	private void setPalette(ReadOnlySpan<uint> pal)
	{
		if (palette.IsEmpty)
			palette = BufferPool.RentAligned<uint>(256);

		var pspan = palette.Span;
		pal.CopyTo(pspan);

		if (pal.Length < pspan.Length)
			pspan[pal.Length..].Fill(pspan[0]);
	}

	public override bool IsCompatible(PixelSource newSource) => PrevSource.Format == newSource.Format && newSource is IIndexedPixelSource;

	public override void ReInit(PixelSource newSource)
	{
		base.ReInit(newSource);

		setPalette(((IIndexedPixelSource)newSource).Palette);
	}

	protected override unsafe void CopyPixelsInternal(in PixelArea prc, int cbStride, int cbBufferSize, byte* pbBuffer)
	{
		int cbi = prc.Width;
		int cbo = MathUtil.DivCeiling(prc.Width * Format.BitsPerPixel, 8);

		fixed (uint* ppal = palette)
		{
			for (int y = 0; y < prc.Height; y++)
			{
				byte* op = pbBuffer + y * cbStride;
				byte* ip = op + cbo - cbi;

				Profiler.PauseTiming();
				PrevSource.CopyPixels(prc.Slice(y, 1), cbStride, cbi, ip);
				Profiler.ResumeTiming();

				switch (Format.ChannelCount)
				{
					case 1:
						copyPixels1Chan(ip, op, ppal, cbi);
						break;
					case 3:
						copyPixels3Chan(ip, op, ppal, cbi);
						break;
					case 4:
						copyPixels4Chan(ip, op, ppal, cbi);
						break;
				}
			}
		}
	}

	private unsafe void copyPixels1Chan(byte* istart, byte* ostart, uint* pstart, nint cb)
	{
		byte* ip = istart, ipe = istart + cb, op = ostart;
		uint* pp = pstart;

		while (ip < ipe)
		{
			*op = (byte)pp[(nuint)(*ip)];
			ip++;
			op++;
		}
	}

	private unsafe void copyPixels3Chan(byte* istart, byte* ostart, uint* pstart, nint cb)
	{
		byte* ip = istart, ipe = istart + cb, op = ostart;
		uint* pp = pstart;

		while (ip < ipe)
		{
			byte* bp = (byte*)(pp + (nuint)(*ip));
			op[0] = bp[0];
			op[1] = bp[1];
			op[2] = bp[2];
			ip++;
			op += 3;
		}
	}

	private unsafe void copyPixels4Chan(byte* istart, byte* ostart, uint* pstart, nint cb)
	{
		byte* ip = istart, ipe = istart + cb;
		uint* op = (uint*)ostart, pp = pstart;

		while (ip < ipe)
		{
			*op = pp[(nuint)(*ip)];
			ip++;
			op++;
		}
	}

	protected override void Dispose(bool disposing)
	{
		if (disposing)
		{
			palette.Dispose();
			palette = default;
		}

		base.Dispose(disposing);
	}

	public override string ToString() => $"{nameof(PaletteTransform)}: {PrevSource.Format.Name}->{Format.Name}";

}
