// Copyright © Clinton Ingram and Contributors.  Licensed under the MIT License.

using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Runtime.CompilerServices;

#if HWINTRINSICS
using System.Runtime.Intrinsics;
using System.Runtime.Intrinsics.X86;
#endif

namespace PhotoSauce.MagicScaler.Transforms
{
	internal sealed unsafe class IndexedColorTransform : ChainedPixelSource
	{
		private const int maxPaletteMapSize = 5376;  // max possible nodes at minLeafLevel = 8^3 + ... + 8^(minLeafLevel+1) + maxPaletteSize * (6 - minLeafLevel)
		private const int maxPaletteSize = 256;
		private const int channels = 4;
		private const uint minLeafLevel = 3;
		private const uint alphaThreshold = 85;

		private bool dither;
		private bool hasAlpha;
		private int paletteLength;
		private uint leafLevel;
		private uint mapNextFree;

		private RentedBuffer<byte> lineBuff;
		private RentedBuffer<uint> palBuff;
		private RentedBuffer<short> errBuff;
		private RentedBuffer<PaletteMapNode> mapBuff;

		public ReadOnlySpan<uint> Palette => palBuff.Span.Slice(isFixedGrey ? 0 : maxPaletteSize, paletteLength);

		public override PixelFormat Format => PixelFormat.Indexed8;

		private bool isFixedGrey => PrevSource.Format == PixelFormat.Grey8;
		private int paletteColors => paletteLength - (hasAlpha ? 1 : 0);

		public IndexedColorTransform(PixelSource source) : base(source)
		{
			if (isFixedGrey)
			{
				palBuff = BufferPool.Rent<uint>(maxPaletteSize);
				paletteLength = palBuff.Length;

				var palSpan = palBuff.Span;
				for (int i = 0; i < palSpan.Length; i++)
					palSpan[i] = (uint)i * 0x10101u | 0xff000000u;
			}
			else
			{
				var pfmt = PrevSource.Format;
				if (pfmt.ChannelCount != channels || pfmt.BytesPerPixel != channels || pfmt.ColorRepresentation != PixelColorRepresentation.Bgr)
					throw new NotSupportedException("Pixel format not supported.");

				if (PrevSource is not FrameBufferSource)
					lineBuff = BufferPool.Rent<byte>(BufferStride);

				palBuff = BufferPool.Rent<uint>(maxPaletteSize * 2, true);
				errBuff = BufferPool.RentAligned<short>((Width + 2) * channels, true);
				mapBuff = BufferPool.RentAligned<PaletteMapNode>(maxPaletteMapSize);
			}
		}

		public void SetPalette(ReadOnlySpan<uint> pal, bool isExact)
		{
			if (pal.Length < 1 || pal.Length > maxPaletteSize) throw new ArgumentException($"Palette must have between 1 and {maxPaletteSize} entries.", nameof(pal));

			pal.CopyTo(palBuff.Span);
			pal.CopyTo(palBuff.Span[maxPaletteSize..]);

			dither = !isExact;
			hasAlpha = pal[^1] < 0x00ffffffu;
			paletteLength = pal.Length;

			palBuff.Span[paletteLength..maxPaletteSize].Fill(MemoryMarshal.GetReference(pal));

			mapNextFree = seedPaletteMap(mapBuff.Span);
		}

		protected override void CopyPixelsInternal(in PixelArea prc, int cbStride, int cbBufferSize, byte* pbBuffer)
		{
			if (palBuff.IsEmpty) throw new ObjectDisposedException(nameof(IndexedColorTransform));

			if (isFixedGrey)
				copyPixelsDirect(prc, cbStride, cbBufferSize, pbBuffer);
			else
				copyPixelsBuffered(prc, cbStride, pbBuffer);
		}

		private unsafe void copyPixelsDirect(in PixelArea prc, int cbStride, int cbBufferSize, byte* pbBuffer)
		{
			Profiler.PauseTiming();
			PrevSource.CopyPixels(prc, cbStride, cbBufferSize, pbBuffer);
			Profiler.ResumeTiming();
		}

		private unsafe void copyPixelsBuffered(in PixelArea prc, nint cbStride, byte* pbBuffer)
		{
			if (paletteLength == 0) throw new InvalidOperationException("No palette has been set.");

			nint lstride = 0;
			var lspan = lineBuff.Span;
			if (PrevSource is FrameBufferSource fbuff)
			{
				lstride = fbuff.Stride;
				lspan = fbuff.Span;
			}

			fixed (byte* pimg = lspan)
			fixed (short* perr = errBuff)
			fixed (uint* ppal = palBuff, pilut = &LookupTables.OctreeIndexTable.GetDataRef())
			fixed (PaletteMapNode* ptree = mapBuff)
			{
				for (nint y = 0; y < prc.Height; y++)
				{
					byte* pline = pimg;
					if (lstride != 0)
					{
						pline = pimg + ((nint)(uint)prc.Y + y) * lstride + (nint)(uint)prc.X * channels;
					}
					else
					{
						Profiler.PauseTiming();
						PrevSource.CopyPixels(prc, lineBuff.Length, lineBuff.Length, pline);
						Profiler.ResumeTiming();
					}

					byte* poutline = pbBuffer + y * cbStride;
					short* perrline = perr + (nint)(uint)prc.X * channels + channels;

					if (!dither)
						remap(pline, poutline, pilut, ptree, ppal, prc.Width);
#if HWINTRINSICS
					else if (Sse41.IsSupported)
						remapDitherSse41(pline, perrline, poutline, pilut, ptree, ppal, prc.Width);
#endif
					else
						remapDitherScalar(pline, perrline, poutline, pilut, ptree, ppal, prc.Width);
				}
			}
		}

		public override void ReInit(PixelSource newSource)
		{
			Debug.Assert(newSource == PrevSource);

			errBuff.Span.Clear();
		}

		protected override void Dispose(bool disposing)
		{
			if (disposing)
			{
				lineBuff.Dispose();
				palBuff.Dispose();
				errBuff.Dispose();
				mapBuff.Dispose();

				lineBuff = default;
				palBuff = default;
				errBuff = default;
				mapBuff = default;
			}

			base.Dispose(disposing);
		}

		public override string ToString() => $"{nameof(IndexedColorTransform)}: {PrevSource.Format.Name}->{Format.Name}";

		private uint seedPaletteMap(Span<PaletteMapNode> palMap)
		{
			palMap.Clear();

			fixed (uint* ppal = palBuff, pilut = &LookupTables.OctreeIndexTable.GetDataRef())
			fixed (PaletteMapNode* pmap = palMap)
			{
				nuint nextFree = 512;
				nuint level = 6;
				nuint ll = minLeafLevel;

				for (nuint i = 0; i < (uint)paletteColors; i++)
				{
					nuint cpal = ppal[i];
					nuint idx = getNodeIndex(pilut, cpal);

					var node = pmap + (idx & 511);
					idx >>= 9;

					for (nuint l = 3; l <= level; l++)
					{
						if (l > ll)
							ll = l;

						nuint next = PaletteMapNode.GetChild(node, idx & 7);
						if (next == 0 && (l <= minLeafLevel || PaletteMapNode.HasChildren(node)))
						{
							next = nextFree++;
							PaletteMapNode.SetChild(node, idx & 7, next);

							if (l >= minLeafLevel)
							{
								node = pmap + next;
								idx >>= 3;
								PaletteMapNode.SetLeaf(node);
								break;
							}
						}

						node = pmap + next;
						idx >>= 3;

						if (l >= minLeafLevel && l < level && PaletteMapNode.IsLeaf(node))
						{
							if (PaletteMapNode.HasPaletteEntry(node, idx & 7))
							{
								nuint tcol = PaletteMapNode.GetPaletteIndex(node, idx & 7);
								nuint tpal = ppal[tcol];
								if (tpal == cpal)
									break;

								int leafShift = (int)((l + 1) * 3);
								var tnode = *node;
								*node = default;

								for (nuint j = 1; j < 8; j++)
								{
									if (PaletteMapNode.HasPaletteEntry(&tnode, (idx & 7) ^ j))
									{
										nuint scol = PaletteMapNode.GetPaletteIndex(&tnode, (idx & 7) ^ j);
										nuint spal = ppal[scol];
										nuint sidx = getNodeIndex(pilut, spal);
										sidx >>= leafShift;

										next = nextFree++;
										PaletteMapNode.SetChild(node, sidx & 7, next);

										var snode = pmap + next;
										sidx >>= 3;

										PaletteMapNode.SetLeaf(snode);
										PaletteMapNode.SetPaletteIndex(snode, sidx & 7, scol);
									}
								}

								nuint tidx = getNodeIndex(pilut, tpal);
								tidx >>= leafShift;

								do
								{
									next = nextFree++;
									PaletteMapNode.SetChild(node, idx & 7, next);

									node = pmap + next;
									idx  >>= 3;
									tidx >>= 3;
								}
								while (++l < level && (idx & 7) == (tidx & 7));
								if (l > ll)
									ll = l;

								PaletteMapNode.SetLeaf(node);
								PaletteMapNode.SetPaletteIndex(node, tidx & 7, tcol);
							}

							break;
						}
					}

					PaletteMapNode.SetPaletteIndex(node, idx & 7, i);
				}

				leafLevel = (uint)ll;
				return (uint)nextFree;
			}
		}

#if HWINTRINSICS
		private void remapDitherSse41(byte* pimage, short* perror, byte* pout, uint* pilut, PaletteMapNode* pmap, uint* ppal, nint cp)
		{
			nuint nextFree = mapNextFree;
			nuint maxcol = (uint)paletteColors - 1;
			nuint level = leafLevel;
			nuint pidx = maxcol;

			var vpmax = Vector128.Create((short)byte.MaxValue);
			var vprnd = Vector128.Create((short)7);
			var vzero = Vector128<short>.Zero;

			byte* ip = pimage, ipe = ip + cp * channels;
			byte* op = pout;
			short* ep = perror;

			var vppix = vzero;
			var vperr = vzero;
			var vnerr = vzero;

			do
			{
				if (ip[3] < alphaThreshold)
				{
					vppix = vzero;
					pidx = maxcol + 1;
					goto FoundExact;
				}

				var vpix = Sse41.ConvertToVector128Int16(Sse2.LoadScalarVector128((int*)ip).AsByte());
				var verr = Sse2.Add(Sse2.Add(vprnd, Sse2.LoadScalarVector128((long*)ep).AsInt16()), Sse2.Subtract(Sse2.ShiftLeftLogical(vnerr, 3), vnerr));
				vpix = Sse2.Add(vpix, Sse2.ShiftRightArithmetic(verr, 4));
				vpix = Sse2.Min(Sse2.Max(vpix, vzero), vpmax);

				var vpeq = Sse2.Xor(vppix, vpix);
				if (Sse41.TestZ(vpeq, vpeq))
					goto FoundExact;

				vppix = vpix;
				nuint idx =
					pilut[(nuint)Sse2.Extract(vppix.AsUInt16(), 0)      ] |
					pilut[(nuint)Sse2.Extract(vppix.AsUInt16(), 1) + 256] |
					pilut[(nuint)Sse2.Extract(vppix.AsUInt16(), 2) + 512];

				var node = pmap + (idx & 511);
				idx >>= 9;

				for (nuint l = 3; l <= level; l++)
				{
					nuint next = PaletteMapNode.GetChild(node, idx & 7);
					if (next == 0 && (l <= minLeafLevel || PaletteMapNode.HasChildren(node)))
					{
						next = nextFree++;
						PaletteMapNode.SetChild(node, idx & 7, next);

						if (l >= minLeafLevel)
						{
							node = pmap + next;
							idx >>= 3;
							PaletteMapNode.SetLeaf(node);
							break;
						}
					}

					node = pmap + next;
					idx >>= 3;

					if (l >= minLeafLevel && PaletteMapNode.IsLeaf(node))
						break;
				}

				pidx = getPaletteIndexSse41(ppal, node, idx & 7, vppix, maxcol);
				var vdiff = Sse2.Subtract(vppix, Sse41.ConvertToVector128Int16((byte*)(ppal + pidx)));

				Sse2.StoreScalar((long*)(ep - channels), Sse2.Add(vperr, Sse2.Add(vdiff, vdiff)).AsInt64());
				vperr = Sse2.Add(Sse2.ShiftLeftLogical(vdiff, 2), vnerr);
				vnerr = vdiff;
				goto Advance;

			FoundExact:
				Sse2.StoreScalar((long*)(ep - channels), vperr.AsInt64());
				vperr = vnerr;
				vnerr = vzero;

			Advance:
				ip += channels;
				ep += channels;
				*op++ = (byte)pidx;
			}
			while (ip < ipe);

			Sse2.StoreScalar((long*)(ep - channels), vperr.AsInt64());

			mapNextFree = (uint)nextFree;
		}
#endif

		private void remapDitherScalar(byte* pimage, short* perror, byte* pout, uint* pilut, PaletteMapNode* pmap, uint* ppal, nint cp)
		{
			nuint nextFree = mapNextFree;
			nuint maxcol = (uint)paletteColors - 1;
			nuint level = leafLevel;
			nuint pidx = maxcol;

			byte* ip = pimage, ipe = ip + cp * sizeof(uint);
			byte* op = pout;
			short* ep = perror;

			nuint ppix = 0;
			int perb = 0, perg = 0, perr = 0;
			int nerb = 0, nerg = 0, nerr = 0;

			do
			{
				if (ip[3] < alphaThreshold)
				{
					ppix = 0;
					pidx = maxcol + 1;
					goto FoundExact;
				}

				nuint cb = (uint)(((ip[0] << 4) + ep[0] + (nerb << 3) - nerb + 7).Clamp(0, 4095) >> 4);
				nuint cg = (uint)(((ip[1] << 4) + ep[1] + (nerg << 3) - nerg + 7).Clamp(0, 4095) >> 4);
				nuint cr = (uint)(((ip[2] << 4) + ep[2] + (nerr << 3) - nerr + 7).Clamp(0, 4095) >> 4);
				uint cpix = (uint)byte.MaxValue << 24 | (uint)cr << 16 | (uint)cg << 8 | (uint)cb;

				if (ppix == cpix)
					goto FoundExact;

				ppix = cpix;
				nuint idx = pilut[cb] | pilut[cg + 256] | pilut[cr + 512];

				var node = pmap + (idx & 511);
				idx >>= 9;

				for (nuint l = 3; l <= level; l++)
				{
					nuint next = PaletteMapNode.GetChild(node, idx & 7);
					if (next == 0 && (l <= minLeafLevel || PaletteMapNode.HasChildren(node)))
					{
						next = nextFree++;
						PaletteMapNode.SetChild(node, idx & 7, next);

						if (l >= minLeafLevel)
						{
							node = pmap + next;
							idx >>= 3;
							PaletteMapNode.SetLeaf(node);
							break;
						}
					}

					node = pmap + next;
					idx >>= 3;

					if (l >= minLeafLevel && PaletteMapNode.IsLeaf(node))
						break;
				}

				pidx = getPaletteIndex(ppal, node, idx & 7, ppix, maxcol);

				byte* pcol = (byte*)(ppal + pidx);
				int db = (byte)(ppix      ) - pcol[0];
				int dg = (byte)(ppix >>  8) - pcol[1];
				int dr = (byte)(ppix >> 16) - pcol[2];

				ep[-4] = (short)(perb + db + db);
				ep[-3] = (short)(perg + dg + dg);
				ep[-2] = (short)(perr + dr + dr);

				perb = (db << 2) + nerb;
				perg = (dg << 2) + nerg;
				perr = (dr << 2) + nerr;

				nerb = db;
				nerg = dg;
				nerr = dr;
				goto Advance;

			FoundExact:
				ep[-4] = (short)perb;
				ep[-3] = (short)perg;
				ep[-2] = (short)perr;

				perb = nerb;
				perg = nerg;
				perr = nerr;

				nerb = nerg = nerr = 0;

			Advance:
				ip += channels;
				ep += channels;
				*op++ = (byte)pidx;
			}
			while (ip < ipe);

			ep[-4] = (short)perb;
			ep[-3] = (short)perg;
			ep[-2] = (short)perr;

			mapNextFree = (uint)nextFree;
		}

		private void remap(byte* pimage, byte* pout, uint* pilut, PaletteMapNode* pmap, uint* ppal, nint cp)
		{
			nuint nextFree = mapNextFree;
			nuint maxcol = (uint)paletteColors - 1;
			nuint level = leafLevel;
			nuint pidx = maxcol;

			byte* ip = pimage, ipe = ip + cp * sizeof(uint);
			byte* op = pout;

			nuint ppix = 0;
			do
			{
				if (ip[3] < alphaThreshold)
				{
					ppix = 0;
					pidx = maxcol + 1;
					goto Found;
				}

				nuint cpix = *(uint*)ip;
				if (ppix == cpix)
					goto Found;

				ppix = cpix;
				nuint idx = getNodeIndex(pilut, ppix);

				var node = pmap + (idx & 511);
				idx >>= 9;

				for (nuint l = 3; l <= level; l++)
				{
					nuint next = PaletteMapNode.GetChild(node, idx & 7);
					if (next == 0 && (l <= minLeafLevel || PaletteMapNode.HasChildren(node)))
					{
						next = nextFree++;
						PaletteMapNode.SetChild(node, idx & 7, next);

						if (l >= minLeafLevel)
						{
							node = pmap + next;
							idx >>= 3;
							PaletteMapNode.SetLeaf(node);
							break;
						}
					}

					node = pmap + next;
					idx >>= 3;

					if (l >= minLeafLevel && PaletteMapNode.IsLeaf(node))
						break;
				}

				pidx = getPaletteIndex(ppal, node, idx & 7, ppix, maxcol);

			Found:
				ip += channels;
				*op++ = (byte)pidx;
			}
			while (ip < ipe);

			mapNextFree = (uint)nextFree;
		}

#if HWINTRINSICS
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		private static nuint getPaletteIndexSse41(uint* ppal, PaletteMapNode* node, nuint idx, Vector128<short> vpix, nuint maxidx)
		{
			if (PaletteMapNode.HasPaletteEntry(node, idx))
				return PaletteMapNode.GetPaletteIndex(node, idx);

			nuint pidx = findNearestColorSse41(ppal, maxidx, vpix);
			PaletteMapNode.SetPaletteIndex(node, idx, pidx);

			return pidx;
		}
#endif

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		private static nuint getPaletteIndex(uint* ppal, PaletteMapNode* node, nuint idx, nuint pix, nuint maxidx)
		{
			if (PaletteMapNode.HasPaletteEntry(node, idx))
				return PaletteMapNode.GetPaletteIndex(node, idx);

			nuint pidx;
#if HWINTRINSICS
			if (Sse41.IsSupported)
				pidx = findNearestColorSse41(ppal, maxidx, Sse41.ConvertToVector128Int16(Vector128.CreateScalarUnsafe((uint)pix).AsByte()));
			else
#endif
				pidx = findNearestColor(ppal, maxidx, (uint)pix);

			PaletteMapNode.SetPaletteIndex(node, idx, pidx);

			return pidx;
		}

#if HWINTRINSICS
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		private static nuint findNearestColorSse41(uint* ppal, nuint maxidx, Vector128<short> vpix)
		{
			Vector128<int> vdst;
			Vector128<uint> vidx;

			if (Avx2.IsSupported)
			{
				var wmul = HWIntrinsics.CreateVector256(0x0001_0000_fffful).AsInt16();
				var wadd = HWIntrinsics.CreateVector256(0x0040_0080_0060ul).AsInt16();

				var widm = HWIntrinsics.CreateVector256((ulong)maxidx).AsUInt32();
				var wdsm = HWIntrinsics.CreateVector256((ulong)int.MaxValue).AsInt32();
				var winc = HWIntrinsics.CreateVector256((ulong)Vector256<ulong>.Count).AsUInt32();
				var wcnt = Avx.LoadVector256(HWIntrinsics.IndicesUInt64.GetAddressOf()).AsUInt32();
				var widx = widm;
				var wdst = wdsm;

				var wppix = Avx2.BroadcastScalarToVector256(vpix.AsUInt64()).AsInt16();
				byte* pp = (byte*)ppal, ppe = (byte*)(ppal + maxidx);

				do
				{
					var vpal = Avx2.ConvertToVector256Int16(pp);
					pp += Vector256<short>.Count;

					var wavg = Avx2.ShiftRightArithmetic(Avx2.Average(vpal.AsUInt16(), wppix.AsUInt16()).AsInt16(), 3);
					wavg = Avx2.MultiplyLow(Avx2.ShuffleHigh(Avx2.ShuffleLow(wavg, HWIntrinsics.ShuffleMaskChan2), HWIntrinsics.ShuffleMaskChan2), wmul);

					var wdif = Avx2.Subtract(vpal, wppix);
					var wdip = Avx2.MultiplyAddAdjacent(wdif, Avx2.MultiplyLow(Avx2.Add(wavg, wadd), wdif));
					wdip = Avx2.Add(wdip, Avx2.Shuffle(wdip, HWIntrinsics.ShuffleMaskOddToEven));

					var wmsk = Avx2.CompareGreaterThan(wdst, wdip);
					widx = Avx2.BlendVariable(widx, wcnt, wmsk.AsUInt32());
					wdst = Avx2.BlendVariable(wdst, wdip, wmsk);

					wcnt = Avx2.Add(wcnt, winc);
				}
				while (pp <= ppe);

				wdst = Avx2.BlendVariable(wdst, wdsm, Avx2.CompareGreaterThan(widx.AsInt32(), widm.AsInt32()));

				var vmsk = Sse2.CompareGreaterThan(wdst.GetLower(), wdst.GetUpper());
				vidx = Sse41.BlendVariable(widx.GetLower(), widx.GetUpper(), vmsk.AsUInt32());
				vdst = Sse41.BlendVariable(wdst.GetLower(), wdst.GetUpper(), vmsk);
			}
			else
			{
				var vmul = HWIntrinsics.CreateVector128(0x0001_0000_fffful).AsInt16();
				var vadd = HWIntrinsics.CreateVector128(0x0040_0080_0060ul).AsInt16();

				vidx = HWIntrinsics.CreateVector128((ulong)maxidx).AsUInt32();
				vdst = HWIntrinsics.CreateVector128((ulong)int.MaxValue).AsInt32();
				var vinc = HWIntrinsics.CreateVector128((ulong)Vector128<ulong>.Count).AsUInt32();
				var vcnt = Sse2.LoadVector128(HWIntrinsics.IndicesUInt64.GetAddressOf()).AsUInt32();

				var vppix = Sse2.UnpackLow(vpix.AsUInt64(), vpix.AsUInt64()).AsInt16();
				byte* pp = (byte*)ppal, ppe = (byte*)(ppal + maxidx);

				do
				{
					var vpal = Sse41.ConvertToVector128Int16(pp);
					pp += Vector128<short>.Count;

					var vavg = Sse2.ShiftRightArithmetic(Sse2.Average(vpal.AsUInt16(), vppix.AsUInt16()).AsInt16(), 3);
					vavg = Sse2.MultiplyLow(Sse2.ShuffleHigh(Sse2.ShuffleLow(vavg, HWIntrinsics.ShuffleMaskChan2), HWIntrinsics.ShuffleMaskChan2), vmul);

					var vdif = Sse2.Subtract(vpal, vppix);
					var vdip = Sse2.MultiplyAddAdjacent(vdif, Sse2.MultiplyLow(Sse2.Add(vavg, vadd), vdif));
					vdip = Sse2.Add(vdip, Sse2.Shuffle(vdip, HWIntrinsics.ShuffleMaskOddToEven));

					var vmsk = Sse2.CompareGreaterThan(vdst, vdip);
					vidx = Sse41.BlendVariable(vidx, vcnt, vmsk.AsUInt32());
					vdst = Sse41.BlendVariable(vdst, vdip, vmsk);

					vcnt = Sse2.Add(vcnt, vinc);
				}
				while (pp <= ppe);
			}

			var vmsr = Sse2.CompareGreaterThan(vdst, Sse2.UnpackHigh(vdst.AsUInt64(), vdst.AsUInt64()).AsInt32()).AsUInt32();
			return Sse2.ConvertToUInt32(Sse41.BlendVariable(vidx, Sse2.UnpackHigh(vidx.AsUInt64(), vidx.AsUInt64()).AsUInt32(), vmsr));
		}
#endif

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		private static nuint findNearestColor(uint* ppal, nuint maxidx, uint color)
		{
			int dist = int.MaxValue;
			nuint pidx = maxidx;

			int cb = (byte)(color      );
			int cg = (byte)(color >>  8);
			int cr = (byte)(color >> 16);

			for (nuint i = 0; i <= maxidx; i++)
			{
				byte* pc = (byte*)(ppal + i);
				int db = pc[0] - cb;
				int dg = pc[1] - cg;
				int dr = pc[2] - cr;
				int rr = pc[2] + cr;

				int dd = dr * dr * (0x400 + rr) + dg * dg * 0x800 + db * db * (0x600 - rr);
				if (dd < dist)
				{
					dist = dd;
					pidx = i;
				}
			}

			return pidx;
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		private static nuint getNodeIndex(uint* plut, nuint bgr) =>
			plut[(nuint)(byte)(bgr      )      ] |
			plut[(nuint)(byte)(bgr >>  8) + 256] |
			plut[(nuint)(byte)(bgr >> 16) + 512];

		// TODO Check/log RyuJIT issue. All the static methods on the node structs should be instance methods or simply direct
		// accesses to the fixed fields, but the JIT currently inserts extraneous null checks on pointer deref when inlining them.
		// For the same reason, the fields are not used in the methods; we cast the node to the field type and offset manually.
		// Related: https://github.com/dotnet/runtime/issues/37727
		[StructLayout(LayoutKind.Explicit)]
		private struct PaletteMapNode
		{
			public const uint LeafMarker = uint.MaxValue;

			[FieldOffset(0)]
			public fixed ushort ChildNodes[8];
			[FieldOffset(0)]
			public fixed byte PaletteIndices[8];
			[FieldOffset(12)]
			public uint LeafValue;

			[MethodImpl(MethodImplOptions.AggressiveInlining)]
			public static bool HasChildren(PaletteMapNode* node)
			{
#if HWINTRINSICS
				if (Sse41.IsSupported)
				{
					var vc = Sse2.LoadVector128((ushort*)node);

#pragma warning disable IDE0075 // https://github.com/dotnet/runtime/issues/4207
					return Sse41.TestZ(vc, vc) ? false : true;
#pragma warning restore IDE0075
				}
#endif

				nuint* children = (nuint*)node;
				if (children[0] != 0) return true;
				if (children[1] != 0) return true;
				if (sizeof(nuint) == sizeof(uint))
				{
					if (children[2] != 0) return true;
					if (children[3] != 0) return true;
				}

				return false;
			}

			[MethodImpl(MethodImplOptions.AggressiveInlining)]
			public static nuint GetChild(PaletteMapNode* node, nuint idx)
			{
				return *((ushort*)node + idx);
			}

			[MethodImpl(MethodImplOptions.AggressiveInlining)]
			public static void SetChild(PaletteMapNode* node, nuint idx, nuint child)
			{
				*((ushort*)node + idx) = (ushort)child;
			}

			[MethodImpl(MethodImplOptions.AggressiveInlining)]
			public static bool IsLeaf(PaletteMapNode* node)
			{
				return *((uint*)node + 3) == LeafMarker;
			}

			[MethodImpl(MethodImplOptions.AggressiveInlining)]
			public static void SetLeaf(PaletteMapNode* node)
			{
				*((uint*)node + 3) = LeafMarker;
			}

			[MethodImpl(MethodImplOptions.AggressiveInlining)]
			public static bool HasPaletteEntry(PaletteMapNode* node, nuint idx)
			{
#pragma warning disable IDE0075 // https://github.com/dotnet/runtime/issues/4207
				return ((1u << (int)(nint)idx) & *((byte*)node + 8)) != 0 ? true : false;
#pragma warning restore IDE0075
			}

			[MethodImpl(MethodImplOptions.AggressiveInlining)]
			public static nuint GetPaletteIndex(PaletteMapNode* node, nuint idx)
			{
				return *((byte*)node + idx);
			}

			[MethodImpl(MethodImplOptions.AggressiveInlining)]
			public static void SetPaletteIndex(PaletteMapNode* node, nuint idx, nuint pidx)
			{
				*((byte*)node + idx) = (byte)pidx;
				*((byte*)node + 8) |= (byte)(1 << (int)idx);
			}
		}
	}
}
