using System;
using System.Buffers;
using System.Runtime.CompilerServices;

using static PhotoSauce.MagicScaler.MathUtil;

namespace PhotoSauce.MagicScaler
{
	internal class HybridScaleTransform : PixelSource, IDisposable
	{
#pragma warning disable IDE0044
		// read from static prevents JIT from inlining the const value with the FastAvg helpers, which causes redundant 64-bit immediate loads
		private static ulong maskb = ~0x0101010101010101ul;
#pragma warning restore IDE0044

		private readonly int scale;

		private byte[] lineBuff;

		public HybridScaleTransform(PixelSource source, int hybridScale) : base(source)
		{
			if (hybridScale == 0 || (hybridScale & (hybridScale - 1)) != 0) throw new ArgumentException("Must be power of two", nameof(hybridScale));
			scale = hybridScale;

			BufferStride = PowerOfTwoCeiling(PowerOfTwoCeiling(Width, scale) * Format.BytesPerPixel, IntPtr.Size);
			lineBuff = ArrayPool<byte>.Shared.Rent(BufferStride * scale);

			Width = DivCeiling(Source.Width, scale);
			Height = DivCeiling(Source.Height, scale);
		}

		unsafe protected override void CopyPixelsInternal(in PixelArea prc, int cbStride, int cbBufferSize, IntPtr pbBuffer)
		{
			fixed (byte* bstart = &lineBuff[0])
			{
				int iw = prc.Width * scale;
				int iiw = Math.Min(iw, Source.Width - prc.X * scale);
				uint bpp = (uint)Source.Format.BytesPerPixel;
				uint stride = (uint)iw * bpp;

				for (int y = 0; y < prc.Height; y++)
				{
					int iy = (prc.Y + y) * scale;
					int ih = scale;
					int iih = Math.Min(ih, Source.Height - iy);

					Profiler.PauseTiming();
					Source.CopyPixels(new PixelArea(prc.X * scale, iy, iiw, iih), (int)stride, lineBuff.Length, (IntPtr)bstart);
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
						Unsafe.CopyBlock(bstart + iih * stride, bstart + (iih - 1) * stride, stride);

					int ratio = scale;
					uint cb = stride;
					bool flip = ((prc.Y + y) & 1) != 0;
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
									process3(ip, op, cb, flip);
									break;
								case 4:
									if (Format.AlphaRepresentation == PixelAlphaRepresentation.Unassociated)
										process4A(ip, op, cb, flip);
									else
										process4(ip, op, cb, flip);
									break;
								default:
									process(ip, op, cb, flip);
									break;
							}

							flip = !flip;
							ip += cb * 2;
							op += cb / 2;
						}

						ratio /= 2;
						cb /= 2;
					}
				}
			}
		}

		unsafe private void process4A(byte* ipstart, byte* opstart, uint stride, bool rup)
		{
			byte* ip = ipstart, ipe = ipstart + stride;
			byte* op = opstart;

			do
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
			} while (ip < ipe);
		}

		unsafe private void process4(byte* ipstart, byte* opstart, uint stride, bool rup)
		{
			byte* ip = ipstart, ipe = ipstart + stride;
			byte* op = opstart;

			if (IntPtr.Size == sizeof(ulong) && stride > sizeof(ulong) * 2)
			{
				const ulong mask0 = 0xfffffffful;
				const ulong mask1 = 0xfffffffful << 32;
				ulong m = maskb;

				ipe -= sizeof(ulong) * 2;
				do
				{
					ulong i0 = *(ulong*)ip;
					ulong i1 = *(ulong*)(ip + sizeof(ulong));
					ulong i2 = *(ulong*)(ip + stride);
					ulong i3 = *(ulong*)(ip + stride + sizeof(ulong));
					ip += sizeof(ulong) * 2;

					i0 = FastAvgU(i0, i0 >> 32, m);
					i1 = FastAvgU(i1, i1 << 32, m);
					i2 = FastAvgD(i2, i2 >> 32, m);
					i3 = FastAvgD(i3, i3 << 32, m);

					i0 &= mask0;
					i2 &= mask0;
					i1 &= mask1;
					i3 &= mask1;
					i0 |= i1;
					i2 |= i3;

					i0 = rup ? FastAvgU(i0, i2, m) : FastAvgD(i0, i2, m);

					*(ulong*)op = i0;
					op += sizeof(ulong);
				} while (ip <= ipe);

				ipe += sizeof(ulong) * 2;
				if (ip >= ipe)
					return;
			}

			do
			{
				uint i0 = *(uint*)ip;
				uint i1 = *(uint*)(ip + sizeof(uint));

				i0 = FastAvgBytesU(i0, i1);

				uint i2 = *(uint*)(ip + stride);
				uint i3 = *(uint*)(ip + stride + sizeof(uint));
				ip += sizeof(uint) * 2;

				i1 = FastAvgBytesD(i2, i3);

				i0 = rup ? FastAvgBytesU(i0, i1) : FastAvgBytesD(i0, i1);

				*(uint*)op = i0;
				op += sizeof(uint);
			} while (ip < ipe);
		}

		unsafe private void process3(byte* ipstart, byte* opstart, uint stride, bool rup)
		{
			byte* ip = ipstart, ipe = ipstart + stride;
			byte* op = opstart;

			if (IntPtr.Size == sizeof(ulong) && stride > sizeof(ulong) * 2)
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

					i0 = FastAvgU(i0, i0 >> 24, m);
					i1 = FastAvgU(i1, i1 << 24, m);
					i2 = FastAvgD(i2, i2 >> 24, m);
					i3 = FastAvgD(i3, i3 << 24, m);

					i0 &= mask0;
					i2 &= mask0;
					i1 &= mask1;
					i3 &= mask1;
					i0 |= i1;
					i2 |= i3;

					i0 = rup ? FastAvgU(i0, i2, m) : FastAvgD(i0, i2, m);

					*(ulong*)op = i0;
					op += 6;
				} while (ip <= ipe);

				ipe += sizeof(ulong) * 2;
				if (ip >= ipe)
					return;
			}

			do
			{
				uint i0 = *(uint*)ip;
				uint i1 = *(uint*)(ip + 3);

				i0 = FastAvgBytesU(i0, i1);

				uint i2 = *(uint*)(ip + stride);
				uint i3 = *(uint*)(ip + stride + 3);
				ip += 6;

				i1 = FastAvgBytesD(i2, i3);

				i0 = rup ? FastAvgBytesU(i0, i1) : FastAvgBytesD(i0, i1);

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

		unsafe private void process(byte* ipstart, byte* opstart, uint stride, bool rup)
		{
			byte* ip = ipstart, ipe = ipstart + stride;
			byte* op = opstart;

			if (IntPtr.Size == sizeof(ulong) && stride > sizeof(ulong))
			{
				ulong m = maskb;

				ipe -= sizeof(ulong);
				do
				{
					ulong i0 = *(ulong*)ip;
					ulong i1 = *(ulong*)(ip + stride);
					ip += sizeof(ulong);

					i0 = FastAvgU(i0, i0 >> 8, m);
					i1 = FastAvgD(i1, i1 << 8, m) >> 8;

					i0 = rup ? FastAvgU(i0, i1, m) : FastAvgD(i0, i1, m);

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

				i0 = FastAvgBytesU(i0, i0 >> 8);
				i1 = FastAvgBytesD(i1, i1 << 8) >> 8;

				i0 = rup ? FastAvgBytesU(i0, i1) : FastAvgBytesD(i0, i1);

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

				i0 = FastAvgBytesU(i0, i0 >> 8);
				i1 = FastAvgBytesD(i1, i1 << 8) >> 8;

				i0 = rup ? FastAvgBytesU(i0, i1) : FastAvgBytesD(i0, i1);

				*op = (byte)i0;
				op += sizeof(byte);
			}
		}

		public void Dispose()
		{
			ArrayPool<byte>.Shared.Return(lineBuff ?? Array.Empty<byte>());
			lineBuff = null!;
		}

		public override string ToString() => $"{nameof(HybridScaleTransform)}: {Format.Name} {scale}:1";
	}
}
