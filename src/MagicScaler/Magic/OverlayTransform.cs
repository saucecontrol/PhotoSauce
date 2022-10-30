// Copyright © Clinton Ingram and Contributors.  Licensed under the MIT License.

using System;
using System.Runtime.InteropServices;
using System.Runtime.CompilerServices;
using System.Diagnostics.CodeAnalysis;

#if HWINTRINSICS
using System.Runtime.Intrinsics;
using System.Runtime.Intrinsics.X86;
#endif

using static PhotoSauce.MagicScaler.MathUtil;

namespace PhotoSauce.MagicScaler.Transforms;

internal sealed class OverlayTransform : ChainedPixelSource
{
	const int bytesPerPixel = 4;

	private PixelSource overSource;
	private int offsX, offsY;
	private bool directCopy, blendOver;

	public override bool Passthrough => false;

	public OverlayTransform(PixelSource source, PixelSource over, int left, int top, bool alpha, AlphaBlendMethod blend) : base(source)
	{
		if (Format.NumericRepresentation != PixelNumericRepresentation.UnsignedInteger || Format.ChannelCount != bytesPerPixel || Format.BytesPerPixel != bytesPerPixel)
			throw new NotSupportedException("Pixel format not supported.");

		SetOver(over, left, top, alpha, blend);
	}

	[MemberNotNull(nameof(overSource))]
	public void SetOver(PixelSource over, int left, int top, bool alpha, AlphaBlendMethod blend)
	{
		if (over.Format != Format)
			throw new NotSupportedException("Sources must be same pixel format.");

		overSource = over;
		(offsX, offsY) = (left, top);

		directCopy = !alpha || blend == AlphaBlendMethod.Source;
		blendOver = blend == AlphaBlendMethod.Over;
	}

	protected override unsafe void CopyPixelsInternal(in PixelArea prc, int cbStride, int cbBufferSize, byte* pbBuffer)
	{
		bool copyBase = true;
		if (PrevSource is FrameBufferSource fbuff)
		{
			ref byte baseref = ref MemoryMarshal.GetReference(fbuff.Span);
			copyBase = Unsafe.IsAddressLessThan(ref *pbBuffer, ref baseref) || Unsafe.IsAddressGreaterThan(ref *pbBuffer, ref Unsafe.Add(ref baseref, fbuff.Span.Length));
		}

		var inner = new PixelArea(offsX, offsY, overSource.Width, overSource.Height);

		int tx = Math.Max(prc.X - inner.X, 0);
		int tw = Math.Min(prc.Width, Math.Min(Math.Max(prc.X + prc.Width - inner.X, 0), inner.Width - tx));
		int cx = Math.Max(inner.X - prc.X, 0);

		for (int y = 0; y < prc.Height; y++)
		{
			int cy = prc.Y + y;

			if (copyBase)
			{
				Profiler.PauseTiming();
				PrevSource.CopyPixels(new PixelArea(prc.X, cy, prc.Width, 1), cbStride, cbBufferSize, pbBuffer);
				Profiler.ResumeTiming();
			}

			if (tw > 0 && cy >= inner.Y && cy < inner.Y + inner.Height)
			{
				var area = new PixelArea(tx, cy - inner.Y, tw, 1);
				byte* ptr = pbBuffer + cx * bytesPerPixel;

				if (directCopy)
					copyPixelsDirect(area, cbStride, cbBufferSize, ptr);
				else
					copyPixelsBuffered(area, ptr);
			}

			pbBuffer += cbStride;
		}
	}

	private unsafe void copyPixelsDirect(in PixelArea prc, int cbStride, int cbBufferSize, byte* pbBuffer)
	{
		Profiler.PauseTiming();
		overSource.CopyPixels(prc, cbStride, cbBufferSize, pbBuffer);
		Profiler.ResumeTiming();
	}

	private unsafe void copyPixelsBuffered(in PixelArea prc, byte* pbBuffer)
	{
		using var lineBuff = BufferPool.RentLocalAligned<byte>(overSource.Width * bytesPerPixel);
		var buffspan = lineBuff.Span;

		fixed (byte* buff = buffspan)
		{
			Profiler.PauseTiming();
			overSource.CopyPixels(prc, buffspan.Length, buffspan.Length, buff);
			Profiler.ResumeTiming();

			uint* ip = (uint*)buff, ipe = ip + prc.Width;
			uint* op = (uint*)pbBuffer;

#if HWINTRINSICS
			if (HWIntrinsics.IsSupported && prc.Width >= HWIntrinsics.VectorCount<uint>() && (blendOver || noAlphaBlend(ip, ipe)))
			{
				copyPixelsIntrinsic(ip, ipe, op);
				return;
			}
#endif

			if (!blendOver)
			{
				copyPixelsAlphaBlend(ip, ipe, op);
				return;
			}

			while (ip < ipe)
			{
				uint i = *ip++;
				if ((int)i < 0)
					*op = i;

				op++;
			}
		}
	}

#if HWINTRINSICS
	private static unsafe bool noAlphaBlend(uint* ip, uint* ipe)
	{
		if (Avx2.IsSupported)
		{
			var vadd = Vector256.Create(0x01000000).AsByte();
			var vmsk = Vector256.Create(0xfe000000).AsByte();
			var vacc = Vector256<byte>.Zero;
			ipe -= Vector256<uint>.Count;

			LoopTop:
			do
			{
				var vi = Avx2.Add(vadd, Avx.LoadVector256(ip).AsByte());
				ip += Vector256<uint>.Count;

				vacc = Avx2.Or(vacc, Avx2.And(vmsk, vi));
			}
			while (ip <= ipe);

			if (ip < ipe + Vector256<uint>.Count)
			{
				nuint offs = UnsafeUtil.ByteOffset(ipe, ip);
				ip = UnsafeUtil.SubtractOffset(ip, offs);
				goto LoopTop;
			}

			return Avx.TestZ(vacc, vacc);
		}
		else
		{
			var vadd = Vector128.Create(0x01000000).AsByte();
			var vmsk = Vector128.Create(0xfe000000).AsByte();
			var vacc = Vector128<byte>.Zero;
			ipe -= Vector128<uint>.Count;

			LoopTop:
			do
			{
				var vi = Sse2.Add(vadd, Sse2.LoadVector128(ip).AsByte());
				ip += Vector128<uint>.Count;

				vacc = Sse2.Or(vacc, Sse2.And(vmsk, vi));
			}
			while (ip <= ipe);

			if (ip < ipe + Vector128<uint>.Count)
			{
				nuint offs = UnsafeUtil.ByteOffset(ipe, ip);
				ip = UnsafeUtil.SubtractOffset(ip, offs);
				goto LoopTop;
			}

			return HWIntrinsics.IsZero(vacc);
		}
	}

	private static unsafe void copyPixelsIntrinsic(uint* ip, uint* ipe, uint* op)
	{
		if (Avx2.IsSupported)
		{
			var vzero = Vector256<int>.Zero;
			ipe -= Vector256<uint>.Count;

			LoopTop:
			do
			{
				var vi = Avx.LoadVector256(ip);
				ip += Vector256<uint>.Count;

				var vm = Avx2.CompareGreaterThan(vzero, vi.AsInt32()).AsUInt32();
				var vo = Avx2.BlendVariable(Avx.LoadVector256(op), vi, vm);

				Avx.Store(op, vo);
				op += Vector256<uint>.Count;
			}
			while (ip <= ipe);

			if (ip < ipe + Vector256<uint>.Count)
			{
				nuint offs = UnsafeUtil.ByteOffset(ipe, ip);
				ip = UnsafeUtil.SubtractOffset(ip, offs);
				op = UnsafeUtil.SubtractOffset(op, offs);
				goto LoopTop;
			}
		}
		else
		{
			var vzero = Vector128<int>.Zero;
			ipe -= Vector128<uint>.Count;

			LoopTop:
			do
			{
				var vi = Sse2.LoadVector128(ip);
				ip += Vector128<uint>.Count;

				var vm = Sse2.CompareGreaterThan(vzero, vi.AsInt32()).AsUInt32();
				var vo = HWIntrinsics.BlendVariable(Sse2.LoadVector128(op), vi, vm);

				Sse2.Store(op, vo);
				op += Vector128<uint>.Count;
			}
			while (ip <= ipe);

			if (ip < ipe + Vector128<uint>.Count)
			{
				nuint offs = UnsafeUtil.ByteOffset(ipe, ip);
				ip = UnsafeUtil.SubtractOffset(ip, offs);
				op = UnsafeUtil.SubtractOffset(op, offs);
				goto LoopTop;
			}
		}
	}
#endif

	private unsafe void copyPixelsAlphaBlend(uint* ip, uint* ipe, uint* op)
	{
		const uint maxalpha = byte.MaxValue;

		fixed (ushort* igtstart = &LookupTables.SrgbInverseGammaUQ15.GetDataRef())
		fixed (byte* gtstart = &LookupTables.SrgbGammaUQ15.GetDataRef())
		{
			byte* gt = gtstart;
			ushort* igt = igtstart;

			while (ip < ipe)
			{
				byte alpha = ((byte*)ip)[3];
				if (alpha == maxalpha)
				{
					*op = *ip;
				}
				else if (alpha != 0)
				{
					uint ia = Fix15(alpha);
					uint ib = igt[(nuint)((byte*)ip)[0]];
					uint ig = igt[(nuint)((byte*)ip)[1]];
					uint ir = igt[(nuint)((byte*)ip)[2]];

					uint ob = igt[(nuint)((byte*)op)[0]];
					uint og = igt[(nuint)((byte*)op)[1]];
					uint or = igt[(nuint)((byte*)op)[2]];
					uint oa = Fix15(((byte*)op)[3]);

					uint ma = UnFix15(oa * (UQ15One - ia));
					ib = UnFix15(ib * ia + ob * ma);
					ig = UnFix15(ig * ia + og * ma);
					ir = UnFix15(ir * ia + or * ma);
					ia += ma;

					uint fa = UQ15One * UQ15One / ia;
					ib = UnFix15(ib * fa);
					ig = UnFix15(ig * fa);
					ir = UnFix15(ir * fa);

					ib = ClampToUQ15One(ib);
					ig = ClampToUQ15One(ig);
					ir = ClampToUQ15One(ir);

					((byte*)op)[0] = gt[ib];
					((byte*)op)[1] = gt[ig];
					((byte*)op)[2] = gt[ir];
					((byte*)op)[3] = UnFix15ToByte(ia * maxalpha);
				}
				ip++;
				op++;
			}
		}
	}

	protected override void Dispose(bool disposing)
	{
		if (disposing)
			overSource.Dispose();

		base.Dispose(disposing);
	}

	public override string ToString() => nameof(OverlayTransform);
}
