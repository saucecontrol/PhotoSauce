// Copyright © Clinton Ingram and Contributors.  Licensed under the MIT License.

using System;
using System.Runtime.InteropServices;
using System.Runtime.CompilerServices;

#if HWINTRINSICS
using System.Runtime.Intrinsics;
using System.Runtime.Intrinsics.X86;
#endif

namespace PhotoSauce.MagicScaler.Transforms;

internal sealed class OrientationTransformInternal : ChainedPixelSource
{
	private readonly Orientation orient;
	private readonly PixelBuffer<BufferType.Caching>? outBuff;
	private readonly int bytesPerPixel;

	private RentedBuffer<byte> lineBuff;

	public override int Width { get; }
	public override int Height { get; }

	public OrientationTransformInternal(PixelSource source, Orientation orientation) : base(source)
	{
		bytesPerPixel = source.Format.BytesPerPixel;
		if (!(bytesPerPixel == 1 || bytesPerPixel == 3 || bytesPerPixel == 4))
			throw new NotSupportedException("Pixel format not supported.");

		Width = source.Width;
		Height = source.Height;

		orient = orientation;
		if (orient.SwapsDimensions())
		{
			int buffLines = bytesPerPixel == 1 ? 8 : 4;
			lineBuff = BufferPool.Rent<byte>(BufferStride * buffLines);
			(Width, Height) = (Height, Width);
		}

		int bufferStride = MathUtil.PowerOfTwoCeiling(Width * bytesPerPixel, IntPtr.Size);
		if (orient.RequiresCache())
			outBuff = new PixelBuffer<BufferType.Caching>(Height, bufferStride);
	}

	protected override unsafe void CopyPixelsInternal(in PixelArea prc, int cbStride, int cbBufferSize, byte* pbBuffer)
	{
		if (orient.RequiresCache())
			copyPixelsBuffered(prc, cbStride, cbBufferSize, pbBuffer);
		else
			copyPixelsDirect(prc, cbStride, cbBufferSize, pbBuffer);
	}

	protected override void Reset() => outBuff?.Reset();

	private unsafe void copyPixelsDirect(in PixelArea prc, int cbStride, int cbBufferSize, byte* pbBuffer)
	{
		Profiler.PauseTiming();
		PrevSource.CopyPixels(prc, cbStride, cbBufferSize, pbBuffer);
		Profiler.ResumeTiming();

		if (orient == Orientation.FlipHorizontal)
		{
			nint cb = prc.Width * bytesPerPixel;

			for (int y = 0; y < prc.Height; y++)
			{
				flipLine(pbBuffer, cb);
				pbBuffer += cbStride;
			}
		}
	}

	private unsafe void copyPixelsBuffered(in PixelArea prc, int cbStride, int cbBufferSize, byte* pbBuffer)
	{
		if (outBuff is null) throw new ObjectDisposedException(nameof(OrientationTransformInternal));

		if (!outBuff.ContainsLine(0))
		{
			fixed (byte* bstart = outBuff.PrepareLoad(0, Height))
			{
				if (orient.SwapsDimensions())
					loadBufferTransposed(bstart);
				else
					loadBufferReversed(bstart);
			}
		}

		for (int y = 0; y < prc.Height; y++)
		{
			int line = prc.Y + y;

			var lspan = outBuff.PrepareRead(line, 1).Slice(prc.X * bytesPerPixel, prc.Width * bytesPerPixel);
			Unsafe.CopyBlockUnaligned(ref *(pbBuffer + y * cbStride), ref MemoryMarshal.GetReference(lspan), (uint)lspan.Length);
		}
	}

	private unsafe void loadBufferReversed(byte* bstart)
	{
		byte* pb = bstart + (Height - 1) * outBuff!.Stride;

		for (int y = 0; y < Height; y++)
		{
			Profiler.PauseTiming();
			PrevSource.CopyPixels(new PixelArea(0, y, PrevSource.Width, 1), outBuff.Stride, outBuff.Stride, pb);
			Profiler.ResumeTiming();

			if (orient == Orientation.Rotate180)
				flipLine(pb, PrevSource.Width * bytesPerPixel);

			pb -= outBuff.Stride;
		}
	}

	private unsafe void loadBufferTransposed(byte* bstart)
	{
		var buffSpan = lineBuff.Span;
		int lineBuffStride = BufferStride;
		int lineBuffHeight = buffSpan.Length / lineBuffStride;

		fixed (byte* lstart = buffSpan)
		{
			byte* bp = bstart, lp = lstart;
			nint colStride = outBuff!.Stride;
			nint rowStride = bytesPerPixel;
			nint bufStride = lineBuffStride;

			if (orient == Orientation.Transverse || orient == Orientation.Rotate270)
			{
				bp += (PrevSource.Width - 1) * colStride;
				colStride = -colStride;
			}

			if (orient == Orientation.Transverse || orient == Orientation.Rotate90)
			{
				bp += (PrevSource.Height - 1) * rowStride;
				lp += (lineBuffHeight - 1) * bufStride;
				rowStride = -rowStride;
				bufStride = -bufStride;
			}

			nint cb = PrevSource.Width * bytesPerPixel;
			nint stripOffs = rowStride < 0 ? (lineBuffHeight - 1) * rowStride : 0;

			int y = 0;
			for (; y <= PrevSource.Height - lineBuffHeight; y += lineBuffHeight)
			{
				Profiler.PauseTiming();
				PrevSource.CopyPixels(new PixelArea(0, y, PrevSource.Width, lineBuffHeight), lineBuffStride, buffSpan.Length, lstart);
				Profiler.ResumeTiming();

				byte* op = bp + y * rowStride + stripOffs;

				switch (bytesPerPixel)
				{
					case 1:
						transposeStrip1(lp, op, bufStride, colStride, cb);
						break;
					case 3:
						transposeStrip3(lp, op, bufStride, colStride, cb);
						break;
					case 4:
						transposeStrip4(lp, op, bufStride, colStride, cb);
						break;
				}
			}

			for (; y < PrevSource.Height; y++)
			{
				Profiler.PauseTiming();
				PrevSource.CopyPixels(new PixelArea(0, y, PrevSource.Width, 1), lineBuffStride, lineBuffStride, lp);
				Profiler.ResumeTiming();

				byte* ip = lp, ipe = lp + cb;
				byte* op = bp + y * rowStride;

				switch (bytesPerPixel)
				{
					case 1:
						while (ip < ipe)
						{
							*op = *ip;

							ip++;
							op += colStride;
						}
						break;
					case 3:
						while (ip < ipe)
						{
							*(ushort*)op = *(ushort*)ip;
							op[2] = ip[2];

							ip += 3;
							op += colStride;
						}
						break;
					case 4:
						while (ip < ipe)
						{
							*(uint*)op = *(uint*)ip;

							ip += 4;
							op += colStride;
						}
						break;
				}
			}
		}
	}

	private static unsafe void transposeStrip1(byte* ipb, byte* opb, nint rowStride, nint colStride, nint cb)
	{
		byte* ip = ipb, ipe = ip + cb;
		byte* op = opb;

#if HWINTRINSICS
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		static void store1x8Pair(byte* op, nint colStride, Vector128<ulong> vec)
		{
			Sse2.StoreScalar((ulong*)(op), vec);
			Sse2.StoreScalar((ulong*)(op + colStride), Sse2.UnpackHigh(vec, vec));
		}

		if (Sse2.IsSupported && cb >= Vector128<byte>.Count)
		{
			ipe -= Vector128<byte>.Count;
			do
			{
				byte* ipt = ip;
				var vi0 = Sse2.LoadVector128(ipt);
				var vi1 = Sse2.LoadVector128(ipt + rowStride);
				ipt += rowStride * 2;
				var vi2 = Sse2.LoadVector128(ipt);
				var vi3 = Sse2.LoadVector128(ipt + rowStride);
				ipt += rowStride * 2;

				var vi0l = Sse2.UnpackLow (vi0, vi1).AsUInt16();
				var vi0h = Sse2.UnpackHigh(vi0, vi1).AsUInt16();
				var vi1l = Sse2.UnpackLow (vi2, vi3).AsUInt16();
				var vi1h = Sse2.UnpackHigh(vi2, vi3).AsUInt16();

				var vi0ll = Sse2.UnpackLow (vi0l, vi1l).AsUInt32();
				var vi0lh = Sse2.UnpackHigh(vi0l, vi1l).AsUInt32();
				var vi0hl = Sse2.UnpackLow (vi0h, vi1h).AsUInt32();
				var vi0hh = Sse2.UnpackHigh(vi0h, vi1h).AsUInt32();

				vi0 = Sse2.LoadVector128(ipt);
				vi1 = Sse2.LoadVector128(ipt + rowStride);
				ipt += rowStride * 2;
				vi2 = Sse2.LoadVector128(ipt);
				vi3 = Sse2.LoadVector128(ipt + rowStride);
				ip += Vector128<byte>.Count;

				vi0l = Sse2.UnpackLow (vi0, vi1).AsUInt16();
				vi0h = Sse2.UnpackHigh(vi0, vi1).AsUInt16();
				vi1l = Sse2.UnpackLow (vi2, vi3).AsUInt16();
				vi1h = Sse2.UnpackHigh(vi2, vi3).AsUInt16();

				var vi1ll = Sse2.UnpackLow (vi0l, vi1l).AsUInt32();
				var vi1lh = Sse2.UnpackHigh(vi0l, vi1l).AsUInt32();
				var vi1hl = Sse2.UnpackLow (vi0h, vi1h).AsUInt32();
				var vi1hh = Sse2.UnpackHigh(vi0h, vi1h).AsUInt32();

				store1x8Pair(op, colStride, Sse2.UnpackLow (vi0ll, vi1ll).AsUInt64());
				op += colStride * 2;
				store1x8Pair(op, colStride, Sse2.UnpackHigh(vi0ll, vi1ll).AsUInt64());
				op += colStride * 2;
				store1x8Pair(op, colStride, Sse2.UnpackLow (vi0lh, vi1lh).AsUInt64());
				op += colStride * 2;
				store1x8Pair(op, colStride, Sse2.UnpackHigh(vi0lh, vi1lh).AsUInt64());
				op += colStride * 2;
				store1x8Pair(op, colStride, Sse2.UnpackLow (vi0hl, vi1hl).AsUInt64());
				op += colStride * 2;
				store1x8Pair(op, colStride, Sse2.UnpackHigh(vi0hl, vi1hl).AsUInt64());
				op += colStride * 2;
				store1x8Pair(op, colStride, Sse2.UnpackLow (vi0hh, vi1hh).AsUInt64());
				op += colStride * 2;
				store1x8Pair(op, colStride, Sse2.UnpackHigh(vi0hh, vi1hh).AsUInt64());
				op += colStride * 2;
			}
			while (ip <= ipe);
			ipe += Vector128<byte>.Count;
		}
#endif
		while (ip < ipe)
		{
			byte* ipt = ip++;
			op[0] = *(ipt);
			op[1] = *(ipt + rowStride);
			ipt += rowStride * 2;
			op[2] = *(ipt);
			op[3] = *(ipt + rowStride);
			ipt += rowStride * 2;
			op[4] = *(ipt);
			op[5] = *(ipt + rowStride);
			ipt += rowStride * 2;
			op[6] = *(ipt);
			op[7] = *(ipt + rowStride);

			op += colStride;
		}
	}

	private static unsafe void transposeStrip3(byte* ipb, byte* opb, nint bufStride, nint colStride, nint cb)
	{
		byte* ip = ipb, ipe = ip + cb;
		byte* op = opb;

#if HWINTRINSICS
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		static void store3x4(byte* op, in Vector128<byte> vi)
		{
			if (Avx2.IsSupported)
				Sse2.Store(op, Avx2.Blend(vi.AsUInt32(), Sse2.LoadVector128(op).AsUInt32(), 0b_0000_1000).AsByte());
			else
				Sse2.Store(op, Sse41.Blend(vi.AsUInt16(), Sse2.LoadVector128(op).AsUInt16(), 0b_1100_0000).AsByte());
		}

		if (Sse41.IsSupported && cb >= Vector128<byte>.Count)
		{
			var shuf3to3x = Sse2.LoadVector128(HWIntrinsics.ShuffleMask3To3xChan.GetAddressOf());
			var shuf3xto3 = Sse2.LoadVector128(HWIntrinsics.ShuffleMask3xTo3Chan.GetAddressOf());

			ipe -= Vector128<byte>.Count;
			do
			{
				var vi0 = Sse2.LoadVector128(ip);
				var vi1 = Sse2.LoadVector128(ip + bufStride);
				var vi2 = Sse2.LoadVector128(ip + bufStride * 2);
				var vi3 = Sse2.LoadVector128(ip + bufStride * 3);
				ip += Vector128<byte>.Count * 3 / 4;

				var vs0 = Ssse3.Shuffle(vi0, shuf3to3x).AsUInt32();
				var vs1 = Ssse3.Shuffle(vi1, shuf3to3x).AsUInt32();
				var vs2 = Ssse3.Shuffle(vi2, shuf3to3x).AsUInt32();
				var vs3 = Ssse3.Shuffle(vi3, shuf3to3x).AsUInt32();

				var vl0 = Sse2.UnpackLow (vs0, vs1).AsUInt64();
				var vh0 = Sse2.UnpackHigh(vs0, vs1).AsUInt64();
				var vl1 = Sse2.UnpackLow (vs2, vs3).AsUInt64();
				var vh1 = Sse2.UnpackHigh(vs2, vs3).AsUInt64();

				vi0 = Sse2.UnpackLow (vl0, vl1).AsByte();
				vi1 = Sse2.UnpackHigh(vl0, vl1).AsByte();
				vi2 = Sse2.UnpackLow (vh0, vh1).AsByte();
				vi3 = Sse2.UnpackHigh(vh0, vh1).AsByte();

				vi0 = Ssse3.Shuffle(vi0, shuf3xto3);
				vi1 = Ssse3.Shuffle(vi1, shuf3xto3);
				vi2 = Ssse3.Shuffle(vi2, shuf3xto3);
				vi3 = Ssse3.Shuffle(vi3, shuf3xto3);

				store3x4(op, vi0);
				op += colStride;
				store3x4(op, vi1);
				op += colStride;
				store3x4(op, vi2);
				op += colStride;
				store3x4(op, vi3);
				op += colStride;
			}
			while (ip <= ipe);
			ipe += Vector128<byte>.Count;
		}
#endif
		while (ip < ipe)
		{
			*(ushort*)(op + 0) = *(ushort*)(ip);
			*(op + 2) = *(ip + 2);
			*(ushort*)(op + 3) = *(ushort*)(ip + bufStride);
			*(op + 5) = *(ip + bufStride + 2);
			*(ushort*)(op + 6) = *(ushort*)(ip + bufStride * 2);
			*(op + 8) = *(ip + bufStride * 2 + 2);
			*(ushort*)(op + 9) = *(ushort*)(ip + bufStride * 3);
			*(op + 11) = *(ip + bufStride * 3 + 2);

			ip += 3;
			op += colStride;
		}
	}

	private static unsafe void transposeStrip4(byte* ipb, byte* opb, nint bufStride, nint colStride, nint cb)
	{
		byte* ip = ipb, ipe = ip + cb;
		byte* op = opb;

#if HWINTRINSICS
		if (Sse2.IsSupported && cb >= Vector128<byte>.Count)
		{
			ipe -= Vector128<byte>.Count;
			do
			{
				var vi0 = Sse2.LoadVector128((uint*)(ip));
				var vi1 = Sse2.LoadVector128((uint*)(ip + bufStride));
				var vi2 = Sse2.LoadVector128((uint*)(ip + bufStride * 2));
				var vi3 = Sse2.LoadVector128((uint*)(ip + bufStride * 3));
				ip += Vector128<byte>.Count;

				var vl0 = Sse2.UnpackLow (vi0, vi1).AsUInt64();
				var vh0 = Sse2.UnpackHigh(vi0, vi1).AsUInt64();
				var vl1 = Sse2.UnpackLow (vi2, vi3).AsUInt64();
				var vh1 = Sse2.UnpackHigh(vi2, vi3).AsUInt64();

				vi0 = Sse2.UnpackLow (vl0, vl1).AsUInt32();
				vi1 = Sse2.UnpackHigh(vl0, vl1).AsUInt32();
				vi2 = Sse2.UnpackLow (vh0, vh1).AsUInt32();
				vi3 = Sse2.UnpackHigh(vh0, vh1).AsUInt32();

				Sse2.Store((uint*)(op), vi0);
				Sse2.Store((uint*)(op + colStride), vi1);
				op += colStride * 2;
				Sse2.Store((uint*)(op), vi2);
				Sse2.Store((uint*)(op + colStride), vi3);
				op += colStride * 2;
			}
			while (ip <= ipe);
			ipe += Vector128<byte>.Count;
		}
#endif
		while (ip < ipe)
		{
			*((uint*)op) = *(uint*)ip;
			*((uint*)op + 1) = *(uint*)(ip + bufStride);
			*((uint*)op + 2) = *(uint*)(ip + bufStride * 2);
			*((uint*)op + 3) = *(uint*)(ip + bufStride * 3);

			ip += sizeof(uint);
			op += colStride;
		}
	}

	private unsafe void flipLine(byte* bp, nint cb)
	{
		byte* pp = bp, pe = pp + cb;

		switch (bytesPerPixel)
		{
			case 1:
#if HWINTRINSICS
				if (Ssse3.IsSupported && cb >= Vector128<byte>.Count)
				{
					var mask = (ReadOnlySpan<byte>)(new byte[] { 15, 14, 13, 12, 11, 10, 9, 8, 7, 6, 5, 4, 3, 2, 1, 0 });
					var vshuf = Sse2.LoadVector128(mask.GetAddressOf());

					pe -= Vector128<byte>.Count;
					do
					{
						var vs = Sse2.LoadVector128(pp);
						var ve = Sse2.LoadVector128(pe);

						Sse2.Store(pe, Ssse3.Shuffle(vs, vshuf));
						Sse2.Store(pp, Ssse3.Shuffle(ve, vshuf));

						pe -= Vector128<byte>.Count;
						pp += Vector128<byte>.Count;
					}
					while (pp <= pe);
					pe += Vector128<byte>.Count;
				}
#endif
				pe -= 1;
				while (pp < pe)
				{
					byte t0 = *pe;
					*pe-- = *pp;
					*pp++ = t0;
				}
				break;
			case 3:
#if HWINTRINSICS
				if (Ssse3.IsSupported && cb > Vector128<byte>.Count * 2)
				{
					var mask = (ReadOnlySpan<byte>)(new byte[] { 0, 13, 14, 15, 10, 11, 12, 7, 8, 9, 4, 5, 6, 1, 2, 3 });
					var vshufs = Sse2.LoadVector128(mask.GetAddressOf());
					var vshufe = Sse2.ShiftRightLogical128BitLane(vshufs, 1);

					pe -= Vector128<byte>.Count;
					do
					{
						var vs = Sse2.LoadVector128(pp);
						var ve = Ssse3.Shuffle(Sse2.LoadVector128(pe), vshufe);

						var vsa = Ssse3.AlignRight(vs, ve, 15);
						var vea = Ssse3.AlignRight(ve, vs, 15);

						Sse2.Store(pe, Ssse3.Shuffle(vsa, vshufs));
						Sse2.Store(pp, Ssse3.AlignRight(vea, vea, 1));

						pe -= 15;
						pp += 15;
					}
					while ((pe - pp) > Vector128<byte>.Count);
					pe += Vector128<byte>.Count;
				}
#endif
				pe -= 3;
				while (pp < pe)
				{
					ushort t0 = *(ushort*)pe;
					*(ushort*)pe = *(ushort*)pp;
					*(ushort*)pp = t0;

					byte t1 = pe[2];
					pe[2] = pp[2];
					pp[2] = t1;

					pe -= 3;
					pp += 3;
				}
				break;
			case 4:
#if HWINTRINSICS
				if (Sse2.IsSupported && cb >= Vector128<byte>.Count)
				{
					pe -= Vector128<byte>.Count;
					do
					{
						var vs = Sse2.LoadVector128(pp);
						var ve = Sse2.LoadVector128(pe);

						Sse2.Store(pe, Sse2.Shuffle(vs.AsUInt32(), 0b_00_01_10_11).AsByte());
						Sse2.Store(pp, Sse2.Shuffle(ve.AsUInt32(), 0b_00_01_10_11).AsByte());

						pe -= Vector128<byte>.Count;
						pp += Vector128<byte>.Count;
					}
					while (pp <= pe);
					pe += Vector128<byte>.Count;
				}
#endif
				pe -= 4;
				while (pp < pe)
				{
					uint t0 = *(uint*)pe;
					*(uint*)pe = *(uint*)pp;
					*(uint*)pp = t0;

					pe -= sizeof(uint);
					pp += sizeof(uint);
				}
				break;
		}
	}

	protected override void Dispose(bool disposing)
	{
		if (disposing)
		{
			outBuff?.Dispose();

			lineBuff.Dispose();
			lineBuff = default;
		}

		base.Dispose(disposing);
	}

	public override string ToString() => nameof(OrientationTransform);
}

/// <summary>Transforms an image by changing its column/row order according to an <see cref="Orientation" /> value.</summary>
public sealed class OrientationTransform : PixelTransformInternalBase
{
	private readonly Orientation orientation;

	/// <summary>Creates a new transform with the specified <paramref name="orientation" /> value.</summary>
	/// <param name="orientation">The <see cref="Orientation" /> correction to apply to the image.</param>
	public OrientationTransform(Orientation orientation) => this.orientation = orientation;

	internal override void Init(PipelineContext ctx)
	{
		MagicTransforms.AddFlipRotator(ctx, orientation);

		Source = ctx.Source;
	}
}
