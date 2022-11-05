// Copyright © Clinton Ingram and Contributors.  Licensed under the MIT License.

using System;

#if HWINTRINSICS
using System.Runtime.Intrinsics;
using System.Runtime.Intrinsics.X86;
#endif

namespace PhotoSauce.MagicScaler.Transforms;

internal sealed class PaletteTransform : ChainedPixelSource
{
	private RentedBuffer<uint> palette;

	public override PixelFormat Format { get; }

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
						CopyPixels1Chan(ip, op, ppal, cbi);
						break;
					case 3:
						CopyPixels3Chan(ip, op, ppal, cbi);
						break;
					case 4:
						CopyPixels4Chan(ip, op, ppal, cbi);
						break;
				}
			}
		}
	}

	public static unsafe void CopyPixels1Chan(byte* istart, byte* ostart, uint* pstart, nint cb)
	{
		byte* ip = istart, ipe = istart + cb, op = ostart;
		uint* pp = pstart;

		ipe -= 4;
		while (ip <= ipe)
		{
			uint i0 = pp[(nuint)ip[0]];
			uint i1 = pp[(nuint)ip[1]];
			uint i2 = pp[(nuint)ip[2]];
			uint i3 = pp[(nuint)ip[3]];
			ip += 4;

			op[0] = (byte)i0;
			op[1] = (byte)i1;
			op[2] = (byte)i2;
			op[3] = (byte)i3;
			op += 4;
		}
		ipe += 4;

		while (ip < ipe)
		{
			op[0] = (byte)pp[(nuint)ip[0]];
			ip++;
			op++;
		}
	}

	public static unsafe void CopyPixels3Chan(byte* istart, byte* ostart, uint* pstart, nint cb)
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

	public static unsafe void CopyPixels4Chan(byte* istart, byte* ostart, uint* pstart, nint cb)
	{
		byte* ip = istart, ipe = istart + cb;
		uint* op = (uint*)ostart, pp = pstart;

#if HWINTRINSICS
		if (Avx2.IsSupported && HWIntrinsics.HasFastGather && cb >= Vector256<byte>.Count)
		{
			ipe -= Vector256<byte>.Count;
			var vlast = Avx.LoadVector256(ipe);

			LoopTop:
			do
			{
				var vi0 = Avx2.ConvertToVector256Int32(ip);
				var vi1 = Avx2.ConvertToVector256Int32(ip + Vector256<int>.Count);
				var vi2 = Avx2.ConvertToVector256Int32(ip + Vector256<int>.Count * 2);
				var vi3 = Avx2.ConvertToVector256Int32(ip + Vector256<int>.Count * 3);
				ip += Vector256<byte>.Count;

				var vd0 = Avx2.GatherVector256(pp, vi0, sizeof(int));
				var vd1 = Avx2.GatherVector256(pp, vi1, sizeof(int));
				var vd2 = Avx2.GatherVector256(pp, vi2, sizeof(int));
				var vd3 = Avx2.GatherVector256(pp, vi3, sizeof(int));

				Avx.Store(op, vd0);
				Avx.Store(op + Vector256<int>.Count, vd1);
				Avx.Store(op + Vector256<int>.Count * 2, vd2);
				Avx.Store(op + Vector256<int>.Count * 3, vd3);
				op += Vector256<int>.Count * 4;
			}
			while (ip <= ipe);

			if (ip < ipe + Vector256<byte>.Count)
			{
				nuint offs = UnsafeUtil.ByteOffset(ipe, ip);
				ip = UnsafeUtil.SubtractOffset(ip, offs);
				op = UnsafeUtil.SubtractOffset(op, UnsafeUtil.ConvertOffset<byte, int>(offs));
				Avx.Store(ip, vlast);
				goto LoopTop;
			}

			return;
		}
#endif

		ipe -= 4;
		while (ip <= ipe)
		{
			uint i0 = pp[(nuint)ip[0]];
			uint i1 = pp[(nuint)ip[1]];
			uint i2 = pp[(nuint)ip[2]];
			uint i3 = pp[(nuint)ip[3]];
			ip += 4;

			op[0] = i0;
			op[1] = i1;
			op[2] = i2;
			op[3] = i3;
			op += 4;
		}
		ipe += 4;

		while (ip < ipe)
		{
			op[0] = pp[(nuint)ip[0]];
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
