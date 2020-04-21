#if HWINTRINSICS
using System;
using System.Numerics;
using System.Runtime.Intrinsics;
using System.Runtime.Intrinsics.X86;
using System.Runtime.CompilerServices;
#endif

namespace PhotoSauce.MagicScaler
{
	internal static class TemporalFilters
	{
		unsafe public static void Dedupe(WicAnimatedGifEncoder buffer, uint bgcolor)
		{
			if (!HWIntrinsics.IsSupported)
			{
				buffer.Current.Source.Span.CopyTo(buffer.EncodeFrame.Source.Span);
				return;
			}

#if HWINTRINSICS
			var src = buffer.Current.Source;
			if (src.Format != PixelFormat.Bgra32Bpp)
				return;

			var prev = buffer.Previous ?? buffer.Current;
			var next = buffer.Next ?? buffer.Current;

			fixed (byte* pcurr = src.Span, pprev = prev.Source.Span, pnext = next.Source.Span, penc = buffer.EncodeFrame.Source.Span)
			{
				int cb = src.Width * src.Format.BytesPerPixel;
				bool tfound = false;
				uint al = (uint)src.Width, ar = al, at = 0u, ab = (uint)(src.Height - 1);

				byte* pp = pprev;
				if (prev == buffer.Current || prev.Disposal == GifDisposalMethod.RestoreBackground)
				{
					pp = (byte*)0;
					if (buffer.Current.Trans)
						bgcolor = 0u;
				}

				for (int y = 0; y < src.Height; y++)
				{
					uint offs = (uint)(y * src.Stride);
					uint eql = 0u, eqr = 0u;

					if (Avx2.IsSupported && cb >= Vector256<byte>.Count)
					{
						if (pprev != pcurr && pnext != pcurr)
							denoiseLineAvx2(pcurr + offs, pprev + offs, pnext + offs, cb);

						(eql, eqr) = dedupeLineAvx2(pcurr + offs, pp == (byte*)0 ? pp : pp + offs, penc + offs, cb, bgcolor);
					}
					else if (cb >= Vector128<byte>.Count)
					{
						if (pprev != pcurr && pnext != pcurr)
							denoiseLineSse2(pcurr + offs, pprev + offs, pnext + offs, cb);

						(eql, eqr) = dedupeLineSse2(pcurr + offs, pp == (byte*)0 ? pp : pp + offs, penc + offs, cb, bgcolor);
					}

					if (eql == (uint)src.Width)
					{
						if (!tfound)
							at++;
					}
					else
					{
						tfound = true;
						ab = (uint)y;
						al = Math.Min(al, eql);
						ar = Math.Min(ar, eqr);
					}
				}

				buffer.Current.Area = new PixelArea(
					MathUtil.Clamp((int)al, 0, src.Width - 2),
					MathUtil.Clamp((int)at, 0, src.Height - 2),
					Math.Max(1, src.Width - (int)al - (int)ar),
					Math.Max(1, (int)ab + 1 - (int)at)
				);
			}
#endif
		}

#if HWINTRINSICS
		[MethodImpl(MethodImplOptions.AggressiveOptimization)]
		unsafe private static void denoiseLineAvx2(byte* pcurr, byte* pprev, byte* pnext, int cb)
		{
			byte* ip = pcurr, pp = pprev, np = pnext;
			uint cnt = 0, end = (uint)cb - (uint)Vector256<byte>.Count;

			var voffset = Vector256.Create((byte)0x80);
			var vthresh = Vector256.Create((byte)0x0f);

			LoopTop:
			do
			{
				var vcurr = Avx.LoadVector256(ip + cnt);
				var vprev = Avx.LoadVector256(pp + cnt);
				var vnext = Avx.LoadVector256(np + cnt);

				var vdiffp = Avx2.Or(Avx2.SubtractSaturate(vcurr, vprev), Avx2.SubtractSaturate(vprev, vcurr));
				var vmaskp = Avx2.CompareEqual(vthresh, Avx2.Max(vdiffp, vthresh));

				var vdiffn = Avx2.Or(Avx2.SubtractSaturate(vcurr, vnext), Avx2.SubtractSaturate(vnext, vcurr));
				var vmaskn = Avx2.CompareEqual(vthresh, Avx2.Max(vdiffn, vthresh));

				var vavgp = Avx2.Average(vcurr, vprev);
				var vavgn = Avx2.Average(vcurr, vnext);

				var voutval = Avx2.Average(Avx2.BlendVariable(vavgn, vavgp, vmaskp), Avx2.BlendVariable(vavgp, vavgn, vmaskn));
				var voutmsk = Avx2.Or(vmaskp, vmaskn);
				voutval = Avx2.Average(voutval, Avx2.BlendVariable(voutval, Avx2.Average(vprev, vnext), Avx2.And(vmaskp, vmaskn)));

				var vcurrs = Avx2.Or(vcurr, voffset).AsSByte();
				var vprevs = Avx2.Or(vprev, voffset).AsSByte();
				var vnexts = Avx2.Or(vnext, voffset).AsSByte();

				var vsurlt = Avx2.And(Avx2.CompareGreaterThan(vcurrs, vprevs), Avx2.CompareGreaterThan(vcurrs, vnexts));
				var vsurgt = Avx2.And(Avx2.CompareGreaterThan(vprevs, vcurrs), Avx2.CompareGreaterThan(vnexts, vcurrs));

				voutmsk = Avx2.And(voutmsk, Avx2.Or(vsurlt, vsurgt).AsByte());
				voutval = Avx2.BlendVariable(vcurr, voutval, voutmsk);

				Avx.Store(ip + cnt, voutval);
				cnt += (uint)Vector256<byte>.Count;

			} while (cnt <= end);

			if (cnt < end + (uint)Vector256<byte>.Count)
			{
				cnt = end;
				goto LoopTop;
			}
		}

		[MethodImpl(MethodImplOptions.AggressiveOptimization)]
		unsafe private static void denoiseLineSse2(byte* pcurr, byte* pprev, byte* pnext, int cb)
		{
			byte* ip = pcurr, pp = pprev, np = pnext;
			uint cnt = 0, end = (uint)cb - (uint)Vector128<byte>.Count;

			var voffset = Vector128.Create((byte)0x80);
			var vthresh = Vector128.Create((byte)0x0f);

			LoopTop:
			do
			{
				var vcurr = Sse2.LoadVector128(ip + cnt);
				var vprev = Sse2.LoadVector128(pp + cnt);
				var vnext = Sse2.LoadVector128(np + cnt);

				var vdiffp = Sse2.Or(Sse2.SubtractSaturate(vcurr, vprev), Sse2.SubtractSaturate(vprev, vcurr));
				var vmaskp = Sse2.CompareEqual(vthresh, Sse2.Max(vdiffp, vthresh));

				var vdiffn = Sse2.Or(Sse2.SubtractSaturate(vcurr, vnext), Sse2.SubtractSaturate(vnext, vcurr));
				var vmaskn = Sse2.CompareEqual(vthresh, Sse2.Max(vdiffn, vthresh));

				var vavgp = Sse2.Average(vcurr, vprev);
				var vavgn = Sse2.Average(vcurr, vnext);

				var voutval = Sse2.Average(HWIntrinsics.BlendVariable(vavgn, vavgp, vmaskp), HWIntrinsics.BlendVariable(vavgp, vavgn, vmaskn));
				var voutmsk = Sse2.Or(vmaskp, vmaskn);
				voutval = Sse2.Average(voutval, HWIntrinsics.BlendVariable(voutval, Sse2.Average(vprev, vnext), Sse2.And(vmaskp, vmaskn)));

				var vcurrs = Sse2.Or(vcurr, voffset).AsSByte();
				var vprevs = Sse2.Or(vprev, voffset).AsSByte();
				var vnexts = Sse2.Or(vnext, voffset).AsSByte();

				var vsurlt = Sse2.And(Sse2.CompareGreaterThan(vcurrs, vprevs), Sse2.CompareGreaterThan(vcurrs, vnexts));
				var vsurgt = Sse2.And(Sse2.CompareGreaterThan(vprevs, vcurrs), Sse2.CompareGreaterThan(vnexts, vcurrs));

				voutmsk = Sse2.And(voutmsk, Sse2.Or(vsurlt, vsurgt).AsByte());
				voutval = HWIntrinsics.BlendVariable(vcurr, voutval, voutmsk);

				Sse2.Store(ip + cnt, voutval);
				cnt += (uint)Vector128<byte>.Count;

			} while (cnt <= end);

			if (cnt < end + (uint)Vector128<byte>.Count)
			{
				cnt = end;
				goto LoopTop;
			}
		}

		[MethodImpl(MethodImplOptions.AggressiveOptimization)]
		unsafe private static (uint eql, uint eqr) dedupeLineAvx2(byte* pcurr, byte* pprev, byte* penc, int cb, uint bg)
		{
			byte* ip = pcurr, pp = pprev, op = penc;
			uint cnt = 0, end = (uint)cb - (uint)Vector256<byte>.Count;

			bool lfound = false;
			uint eql = 0u, eqr = 0u;
			var vbg = pp == (byte*)0 ? Vector256.Create(bg) : Vector256<uint>.Zero;

			LoopTop:
			do
			{
				var vprev = pp != (byte*)0 ? Avx.LoadVector256(pp + cnt).AsUInt32() : vbg;
				var vcurr = Avx.LoadVector256(ip + cnt).AsUInt32();

				var veq = Avx2.CompareEqual(vcurr, vprev);
				uint msk = (uint)Avx2.MoveMask(veq.AsByte());

				vcurr = Avx2.BlendVariable(vcurr, vbg, veq);

				if (msk == uint.MinValue)
				{
					lfound = true;
					eqr = 0u;
				}
				else if (msk == uint.MaxValue)
				{
					if (!lfound)
						eql += (uint)Vector256<uint>.Count;

					eqr += (uint)Vector256<uint>.Count;
				}
				else
				{
					if (!lfound)
					{
						eql += (uint)BitOperations.TrailingZeroCount(msk) / sizeof(uint);
						lfound = true;
					}

					eqr = (uint)BitOperations.LeadingZeroCount(msk) / sizeof(uint);
				}

				Avx.Store(op + cnt, vcurr.AsByte());
				cnt += (uint)Vector256<byte>.Count;

			} while (cnt <= end);

			if (cnt < end + (uint)Vector256<byte>.Count)
			{
				uint offs = (cnt - end) / sizeof(uint);
				if (!lfound)
					eql -= offs;
				eqr -= offs;
				cnt = end;
				goto LoopTop;
			}

			return (eql, eqr);
		}

		[MethodImpl(MethodImplOptions.AggressiveOptimization)]
		unsafe private static (uint eql, uint eqr) dedupeLineSse2(byte* pcurr, byte* pprev, byte* penc, int cb, uint bg)
		{
			byte* ip = pcurr, pp = pprev, op = penc;
			uint cnt = 0, end = (uint)cb - (uint)Vector128<byte>.Count;

			bool lfound = false;
			uint eql = 0u, eqr = 0u;
			var vbg = pp == (byte*)0 ? Vector128.Create(bg) : Vector128<uint>.Zero;

			LoopTop:
			do
			{
				var vprev = pp != (byte*)0 ? Sse2.LoadVector128(pp + cnt).AsUInt32() : vbg;
				var vcurr = Sse2.LoadVector128(ip + cnt).AsUInt32();

				var veq = Sse2.CompareEqual(vcurr, vprev);
				uint msk = (uint)Sse2.MoveMask(veq.AsByte());

				vcurr = HWIntrinsics.BlendVariable(vcurr, vbg, veq);

				if (msk == ushort.MinValue)
				{
					lfound = true;
					eqr = 0u;
				}
				else if (msk == ushort.MaxValue)
				{
					if (!lfound)
						eql += (uint)Vector128<uint>.Count;

					eqr += (uint)Vector128<uint>.Count;
				}
				else
				{
					if (!lfound)
					{
						eql += (uint)BitOperations.TrailingZeroCount(msk) / sizeof(uint);
						lfound = true;
					}

					eqr = (uint)BitOperations.LeadingZeroCount(msk) / sizeof(uint);
				}

				Sse2.Store(op + cnt, vcurr.AsByte());
				cnt += (uint)Vector128<byte>.Count;

			} while (cnt <= end);

			if (cnt < end + (uint)Vector128<byte>.Count)
			{
				uint offs = (cnt - end) / sizeof(uint);
				if (!lfound)
					eql -= offs;
				eqr -= offs;
				cnt = end;
				goto LoopTop;
			}

			return (eql, eqr);
		}
#endif
	}
}
