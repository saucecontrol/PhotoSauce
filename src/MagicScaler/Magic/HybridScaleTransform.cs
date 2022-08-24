// Copyright © Clinton Ingram and Contributors.  Licensed under the MIT License.

using System;
using System.Runtime.CompilerServices;

#if HWINTRINSICS
using System.Runtime.Intrinsics;
using System.Runtime.Intrinsics.X86;
#endif

using static PhotoSauce.MagicScaler.MathUtil;

namespace PhotoSauce.MagicScaler.Transforms;

internal sealed class HybridScaleTransform : ChainedPixelSource
{
#pragma warning disable IDE0044
	// read from static prevents JIT from inlining the const value with the FastAvg helpers, which causes redundant 64-bit immediate loads
	private static ulong maskb = ~0x0101010101010101ul;
#pragma warning restore IDE0044

	private readonly int scale;

	private RentedBuffer<byte> lineBuff;

	public override int Width { get; }
	public override int Height { get; }

	public HybridScaleTransform(PixelSource source, int hybridScale) : base(source)
	{
		if (hybridScale <= 1 || (hybridScale & (hybridScale - 1)) != 0) throw new ArgumentException("Must be power of two", nameof(hybridScale));
		scale = hybridScale;

		int bufferStride = PowerOfTwoCeiling(PowerOfTwoCeiling(source.Width, scale) * source.Format.BytesPerPixel, IntPtr.Size);
		lineBuff = BufferPool.RentAligned<byte>(bufferStride * scale);

		Width = DivCeiling(PrevSource.Width, scale);
		Height = DivCeiling(PrevSource.Height, scale);
	}

	protected override unsafe void CopyPixelsInternal(in PixelArea prc, int cbStride, int cbBufferSize, byte* pbBuffer)
	{
		var buffspan = lineBuff.Span;
		if (buffspan.IsEmpty) ThrowHelper.ThrowObjectDisposed(nameof(HybridScaleTransform));

		fixed (byte* bstart = buffspan)
		{
			int iw = prc.Width * scale;
			int iiw = Math.Min(iw, PrevSource.Width - prc.X * scale);
			int bpp = PrevSource.Format.BytesPerPixel;
			int stride = iw * bpp;

			for (int y = 0; y < prc.Height; y++)
			{
				int iy = (prc.Y + y) * scale;
				int ih = scale;
				int iih = Math.Min(ih, PrevSource.Height - iy);

				Profiler.PauseTiming();
				PrevSource.CopyPixels(new PixelArea(prc.X * scale, iy, iiw, iih), stride, buffspan.Length, bstart);
				Profiler.ResumeTiming();

				for (int i = 0; iiw < iw && i < iih; i++)
				{
					byte* bp = bstart + i * stride + iiw * bpp;
					switch (bpp)
					{
						case 1:
							new Span<byte>(bp, iw - iiw).Fill(bp[-1]);
							break;
						case 3:
							new Span<Triple>(bp, iw - iiw).Fill(((Triple*)bp)[-1]);
							break;
						case 4:
							new Span<uint>(bp, iw - iiw).Fill(((uint*)bp)[-1]);
							break;
					}
				}

				for (; iih < ih; iih++)
					Unsafe.CopyBlock(bstart + iih * stride, bstart + (iih - 1) * stride, (uint)stride);

				int ratio = scale;
				uint cb = (uint)stride;
				while (ratio > 1)
				{
					byte* ip = bstart, op = bstart;
					if (ratio == 2)
						op = pbBuffer + y * cbStride;

					for (int i = 0; i < ratio; i += 2)
					{
						switch (bpp)
						{
							case 3:
								process3(ip, op, cb);
								break;
							case 4:
								if (Format.AlphaRepresentation == PixelAlphaRepresentation.Unassociated)
									process4A(ip, op, cb);
								else
									process4(ip, op, cb);
								break;
							default:
								process(ip, op, cb);
								break;
						}

						ip += cb * 2;
						op += cb / 2;
					}

					ratio /= 2;
					cb /= 2;
				}
			}
		}
	}

#if HWINTRINSICS
	[MethodImpl(MethodImplOptions.AggressiveOptimization)]
#endif
	private static unsafe void process4A(byte* istart, byte* ostart, nuint stride)
	{
		const ushort scalec = ((UQ15One << 8) + byte.MaxValue / 2) / byte.MaxValue;
		const uint scalea = (byte.MaxValue << 16) + byte.MaxValue << 2;

		byte* ip = istart, ipe = istart + stride;
		byte* op = ostart;

#if HWINTRINSICS
		if (Avx2.IsSupported && stride >= (nuint)Vector256<byte>.Count * 2)
		{
			var vshufa = Avx2.BroadcastVector128ToVector256(HWIntrinsics.ShuffleMaskWidenAlpha.GetAddressOf());
			var vmaske = Avx2.BroadcastVector128ToVector256(HWIntrinsics.ShuffleMaskWidenEven.GetAddressOf());
			var vmasko = Avx2.BroadcastVector128ToVector256(HWIntrinsics.ShuffleMaskWidenOdd.GetAddressOf());
			var vmaskd = Avx2.BroadcastVector128ToVector256(HWIntrinsics.ShuffleMaskDeinterleave1x16.GetAddressOf());
			var vscala = Vector256.Create((float)scalea);
			var vscalc = Vector256.Create(scalec);

			ipe -= Vector256<byte>.Count * 2;
			do
			{
				byte* ipn = ip + stride;
				var vi0 = Avx.LoadVector256(ip);
				var vi1 = Avx.LoadVector256(ip + Vector256<byte>.Count);
				var vi2 = Avx.LoadVector256(ipn);
				var vi3 = Avx.LoadVector256(ipn + Vector256<byte>.Count);
				ip += Vector256<byte>.Count * 2;

				var va0 = Avx2.Shuffle(vi0, vshufa).AsUInt16();
				var ve0 = Avx2.MultiplyLow(Avx2.Shuffle(vi0, vmaske).AsUInt16(), va0);
				var vo0 = Avx2.MultiplyLow(Avx2.Shuffle(vi0, vmasko).AsUInt16(), va0);

				var va2 = Avx2.Shuffle(vi2, vshufa).AsUInt16();
				var ve2 = Avx2.MultiplyLow(Avx2.Shuffle(vi2, vmaske).AsUInt16(), va2);
				var vo2 = Avx2.MultiplyLow(Avx2.Shuffle(vi2, vmasko).AsUInt16(), va2);

				va0 = Avx2.Add(va0, va2);
				ve0 = Avx2.Average(ve0, ve2);
				vo0 = Avx2.Average(vo0, vo2);

				va0 = Avx2.Add(va0, Avx2.ShiftRightLogical128BitLane(va0, 2));
				ve0 = Avx2.Average(ve0, Avx2.ShiftRightLogical128BitLane(ve0, 2));
				vo0 = Avx2.Average(vo0, Avx2.ShiftRightLogical128BitLane(vo0, 2));

				var va1 = Avx2.Shuffle(vi1, vshufa).AsUInt16();
				var ve1 = Avx2.MultiplyLow(Avx2.Shuffle(vi1, vmaske).AsUInt16(), va1);
				var vo1 = Avx2.MultiplyLow(Avx2.Shuffle(vi1, vmasko).AsUInt16(), va1);

				var va3 = Avx2.Shuffle(vi3, vshufa).AsUInt16();
				var ve3 = Avx2.MultiplyLow(Avx2.Shuffle(vi3, vmaske).AsUInt16(), va3);
				var vo3 = Avx2.MultiplyLow(Avx2.Shuffle(vi3, vmasko).AsUInt16(), va3);

				va1 = Avx2.Add(va1, va3);
				ve1 = Avx2.Average(ve1, ve3);
				vo1 = Avx2.Average(vo1, vo3);

				va1 = Avx2.Add(va1, Avx2.ShiftRightLogical128BitLane(va1, 2));
				ve1 = Avx2.Average(ve1, Avx2.ShiftRightLogical128BitLane(ve1, 2));
				vo1 = Avx2.Average(vo1, Avx2.ShiftRightLogical128BitLane(vo1, 2));

				var va = Avx2.UnpackLow(va0.AsUInt64(), va1.AsUInt64()).AsUInt16();
				var v0 = Avx2.UnpackLow(ve0.AsUInt64(), ve1.AsUInt64()).AsUInt16();
				var v1 = Avx2.UnpackLow(vo0.AsUInt64(), vo1.AsUInt64()).AsUInt16();
				var v2 = Avx2.UnpackHigh(ve0.AsUInt64(), ve1.AsUInt64()).AsUInt16();

				v0 = Avx2.MultiplyHigh(v0, vscalc);
				v1 = Avx2.MultiplyHigh(v1, vscalc);
				v2 = Avx2.MultiplyHigh(v2, vscalc);

				var vmaskl = Avx2.ShiftRightLogical(Avx2.CompareEqual(va, va).AsUInt32(), 16).AsUInt16();
				var vai = Avx2.And(va, vmaskl).AsInt32();
				var v0i = Avx2.And(v0, vmaskl).AsInt32();
				var v1i = Avx2.And(v1, vmaskl).AsInt32();
				var v2i = Avx2.And(v2, vmaskl).AsInt32();

				var vrnd = Avx2.ShiftRightLogical(vscalc.AsInt32(), 30);
				var vma = Avx.ConvertToVector256Int32WithTruncation(Avx.Divide(vscala, Avx.ConvertToVector256Single(vai)));
				vma = Avx2.AndNot(Avx2.CompareGreaterThan(vrnd, vai), vma);

				vai = Avx2.ShiftRightLogical(Avx2.Add(vai, vrnd), 2);
				v0i = Avx2.MultiplyLow(v0i, vma);
				v1i = Avx2.MultiplyLow(v1i, vma);
				v2i = Avx2.MultiplyLow(v2i, vma);

				vrnd = Avx2.ShiftRightLogical(vscala.AsInt32(), 9);
				v0i = Avx2.Add(v0i, vrnd);
				v1i = Avx2.Add(v1i, vrnd);
				v2i = Avx2.Add(v2i, vrnd);

				v0i = Avx2.ShiftRightLogical(v0i, 23);
				v1i = Avx2.ShiftRightLogical(v1i, 23);
				v2i = Avx2.ShiftRightLogical(v2i, 23);

				var vil = Avx2.PackSignedSaturate(v0i, v1i);
				var vih = Avx2.PackSignedSaturate(v2i, vai);
				vi0 = Avx2.PackUnsignedSaturate(vil, vih);
				vi0 = Avx2.Shuffle(vi0, vmaskd);
				vi0 = Avx2.Permute4x64(vi0.AsUInt64(), HWIntrinsics.PermuteMaskDeinterleave4x64).AsByte();

				Avx.Store(op, vi0);
				op += Vector256<byte>.Count;
			}
			while (ip <= ipe);
			ipe += Vector256<byte>.Count * 2;
		}
		else if (Sse41.IsSupported && stride >= (nuint)Vector128<byte>.Count * 2)
		{
			var vshufa = Sse2.LoadVector128(HWIntrinsics.ShuffleMaskWidenAlpha.GetAddressOf());
			var vmaske = Sse2.LoadVector128(HWIntrinsics.ShuffleMaskWidenEven.GetAddressOf());
			var vmasko = Sse2.LoadVector128(HWIntrinsics.ShuffleMaskWidenOdd.GetAddressOf());
			var vmaskd = Sse2.LoadVector128(HWIntrinsics.ShuffleMaskDeinterleave1x16.GetAddressOf());
			var vscala = Vector128.Create((float)scalea);
			var vscalc = Vector128.Create(scalec);

			ipe -= Vector128<byte>.Count * 2;
			do
			{
				byte* ipn = ip + stride;
				var vi0 = Sse2.LoadVector128(ip);
				var vi1 = Sse2.LoadVector128(ip + Vector128<byte>.Count);
				var vi2 = Sse2.LoadVector128(ipn);
				var vi3 = Sse2.LoadVector128(ipn + Vector128<byte>.Count);
				ip += Vector128<byte>.Count * 2;

				var va0 = Ssse3.Shuffle(vi0, vshufa).AsUInt16();
				var ve0 = Sse2.MultiplyLow(Ssse3.Shuffle(vi0, vmaske).AsUInt16(), va0);
				var vo0 = Sse2.MultiplyLow(Ssse3.Shuffle(vi0, vmasko).AsUInt16(), va0);

				var va2 = Ssse3.Shuffle(vi2, vshufa).AsUInt16();
				var ve2 = Sse2.MultiplyLow(Ssse3.Shuffle(vi2, vmaske).AsUInt16(), va2);
				var vo2 = Sse2.MultiplyLow(Ssse3.Shuffle(vi2, vmasko).AsUInt16(), va2);

				va0 = Sse2.Add(va0, va2);
				ve0 = Sse2.Average(ve0, ve2);
				vo0 = Sse2.Average(vo0, vo2);

				va0 = Sse2.Add(va0, Sse2.ShiftRightLogical128BitLane(va0, 2));
				ve0 = Sse2.Average(ve0, Sse2.ShiftRightLogical128BitLane(ve0, 2));
				vo0 = Sse2.Average(vo0, Sse2.ShiftRightLogical128BitLane(vo0, 2));

				var va1 = Ssse3.Shuffle(vi1, vshufa).AsUInt16();
				var ve1 = Sse2.MultiplyLow(Ssse3.Shuffle(vi1, vmaske).AsUInt16(), va1);
				var vo1 = Sse2.MultiplyLow(Ssse3.Shuffle(vi1, vmasko).AsUInt16(), va1);

				var va3 = Ssse3.Shuffle(vi3, vshufa).AsUInt16();
				var ve3 = Sse2.MultiplyLow(Ssse3.Shuffle(vi3, vmaske).AsUInt16(), va3);
				var vo3 = Sse2.MultiplyLow(Ssse3.Shuffle(vi3, vmasko).AsUInt16(), va3);

				va1 = Sse2.Add(va1, va3);
				ve1 = Sse2.Average(ve1, ve3);
				vo1 = Sse2.Average(vo1, vo3);

				va1 = Sse2.Add(va1, Sse2.ShiftRightLogical128BitLane(va1, 2));
				ve1 = Sse2.Average(ve1, Sse2.ShiftRightLogical128BitLane(ve1, 2));
				vo1 = Sse2.Average(vo1, Sse2.ShiftRightLogical128BitLane(vo1, 2));

				var va = Sse2.UnpackLow(va0.AsUInt64(), va1.AsUInt64()).AsUInt16();
				var v0 = Sse2.UnpackLow(ve0.AsUInt64(), ve1.AsUInt64()).AsUInt16();
				var v1 = Sse2.UnpackLow(vo0.AsUInt64(), vo1.AsUInt64()).AsUInt16();
				var v2 = Sse2.UnpackHigh(ve0.AsUInt64(), ve1.AsUInt64()).AsUInt16();

				v0 = Sse2.MultiplyHigh(v0, vscalc);
				v1 = Sse2.MultiplyHigh(v1, vscalc);
				v2 = Sse2.MultiplyHigh(v2, vscalc);

				var vmaskl = Sse2.ShiftRightLogical(Sse2.CompareEqual(va, va).AsUInt32(), 16).AsUInt16();
				var vai = Sse2.And(va, vmaskl).AsInt32();
				var v0i = Sse2.And(v0, vmaskl).AsInt32();
				var v1i = Sse2.And(v1, vmaskl).AsInt32();
				var v2i = Sse2.And(v2, vmaskl).AsInt32();

				var vrnd = Sse2.ShiftRightLogical(vscalc.AsInt32(), 30);
				var vma = Sse2.ConvertToVector128Int32WithTruncation(Sse.Divide(vscala, Sse2.ConvertToVector128Single(vai)));
				vma = Sse2.AndNot(Sse2.CompareGreaterThan(vrnd, vai), vma);

				vai = Sse2.ShiftRightLogical(Sse2.Add(vai, vrnd), 2);
				v0i = Sse41.MultiplyLow(v0i, vma);
				v1i = Sse41.MultiplyLow(v1i, vma);
				v2i = Sse41.MultiplyLow(v2i, vma);

				vrnd = Sse2.ShiftRightLogical(vscala.AsInt32(), 9);
				v0i = Sse2.Add(v0i, vrnd);
				v1i = Sse2.Add(v1i, vrnd);
				v2i = Sse2.Add(v2i, vrnd);

				v0i = Sse2.ShiftRightLogical(v0i, 23);
				v1i = Sse2.ShiftRightLogical(v1i, 23);
				v2i = Sse2.ShiftRightLogical(v2i, 23);

				var vil = Sse2.PackSignedSaturate(v0i, v1i);
				var vih = Sse2.PackSignedSaturate(v2i, vai);
				vi0 = Sse2.PackUnsignedSaturate(vil, vih);
				vi0 = Ssse3.Shuffle(vi0, vmaskd);

				Sse2.Store(op, vi0);
				op += Vector128<byte>.Count;
			}
			while (ip <= ipe);
			ipe += Vector128<byte>.Count * 2;
		}
#endif

		while (ip < ipe)
		{
			uint i0a0 = ip[3];
			uint i0a1 = ip[7];
			uint i1a0 = ip[stride + 3];
			uint i1a1 = ip[stride + 7];
			uint iaa  = i0a0 + i0a1 + i1a0 + i1a1;

			if (iaa < 2)
			{
				*(uint*)op = 0;
			}
			else
			{
				uint i0 = ip[0] * i0a0;
				uint i1 = ip[1] * i0a0;
				uint i2 = ip[2] * i0a0;

				i0 += ip[4] * i0a1;
				i1 += ip[5] * i0a1;
				i2 += ip[6] * i0a1;

				byte* ipn = ip + stride;
				i0 += ipn[0] * i1a0;
				i1 += ipn[1] * i1a0;
				i2 += ipn[2] * i1a0;

				i0 += ipn[4] * i1a1;
				i1 += ipn[5] * i1a1;
				i2 += ipn[6] * i1a1;

				uint iaai = scalea / iaa;
				i0 = ((i0 + 3 >> 2) * scalec >> 17) * iaai;
				i1 = ((i1 + 3 >> 2) * scalec >> 17) * iaai;
				i2 = ((i2 + 3 >> 2) * scalec >> 17) * iaai;

				op[0] = UnFix22ToByte(i0);
				op[1] = UnFix22ToByte(i1);
				op[2] = UnFix22ToByte(i2);
				op[3] = (byte)(iaa + 2 >> 2);
			}

			ip += sizeof(uint) * 2;
			op += sizeof(uint);
		}
	}

#if HWINTRINSICS
	[MethodImpl(MethodImplOptions.AggressiveOptimization)]
#endif
	private static unsafe void process4(byte* istart, byte* ostart, nuint stride)
	{
		byte* ip = istart, ipe = istart + stride;
		byte* op = ostart;

#if HWINTRINSICS
		if (Avx2.IsSupported && stride >= (nuint)Vector256<byte>.Count * 2)
		{
			var vmaskb = Avx2.BroadcastVector128ToVector256(HWIntrinsics.ShuffleMask4ChanPairs.GetAddressOf());
			var vone = Vector256.Create((sbyte)1);

			ipe -= Vector256<byte>.Count * 2;
			do
			{
				byte* ipn = ip + stride;
				var vi0 = Avx.LoadVector256(ip);
				var vi1 = Avx.LoadVector256(ip + Vector256<byte>.Count);
				var vi2 = Avx.LoadVector256(ipn);
				var vi3 = Avx.LoadVector256(ipn + Vector256<byte>.Count);
				ip += Vector256<byte>.Count * 2;

				vi0 = Avx2.Average(vi0, vi2);
				vi1 = Avx2.Average(vi1, vi3);

				vi0 = Avx2.Shuffle(vi0, vmaskb);
				vi1 = Avx2.Shuffle(vi1, vmaskb);

				var vs0 = Avx2.MultiplyAddAdjacent(vi0, vone);
				var vs1 = Avx2.MultiplyAddAdjacent(vi1, vone);

				vs0 = Avx2.ShiftRightLogical(vs0, 1);
				vs1 = Avx2.ShiftRightLogical(vs1, 1);

				vi0 = Avx2.PackUnsignedSaturate(vs0, vs1);
				vi0 = Avx2.Permute4x64(vi0.AsUInt64(), HWIntrinsics.PermuteMaskDeinterleave4x64).AsByte();

				Avx.Store(op, vi0);
				op += Vector256<byte>.Count;
			}
			while (ip <= ipe);
			ipe += Vector256<byte>.Count * 2;
		}
		else if (Ssse3.IsSupported && stride >= (nuint)Vector128<byte>.Count * 2)
		{
			var vmaskb = Sse2.LoadVector128(HWIntrinsics.ShuffleMask4ChanPairs.GetAddressOf());
			var vone = Vector128.Create((sbyte)1);

			ipe -= Vector128<byte>.Count * 2;
			do
			{
				byte* ipn = ip + stride;
				var vi0 = Sse2.LoadVector128(ip);
				var vi1 = Sse2.LoadVector128(ip + Vector128<byte>.Count);
				var vi2 = Sse2.LoadVector128(ipn);
				var vi3 = Sse2.LoadVector128(ipn + Vector128<byte>.Count);
				ip += Vector128<byte>.Count * 2;

				vi0 = Sse2.Average(vi0, vi2);
				vi1 = Sse2.Average(vi1, vi3);

				vi0 = Ssse3.Shuffle(vi0, vmaskb);
				vi1 = Ssse3.Shuffle(vi1, vmaskb);

				var vs0 = Ssse3.MultiplyAddAdjacent(vi0, vone);
				var vs1 = Ssse3.MultiplyAddAdjacent(vi1, vone);

				vs0 = Sse2.ShiftRightLogical(vs0, 1);
				vs1 = Sse2.ShiftRightLogical(vs1, 1);

				vi0 = Sse2.PackUnsignedSaturate(vs0, vs1);

				Sse2.Store(op, vi0);
				op += Vector128<byte>.Count;
			}
			while (ip <= ipe);
			ipe += Vector128<byte>.Count * 2;
		}
#endif

		ulong m = maskb;
		while (ip < ipe)
		{
			uint pix;
			if (sizeof(nuint) == sizeof(ulong))
			{
				ulong i0 = *(ulong*)ip;
				ulong i1 = *(ulong*)(ip + stride);
				ip += sizeof(ulong);

				i0 = FastAvgBytesU(i0, i1, m);
				i0 = FastAvgBytesD(i0, i0 >> 32, m);

				pix = (uint)i0;
			}
			else
			{
				uint i0 = *(uint*)ip;
				uint i2 = *(uint*)(ip + stride);

				i0 = FastAvgBytesU(i0, i2);

				uint i1 = *(uint*)(ip + sizeof(uint));
				uint i3 = *(uint*)(ip + stride + sizeof(uint));
				ip += sizeof(uint) * 2;

				i1 = FastAvgBytesU(i1, i3);

				pix = FastAvgBytesD(i0, i1);
			}

			*(uint*)op = pix;
			op += sizeof(uint);
		}
	}

#if HWINTRINSICS
	[MethodImpl(MethodImplOptions.AggressiveOptimization)]
#endif
	private static unsafe void process3(byte* istart, byte* ostart, nuint stride)
	{
		byte* ip = istart, ipe = istart + stride;
		byte* op = ostart;

#if HWINTRINSICS
		if (Ssse3.IsSupported && stride > (nuint)Vector128<byte>.Count * 2)
		{
			var vmaskb = Sse2.LoadVector128(HWIntrinsics.ShuffleMask3ChanPairs.GetAddressOf());
			var vmaskw = Sse2.LoadVector128(HWIntrinsics.ShuffleMask3xTo3Chan.GetAddressOf());
			var vone = Vector128.Create((sbyte)1);

			ipe -= Vector128<byte>.Count * 2;
			do
			{
				byte* ipn = ip + stride;
				var vi0 = Sse2.LoadVector128(ip);
				var vi1 = Sse2.LoadVector128(ip + 12);
				var vi2 = Sse2.LoadVector128(ipn);
				var vi3 = Sse2.LoadVector128(ipn + 12);
				ip += 24;

				vi0 = Sse2.Average(vi0, vi2);
				vi1 = Sse2.Average(vi1, vi3);

				vi0 = Ssse3.Shuffle(vi0, vmaskb);
				vi1 = Ssse3.Shuffle(vi1, vmaskb);

				var vs0 = Ssse3.MultiplyAddAdjacent(vi0, vone);
				var vs1 = Ssse3.MultiplyAddAdjacent(vi1, vone);

				vs0 = Sse2.ShiftRightLogical(vs0, 1);
				vs1 = Sse2.ShiftRightLogical(vs1, 1);

				vi0 = Sse2.PackUnsignedSaturate(vs0, vs1);
				vi0 = Ssse3.Shuffle(vi0, vmaskw);

				Sse2.Store(op, vi0);
				op += 12;
			}
			while (ip <= ipe);
			ipe += Vector128<byte>.Count * 2;
		}
#endif

		ulong m = maskb;
		ipe -= sizeof(ulong);
		while (ip <= ipe)
		{
			uint pix;
			if (sizeof(nuint) == sizeof(ulong))
			{
				ulong i0 = *(ulong*)ip;
				ulong i1 = *(ulong*)(ip + stride);
				ip += 6;

				i0 = FastAvgBytesU(i0, i1, m);
				i0 = FastAvgBytesD(i0, i0 >> 24, m);

				pix = (uint)i0;
			}
			else
			{
				uint i0 = *(uint*)ip;
				uint i2 = *(uint*)(ip + stride);

				i0 = FastAvgBytesU(i0, i2);

				uint i1 = *(uint*)(ip + 3);
				uint i3 = *(uint*)(ip + stride + 3);
				ip += 6;

				i1 = FastAvgBytesU(i1, i3);

				pix = FastAvgBytesD(i0, i1);
			}

			*(uint*)op = pix;
			op += 3;
		}

		{
			uint i0 = *(uint*)ip;
			uint i2 = *(uint*)(ip + stride);

			i0 = FastAvgBytesU(i0, i2);

			uint i1 = *(ushort*)(ip + 3);
			i1 |= (uint)*(ip + 5) << 16;
			uint i3 = *(ushort*)(ip + stride + 3);
			i3 |= (uint)*(ip + stride + 5) << 16;

			i1 = FastAvgBytesU(i1, i3);

			uint pix = FastAvgBytesD(i0, i1);

			op[0] = (byte)pix; pix >>= 8;
			op[1] = (byte)pix; pix >>= 8;
			op[2] = (byte)pix;
		}
	}

#if HWINTRINSICS
	[MethodImpl(MethodImplOptions.AggressiveOptimization)]
#endif
	private static unsafe void process(byte* istart, byte* ostart, nuint stride)
	{
		byte* ip = istart, ipe = istart + stride;
		byte* op = ostart;

#if HWINTRINSICS
		if (Avx2.IsSupported && stride >= (nuint)Vector256<byte>.Count * 2)
		{
			var vone = Vector256.Create((sbyte)1);

			ipe -= Vector256<byte>.Count * 2;
			do
			{
				byte* ipn = ip + stride;
				var vi0 = Avx.LoadVector256(ip);
				var vi1 = Avx.LoadVector256(ip + Vector256<byte>.Count);
				var vi2 = Avx.LoadVector256(ipn);
				var vi3 = Avx.LoadVector256(ipn + Vector256<byte>.Count);
				ip += Vector256<byte>.Count * 2;

				vi0 = Avx2.Average(vi0, vi2);
				vi1 = Avx2.Average(vi1, vi3);

				var vs0 = Avx2.MultiplyAddAdjacent(vi0, vone);
				var vs1 = Avx2.MultiplyAddAdjacent(vi1, vone);

				vs0 = Avx2.ShiftRightLogical(vs0, 1);
				vs1 = Avx2.ShiftRightLogical(vs1, 1);

				vi0 = Avx2.PackUnsignedSaturate(vs0, vs1);
				vi0 = Avx2.Permute4x64(vi0.AsUInt64(), HWIntrinsics.PermuteMaskDeinterleave4x64).AsByte();

				Avx.Store(op, vi0);
				op += Vector256<byte>.Count;
			}
			while (ip <= ipe);
			ipe += Vector256<byte>.Count * 2;
		}
		else if (Ssse3.IsSupported && stride >= (nuint)Vector128<byte>.Count * 2)
		{
			var vone = Vector128.Create((sbyte)1);

			ipe -= Vector128<byte>.Count * 2;
			do
			{
				byte* ipn = ip + stride;
				var vi0 = Sse2.LoadVector128(ip);
				var vi1 = Sse2.LoadVector128(ip + Vector128<byte>.Count);
				var vi2 = Sse2.LoadVector128(ipn);
				var vi3 = Sse2.LoadVector128(ipn + Vector128<byte>.Count);
				ip += Vector128<byte>.Count * 2;

				vi0 = Sse2.Average(vi0, vi2);
				vi1 = Sse2.Average(vi1, vi3);

				var vs0 = Ssse3.MultiplyAddAdjacent(vi0, vone);
				var vs1 = Ssse3.MultiplyAddAdjacent(vi1, vone);

				vs0 = Sse2.ShiftRightLogical(vs0, 1);
				vs1 = Sse2.ShiftRightLogical(vs1, 1);

				vi0 = Sse2.PackUnsignedSaturate(vs0, vs1);

				Sse2.Store(op, vi0);
				op += Vector128<byte>.Count;
			}
			while (ip <= ipe);
			ipe += Vector128<byte>.Count * 2;
		}
		else
#endif

		if (sizeof(nuint) == sizeof(ulong) && stride >= sizeof(ulong))
		{
			ulong m = maskb;

			ipe -= sizeof(ulong);
			do
			{
				ulong i0 = *(ulong*)ip;
				ulong i1 = *(ulong*)(ip + stride);
				ip += sizeof(ulong);

				i0 = FastAvgBytesU(i0, i1, m);
				i0 = FastAvgBytesD(i0, i0 >> 8, m);

				op[0] = (byte)i0; i0 >>= 16;
				op[1] = (byte)i0; i0 >>= 16;
				op[2] = (byte)i0; i0 >>= 16;
				op[3] = (byte)i0;
				op += sizeof(uint);
			}
			while (ip <= ipe);
			ipe += sizeof(ulong);

			if (ip >= ipe)
				return;
		}

		ipe -= sizeof(uint);
		while (ip <= ipe)
		{
			uint i0 = *(uint*)ip;
			uint i1 = *(uint*)(ip + stride);
			ip += sizeof(uint);

			i0 = FastAvgBytesU(i0, i1);
			i0 = FastAvgBytesD(i0, i0 >> 8);

			op[0] = (byte)i0; i0 >>= 16;
			op[1] = (byte)i0;
			op += sizeof(ushort);
		}
		ipe += sizeof(uint);

		while (ip < ipe)
		{
			uint i0 = *(ushort*)ip;
			uint i1 = *(ushort*)(ip + stride);
			ip += sizeof(ushort);

			i0 = FastAvgBytesU(i0, i1);
			i0 = FastAvgBytesD(i0, i0 >> 8);

			*op = (byte)i0;
			op += sizeof(byte);
		}
	}

	protected override void Dispose(bool disposing)
	{
		if (disposing)
		{
			lineBuff.Dispose();
			lineBuff = default;
		}

		base.Dispose(disposing);
	}

	public override string ToString() => $"{nameof(HybridScaleTransform)}: {Format.Name} {scale}:1";
}
