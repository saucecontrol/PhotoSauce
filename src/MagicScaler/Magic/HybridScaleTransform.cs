// Copyright © Clinton Ingram and Contributors.  Licensed under the MIT License.

using System;
using System.Runtime.CompilerServices;

#if HWINTRINSICS
using System.Runtime.Intrinsics;
using System.Runtime.Intrinsics.X86;
using System.Runtime.InteropServices;
#endif

using static PhotoSauce.MagicScaler.MathUtil;

namespace PhotoSauce.MagicScaler.Transforms
{
	internal sealed class HybridScaleTransform : ChainedPixelSource, IDisposable
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
			if (hybridScale == 0 || (hybridScale & (hybridScale - 1)) != 0) throw new ArgumentException("Must be power of two", nameof(hybridScale));
			scale = hybridScale;

			int bufferStride = PowerOfTwoCeiling(PowerOfTwoCeiling(source.Width, scale) * source.Format.BytesPerPixel, IntPtr.Size);
			lineBuff = BufferPool.RentAligned<byte>(bufferStride * scale);

			Width = DivCeiling(PrevSource.Width, scale);
			Height = DivCeiling(PrevSource.Height, scale);
		}

		protected override unsafe void CopyPixelsInternal(in PixelArea prc, int cbStride, int cbBufferSize, IntPtr pbBuffer)
		{
			var buffspan = lineBuff.Span;
			if (buffspan.Length == 0) throw new ObjectDisposedException(nameof(HybridScaleTransform));

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
					PrevSource.CopyPixels(new PixelArea(prc.X * scale, iy, iiw, iih), stride, buffspan.Length, (IntPtr)bstart);
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
								new Span<triple>(bp, iw - iiw).Fill(((triple*)bp)[-1]);
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
							op = (byte*)pbBuffer + y * cbStride;

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
		private static unsafe void process4A(byte* ipstart, byte* opstart, nuint stride)
		{
			byte* ip = ipstart, ipe = ipstart + stride;
			byte* op = opstart;

#if HWINTRINSICS
			const byte blendMaskAlpha = 0b_1100_1100;
			const ushort scaleAlpha = (UQ15One << 8) / byte.MaxValue;
			const ulong bitMaskAlpha = 0xffff0000ffff0000ul;

			if (Avx2.IsSupported && stride >= (nuint)Vector256<byte>.Count)
			{
				fixed (int* iatstart = LookupTables.InverseAlpha)
				{
					var vshufa = Avx2.BroadcastVector128ToVector256((byte*)Unsafe.AsPointer(ref MemoryMarshal.GetReference(HWIntrinsics.ShuffleMask8bitAlpha)));
					var vmaske = Avx2.BroadcastVector128ToVector256((byte*)Unsafe.AsPointer(ref MemoryMarshal.GetReference(HWIntrinsics.ShuffleMask8bitEven)));
					var vmasko = Avx2.BroadcastVector128ToVector256((byte*)Unsafe.AsPointer(ref MemoryMarshal.GetReference(HWIntrinsics.ShuffleMask8bitOdd)));
					var vmaskp = Avx2.BroadcastVector128ToVector256((int*)Unsafe.AsPointer(ref MemoryMarshal.GetReference(HWIntrinsics.PermuteMaskEvenOdd8x32)) + Vector128<int>.Count);

					var vmaska = Vector256.Create(bitMaskAlpha).AsByte();
					var vscale = Vector256.Create(scaleAlpha);
					var vone = Vector256.Create((short)1);

					ipe -= Vector256<byte>.Count;
					do
					{
						var vi0 = Avx.LoadVector256(ip);
						var vi1 = Avx.LoadVector256(ip + stride);
						ip += Vector256<byte>.Count;

						var va0 = Avx2.MultiplyHigh(Avx2.Shuffle(vi0, vshufa).AsUInt16(), vscale);
						var va1 = Avx2.MultiplyHigh(Avx2.Shuffle(vi1, vshufa).AsUInt16(), vscale);

						var ve0 = Avx2.MultiplyHigh(Avx2.Shuffle(vi0, vmaske).AsUInt16(), va0);
						var ve1 = Avx2.MultiplyHigh(Avx2.Shuffle(vi1, vmaske).AsUInt16(), va1);

						ve0 = Avx2.Average(ve0, ve1);
						var ve = Avx2.MultiplyAddAdjacent(ve0.AsInt16(), vone);

						vi0 = Avx2.Or(vi0, vmaska);
						vi1 = Avx2.Or(vi1, vmaska);

						var vo0 = Avx2.MultiplyHigh(Avx2.Shuffle(vi0, vmasko).AsUInt16(), va0);
						var vo1 = Avx2.MultiplyHigh(Avx2.Shuffle(vi1, vmasko).AsUInt16(), va1);

						vo0 = Avx2.Average(vo0, vo1);
						var vo = Avx2.MultiplyAddAdjacent(vo0.AsInt16(), vone);

						ulong* iat = (ulong*)iatstart;
						var vai = Avx2.ShiftRightLogical(vo, 7);
						vai = Avx2.PermuteVar8x32(vai, vmaskp);
						vai = Avx2.GatherVector256(iat, vai.GetLower(), sizeof(ulong)).AsInt32();

						var vb = vo;
						ve = Avx2.ShiftRightLogical(Avx2.MultiplyLow(ve, vai), 15);
						vo = Avx2.ShiftRightLogical(Avx2.MultiplyLow(vo, vai), 15);
						vo = Avx2.Blend(vo.AsInt16(), vb.AsInt16(), blendMaskAlpha).AsInt32();

						var vs0 = Avx2.PackUnsignedSaturate(Avx2.UnpackLow(ve, vo), Avx2.UnpackHigh(ve, vo));
						vs0 = Avx2.ShiftRightLogical(vs0, 8);

						vi0 = Avx2.PackUnsignedSaturate(vs0.AsInt16(), vs0.AsInt16());
						vi0 = Avx2.Permute4x64(vi0.AsUInt64(), HWIntrinsics.PermuteMaskDeinterleave4x64).AsByte();

						Sse2.Store(op, vi0.GetLower());
						op += Vector128<byte>.Count;

					} while (ip <= ipe);
					ipe += Vector256<byte>.Count;
				}
			}
			else if (Sse41.IsSupported && stride >= (nuint)Vector128<byte>.Count)
			{
				fixed (int* iatstart = LookupTables.InverseAlpha)
				{
					var vshufa = Sse2.LoadVector128((byte*)Unsafe.AsPointer(ref MemoryMarshal.GetReference(HWIntrinsics.ShuffleMask8bitAlpha)));
					var vmaske = Sse2.LoadVector128((byte*)Unsafe.AsPointer(ref MemoryMarshal.GetReference(HWIntrinsics.ShuffleMask8bitEven)));
					var vmasko = Sse2.LoadVector128((byte*)Unsafe.AsPointer(ref MemoryMarshal.GetReference(HWIntrinsics.ShuffleMask8bitOdd)));

					var vmaska = Vector128.Create(bitMaskAlpha).AsByte();
					var vscale = Vector128.Create(scaleAlpha);
					var vone = Vector128.Create((short)1);

					ipe -= Vector128<byte>.Count;
					do
					{
						var vi0 = Sse2.LoadVector128(ip);
						var vi1 = Sse2.LoadVector128(ip + stride);
						ip += Vector128<byte>.Count;

						var va0 = Sse2.MultiplyHigh(Ssse3.Shuffle(vi0, vshufa).AsUInt16(), vscale);
						var va1 = Sse2.MultiplyHigh(Ssse3.Shuffle(vi1, vshufa).AsUInt16(), vscale);

						var ve0 = Sse2.MultiplyHigh(Ssse3.Shuffle(vi0, vmaske).AsUInt16(), va0);
						var ve1 = Sse2.MultiplyHigh(Ssse3.Shuffle(vi1, vmaske).AsUInt16(), va1);

						ve0 = Sse2.Average(ve0, ve1);
						var ve = Sse2.MultiplyAddAdjacent(ve0.AsInt16(), vone);

						vi0 = Sse2.Or(vi0, vmaska);
						vi1 = Sse2.Or(vi1, vmaska);

						var vo0 = Sse2.MultiplyHigh(Ssse3.Shuffle(vi0, vmasko).AsUInt16(), va0);
						var vo1 = Sse2.MultiplyHigh(Ssse3.Shuffle(vi1, vmasko).AsUInt16(), va1);

						vo0 = Sse2.Average(vo0, vo1);
						var vo = Sse2.MultiplyAddAdjacent(vo0.AsInt16(), vone);

						ulong* iat = (ulong*)iatstart;
						var vai = Sse2.ShiftRightLogical(vo, 7);
						var vai0 = Sse2.LoadScalarVector128(iat + Sse41.Extract(vai.AsUInt32(), 1));
						var vai1 = Sse2.LoadScalarVector128(iat + Sse41.Extract(vai.AsUInt32(), 3));
						vai = Sse2.UnpackLow(vai0, vai1).AsInt32();

						var vb = vo;
						ve = Sse2.ShiftRightLogical(Sse41.MultiplyLow(ve, vai), 15);
						vo = Sse2.ShiftRightLogical(Sse41.MultiplyLow(vo, vai), 15);
						vo = Sse41.Blend(vo.AsInt16(), vb.AsInt16(), blendMaskAlpha).AsInt32();

						var vs0 = Sse41.PackUnsignedSaturate(Sse2.UnpackLow(ve, vo), Sse2.UnpackHigh(ve, vo));
						vs0 = Sse2.ShiftRightLogical(vs0, 8);

						vi0 = Sse2.PackUnsignedSaturate(vs0.AsInt16(), vs0.AsInt16());

						Sse2.StoreScalar((long*)op, vi0.AsInt64());
						op += Vector128<byte>.Count / 2;

					} while (ip <= ipe);
					ipe += Vector128<byte>.Count;
				}
			}
#endif

			while (ip < ipe)
			{
				uint i0a0 = FastFix15(ip[3]);
				uint i0a1 = FastFix15(ip[7]);
				uint i1a0 = FastFix15(ip[stride + 3]);
				uint i1a1 = FastFix15(ip[stride + 7]);
				uint iaa  = i0a0 + i0a1 + i1a0 + i1a1 >> 2;

				if (iaa < (UQ15Round >> 8))
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

					uint iaai = UQ15One * UQ15One / iaa;
					i0 = UnFix10(i0) * iaai;
					i1 = UnFix10(i1) * iaai;
					i2 = UnFix10(i2) * iaai;

					op[0] = UnFix22ToByte(i0);
					op[1] = UnFix22ToByte(i1);
					op[2] = UnFix22ToByte(i2);
					op[3] = (byte)UnFix15(iaa * byte.MaxValue);
				}

				ip += sizeof(uint) * 2;
				op += sizeof(uint);
			}
		}

#if HWINTRINSICS
		[MethodImpl(MethodImplOptions.AggressiveOptimization)]
#endif
		private static unsafe void process4(byte* ipstart, byte* opstart, nuint stride)
		{
			byte* ip = ipstart, ipe = ipstart + stride;
			byte* op = opstart;

#if HWINTRINSICS
			if (Avx2.IsSupported && stride >= (nuint)Vector256<byte>.Count * 2)
			{
				var vmaskb = Avx2.BroadcastVector128ToVector256((byte*)Unsafe.AsPointer(ref MemoryMarshal.GetReference(HWIntrinsics.ShuffleMask4ChanPairs)));
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

				} while (ip <= ipe);
				ipe += Vector256<byte>.Count * 2;
			}
			else if (Ssse3.IsSupported && stride >= (nuint)Vector128<byte>.Count * 2)
			{
				var vmaskb = Sse2.LoadVector128((byte*)Unsafe.AsPointer(ref MemoryMarshal.GetReference(HWIntrinsics.ShuffleMask4ChanPairs)));
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

				} while (ip <= ipe);
				ipe += Vector128<byte>.Count * 2;
			}
			else
#endif

			if (sizeof(nuint) == sizeof(ulong) && stride >= sizeof(ulong) * 2)
			{
				const ulong mask0 = 0xfffffffful;
				const ulong mask1 = mask0 << 32;
				ulong m = maskb;

				ipe -= sizeof(ulong) * 2;
				do
				{
					ulong i0 = *(ulong*)ip;
					ulong i1 = *(ulong*)(ip + sizeof(ulong));
					ulong i2 = *(ulong*)(ip + stride);
					ulong i3 = *(ulong*)(ip + stride + sizeof(ulong));
					ip += sizeof(ulong) * 2;

					i0 = FastAvgU(i0, i2, m);
					i1 = FastAvgU(i1, i3, m);

					i0 = FastAvgD(i0, i0 >> 32, m);
					i1 = FastAvgD(i1, i1 << 32, m);

					i0 &= mask0;
					i1 &= mask1;
					i0 |= i1;

					*(ulong*)op = i0;
					op += sizeof(ulong);

				} while (ip <= ipe);
				ipe += sizeof(ulong) * 2;
			}

			while (ip < ipe)
			{
				uint i0 = *(uint*)ip;
				uint i2 = *(uint*)(ip + stride);

				i0 = FastAvgBytesU(i0, i2);

				uint i1 = *(uint*)(ip + sizeof(uint));
				uint i3 = *(uint*)(ip + stride + sizeof(uint));
				ip += sizeof(uint) * 2;

				i1 = FastAvgBytesU(i1, i3);

				i0 = FastAvgBytesD(i0, i1);

				*(uint*)op = i0;
				op += sizeof(uint);
			}
		}

#if HWINTRINSICS
		[MethodImpl(MethodImplOptions.AggressiveOptimization)]
#endif
		private static unsafe void process3(byte* ipstart, byte* opstart, nuint stride)
		{
			byte* ip = ipstart, ipe = ipstart + stride;
			byte* op = opstart;

#if HWINTRINSICS
			if (Ssse3.IsSupported && stride > (nuint)Vector128<byte>.Count * 2)
			{
				var vmaskb = Sse2.LoadVector128((byte*)Unsafe.AsPointer(ref MemoryMarshal.GetReference(HWIntrinsics.ShuffleMask3ChanPairs)));
				var vmaskw = Sse2.LoadVector128((byte*)Unsafe.AsPointer(ref MemoryMarshal.GetReference(HWIntrinsics.ShuffleMask3xTo3Chan)));
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

				} while (ip <= ipe);
				ipe += Vector128<byte>.Count * 2;
			}
			else
#endif

			if (sizeof(nuint) == sizeof(ulong) && stride > sizeof(ulong) * 2)
			{
				const ulong mask0 = 0xfffffful;
				const ulong mask1 = 0xfffffful << 24;
				ulong m = maskb;

				ipe -= sizeof(ulong) * 2;
				do
				{
					ulong i0 = *(ulong*)ip;
					ulong i1 = *(ulong*)(ip + 6);
					ulong i2 = *(ulong*)(ip + stride);
					ulong i3 = *(ulong*)(ip + stride + 6);
					ip += 12;

					i0 = FastAvgU(i0, i2, m);
					i1 = FastAvgU(i1, i3, m);

					i0 = FastAvgD(i0, i0 >> 24, m);
					i1 = FastAvgD(i1, i1 << 24, m);

					i0 &= mask0;
					i1 &= mask1;
					i0 |= i1;

					*(ulong*)op = i0;
					op += 6;

				} while (ip <= ipe);
				ipe += sizeof(ulong) * 2;
			}

			do
			{
				uint i0 = *(uint*)ip;
				uint i2 = *(uint*)(ip + stride);

				i0 = FastAvgBytesU(i0, i2);

				uint i1 = *(uint*)(ip + 3);
				uint i3 = *(uint*)(ip + stride + 3);
				ip += 6;

				i1 = FastAvgBytesU(i1, i3);

				i0 = FastAvgBytesD(i0, i1);

				if (ip >= ipe)
					goto LastPixel;

				*(uint*)op = i0;
				op += 3;
				continue;

				LastPixel:
				op[0] = (byte)i0; i0 >>= 8;
				op[1] = (byte)i0; i0 >>= 8;
				op[2] = (byte)i0;
				break;

			} while (true);
		}

#if HWINTRINSICS
		[MethodImpl(MethodImplOptions.AggressiveOptimization)]
#endif
		private static unsafe void process(byte* ipstart, byte* opstart, nuint stride)
		{
			byte* ip = ipstart, ipe = ipstart + stride;
			byte* op = opstart;

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

				} while (ip <= ipe);
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

				} while (ip <= ipe);
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

					i0 = FastAvgU(i0, i1, m);
					i0 = FastAvgD(i0, i0 >> 8, m);

					op[0] = (byte)i0; i0 >>= 16;
					op[1] = (byte)i0; i0 >>= 16;
					op[2] = (byte)i0; i0 >>= 16;
					op[3] = (byte)i0;
					op += sizeof(uint);

				} while (ip <= ipe);
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

		public void Dispose()
		{
			lineBuff.Dispose();
			lineBuff = default;
		}

		public override string ToString() => $"{nameof(HybridScaleTransform)}: {Format.Name} {scale}:1";
	}
}
