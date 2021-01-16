// Copyright © Clinton Ingram and Contributors.  Licensed under the MIT License.

using System;
using System.Numerics;
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
		private const int alphaThreshold = 85;
		private const int maxHistogramSize = 8191;
		private const int maxPaletteSize = 256;
		private const int maxSamples = 1 << 20;
		private const int minLeafLevel = 3;

		private uint leafLevel = 7;
		private bool isSubsampled;
		private int palEntries = maxPaletteSize;

		private ArraySegment<byte> nodeBuffer, palBuffer;

		public ReadOnlySpan<uint> Palette => MemoryMarshal.Cast<byte, uint>(palBuffer).Slice(0, palEntries);

		public OctreeQuantizer()
		{
			nodeBuffer = BufferPool.Rent(Unsafe.SizeOf<OctreeNode>() * maxHistogramSize, true);
			palBuffer = BufferPool.Rent(sizeof(uint) * maxPaletteSize);
		}

		public void CreateHistorgram(Span<byte> image, nint width, nint height, nint stride)
		{
			nint srx = 1;
			float sry = 1f;

			int csamp = (int)width * (int)height;
			if (csamp > maxSamples)
			{
				float sr = (float)csamp / maxSamples;
				srx = sr.Log2().Clamp(1, 8);
				sry = sr / srx;
				isSubsampled = true;
			}

			var listBuffer = BufferPool.Rent(sizeof(ushort) * maxHistogramSize);
			initFreeList(listBuffer, maxHistogramSize);
			nodeBuffer.AsSpan().Clear();

			fixed (byte* pimage = image)
			fixed (uint* pilut = &LookupTables.OctreeIndexTable[0])
			fixed (ushort* pfree = MemoryMarshal.Cast<byte, ushort>(listBuffer))
			fixed (OctreeNode* ptree = MemoryMarshal.Cast<byte, OctreeNode>(nodeBuffer))
			{
				ushort* pnextFree = pfree;
				float yf = 0f;
				for (nint y = 0; y < height; yf += sry, y = (nint)yf)
				{
					uint* pline = (uint*)(pimage + y * stride);
					updateHistogram(pline, pilut, ptree, pfree, ref pnextFree, width, srx);
				}
			}

			BufferPool.Return(listBuffer);
		}

		public void Quantize(Span<byte> image, Span<byte> outbuff, nint width, nint height, nint instride, nint outstride)
		{
			var nc = (Span<int>)stackalloc int[] { 0, 0, 0, 0, 0, 0, 0, 0 };
			getNodeCounts(nc);

			uint level = leafLevel;
			int targetColors = maxPaletteSize - getReservedCount();
			for (uint i = 1; i < leafLevel; i++)
			{
				if (nc[(int)i] > targetColors)
				{
					level = i;
					break;
				}
			}

			fixed (OctreeNode* ptree = MemoryMarshal.Cast<byte, OctreeNode>(nodeBuffer))
			fixed (float* igt = &LookupTables.SrgbInverseGamma[0])
			{
				float ftpix = getPixelCount();
				for (nuint i = 0; i < 8; i++)
					convertNodes(ptree, igt, ptree + i, 0, level, ftpix);

				leafLevel = level;
			}

			int histogramColors = nc[(int)level];
			if (histogramColors > targetColors)
			{
				var listBuffer = BufferPool.Rent(sizeof(ReducibleNode) * histogramColors);
				nuint reducibleCount = 0;

				fixed (OctreeNode* ptree = MemoryMarshal.Cast<byte, OctreeNode>(nodeBuffer))
				fixed (ReducibleNode* pweights = MemoryMarshal.Cast<byte, ReducibleNode>(listBuffer))
				fixed (float* gt = &LookupTables.SrgbGamma[0])
				{
					for (nuint i = 0; i < 8; i++)
						addReducibleNodes(ptree, pweights, gt, ptree + i, ref reducibleCount, 0, leafLevel - 1);
				}

				var weights = MemoryMarshal.Cast<byte, ReducibleNode>(listBuffer).Slice(0, (int)reducibleCount);
				int reduceCount = histogramColors - targetColors;

#if SPAN_SORT
				weights.Sort();
				finalReduce(weights.Slice(0, reduceCount));
#else
				using var buff = new PoolArray<ReducibleNode>(maxHistogramSize);
				weights.CopyTo(buff.Array);

				Array.Sort(buff.Array, 0, weights.Length);
				finalReduce(buff.Array.AsSpan(0, reduceCount));
#endif
			}

			bool dither = isSubsampled || histogramColors > maxPaletteSize;
			nuint nextFree = makePaletteMap(level);

			var pbuff = BufferPool.Rent(((int)width + 2) * 16, true);
			pbuff.AsSpan().Clear();

			fixed (byte* pimage = image, poutbuff = outbuff, perrbuff = pbuff.AsSpan())
			fixed (uint* pilut = &LookupTables.OctreeIndexTable[0])
			fixed (uint* ppal = MemoryMarshal.Cast<byte, uint>(palBuffer))
			fixed (OctreeNode* ptree = MemoryMarshal.Cast<byte, OctreeNode>(nodeBuffer))
			{
				for (nint y = 0; y < height; y++)
				{
					byte* pline = pimage + y * instride;
					byte* poutline = poutbuff + y * outstride;
					int* perrline = (int*)perrbuff + 4;

					if (!dither)
						remap(pline, poutline, pilut, ptree, ppal, ref nextFree, width);
#if HWINTRINSICS
					else if (Sse2.IsSupported)
						remapDitherSse2(pline, perrline, poutline, pilut, ptree, ppal, ref nextFree, width);
#endif
					else
						remapDitherScalar(pline, perrline, poutline, pilut, ptree, ppal, ref nextFree, width);
				}
			}

			BufferPool.Return(pbuff);
		}

		public void Dispose()
		{
			BufferPool.Return(nodeBuffer);
			BufferPool.Return(palBuffer);
			nodeBuffer = palBuffer = default;
		}

		private void updateHistogram(uint* pimage, uint* pilut, OctreeNode* ptree, ushort* plist, ref ushort* pfree, nint cp, nint sr)
		{
			nuint level = leafLevel;
			nuint prnod = 0;
			uint ppix = 0;

			uint* ip = pimage, ipe = ip + cp;
			for (; ip < ipe; ip += sr)
			{
				uint cpix = *ip;
				if (cpix == 0)
					continue;

				OctreeNode* pnode;
				if (ppix == cpix & prnod != 0)
				{
					pnode = ptree + prnod;
					goto Accumulate;
				}

				ppix = cpix;
				nuint idx =
					pilut[(nuint)(byte)(ppix      )      ] |
					pilut[(nuint)(byte)(ppix >>  8) + 256] |
					pilut[(nuint)(byte)(ppix >> 16) + 512];
				nuint parent = idx & 7;

				pnode = ptree + parent;
				for (nuint i = 1; i <= level; i++)
				{
					idx >>= 3;

					ushort* childloc = (ushort*)pnode + (idx & 7);
					nuint next = *childloc;
					if (next == 0)
					{
						if (*pfree == 0)
						{
							pfree = plist;
							pruneTree(ptree, pfree);

							level = leafLevel;
							prnod = 0;
							if (i > level)
								break;
						}

						next = *pfree++;
						*childloc = (ushort)next;

						OctreeNode.SetLevel(ptree + next, (uint)i);
					}

					parent = next;
					pnode = ptree + parent;
				}

				prnod = parent;

				Accumulate:
				OctreeNode.AddSample(pnode, ppix);
			}
		}

		private void pruneTree(OctreeNode* ptree, ushort* pfree)
		{
#if HWINTRINSICS
			var sumsMask = Vector128.Create(0xffffffffu, 0xffffffffu, 0xffffffffu, 0x1fffffffu);
			var vzero = Vector128<uint>.Zero;
#endif

			ushort* pnext = pfree;
			uint level = --leafLevel;

			for (nuint i = 8; i < maxHistogramSize; i++)
			{
				var node = ptree + i;
				uint nl = OctreeNode.GetLevel(node);
				if (nl == level)
				{
					ushort* children = (ushort*)node;
					uint* sums = (uint*)(children + 8);

#if HWINTRINSICS
					if (Sse2.IsSupported)
					{
						var vsums = Sse2.LoadVector128(sums);

						for (nuint j = 0; j < 8; j++)
						{
							nuint child = children[j];
							if (child != 0)
							{
								var cnode = ptree + child;
								uint* csums = (uint*)((ushort*)cnode + 8);

								var vcsum = Sse2.And(sumsMask, Sse2.LoadVector128(csums));
								vsums = Sse2.Add(vsums, vcsum);

								Sse2.Store((uint*)cnode, vzero);
								Sse2.Store(csums, vzero);
								*pnext++ = (ushort)child;
							}
						}

						Sse2.Store((uint*)children, vzero);
						Sse2.Store(sums, vsums);
					}
					else
#endif
					{
						for (nuint j = 0; j < 8; j++)
						{
							nuint child = children[j];
							if (child != 0)
							{
								var cnode = ptree + child;
								uint* csums = (uint*)((ushort*)cnode + 8);

								sums[0] += csums[0];
								sums[1] += csums[1];
								sums[2] += csums[2];
								sums[3] += csums[3] & 0x1fffffff;

								Unsafe.InitBlockUnaligned(cnode, 0, (uint)sizeof(OctreeNode));
								*pnext++ = (ushort)child;
							}
						}

						Unsafe.InitBlockUnaligned(children, 0, sizeof(ushort) * 8);
					}
				}
			}

			*pnext = 0;
		}

		private void convertNodes(OctreeNode* ptree, float* igt, OctreeNode* node, uint currLevel, uint pruneLevel, float ftpix)
		{
			if (currLevel == leafLevel)
			{
				uint* sums = (uint*)((ushort*)node + 8);
				float* fsums = (float*)sums;

				uint pixcnt = sums[3] & 0x1fffffff;
				uint rnd = pixcnt >> 1;
				float weight = (int)pixcnt / ftpix;

				fsums[0] = igt[(sums[0] + rnd) / pixcnt] * weight;
				fsums[1] = igt[(sums[1] + rnd) / pixcnt] * weight;
				fsums[2] = igt[(sums[2] + rnd) / pixcnt] * weight;
				fsums[3] = weight;
			}
			else
			{
				ushort* children = (ushort*)node;
				for (nuint i = 0; i < 8; i++)
				{
					nuint child = children[i];
					if (child != 0)
						convertNodes(ptree, igt, ptree + child, currLevel + 1, pruneLevel, ftpix);
				}

				float* sums = (float*)(children + 8);
				Unsafe.InitBlockUnaligned(sums, 0, sizeof(uint) * 4);

				if (currLevel >= pruneLevel)
				{
					for (nuint i = 0; i < 8; i++)
					{
						nuint child = children[i];
						if (child != 0)
						{
							var cnode = ptree + child;
							float* csums = (float*)((ushort*)cnode + 8);

#if HWINTRINSICS
							if (Sse.IsSupported)
							{
								Sse.Store(sums, Sse.Add(Sse.LoadVector128(sums), Sse.LoadVector128(csums)));
							}
							else
#endif
							{
								sums[0] += csums[0];
								sums[1] += csums[1];
								sums[2] += csums[2];
								sums[3] += csums[3];
							}

							Unsafe.InitBlockUnaligned(cnode, 0, (uint)sizeof(OctreeNode));
						}
					}

					Unsafe.InitBlockUnaligned(children, 0, sizeof(ushort) * 8);
				}
			}
		}

		private void finalReduce(Span<ReducibleNode> nodes)
		{
			fixed (OctreeNode* ptree = MemoryMarshal.Cast<byte, OctreeNode>(nodeBuffer))
			{
				for (int i = 0; i < nodes.Length; i++)
				{
					var cand = nodes[i];
					var par = ptree + cand.Parent;
					nuint pos = cand.Index;

					ushort* children = (ushort*)par;
					var cnode = ptree + children[pos];

					for (nuint j = 1; j < 8; j++)
					{
						nuint sibling = children[pos ^ j];
						if (sibling != 0)
						{
							var snode = ptree + sibling;
							float* csums = (float*)((ushort*)cnode + 8);
							float* ssums = (float*)((ushort*)snode + 8);

#if HWINTRINSICS
							if (Sse.IsSupported)
							{
								Sse.Store(ssums, Sse.Add(Sse.LoadVector128(ssums), Sse.LoadVector128(csums)));
							}
							else
#endif
							{
								ssums[0] += csums[0];
								ssums[1] += csums[1];
								ssums[2] += csums[2];
								ssums[3] += csums[3];
							}

							Unsafe.InitBlockUnaligned(csums, 0, sizeof(uint) * 4);
							children[pos] = 0;
							break;
						}
					}
				}
			}
		}

		private nuint makePaletteMap(nuint minLevel)
		{
			fixed (OctreeNode* ptree = MemoryMarshal.Cast<byte, OctreeNode>(nodeBuffer))
			fixed (uint* ppal = MemoryMarshal.Cast<byte, uint>(palBuffer))
			fixed (byte* gt = &LookupTables.SrgbGammaUQ15[0])
			{
				nuint palidx = 0;
				for (nuint i = 0; i < 8; i++)
					populatePalette(ptree, gt, ppal, ptree + i, minLevel, 0, ref palidx);

				if (isSubsampled)
				{
					for (nuint i = 0; i < 8; i++)
					{
						var node = ptree + i;
						ushort* children = (ushort*)node;
						for (nuint j = 0; j < 8; j++)
						{
							if (children[j] == 0)
							{
								uint b = (((uint)i & 1) << 1 | ((uint)j & 1)     ) * 0x55;
								uint g = (((uint)i & 4) >> 1 | ((uint)j & 4) >> 2) * 0x55;
								uint r = (((uint)i & 2)      | ((uint)j & 2) >> 1) * 0x55;

								ppal[palidx++] = 0xffu << 24 | r << 16 | g << 8 | b;
							}
						}
					}
				}

				ppal[palidx] = 0x00ff00ffu;
				palEntries = (int)palidx + 1;

				Unsafe.InitBlockUnaligned(ppal + palEntries, 0, (maxPaletteSize - (uint)palEntries) * sizeof(uint));
			}

			leafLevel = Math.Max(leafLevel, minLeafLevel);

			var palMap = BufferPool.Rent(sizeof(OctreeNode) * maxHistogramSize, true);
			palMap.AsSpan().Clear();

			nuint mapidx = 8;

			fixed (uint* pilut = &LookupTables.OctreeIndexTable[0])
			fixed (OctreeNode* ptree = MemoryMarshal.Cast<byte, OctreeNode>(nodeBuffer))
			fixed (OctreeNode* pmap = MemoryMarshal.Cast<byte, OctreeNode>(palMap))
			{
				for (nuint i = 0; i < 8; i++)
					migrateNodes(ptree, pmap, pilut, ptree + i, pmap + i, 0, ref mapidx);
			}

			BufferPool.Return(nodeBuffer);
			nodeBuffer = palMap;

			return mapidx;
		}

		private void populatePalette(OctreeNode* ptree, byte* gt, uint* ppal, OctreeNode* node, nuint minLevel, nuint currLevel, ref nuint nidx)
		{
			if (currLevel == leafLevel)
			{
				float* sums = (float*)((ushort*)node + 8);
				float weight = 1f / sums[3];
				uint b = gt[(nuint)MathUtil.FixToUQ15One(sums[0] * weight)];
				uint g = gt[(nuint)MathUtil.FixToUQ15One(sums[1] * weight)];
				uint r = gt[(nuint)MathUtil.FixToUQ15One(sums[2] * weight)];

				ppal[nidx] = 0xffu << 24 | r << 16 | g << 8 | b;

				uint* isums = (uint*)sums;
				isums[0] = b;
				isums[1] = g;
				isums[2] = r;
				isums[3] = (uint)nidx++;
			}
			else
			{
				ushort* children = (ushort*)node;
				uint* sums = (uint*)(children + 8);

				if (currLevel >= minLevel)
					sums[3] = byte.MaxValue;

				for (nuint i = 0; i < 8; i++)
				{
					nuint child = children[i];
					if (child != 0)
						populatePalette(ptree, gt, ppal, ptree + child, minLevel, currLevel + 1, ref nidx);
				}

				if (currLevel >= minLevel && OctreeNode.HasOnlyChild(node))
				{
					for (nuint i = 0; i < 8; i++)
					{
						nuint child = children[i];
						if (child != 0)
						{
							var cnode = ptree + child;
							uint* csums = (uint*)((ushort*)cnode + 8);
							if (csums[3] != byte.MaxValue)
							{
								Unsafe.CopyBlockUnaligned(sums, csums, sizeof(uint) * 4);
								children[i] = 0;
							}
							break;
						}
					}
				}
			}
		}

		private void migrateNodes(OctreeNode* ptree, OctreeNode* pmap, uint* pilut, OctreeNode* node, OctreeNode* nnode, nuint currLevel, ref nuint nidx)
		{
			var pnew = nnode;

			ushort* children = (ushort*)node;
			ushort* nchildren = (ushort*)pnew;

			if (currLevel > 0)
			{
				bool isLeaf = !OctreeNode.HasChildren(node);

				pnew = pmap + nidx++;
				nchildren = (ushort*)pnew;

				uint* sums = (uint*)((ushort*)node + 8);
				if (currLevel < minLeafLevel && isLeaf)
				{
					nuint idx =
						pilut[(nuint)sums[0]      ] |
						pilut[(nuint)sums[1] + 256] |
						pilut[(nuint)sums[2] + 512];

					idx >>= (int)(currLevel * 3);

					nuint nnidx = nidx++;
					nchildren[idx & 7] = (ushort)nnidx;

					pnew = pmap + nnidx;
					nchildren = (ushort*)pnew;
				}

				uint* nsums = (uint*)(nchildren + 8);
				Unsafe.CopyBlockUnaligned(nsums, sums, sizeof(uint) * 4);

				if (currLevel >= minLeafLevel && !isLeaf)
					nsums[3] = byte.MaxValue;

				if (isLeaf)
					return;
			}

			for (nuint i = 0; i < 8; i++)
			{
				nuint child = children[i];
				if (child != 0)
				{
					nchildren[i] = (ushort)nidx;
					migrateNodes(ptree, pmap, pilut, ptree + child, pmap + nidx, currLevel + 1, ref nidx);
				}
			}
		}

		private void addReducibleNodes(OctreeNode* ptree, ReducibleNode* preduce, float* gt, OctreeNode* node, ref nuint reducibleCount, uint currLevel, uint pruneLevel)
		{
			ushort* children = (ushort*)node;
			for (nuint i = 0; i < 8; i++)
			{
				nuint child = children[i];
				if (child != 0)
				{
					var pchild = ptree + child;

					if (currLevel == pruneLevel)
					{
						float* csums = (float*)((ushort*)pchild + 8);
						float weight = csums[3];

						for (nuint j = 1; j < 8; j++)
						{
							nuint sibling = children[i ^ j];
							if (sibling != 0)
							{
								float* ssums = (float*)((ushort*)(ptree + sibling) + 8);
								float sweight = ssums[3];
								if (sweight > weight || (sweight == weight && (i ^ j) > i))
								{
									sweight = 1f / sweight;
									float iweight = 1f / weight;

									float cr = lutLerp(gt, csums[2] * iweight);
									float br = lutLerp(gt, ssums[2] * sweight);
									float rr = (cr + br) * 0.5f;

									float db = lutLerp(gt, csums[0] * iweight) - lutLerp(gt, ssums[0] * sweight);
									float dg = lutLerp(gt, csums[1] * iweight) - lutLerp(gt, ssums[1] * sweight);
									float dr = cr - br;

									float dd = ((2f + rr) * dr * dr + 4f * dg * dg + (3f - rr) * db * db).Sqrt();
									weight *= dd * 4f;

									preduce[reducibleCount++] = new ReducibleNode(weight, (ushort)(node - ptree), (ushort)i);
									break;
								}
							}
						}
					}
					else
					{
						addReducibleNodes(ptree, preduce, gt, pchild, ref reducibleCount, currLevel + 1, pruneLevel);
					}
				}
			}
		}

#if HWINTRINSICS
		private void remapDitherSse2(byte* pimage, int* perr, byte* pout, uint* pilut, OctreeNode* ptree, uint* ppal, ref nuint nextFree, nint cp)
		{
			var transnode = new OctreeNode();
			transnode.Sums[3] = (uint)palEntries - 1;

			var vpmax = Vector128.Create((int)byte.MaxValue);
			var vprnd = Vector128.Create(7);
			var vzero = Vector128<int>.Zero;

			nuint level = leafLevel;
			var prnod = default(OctreeNode*);

			byte* ip = pimage, ipe = ip + cp * sizeof(uint);
			byte* op = pout;
			int* ep = perr;

			var vppix = vzero;
			var vperr = vzero;
			var vnerr = vzero;

			do
			{
				Vector128<int> vpix, vdiff;
				if ((byte)ip[3] < alphaThreshold)
				{
					vppix = vzero;
					vdiff = vzero;
					prnod = &transnode;
					goto FoundExact;
				}

				if (Sse41.IsSupported)
					vpix = Sse41.ConvertToVector128Int32(ip);
				else
					vpix = Sse2.UnpackLow(Sse2.UnpackLow(Sse2.LoadScalarVector128((int*)ip).AsByte(), vzero.AsByte()).AsInt16(), vzero.AsInt16()).AsInt32();

				var verr = Sse2.Add(Sse2.Add(vprnd, Sse2.LoadVector128(ep)), Sse2.Subtract(Sse2.ShiftLeftLogical(vnerr, 3), vnerr));
				vpix = Sse2.Add(vpix, Sse2.ShiftRightArithmetic(verr, 4));
				vpix = Sse2.Min(vpix.AsInt16(), vpmax.AsInt16()).AsInt32();
				vpix = Sse2.Max(vpix.AsInt16(), vzero.AsInt16()).AsInt32();

				if (Sse2.MoveMask(Sse2.CompareEqual(vppix, vpix).AsByte()) == ushort.MaxValue)
				{
					vdiff = vzero;
					goto FoundExact;
				}

				vppix = vpix;
				nuint idx =
					pilut[(nuint)Sse2.ConvertToUInt32(vppix.AsUInt32())] |
					pilut[(nuint)Sse2.Extract(vppix.AsUInt16(), 2) + 256] |
					pilut[(nuint)Sse2.Extract(vppix.AsUInt16(), 4) + 512];
				nuint next = idx & 7;

				var pnode = ptree + next;
				for (nuint i = 0; i <= level; i++)
				{
					idx >>= 3;
					nuint child = idx & 7;

					ushort* children = (ushort*)pnode;
					next = children[child];
					if (next == 0)
					{
						uint* sums = (uint*)(children + 8);

						if (i < minLeafLevel)
						{
							next = nextFree++;
							children[child] = (ushort)next;
							pnode = ptree + next;

							if (i == minLeafLevel - 1)
							{
								initNode(pnode, vppix);
								break;
							}
							else
							{
								uint* csums = (uint*)((ushort*)pnode + 8);
								csums[3] = byte.MaxValue;
							}
						}
						else if ((byte)sums[3] == byte.MaxValue)
						{
							for (nuint j = 1; j < 8; j++)
							{
								nuint sibling = children[child ^ j];
								if (sibling != 0)
								{
									var snode = ptree + sibling;
									uint* ssums = (uint*)((ushort*)snode + 8);
									if ((byte)ssums[3] == byte.MaxValue)
									{
										next = sibling;
										nuint mask = child ^ sibling;
										idx = (child & mask) | (idx & ~mask);
										break;
									}
									else
									{
										prnod = snode;
										goto Found;
									}
								}
							}
						}
						else
						{
							break;
						}
					}

					pnode = ptree + next;
				}

				prnod = pnode;

				Found:
				vdiff = Sse2.Subtract(vppix, Sse2.LoadVector128((int*)((ushort*)prnod + 8)));

				FoundExact:
				int* psums = (int*)((ushort*)prnod + 8);

				ip += sizeof(uint);
				*op++ = (byte)psums[3];

				Sse2.Store(ep - Vector128<int>.Count, Sse2.Add(vperr, Sse2.Add(vdiff, vdiff)));
				ep += Vector128<int>.Count;

				vperr = Sse2.Add(Sse2.ShiftLeftLogical(vdiff, 2), vnerr);
				vnerr = vdiff;

			} while (ip < ipe);

			Sse2.Store(ep - Vector128<int>.Count, vperr);
		}
#endif

		private void remapDitherScalar(byte* pimage, int* perror, byte* pout, uint* pilut, OctreeNode* ptree, uint* ppal, ref nuint nextFree, nint cp)
		{
			var transnode = new OctreeNode();
			transnode.Sums[3] = (uint)palEntries - 1;

			nuint level = leafLevel;
			var prnod = default(OctreeNode*);

			byte* ip = pimage, ipe = ip + cp * sizeof(uint);
			byte* op = pout;
			int* ep = perror;

			uint ppix = 0;
			int perb = 0, perg = 0, perr = 0;
			int nerb = 0, nerg = 0, nerr = 0;

			do
			{
				int db, dg, dr;
				nuint cb, cg, cr;
				if ((byte)ip[3] < alphaThreshold)
				{
					ppix = 0;
					cb = cg = cr = 0;
					db = dg = dr = 0;
					prnod = &transnode;
					goto FoundExact;
				}

				cb = (nuint)(((ip[0] << 4) + ep[0] + (nerb << 3) - nerb + 7).Clamp(0, 4095) >> 4);
				cg = (nuint)(((ip[1] << 4) + ep[1] + (nerg << 3) - nerg + 7).Clamp(0, 4095) >> 4);
				cr = (nuint)(((ip[2] << 4) + ep[2] + (nerr << 3) - nerr + 7).Clamp(0, 4095) >> 4);
				uint cpix = 0xffu << 24 | (uint)cr << 16 | (uint)cg << 8 | (uint)cb;

				if (ppix == cpix)
				{
					db = dg = dr = 0;
					goto FoundExact;
				}

				ppix = cpix;
				nuint idx = pilut[cb] | pilut[cg + 256] | pilut[cr + 512];
				nuint next = idx & 7;

				var pnode = ptree + next;
				for (nuint i = 0; i <= level; i++)
				{
					idx >>= 3;
					nuint child = idx & 7;

					ushort* children = (ushort*)pnode;
					next = children[child];
					if (next == 0)
					{
						uint* sums = (uint*)(children + 8);

						if (i < minLeafLevel)
						{
							next = nextFree++;
							children[child] = (ushort)next;
							pnode = ptree + next;

							if (i == minLeafLevel - 1)
							{
								initNode(pnode, ppix);
								break;
							}
							else
							{
								uint* csums = (uint*)((ushort*)pnode + 8);
								csums[3] = byte.MaxValue;
							}
						}
						else if ((byte)sums[3] == byte.MaxValue)
						{
							for (nuint j = 1; j < 8; j++)
							{
								nuint sibling = children[child ^ j];
								if (sibling != 0)
								{
									var snode = ptree + sibling;
									uint* ssums = (uint*)((ushort*)snode + 8);
									if ((byte)ssums[3] == byte.MaxValue)
									{
										next = sibling;
										nuint mask = child ^ sibling;
										idx = (child & mask) | (idx & ~mask);
										break;
									}
									else
									{
										prnod = snode;
										goto Found;
									}
								}
							}
						}
						else
						{
							break;
						}
					}

					pnode = ptree + next;
				}

				prnod = pnode;

				Found:
				int* psums = (int*)((ushort*)prnod + 8);
				db = (byte)(ppix      ) - psums[0];
				dg = (byte)(ppix >>  8) - psums[1];
				dr = (byte)(ppix >> 16) - psums[2];

				FoundExact:
				psums = (int*)((ushort*)prnod + 8);
				ip += sizeof(uint);
				*op++ = (byte)psums[3];

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

			} while (ip < ipe);

			ep[-4] = perb;
			ep[-3] = perg;
			ep[-2] = perr;
		}

		private void remap(byte* pimage, byte* pout, uint* pilut, OctreeNode* ptree, uint* ppal, ref nuint nextFree, nint cp)
		{
			var transnode = new OctreeNode();
			transnode.Sums[3] = (uint)palEntries - 1;

			nuint level = leafLevel;
			uint ppix = 0;
			var prnod = default(OctreeNode*);

			byte* ip = pimage, ipe = ip + cp * sizeof(uint);
			byte* op = pout;

			do
			{
				if ((byte)ip[3] < alphaThreshold)
				{
					ppix = 0;
					prnod = &transnode;
					goto Found;
				}

				uint cpix = *(uint*)ip;
				if (ppix == cpix)
					goto Found;

				ppix = cpix;
				nuint idx = pilut[(nuint)(byte)ppix] | pilut[(nuint)(byte)(ppix >> 8) + 256] | pilut[(nuint)(byte)(ppix >> 16) + 512];
				nuint next = idx & 7;

				var pnode = ptree + next;
				for (nuint i = 0; i <= level; i++)
				{
					idx >>= 3;
					nuint child = idx & 7;

					ushort* children = (ushort*)pnode;
					next = children[child];
					if (next == 0)
					{
						uint* sums = (uint*)(children + 8);
						if ((byte)sums[3] == byte.MaxValue)
						{
							for (nuint j = 1; j < 8; j++)
							{
								nuint sibling = children[child ^ j];
								if (sibling != 0)
								{
									var snode = ptree + sibling;
									uint* ssums = (uint*)((ushort*)snode + 8);
									if ((byte)ssums[3] == byte.MaxValue)
									{
										next = sibling;
										nuint mask = child ^ sibling;
										idx = (child & mask) | (idx & ~mask);
										break;
									}
									else
									{
										prnod = snode;
										goto Found;
									}
								}
							}
						}
						else
						{
							break;
						}
					}

					pnode = ptree + next;
				}

				prnod = pnode;

				Found:
				ip += sizeof(uint);

				int* psums = (int*)((ushort*)prnod + 8);
				*op++ = (byte)psums[3];

			} while (ip < ipe);
		}

		private void getNodeCounts(Span<int> counts)
		{
			fixed (OctreeNode* ptree = MemoryMarshal.Cast<byte, OctreeNode>(nodeBuffer))
			fixed (int* pcounts = counts)
			{
				for (nuint i = 0; i < maxHistogramSize; i++)
				{
					nuint level = OctreeNode.GetLevel(ptree + i);
					pcounts[level]++;
				}

				pcounts[0] = 8;
			}
		}

		private int getReservedCount()
		{
			if (!isSubsampled)
				return 1;

			fixed (OctreeNode* ptree = MemoryMarshal.Cast<byte, OctreeNode>(nodeBuffer))
			{
				int reserved = 1;
				for (nuint i = 0; i < 8; i++)
				{
					var node = ptree + i;
					if (!OctreeNode.HasChildren(node))
					{
						reserved += 8;
					}
					else
					{
						ushort* children = (ushort*)node;
						for (nuint j = 0; j < 8; j++)
						{
							if (children[j] == 0)
								reserved++;
						}
					}
				}

				return reserved;
			}
		}

		private int getPixelCount()
		{
			fixed (OctreeNode* ptree = MemoryMarshal.Cast<byte, OctreeNode>(nodeBuffer))
			{
				uint count = 0;
				for (nuint i = 0; i < maxHistogramSize; i++)
				{
					uint level = OctreeNode.GetLevel(ptree + i);
					if (level == leafLevel)
					{
						uint* sums = (uint*)((ushort*)(ptree + i) + 8);
						count += sums[3] & 0x1fffffffu;
					}
				}

				return (int)count;
			}
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		private static float lutLerp(float* gt, float val)
		{
			nuint ival = (nuint)val;

			return MathUtil.Lerp(gt[ival], gt[ival + 1], val - (int)ival);
		}

#if HWINTRINSICS
		private void initNode(OctreeNode* node, Vector128<int> color)
		{
			int cb = Sse2.ConvertToInt32(color);
			int cg = Sse2.Extract(color.AsUInt16(), 2);
			int cr = Sse2.Extract(color.AsUInt16(), 4);

			initNode(node, cb, cg, cr);
		}
#endif

		private void initNode(OctreeNode* node, uint color)
		{
			int cb = (byte)(color);
			int cg = (byte)(color >>  8);
			int cr = (byte)(color >> 16);

			initNode(node, cb, cg, cr);
		}

		private void initNode(OctreeNode* node, int cb, int cg, int cr)
		{
			fixed (uint* ppal = MemoryMarshal.Cast<byte, uint>(palBuffer))
			{
				uint dist = uint.MaxValue;
				uint pidx = (uint)palEntries - 1;
				nuint plen = pidx;

				for (nuint i = 0; i < plen; i++)
				{
					uint pc = ppal[i];
					int pb = (byte)(pc);
					int pg = (byte)(pc >>  8);
					int pr = (byte)(pc >> 16);

					int rr = (pr + cr + 1) >> 1;
					int db = cb - pb;
					int dg = cg - pg;
					int dr = cr - pr;

					uint dd = (uint)((512 + rr) * dr * dr + 1024 * dg * dg + (768 - rr) * db * db) >> 8;
					if (dd < dist)
					{
						dist = dd;
						pidx = (uint)i;
					}
				}

				uint* sums = (uint*)((ushort*)node + 8);
				uint mc = ppal[pidx];

				sums[0] = (byte)(mc      );
				sums[1] = (byte)(mc >>  8);
				sums[2] = (byte)(mc >> 16);
				sums[3] = pidx;
			}
		}

		private static void initFreeList(Span<byte> listBuff, uint maxNodes)
		{
			const uint reserveNodes = 8;
			uint maxFree = maxNodes - reserveNodes;

			ref byte listStart = ref listBuff[0];
			ref byte listEnd = ref Unsafe.Add(ref listStart, (IntPtr)(maxFree * sizeof(ushort)));
			ref byte listPtr = ref listStart;

			if (Vector.IsHardwareAccelerated && maxFree > Vector<ushort>.Count)
			{
				var slots = (ReadOnlySpan<byte>)(new byte[] {
					 8, 0,  9, 0, 10, 0, 11, 0, 12, 0, 13, 0, 14, 0, 15, 0,
					16, 0, 17, 0, 18, 0, 19, 0, 20, 0, 21, 0, 22, 0, 23, 0
				});
				var vslot = Unsafe.ReadUnaligned<Vector<ushort>>(ref MemoryMarshal.GetReference(slots));
				var vincr = new Vector<ushort>((ushort)Vector<ushort>.Count);

				listEnd = ref Unsafe.Subtract(ref listEnd, Vector<byte>.Count);
				do
				{
					Unsafe.WriteUnaligned(ref listPtr, vslot);
					listPtr = ref Unsafe.Add(ref listPtr, Vector<byte>.Count);
					vslot += vincr;

				} while (Unsafe.IsAddressLessThan(ref listPtr, ref listEnd));
				listEnd = ref Unsafe.Add(ref listEnd, Vector<byte>.Count);
			}

			uint islot = (uint)Unsafe.ByteOffset(ref listStart, ref listPtr) / sizeof(ushort) + reserveNodes;
			islot |= islot + 1 << 16;

			listEnd = ref Unsafe.Subtract(ref listEnd, sizeof(uint));
			while (Unsafe.IsAddressLessThan(ref listPtr, ref listEnd))
			{
				Unsafe.WriteUnaligned(ref listPtr, islot);
				listPtr = ref Unsafe.Add(ref listPtr, sizeof(uint));
				islot += 0x00020002;
			}

			if ((maxFree & 1) == 1)
				Unsafe.WriteUnaligned(ref listPtr, (ushort)islot);

			Unsafe.InitBlockUnaligned(ref Unsafe.Add(ref listPtr, sizeof(ushort)), 0, reserveNodes * sizeof(ushort));
		}

		private struct OctreeNode
		{
			const uint levelMask = 0xe0000000;
			const uint sumsMask = 0x01ffffff;

			public fixed ushort ChildNodes[8];
			public fixed uint Sums[4];

			[MethodImpl(MethodImplOptions.AggressiveInlining)]
			public static void AddSample(OctreeNode* node, uint bgr)
			{
				uint* sums = (uint*)((ushort*)node + 8);

#if HWINTRINSICS
				if (Sse41.IsSupported)
				{
					bgr &= sumsMask;
					bgr |= 0x01000000;
					var vbgr = Sse41.ConvertToVector128Int32(Sse2.ConvertScalarToVector128UInt32(bgr).AsByte()).AsUInt32();
					Sse2.Store(sums, Sse2.Add(vbgr, Sse2.LoadVector128(sums)));

					return;
				}
#endif

				sums[0] += (byte)(bgr      );
				sums[1] += (byte)(bgr >>  8);
				sums[2] += (byte)(bgr >> 16);
				sums[3]++;
			}

			[MethodImpl(MethodImplOptions.AggressiveInlining)]
			public static bool HasChildren(OctreeNode* node)
			{
				ushort* children = (ushort*)node;

#if HWINTRINSICS
				if (Sse2.IsSupported)
				{
					var veq = Sse2.CompareEqual(Vector128<ushort>.Zero, Sse2.LoadVector128(children));
					return Sse2.MoveMask(veq.AsByte()) != ushort.MaxValue;
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
			public static bool HasOnlyChild(OctreeNode* node)
			{
				ushort* children = (ushort*)node;

#if HWINTRINSICS
				if (Popcnt.IsSupported)
				{
					var veq = Sse2.CompareEqual(Vector128<ushort>.Zero, Sse2.LoadVector128(children));
					return Popcnt.PopCount((uint)Sse2.MoveMask(veq.AsByte())) == Vector128<byte>.Count - sizeof(ushort);
				}
#endif

				uint cnt = 0;
				for (nuint i = 0; i < 8; i++)
				{
					if (children[i] != 0 && ++cnt > 1)
						return false;
				}

				return cnt == 1;
			}

			[MethodImpl(MethodImplOptions.AggressiveInlining)]
			public static uint GetLevel(OctreeNode* node)
			{
				uint* sums = (uint*)((ushort*)node + 8);

				return (sums[3] & levelMask) >> 29;
			}

			[MethodImpl(MethodImplOptions.AggressiveInlining)]
			public static void SetLevel(OctreeNode* node, uint level)
			{
				uint* sums = (uint*)((ushort*)node + 8);

				sums[3] |= level << 29;
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
