// Copyright © Clinton Ingram and Contributors.  Licensed under the MIT License.

using System;
using System.Numerics;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Runtime.CompilerServices;

#if HWINTRINSICS
using System.Runtime.Intrinsics;
using System.Runtime.Intrinsics.X86;
#endif

namespace PhotoSauce.MagicScaler
{
	internal unsafe class OctreeQuantizer : IDisposable
	{
		private const int maxHistogramSize = 8191;   // max possible nodes with 3 bits saved to stuff level into one of the indices
		private const int maxPaletteMapSize = 5448;  // max possible nodes at minLeafLevel = 8^0 + 8^1 + ... + 8^(minLeafLevel+1) + maxPaletteSize * (6 - minLeafLevel)
		private const int maxPaletteSize = 256;
		private const int maxSamples = 1 << 22;
		private const int minLeafLevel = 3;
		private const uint alphaThreshold = 85;
		private const uint transparentValue = 0x00ff00ffu;
		private const uint paletteIndexUnset = byte.MaxValue;

		private uint leafLevel = 7;
		private bool isPaletteExact;
		private int paletteLength;

		private RentedBuffer<uint> palBuffer;

		public ReadOnlySpan<uint> Palette => palBuffer.Span.Slice(0, paletteLength);

		public OctreeQuantizer() => palBuffer = BufferPool.Rent<uint>(maxPaletteSize, true);

		public void CreatePalette(Span<byte> image, nint width, nint height, nint stride)
		{
			float subsampleRatio = 1f;

			int csamp = (int)width * (int)height;
			if (csamp > maxSamples)
				subsampleRatio = (float)csamp / maxSamples;

			using var nodeBuffer = BufferPool.RentLocalAligned<HistogramNode>(maxHistogramSize, true);
			using var listBuffer = BufferPool.RentLocal<ushort>(maxHistogramSize);
			initFreeList(listBuffer.Span);

			fixed (byte* pimage = image)
			fixed (uint* pilut = &LookupTables.OctreeIndexTable[0])
			fixed (ushort* pfree = listBuffer.Span)
			fixed (HistogramNode* ptree = nodeBuffer.Span)
			{
				ushort* pnextFree = pfree;
				float yf = 0f;
				for (nint y = 0; y < height; yf += subsampleRatio, y = (nint)yf)
				{
					uint* pline = (uint*)(pimage + y * stride);
					updateHistogram(pline, pilut, ptree, pfree, ref pnextFree, width);
				}
			}

			buildPalette(nodeBuffer.Span, subsampleRatio > 1f);
		}

		public void Quantize(Span<byte> image, Span<byte> outbuff, nint width, nint height, nint instride, nint outstride)
		{
			if (palBuffer.Length == 0) throw new ObjectDisposedException(nameof(OctreeQuantizer));
			if (paletteLength == 0) throw new InvalidOperationException("No palette has been created.");

			leafLevel = Math.Min(leafLevel, 6);
			bool dither = !isPaletteExact;

			using var palMap = BufferPool.RentLocalAligned<PaletteMapNode>(maxPaletteMapSize, true);
			nuint nextFree = 8;
			seedPaletteMap(palMap.Span, ref nextFree);

			using var errbuff = BufferPool.RentLocalAligned<byte>(((int)width + 2) * sizeof(int) * 4, true);
			fixed (byte* pimage = image, poutbuff = outbuff, perrbuff = errbuff.Span)
			fixed (uint* ppal = palBuffer, pilut = &LookupTables.OctreeIndexTable[0])
			fixed (PaletteMapNode* ptree = palMap.Span)
			{
				for (nint y = 0; y < height; y++)
				{
					byte* pline = pimage + y * instride;
					byte* poutline = poutbuff + y * outstride;
					int* perrline = (int*)perrbuff + 4;

					if (!dither)
						remap(pline, poutline, pilut, ptree, ppal, ref nextFree, width);
#if HWINTRINSICS
					else if (Sse41.IsSupported)
						remapDitherSse41(pline, perrline, poutline, pilut, ptree, ppal, ref nextFree, width);
#endif
					else
						remapDitherScalar(pline, perrline, poutline, pilut, ptree, ppal, ref nextFree, width);
				}
			}
		}

		public void Dispose()
		{
			palBuffer.Dispose();
			palBuffer = default;
		}

		private void updateHistogram(uint* pimage, uint* pilut, HistogramNode* ptree, ushort* plist, ref ushort* pfree, nint cp)
		{
			uint* ip = pimage, ipe = ip + cp;
			nuint level = leafLevel;

			nuint ppix = 0;
			var pnode = default(HistogramNode*);
			do
			{
				if (((byte*)ip)[3] < alphaThreshold)
				{
					ip++;
					continue;
				}

				nuint cpix = *ip++;
				var node = pnode;
				if (ppix == cpix)
					goto Accumulate;

				ppix = cpix;
				nuint idx = getNodeIndex(pilut, ppix);

				node = ptree + (idx & 7);
				for (nuint i = 1; i <= level; i++)
				{
					idx >>= 3;

					nuint next = HistogramNode.GetChild(node, idx & 7);
					if (next == 0)
					{
						if (*pfree == 0)
						{
							// TODO Log RyuJIT issue. None of these conditions will ever be true. Referencing these locals stops the JIT
							// from spilling them on each inner and outer loop iteration; instead it spills only in this (unlikely) block.
							// Related: https://github.com/dotnet/runtime/issues/43318
							if (i == ppix || idx == ppix || node is null)
								break;

							pfree = plist;
							pruneTree(ptree, pfree);

							level = leafLevel;
							if (i > level)
								break;
						}

						next = *pfree++;
						HistogramNode.SetChild(node, idx & 7, next);
						HistogramNode.SetLevel(node, (uint)i - 1);
						HistogramNode.SetLevel(ptree + next, (uint)i);
					}

					node = ptree + next;
				}

				pnode = node;

				Accumulate:
				HistogramNode.AddSample(node, (uint)ppix);
			}
			while (ip < ipe);
		}

		private void pruneTree(HistogramNode* ptree, ushort* pfree)
		{
#if HWINTRINSICS
			var vsmsk = Unsafe.As<byte, Vector128<uint>>(ref MemoryMarshal.GetReference(HistogramNode.SumsMask));
			var vzero = Vector128<uint>.Zero;
#endif

			ushort* pnext = pfree;
			uint level = --leafLevel;

			var tnode = default(HistogramNode);
			HistogramNode.SetLevel(&tnode, level);

			for (nuint i = 8; i < maxHistogramSize; i++)
			{
				var node = ptree + i;
				if (HistogramNode.GetLevel(node) != level)
					continue;

#if HWINTRINSICS
				if (Sse2.IsSupported)
				{
					var vsums = Sse2.LoadVector128((uint*)&tnode);

					for (nuint j = 0; j < 8; j++)
					{
						nuint child = HistogramNode.GetChild(node, j);
						if (child == 0)
							continue;

						uint* csums = (uint*)(ptree + child);
						var vcsum = Sse2.And(vsmsk, Sse2.LoadVector128(csums));
						vsums = Sse2.Add(vsums, vcsum);

						Sse2.Store(csums, vzero);
						*pnext++ = (ushort)child;
					}

					Sse2.Store((uint*)node, vsums);
				}
				else
#endif
				{
					tnode = default;
					HistogramNode.SetLevel(&tnode, level);

					uint* sums = (uint*)&tnode;
					for (nuint j = 0; j < 8; j++)
					{
						nuint child = HistogramNode.GetChild(node, j);
						if (child == 0)
							continue;

						var cnode = ptree + child;
						uint* csums = (uint*)cnode;

						sums[0] += csums[0];
						sums[1] += csums[1];
						sums[2] += csums[2];
						sums[3] += csums[3] & HistogramNode.CountMask;

						*cnode = default;
						*pnext++ = (ushort)child;
					}

					*node = tnode;
				}
			}

			*pnext = 0;
		}

		private void convertNodes(HistogramNode* ptree, float* igt, HistogramNode* node, uint currLevel, uint pruneLevel, float ftpix)
		{
			if (currLevel == leafLevel)
			{
				uint* sums = (uint*)node;
				float* fsums = (float*)sums;

				uint pixcnt = sums[3] & HistogramNode.CountMask;
				uint rnd = pixcnt >> 1;
				float weight = (int)pixcnt / ftpix;

				fsums[0] = igt[(sums[0] + rnd) / pixcnt] * weight;
				fsums[1] = igt[(sums[1] + rnd) / pixcnt] * weight;
				fsums[2] = igt[(sums[2] + rnd) / pixcnt] * weight;
				fsums[3] = weight;
			}
			else
			{
				for (nuint i = 0; i < 8; i++)
				{
					nuint child = HistogramNode.GetChild(node, i);
					if (child != 0)
						convertNodes(ptree, igt, ptree + child, currLevel + 1, pruneLevel, ftpix);
				}

				if (currLevel >= pruneLevel)
				{
					var tnode = default(HistogramNode);
					for (nuint i = 0; i < 8; i++)
					{
						nuint child = HistogramNode.GetChild(node, i);
						if (child == 0)
							continue;

						var cnode = ptree + child;
						float* csums = (float*)cnode;
						float* sums = (float*)&tnode;

						if (Vector.IsHardwareAccelerated)
						{
							Unsafe.WriteUnaligned(sums, Unsafe.ReadUnaligned<Vector4>(sums) + Unsafe.ReadUnaligned<Vector4>(csums));
						}
						else
						{
							sums[0] += csums[0];
							sums[1] += csums[1];
							sums[2] += csums[2];
							sums[3] += csums[3];
						}

						*cnode =default;
					}

					*node = tnode;
				}
			}
		}

		private void addReducibleNodes(HistogramNode* ptree, ReducibleNode* preduce, float* gt, HistogramNode* node, ref nuint reducibleCount, uint currLevel, uint pruneLevel)
		{
			for (nuint i = 0; i < 8; i++)
			{
				nuint child = HistogramNode.GetChild(node, i);
				if (child == 0)
					continue;

				var cnode = ptree + child;

				if (currLevel == pruneLevel)
				{
					float* csums = (float*)cnode;
					float weight = csums[3];

					for (nuint j = 1; j < 8; j++)
					{
						nuint sibling = HistogramNode.GetChild(node, i ^ j);
						if (sibling == 0)
							continue;

						float* ssums = (float*)(ptree + sibling);
						float sweight = ssums[3];
						if (sweight > weight || (sweight == weight && (i ^ j) > i))
						{
							sweight = 1f / sweight;
							float iweight = 1f / weight;

							float cr = lutLerp(gt, csums[2] * iweight);
							float sr = lutLerp(gt, ssums[2] * sweight);
							float rr = (cr + sr) * 0.5f;

							float db = lutLerp(gt, csums[0] * iweight) - lutLerp(gt, ssums[0] * sweight);
							float dg = lutLerp(gt, csums[1] * iweight) - lutLerp(gt, ssums[1] * sweight);
							float dr = cr - sr;

							float dd = ((2f + rr) * dr * dr + 4f * dg * dg + (3f - rr) * db * db).Sqrt();
							weight *= dd * 4f;

							preduce[reducibleCount++] = new ReducibleNode(weight, (ushort)(node - ptree), (ushort)i);
							break;
						}
					}
				}
				else
				{
					addReducibleNodes(ptree, preduce, gt, cnode, ref reducibleCount, currLevel + 1, pruneLevel);
				}
			}
		}

		private static void finalReduce(Span<HistogramNode> nodeBuffer, Span<ReducibleNode> nodes)
		{
			fixed (HistogramNode* ptree = nodeBuffer)
			{
				for (int i = 0; i < nodes.Length; i++)
				{
					var cand = nodes[i];
					var node = ptree + cand.Parent;
					nuint pos = cand.Index;

					var cnode = ptree + HistogramNode.GetChild(node, pos);

					for (nuint j = 1; j < 8; j++)
					{
						nuint sibling = HistogramNode.GetChild(node, pos ^ j);
						if (sibling == 0)
							continue;

						float* csums = (float*)cnode;
						float* ssums = (float*)(ptree + sibling);

						if (Vector.IsHardwareAccelerated)
						{
							Unsafe.WriteUnaligned(ssums, Unsafe.ReadUnaligned<Vector4>(ssums) + Unsafe.ReadUnaligned<Vector4>((float*)cnode));
						}
						{
							ssums[0] += csums[0];
							ssums[1] += csums[1];
							ssums[2] += csums[2];
							ssums[3] += csums[3];
						}

						*cnode = default;
						HistogramNode.SetChild(node, pos, 0);
						break;
					}
				}
			}
		}

		private void buildPalette(Span<HistogramNode> nodeBuffer, bool isSubsampled)
		{
			var nc = (Span<int>)stackalloc int[] { 0, 0, 0, 0, 0, 0, 0, 0 };
			getNodeCounts(nodeBuffer, nc);

			uint level = leafLevel;
			int targetColors = maxPaletteSize - getReservedCount(nodeBuffer, isSubsampled);
			for (uint i = 1; i < leafLevel; i++)
			{
				if (nc[(int)i] > targetColors)
				{
					level = i;
					break;
				}
			}

			fixed (HistogramNode* ptree = nodeBuffer)
			fixed (float* igt = &LookupTables.SrgbInverseGamma[0])
			{
				float ftpix = getPixelCount(nodeBuffer, leafLevel);
				for (nuint i = 0; i < 8; i++)
					convertNodes(ptree, igt, ptree + i, 0, level, ftpix);

				leafLevel = level;
			}

			int histogramColors = nc[(int)leafLevel];
			isPaletteExact = !isSubsampled && histogramColors <= targetColors;

			if (!isPaletteExact)
			{
				using var listBuffer = BufferPool.RentLocal<ReducibleNode>(histogramColors);
				nuint reducibleCount = 0;

				fixed (HistogramNode* ptree = nodeBuffer)
				fixed (ReducibleNode* pweights = listBuffer.Span)
				fixed (float* gt = &LookupTables.SrgbGamma[0])
				{
					for (nuint i = 0; i < 8; i++)
						addReducibleNodes(ptree, pweights, gt, ptree + i, ref reducibleCount, 0, leafLevel - 1);
				}

				var weights = listBuffer.Span.Slice(0, (int)reducibleCount);
				int reduceCount = histogramColors - targetColors;

#if SPAN_SORT
				weights.Sort();
				finalReduce(nodeBuffer, weights.Slice(0, reduceCount));
#else
				using var buff = BufferPool.RentLocalArray<ReducibleNode>(weights.Length);
				weights.CopyTo(buff.Array);

				Array.Sort(buff.Array, 0, weights.Length);
				finalReduce(nodeBuffer, buff.Array.AsSpan(0, reduceCount));
#endif
			}

			makePalette(nodeBuffer, isSubsampled);
		}

		private void makePalette(Span<HistogramNode> nodeBuffer, bool isSubsampled)
		{
			fixed (HistogramNode* ptree = nodeBuffer)
			fixed (uint* ppal = palBuffer)
			fixed (byte* gt = &LookupTables.SrgbGammaUQ15[0])
			{
				nuint palidx = 0;
				for (nuint i = 0; i < 8; i++)
					populatePalette(ptree, gt, ppal, ptree + i, leafLevel, 0, ref palidx);

				if (isSubsampled)
				{
					for (nuint i = 0; i < 8; i++)
					{
						var node = ptree + i;
						for (nuint j = 0; j < 8; j++)
						{
							if (HistogramNode.GetChild(node, j) == 0)
							{
								uint b = (((uint)i & 1) << 1 | ((uint)j & 1)     ) * 0x55;
								uint g = (((uint)i & 4) >> 1 | ((uint)j & 4) >> 2) * 0x55;
								uint r = (((uint)i & 2)      | ((uint)j & 2) >> 1) * 0x55;

								ppal[palidx++] = (uint)byte.MaxValue << 24 | r << 16 | g << 8 | b;
							}
						}
					}
				}

				ppal[palidx] = transparentValue;
				paletteLength = (int)palidx + 1;
			}

			leafLevel = Math.Max(leafLevel, minLeafLevel);
		}

		private void populatePalette(HistogramNode* ptree, byte* gt, uint* ppal, HistogramNode* node, nuint minLevel, nuint currLevel, ref nuint nidx)
		{
			if (currLevel == leafLevel)
			{
				float* sums = (float*)node;
				float weight = 1f / sums[3];
				uint b = gt[(nuint)MathUtil.FixToUQ15One(sums[0] * weight)];
				uint g = gt[(nuint)MathUtil.FixToUQ15One(sums[1] * weight)];
				uint r = gt[(nuint)MathUtil.FixToUQ15One(sums[2] * weight)];

				ppal[nidx++] = (uint)byte.MaxValue << 24 | r << 16 | g << 8 | b;
			}
			else
			{
				for (nuint i = 0; i < 8; i++)
				{
					nuint child = HistogramNode.GetChild(node, i);
					if (child != 0)
						populatePalette(ptree, gt, ppal, ptree + child, minLevel, currLevel + 1, ref nidx);
				}
			}
		}

		private void seedPaletteMap(Span<PaletteMapNode> palMap, ref nuint nextFree)
		{
			fixed (uint* ppal = palBuffer, pilut = &LookupTables.OctreeIndexTable[0])
			fixed (PaletteMapNode* pmap = palMap)
			{
				nuint level = leafLevel;

				for (nuint i = 0; i < (nuint)(paletteLength - 1); i++)
				{
					nuint cpal = ppal[i];
					nuint idx = getNodeIndex(pilut, cpal);

					var node = pmap + (idx & 7);
					idx >>= 3;

					for (nuint l = 1; l <= level; l++)
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

						if (l >= minLeafLevel && l < level && PaletteMapNode.IsLeaf(node))
						{
							nuint tcol = PaletteMapNode.GetPaletteIndex(node, idx & 7);
							if (tcol != paletteIndexUnset)
							{
								nuint tpal = ppal[tcol];
								if (tpal == cpal)
									break;

								int leafShift = (int)((l + 1) * 3);
								var tnode = *node;
								*node = default;

								for (nuint j = 1; j < 8; j++)
								{
									nuint scol = PaletteMapNode.GetPaletteIndex(&tnode, (idx & 7) ^ j);
									if (scol != paletteIndexUnset)
									{
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

								PaletteMapNode.SetLeaf(node);
								PaletteMapNode.SetPaletteIndex(node, tidx & 7, tcol);
							}

							break;
						}
					}

					PaletteMapNode.SetPaletteIndex(node, idx & 7, i);
				}
			}
		}

#if HWINTRINSICS
		private void remapDitherSse41(byte* pimage, int* perror, byte* pout, uint* pilut, PaletteMapNode* pmap, uint* ppal, ref nuint nextFree, nint cp)
		{
			Debug.Assert(paletteLength == maxPaletteSize);

			const byte shuffleMaskRed = 0b_10_10_10_10;

			nuint level = leafLevel;
			nuint trans = (uint)paletteLength - 1;
			nuint pidx = trans;

			ppal[trans] = ppal[trans - 1];

			var vpmax = Vector128.Create((short)byte.MaxValue);
			var vprnd = Vector128.Create((short)7);
			var vzero = Vector128<short>.Zero;

			byte* ip = pimage, ipe = ip + cp * sizeof(uint);
			byte* op = pout;
			short* ep = (short*)perror;

			var vppix = vzero;
			var vperr = vzero;
			var vnerr = vzero;

			do
			{
				Vector128<short> vpix, vdiff;
				if (ip[3] < alphaThreshold)
				{
					vppix = vzero;
					vdiff = vzero;
					pidx = trans;
					goto FoundExact;
				}

				vpix = Sse41.ConvertToVector128Int16(Sse2.LoadScalarVector128((int*)ip).AsByte());

				var verr = Sse2.Add(Sse2.Add(vprnd, Sse2.LoadVector128(ep)), Sse2.Subtract(Sse2.ShiftLeftLogical(vnerr, 3), vnerr));
				vpix = Sse2.Add(vpix, Sse2.ShiftRightArithmetic(verr, 4));
				vpix = Sse2.Min(vpix, vpmax);
				vpix = Sse2.Max(vpix, vzero);

				if (Sse2.MoveMask(Sse2.CompareEqual(vppix, vpix).AsByte()) == ushort.MaxValue)
				{
					vdiff = vzero;
					goto FoundExact;
				}

				vppix = vpix;
				nuint idx =
					pilut[(nuint)Sse2.Extract(vppix.AsUInt16(), 0)      ] |
					pilut[(nuint)Sse2.Extract(vppix.AsUInt16(), 1) + 256] |
					pilut[(nuint)Sse2.Extract(vppix.AsUInt16(), 2) + 512];

				var node = pmap + (idx & 7);
				idx >>= 3;

				for (nuint l = 1; l <= level; l++)
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

				idx &= 7;
				pidx = PaletteMapNode.GetPaletteIndex(node, idx);
				if (pidx == paletteIndexUnset)
				{
					Vector128<int> vdst;
					Vector128<uint> vidx;

					if (Avx2.IsSupported)
					{
						var wmul = Vector256.Create(0x0001_0000_fffful).AsInt16();
						var wadd = Vector256.Create(0x0040_0080_0060ul).AsInt16();

						var winc = Vector256.Create((ulong)Vector256<ulong>.Count).AsUInt32();
						var wcnt = Vector256.Create(0ul, 1ul, 2ul, 3ul).AsUInt32();
						var widx = Vector256.Create((ulong)(trans - 1)).AsUInt32();
						var wdst = Vector256.Create((ulong)int.MaxValue).AsInt32();

						var wppix = Avx2.BroadcastScalarToVector256(vppix.AsUInt64()).AsInt16();
						byte* pp = (byte*)ppal, ppe = pp + trans * sizeof(uint);

						do
						{
							var vpal = Avx2.ConvertToVector256Int16(pp);
							pp += Vector256<short>.Count;

							var wavg = Avx2.ShiftRightArithmetic(Avx2.Average(vpal.AsUInt16(), wppix.AsUInt16()).AsInt16(), 3);
							wavg = Avx2.MultiplyLow(Avx2.ShuffleHigh(Avx2.ShuffleLow(wavg, shuffleMaskRed), shuffleMaskRed), wmul);

							var wdif = Avx2.Subtract(vpal, wppix);
							var wdip = Avx2.MultiplyAddAdjacent(wdif, Avx2.MultiplyLow(Avx2.Add(wavg, wadd), wdif));
							wdip = Avx2.Add(wdip, Avx2.Shuffle(wdip, HWIntrinsics.ShuffleMaskOddToEven));

							var wmsk = Avx2.CompareGreaterThan(wdst, wdip);
							widx = Avx2.BlendVariable(widx.AsByte(), wcnt.AsByte(), wmsk.AsByte()).AsUInt32();
							wdst = Avx2.BlendVariable(wdst.AsByte(), wdip.AsByte(), wmsk.AsByte()).AsInt32();

							wcnt = Avx2.Add(wcnt, winc);
						}
						while (pp < ppe);

						var vmsk = Sse2.CompareGreaterThan(wdst.GetLower(), wdst.GetUpper());
						vidx = Sse41.BlendVariable(widx.GetLower().AsByte(), widx.GetUpper().AsByte(), vmsk.AsByte()).AsUInt32();
						vdst = Sse41.BlendVariable(wdst.GetLower().AsByte(), wdst.GetUpper().AsByte(), vmsk.AsByte()).AsInt32();
					}
					else
					{
						var vmul = Vector128.Create(0x0001_0000_fffful).AsInt16();
						var vadd = Vector128.Create(0x0040_0080_0060ul).AsInt16();

						var vinc = Vector128.Create((ulong)Vector128<ulong>.Count).AsUInt32();
						var vcnt = Vector128.Create(0ul, 1ul).AsUInt32();
						vidx = Vector128.Create((ulong)(trans - 1)).AsUInt32();
						vdst = Vector128.Create((ulong)int.MaxValue).AsInt32();

						vppix = Sse2.UnpackLow(vppix.AsUInt64(), vppix.AsUInt64()).AsInt16();
						byte* pp = (byte*)ppal, ppe = pp + trans * sizeof(uint);

						do
						{
							var vpal = Sse41.ConvertToVector128Int16(pp);
							pp += Vector128<short>.Count;

							var vavg = Sse2.ShiftRightArithmetic(Sse2.Average(vpal.AsUInt16(), vppix.AsUInt16()).AsInt16(), 3);
							vavg = Sse2.MultiplyLow(Sse2.ShuffleHigh(Sse2.ShuffleLow(vavg, shuffleMaskRed), shuffleMaskRed), vmul);

							var vdif = Sse2.Subtract(vpal, vppix);
							var vdip = Sse2.MultiplyAddAdjacent(vdif, Sse2.MultiplyLow(Sse2.Add(vavg, vadd), vdif));
							vdip = Sse2.Add(vdip, Sse2.Shuffle(vdip, HWIntrinsics.ShuffleMaskOddToEven));

							var vmsk = Sse2.CompareGreaterThan(vdst, vdip);
							vidx = Sse41.BlendVariable(vidx.AsByte(), vcnt.AsByte(), vmsk.AsByte()).AsUInt32();
							vdst = Sse41.BlendVariable(vdst.AsByte(), vdip.AsByte(), vmsk.AsByte()).AsInt32();

							vcnt = Sse2.Add(vcnt, vinc);
						}
						while (pp < ppe);

						vppix = Sse2.UnpackLow(vppix.AsUInt64(), vzero.AsUInt64()).AsInt16();
					}

					pidx = Sse41.Extract(vdst, 2) < Sse2.ConvertToInt32(vdst) ? Sse41.Extract(vidx, 2) : Sse2.ConvertToUInt32(vidx);
					PaletteMapNode.SetPaletteIndex(node, idx, pidx);
				}

				vdiff = Sse2.Subtract(vppix, Sse41.ConvertToVector128Int16((byte*)(ppal + pidx)));

				FoundExact:
				ip += sizeof(uint);
				*op++ = (byte)pidx;

				Sse2.Store(ep - Vector128<short>.Count, Sse2.Add(vperr, Sse2.Add(vdiff, vdiff)));
				ep += Vector128<short>.Count;

				vperr = Sse2.Add(Sse2.ShiftLeftLogical(vdiff, 2), vnerr);
				vnerr = vdiff;
			}
			while (ip < ipe);

			Sse2.Store(ep - Vector128<short>.Count, vperr);

			ppal[trans] = transparentValue;
		}
#endif

		private void remapDitherScalar(byte* pimage, int* perror, byte* pout, uint* pilut, PaletteMapNode* pmap, uint* ppal, ref nuint nextFree, nint cp)
		{
			nuint level = leafLevel;
			nuint trans = (uint)paletteLength - 1;
			nuint pidx = trans;

			byte* ip = pimage, ipe = ip + cp * sizeof(uint);
			byte* op = pout;
			int* ep = perror;

			nuint ppix = 0;
			int perb = 0, perg = 0, perr = 0;
			int nerb = 0, nerg = 0, nerr = 0;

			do
			{
				int db, dg, dr;
				if (ip[3] < alphaThreshold)
				{
					ppix = 0;
					db = dg = dr = 0;
					pidx = trans;
					goto FoundExact;
				}

				nuint cb = (uint)(((ip[0] << 4) + ep[0] + (nerb << 3) - nerb + 7).Clamp(0, 4095) >> 4);
				nuint cg = (uint)(((ip[1] << 4) + ep[1] + (nerg << 3) - nerg + 7).Clamp(0, 4095) >> 4);
				nuint cr = (uint)(((ip[2] << 4) + ep[2] + (nerr << 3) - nerr + 7).Clamp(0, 4095) >> 4);
				uint cpix = (uint)byte.MaxValue << 24 | (uint)cr << 16 | (uint)cg << 8 | (uint)cb;

				if (ppix == cpix)
				{
					db = dg = dr = 0;
					goto FoundExact;
				}

				ppix = cpix;
				nuint idx = pilut[cb] | pilut[cg + 256] | pilut[cr + 512];

				var node = pmap + (idx & 7);
				idx >>= 3;

				for (nuint l = 1; l <= level; l++)
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

				idx &= 7;
				pidx = PaletteMapNode.GetPaletteIndex(node, idx);
				if (pidx == paletteIndexUnset)
				{
					pidx = findNearestColor(ppal, (uint)ppix);
					PaletteMapNode.SetPaletteIndex(node, idx, pidx);
				}

				byte* pcol = (byte*)(ppal + pidx);
				db = (byte)(ppix      ) - pcol[0];
				dg = (byte)(ppix >>  8) - pcol[1];
				dr = (byte)(ppix >> 16) - pcol[2];

				FoundExact:
				ip += sizeof(uint);
				*op++ = (byte)pidx;

				ep[-4] = perb + db + db;
				ep[-3] = perg + dg + dg;
				ep[-2] = perr + dr + dr;
				ep += 4;

				perb = (db << 2) + nerb;
				perg = (dg << 2) + nerg;
				perr = (dr << 2) + nerr;

				nerb = db;
				nerg = dg;
				nerr = dr;
			}
			while (ip < ipe);

			ep[-4] = perb;
			ep[-3] = perg;
			ep[-2] = perr;
		}

		private void remap(byte* pimage, byte* pout, uint* pilut, PaletteMapNode* pmap, uint* ppal, ref nuint nextFree, nint cp)
		{
			nuint level = leafLevel;
			nuint trans = (uint)paletteLength - 1;
			nuint pidx = trans;

			byte* ip = pimage, ipe = ip + cp * sizeof(uint);
			byte* op = pout;

			nuint ppix = 0;
			do
			{
				if (ip[3] < alphaThreshold)
				{
					ppix = 0;
					pidx = trans;
					goto Found;
				}

				nuint cpix = *(uint*)ip;
				if (ppix == cpix)
					goto Found;

				ppix = cpix;
				nuint idx = getNodeIndex(pilut, ppix);

				var node = pmap + (idx & 7);
				idx >>= 3;

				for (nuint l = 1; l <= level; l++)
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

				idx &= 7;
				pidx = PaletteMapNode.GetPaletteIndex(node, idx);
				if (pidx == paletteIndexUnset)
				{
					pidx = findNearestColor(ppal, (uint)ppix);
					PaletteMapNode.SetPaletteIndex(node, idx, pidx);
				}

				Found:
				ip += sizeof(uint);
				*op++ = (byte)pidx;
			}
			while (ip < ipe);
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		private uint findNearestColor(uint* ppal, uint color)
		{
			int dist = int.MaxValue;
			uint pidx = (uint)paletteLength - 1;
			nuint plen = pidx;

			int cb = (byte)(color      );
			int cg = (byte)(color >>  8);
			int cr = (byte)(color >> 16);

			for (nuint i = 0; i < plen; i++)
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
					pidx = (uint)i;
				}
			}

			return pidx;
		}

		private static void getNodeCounts(Span<HistogramNode> nodeBuffer, Span<int> counts)
		{
			fixed (HistogramNode* ptree = nodeBuffer)
			fixed (int* pcounts = counts)
			{
				for (nuint i = 0; i < maxHistogramSize; i++)
				{
					nuint level = HistogramNode.GetLevel(ptree + i);
					pcounts[level]++;
				}

				pcounts[0] = 8;
			}
		}

		private static int getReservedCount(Span<HistogramNode> nodeBuffer, bool isSubsampled)
		{
			if (!isSubsampled)
				return 1;

			fixed (HistogramNode* ptree = nodeBuffer)
			{
				int reserved = 1;
				for (nuint i = 0; i < 8; i++)
				{
					var node = ptree + i;
					if (!HistogramNode.HasChildren(node))
					{
						reserved += 8;
					}
					else
					{
						for (nuint j = 0; j < 8; j++)
						{
							if (HistogramNode.GetChild(node, j) == 0)
								reserved++;
						}
					}
				}

				return reserved;
			}
		}

		private static int getPixelCount(Span<HistogramNode> nodeBuffer, uint leafLevel)
		{
			fixed (HistogramNode* ptree = nodeBuffer)
			{
				uint count = 0;
				for (nuint i = 0; i < maxHistogramSize; i++)
				{
					uint level = HistogramNode.GetLevel(ptree + i);
					if (level == leafLevel)
					{
						uint* sums = (uint*)(ptree + i);
						count += sums[3] & HistogramNode.CountMask;
					}
				}

				return (int)count;
			}
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		private static float lutLerp(float* gt, float val)
		{
			nuint ival = (uint)(int)val;

			return MathUtil.Lerp(gt[ival], gt[ival + 1], val - (int)ival);
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		private static nuint getNodeIndex(uint* plut, nuint bgr) =>
			plut[(nuint)(byte)(bgr      )      ] |
			plut[(nuint)(byte)(bgr >>  8) + 256] |
			plut[(nuint)(byte)(bgr >> 16) + 512];

		private static void initFreeList(Span<ushort> listBuff)
		{
			const int reserveNodes = 8;
			nint maxFree = listBuff.Length - reserveNodes;

			ref byte listStart = ref Unsafe.As<ushort, byte>(ref listBuff[0]), listPtr = ref listStart;
			ref byte listEnd = ref Unsafe.Add(ref listStart, maxFree * sizeof(ushort));

			if (Vector.IsHardwareAccelerated && maxFree > Vector<ushort>.Count)
			{
				var slots = (ReadOnlySpan<byte>)(new byte[] {
					 8, 0,  9, 0, 10, 0, 11, 0, 12, 0, 13, 0, 14, 0, 15, 0,
					16, 0, 17, 0, 18, 0, 19, 0, 20, 0, 21, 0, 22, 0, 23, 0
				});
				var vslot = Unsafe.ReadUnaligned<Vector<ushort>>(ref MemoryMarshal.GetReference(slots));
				var vincr = new Vector<ushort>((ushort)Vector<ushort>.Count);

				do
				{
					Unsafe.WriteUnaligned(ref listPtr, vslot);
					listPtr = ref Unsafe.Add(ref listPtr, Unsafe.SizeOf<Vector<ushort>>());
					vslot += vincr;
				}
				while (!Unsafe.IsAddressGreaterThan(ref listPtr, ref Unsafe.Subtract(ref listEnd, Unsafe.SizeOf<Vector<ushort>>())));
			}

			uint islot = (uint)Unsafe.ByteOffset(ref listStart, ref listPtr) / sizeof(ushort) + reserveNodes;
			islot |= islot + 1 << 16;

			while (!Unsafe.IsAddressGreaterThan(ref listPtr, ref Unsafe.Subtract(ref listEnd, sizeof(uint))))
			{
				Unsafe.WriteUnaligned(ref listPtr, islot);
				listPtr = ref Unsafe.Add(ref listPtr, sizeof(uint));
				islot += 0x00020002;
			}

			if (Unsafe.IsAddressLessThan(ref listPtr, ref listEnd))
			{
				Unsafe.WriteUnaligned(ref listPtr, (ushort)islot);
				listPtr = ref Unsafe.Add(ref listPtr, sizeof(ushort));
			}

			Unsafe.InitBlockUnaligned(ref listPtr, 0, reserveNodes * sizeof(ushort));
		}

		// TODO Check/log RyuJIT issue. All the static methods on the node structs should be instance methods or simply direct
		// accesses to the fixed fields, but the JIT currently inserts extraneous null checks on pointer deref when inlining them.
		// For the same reason, the fields are not used in the methods; we cast the node to the field type and offset manually.
		// Related: https://github.com/dotnet/runtime/issues/37727
		[StructLayout(LayoutKind.Explicit)]
		private struct HistogramNode
		{
			public const uint LevelMask = 7u << 29;
			public const uint CountMask = ~LevelMask;
			public const ushort ChildMask = (ushort)(CountMask >> 16);

			[FieldOffset(0)]
			public fixed ushort ChildNodes[8];
			[FieldOffset(0)]
			public fixed uint IntSums[4];
			[FieldOffset(0)]
			public fixed uint FloatSums[4];

			public static ReadOnlySpan<byte> SumsMask => new byte[] { 0xff, 0xff, 0xff, 0xff, 0xff, 0xff, 0xff, 0xff, 0xff, 0xff, 0xff, 0xff, 0xff, 0xff, 0xff, 0x1f };

			[MethodImpl(MethodImplOptions.AggressiveInlining)]
			public static void AddSample(HistogramNode* node, uint bgr)
			{
#if HWINTRINSICS
				if (Sse41.IsSupported)
				{
					bgr &= 0x00ffffff;
					bgr |= 0x01000000;
					var vbgr = Sse41.ConvertToVector128Int32(Sse2.ConvertScalarToVector128UInt32(bgr).AsByte()).AsUInt32();
					Sse2.Store((uint*)node, Sse2.Add(vbgr, Sse2.LoadVector128((uint*)node)));
				}
				else
#endif
				{
					uint* sums = (uint*)node;
					sums[0] += (byte)(bgr      );
					sums[1] += (byte)(bgr >>  8);
					sums[2] += (byte)(bgr >> 16);
					sums[3]++;
				}
			}

			[MethodImpl(MethodImplOptions.AggressiveInlining)]
			public static bool HasChildren(HistogramNode* node)
			{
				ushort* children = (ushort*)node;

#if HWINTRINSICS
				if (Sse2.IsSupported)
				{
					var vmsk = Unsafe.As<byte, Vector128<ushort>>(ref MemoryMarshal.GetReference(SumsMask));
					var veq = Sse2.CompareEqual(Vector128<ushort>.Zero, Sse2.And(vmsk, Sse2.LoadVector128(children)));

#pragma warning disable IDE0075 // https://github.com/dotnet/runtime/issues/4207
					return Sse2.MoveMask(veq.AsByte()) == ushort.MaxValue ? false : true;
#pragma warning restore IDE0075
				}
#endif

				for (nuint i = 0; i < 8; i++)
				{
					if (((uint)children[i] & ChildMask) != 0)
						return true;
				}

				return false;
			}

			[MethodImpl(MethodImplOptions.AggressiveInlining)]
			public static uint GetLevel(HistogramNode* node)
			{
				return (*((uint*)node + 3) & LevelMask) >> 29;
			}

			[MethodImpl(MethodImplOptions.AggressiveInlining)]
			public static void SetLevel(HistogramNode* node, uint level)
			{
				*((uint*)node + 3) |= level << 29;
			}

			[MethodImpl(MethodImplOptions.AggressiveInlining)]
			public static nuint GetChild(HistogramNode* node, nuint idx)
			{
				return (uint)*((ushort*)node + idx) & ChildMask;
			}

			[MethodImpl(MethodImplOptions.AggressiveInlining)]
			public static void SetChild(HistogramNode* node, nuint idx, nuint child)
			{
				*((ushort*)node + idx) = (ushort)child;
			}
		}

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
				ushort* children = (ushort*)node;

#if HWINTRINSICS
				if (Sse2.IsSupported)
				{
					var veq = Sse2.CompareEqual(Vector128<ushort>.Zero, Sse2.LoadVector128(children));

#pragma warning disable IDE0075 // https://github.com/dotnet/runtime/issues/4207
					return Sse2.MoveMask(veq.AsByte()) == ushort.MaxValue ? false : true;
#pragma warning restore IDE0075
				}
#endif

				for (nuint i = 0; i < 8; i++)
				{
					if (children[i] != 0)
						return true;
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
				Unsafe.InitBlockUnaligned(node, byte.MaxValue, (uint)sizeof(PaletteMapNode));
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
			}
		}

		private struct ReducibleNode : IComparable<ReducibleNode>, IEquatable<ReducibleNode>
		{
			public float Weight;
			public ushort Parent;
			public ushort Index;

			public ReducibleNode(float weight, ushort parent, ushort index) => (Weight, Parent, Index) = (weight, parent, index);

			int IComparable<ReducibleNode>.CompareTo(ReducibleNode other) => Weight.CompareTo(other.Weight);
			bool IEquatable<ReducibleNode>.Equals(ReducibleNode other) => Parent == other.Parent && Index == other.Index;
		}
	}
}
