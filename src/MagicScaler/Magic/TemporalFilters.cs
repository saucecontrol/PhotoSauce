// Copyright © Clinton Ingram and Contributors.  Licensed under the MIT License.

using System;

#if HWINTRINSICS
using System.Numerics;
using System.Runtime.Intrinsics;
using System.Runtime.Intrinsics.X86;
using System.Runtime.CompilerServices;
#endif

namespace PhotoSauce.MagicScaler;

internal static unsafe class TemporalFilters
{
	private const byte denoiseThreshold = 15;

	public static void Dedupe(AnimationEncoder buffer, uint bgcolor, bool blend)
	{
		var src = buffer.Current.Source;
		if (src.Format != PixelFormat.Bgra32)
			return;

		var prev = buffer.Previous ?? buffer.Current;
		var next = buffer.Next ?? buffer.Current;

		fixed (byte* pcurr = src.Span, pprev = prev.Source.Span, pnext = next.Source.Span, penc = buffer.EncodeFrame.Source.Span)
		{
			nint cb = src.Width * src.Format.BytesPerPixel;
			bool tfound = false;
			uint al = (uint)src.Width, ar = al, at = 0u, ab = (uint)(src.Height - 1);

			byte* pp = pprev;
			if (prev == buffer.Current || prev.Disposal == FrameDisposalMethod.RestoreBackground)
			{
				pp = null;
				if (buffer.Current.HasTransparency)
					bgcolor = 0u;
			}

			for (int y = 0; y < src.Height; y++)
			{
				nuint offs = (uint)(y * src.Stride);
				uint eql = 0u, eqr = 0u;

				if (pprev != pcurr && pnext != pcurr)
				{
#if HWINTRINSICS
					if (Avx2.IsSupported && cb >= Vector256<byte>.Count)
						denoiseLineAvx2(pcurr + offs, pprev + offs, pnext + offs, cb);
					else if (Sse2.IsSupported && cb >= Vector128<byte>.Count)
						denoiseLineSse2(pcurr + offs, pprev + offs, pnext + offs, cb);
					else
#endif
						denoiseLineScalar(pcurr + offs, pprev + offs, pnext + offs, cb);
				}

#if HWINTRINSICS
				if (Avx2.IsSupported && cb >= Vector256<byte>.Count)
					(eql, eqr) = dedupeLineAvx2(pcurr + offs, pp is null ? pp : pp + offs, penc + offs, cb, bgcolor, blend);
				else if (Sse2.IsSupported && cb >= Vector128<byte>.Count)
					(eql, eqr) = dedupeLineSse2(pcurr + offs, pp is null ? pp : pp + offs, penc + offs, cb, bgcolor, blend);
				else
#endif
					(eql, eqr) = dedupeLineScalar(pcurr + offs, pp is null ? pp : pp + offs, penc + offs, cb, bgcolor, blend);

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
				MathUtil.Clamp((int)al, 0, Math.Max(0, src.Width - 2)),
				MathUtil.Clamp((int)at, 0, Math.Max(0, src.Height - 2)),
				Math.Max(1, src.Width - (int)al - (int)ar),
				Math.Max(1, (int)ab + 1 - (int)at)
			);
		}
	}

#if HWINTRINSICS
	[MethodImpl(MethodImplOptions.AggressiveOptimization)]
	private static void denoiseLineAvx2(byte* pcurr, byte* pprev, byte* pnext, nint cb)
	{
		byte* ip = pcurr, pp = pprev, np = pnext;
		nint cnt = 0, end = cb - Vector256<byte>.Count;

		var vthresh = Vector256.Create(denoiseThreshold);

		LoopTop:
		do
		{
			var vcurr = Avx.LoadVector256(ip + cnt);
			var vprev = Avx.LoadVector256(pp + cnt);
			var vnext = Avx.LoadVector256(np + cnt);

			var vdiffp = Avx2.Or(Avx2.SubtractSaturate(vcurr, vprev), Avx2.SubtractSaturate(vprev, vcurr));
			var vdiffn = Avx2.Or(Avx2.SubtractSaturate(vcurr, vnext), Avx2.SubtractSaturate(vnext, vcurr));
			var vmaskp = Avx2.CompareEqual(Avx2.Max(vdiffp, vthresh), vthresh);
			var vmaskn = Avx2.CompareEqual(Avx2.Max(vdiffn, vthresh), vthresh);

			var vavgp = Avx2.Average(vcurr, vprev);
			var vavgn = Avx2.Average(vcurr, vnext);

			var voutval = Avx2.Average(Avx2.BlendVariable(vavgn, vavgp, vmaskp), Avx2.BlendVariable(vavgp, vavgn, vmaskn));
			var voutmsk = Avx2.Or(vmaskp, vmaskn);
			voutval = Avx2.Average(voutval, Avx2.BlendVariable(voutval, Avx2.Average(vprev, vnext), Avx2.And(vmaskp, vmaskn)));

			var vsurnlt = Avx2.CompareEqual(Avx2.Min(Avx2.Max(vprev, vnext), vcurr), vcurr);
			var vsurngt = Avx2.CompareEqual(Avx2.Max(Avx2.Min(vprev, vnext), vcurr), vcurr);
			voutmsk = Avx2.AndNot(Avx2.And(vsurnlt, vsurngt), voutmsk);
			voutval = Avx2.BlendVariable(vcurr, voutval, voutmsk);

			Avx.Store(ip + cnt, voutval);
			cnt += Vector256<byte>.Count;
		}
		while (cnt <= end);

		if (cnt < end + Vector256<byte>.Count)
		{
			cnt = end;
			goto LoopTop;
		}
	}

	[MethodImpl(MethodImplOptions.AggressiveOptimization)]
	private static void denoiseLineSse2(byte* pcurr, byte* pprev, byte* pnext, nint cb)
	{
		byte* ip = pcurr, pp = pprev, np = pnext;
		nint cnt = 0, end = cb - Vector128<byte>.Count;

		var vthresh = Vector128.Create(denoiseThreshold);

		LoopTop:
		do
		{
			var vcurr = Sse2.LoadVector128(ip + cnt);
			var vprev = Sse2.LoadVector128(pp + cnt);
			var vnext = Sse2.LoadVector128(np + cnt);

			var vdiffp = Sse2.Or(Sse2.SubtractSaturate(vcurr, vprev), Sse2.SubtractSaturate(vprev, vcurr));
			var vdiffn = Sse2.Or(Sse2.SubtractSaturate(vcurr, vnext), Sse2.SubtractSaturate(vnext, vcurr));
			var vmaskp = Sse2.CompareEqual(Sse2.Max(vdiffp, vthresh), vthresh);
			var vmaskn = Sse2.CompareEqual(Sse2.Max(vdiffn, vthresh), vthresh);

			var vavgp = Sse2.Average(vcurr, vprev);
			var vavgn = Sse2.Average(vcurr, vnext);

			var voutval = Sse2.Average(HWIntrinsics.BlendVariable(vavgn, vavgp, vmaskp), HWIntrinsics.BlendVariable(vavgp, vavgn, vmaskn));
			var voutmsk = Sse2.Or(vmaskp, vmaskn);
			voutval = Sse2.Average(voutval, HWIntrinsics.BlendVariable(voutval, Sse2.Average(vprev, vnext), Sse2.And(vmaskp, vmaskn)));

			var vsurnlt = Sse2.CompareEqual(Sse2.Min(Sse2.Max(vprev, vnext), vcurr), vcurr);
			var vsurngt = Sse2.CompareEqual(Sse2.Max(Sse2.Min(vprev, vnext), vcurr), vcurr);
			voutmsk = Sse2.AndNot(Sse2.And(vsurnlt, vsurngt), voutmsk);
			voutval = HWIntrinsics.BlendVariable(vcurr, voutval, voutmsk);

			Sse2.Store(ip + cnt, voutval);
			cnt += Vector128<byte>.Count;
		}
		while (cnt <= end);

		if (cnt < end + Vector128<byte>.Count)
		{
			cnt = end;
			goto LoopTop;
		}
	}
#endif

	private static void denoiseLineScalar(byte* pcurr, byte* pprev, byte* pnext, nint cb)
	{
		byte* ip = pcurr, pp = pprev, np = pnext;

		for (nint cnt = 0; cnt < cb; cnt++)
		{
			int curr = ip[cnt];
			int dprv = pp[cnt] - curr;
			int dnxt = np[cnt] - curr;

			if (dprv == 0 || dnxt == 0 || ((dprv ^ dnxt) & int.MinValue) != 0)
				continue;

			bool mprv = dprv <= denoiseThreshold & dprv >= -denoiseThreshold;
			bool mnxt = dnxt <= denoiseThreshold & dnxt >= -denoiseThreshold;
			if (mprv & mnxt)
				curr = (curr << 3) + dprv + dprv + dprv + dnxt + dnxt + dnxt + 4 >> 3;
			else if (mprv)
				curr = curr + curr + dprv + 1 >> 1;
			else if (mnxt)
				curr = curr + curr + dnxt + 1 >> 1;

			ip[cnt] = (byte)curr;
		}
	}

#if HWINTRINSICS
	[MethodImpl(MethodImplOptions.AggressiveOptimization)]
	private static (uint eql, uint eqr) dedupeLineAvx2(byte* pcurr, byte* pprev, byte* penc, nint cb, uint bg, bool blend)
	{
		byte* ip = pcurr, pp = pprev, op = penc;
		nint cnt = 0, end = cb - Vector256<byte>.Count;

		bool lfound = false;
		uint eql = 0u, eqr = 0u;
		var vbg = pp is null ? Vector256.Create(bg) : Vector256<uint>.Zero;
		var vmb = Vector256<uint>.Zero;
		if (blend)
			vmb = Avx2.CompareEqual(vmb, vmb);

		LoopTop:
		do
		{
			var vprev = pp is not null ? Avx.LoadVector256(pp + cnt).AsUInt32() : vbg;
			var vcurr = Avx.LoadVector256(ip + cnt).AsUInt32();

			var veq = Avx2.CompareEqual(vcurr, vprev);
			vcurr = Avx2.BlendVariable(vcurr, vbg, Avx2.And(veq, vmb));

			Avx.Store(op + cnt, vcurr.AsByte());
			cnt += Vector256<byte>.Count;

			uint msk = (uint)Avx2.MoveMask(veq.AsByte());
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
				msk = ~msk;
				if (!lfound)
				{
					eql += (uint)BitOperations.TrailingZeroCount(msk) / sizeof(uint);
					lfound = true;
				}

				eqr = (uint)BitOperations.LeadingZeroCount(msk) / sizeof(uint);
			}
		} while (cnt <= end);

		if (cnt < end + Vector256<byte>.Count)
		{
			uint offs = (uint)(cnt - end) / sizeof(uint);
			if (!lfound)
				eql -= offs;
			eqr -= offs;
			cnt = end;
			goto LoopTop;
		}

		return (eql, eqr);
	}

	[MethodImpl(MethodImplOptions.AggressiveOptimization)]
	private static (uint eql, uint eqr) dedupeLineSse2(byte* pcurr, byte* pprev, byte* penc, nint cb, uint bg, bool blend)
	{
		byte* ip = pcurr, pp = pprev, op = penc;
		nint cnt = 0, end = cb - Vector128<byte>.Count;

		bool lfound = false;
		uint eql = 0u, eqr = 0u;
		var vbg = pp is null ? Vector128.Create(bg) : Vector128<uint>.Zero;
		var vmb = Vector128<uint>.Zero;
		if (blend)
			vmb = Sse2.CompareEqual(vmb, vmb);

		LoopTop:
		do
		{
			var vprev = pp is not null ? Sse2.LoadVector128(pp + cnt).AsUInt32() : vbg;
			var vcurr = Sse2.LoadVector128(ip + cnt).AsUInt32();

			var veq = Sse2.CompareEqual(vcurr, vprev);
			vcurr = HWIntrinsics.BlendVariable(vcurr, vbg, Sse2.And(veq, vmb));

			Sse2.Store(op + cnt, vcurr.AsByte());
			cnt += Vector128<byte>.Count;

			uint msk = (uint)Sse2.MoveMask(veq.AsByte());
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
				msk = ~msk;
				if (!lfound)
				{
					eql += (uint)BitOperations.TrailingZeroCount(msk) / sizeof(uint);
					lfound = true;
				}

				eqr = (uint)BitOperations.LeadingZeroCount(msk) / sizeof(uint);
			}
		} while (cnt <= end);

		if (cnt < end + Vector128<byte>.Count)
		{
			uint offs = (uint)(cnt - end) / sizeof(uint);
			if (!lfound)
				eql -= offs;
			eqr -= offs;
			cnt = end;
			goto LoopTop;
		}

		return (eql, eqr);
	}
#endif

	private static (uint eql, uint eqr) dedupeLineScalar(byte* pcurr, byte* pprev, byte* penc, nint cb, uint bg, bool blend)
	{
		byte* ip = pcurr, pp = pprev, op = penc;
		nint end = cb;

		bool lfound = false;
		uint eql = 0u, eqr = 0u;
		if (pp is not null)
			bg = 0u;

		for (nint cnt = 0; cnt < end; cnt += sizeof(uint))
		{
			uint curr = *(uint*)(ip + cnt);
			bool peq = curr == (pp is not null ? *(uint*)(pp + cnt) : bg);
			*(uint*)(op + cnt) = (blend & peq) ? bg : curr;

			if (!peq)
			{
				lfound = true;
				eqr = 0u;
			}
			else
			{
				if (!lfound)
					eql++;

				eqr++;
			}
		}

		return (eql, eqr);
	}
}
