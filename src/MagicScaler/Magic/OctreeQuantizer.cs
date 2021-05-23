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
	internal sealed unsafe class OctreeQuantizer : IDisposable
	{
		private const int maxHistogramSize = 8191;   // max possible nodes with 3 bits saved to stuff level into one of the indices
		private const int maxPaletteSize = 256;
		private const int maxSamples = 1 << 22;
		private const uint minLeafLevel = 3;
		private const uint alphaThreshold = 85;
		private const uint transparentValue = 0x00ff00ffu;

		private uint leafLevel = 7;
		private int paletteLength;

		private RentedBuffer<uint> palBuffer;

		public ReadOnlySpan<uint> Palette => palBuffer.Span.Slice(0, paletteLength);

		public OctreeQuantizer() => palBuffer = BufferPool.Rent<uint>(maxPaletteSize, true);

		public bool CreatePalette(Span<byte> image, nint width, nint height, nint stride)
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

			return buildPalette(nodeBuffer.Span, subsampleRatio > 1f);
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

				node = ptree + (idx & 63);
				idx >>= 6;

				for (nuint i = 2; i <= level; i++)
				{
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
					idx >>= 3;
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

			for (nuint i = 64; i < maxHistogramSize; i++)
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

		private bool buildPalette(Span<HistogramNode> nodeBuffer, bool isSubsampled)
		{
			var nc = (Span<int>)stackalloc int[] { 0, 0, 0, 0, 0, 0, 0, 0 };
			getNodeCounts(nodeBuffer, nc);

			uint level = leafLevel;
			int targetColors = maxPaletteSize - 1;
			for (uint i = 2; i < leafLevel; i++)
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
				for (nuint i = 0; i < 64; i++)
					convertNodes(ptree, igt, ptree + i, 1, level, ftpix);

				leafLevel = level;
			}

			int histogramColors = nc[(int)leafLevel];
			bool isPaletteExact = !isSubsampled && histogramColors <= targetColors;

			if (histogramColors > targetColors)
			{
				using var listBuffer = BufferPool.RentLocal<ReducibleNode>(histogramColors);
				nuint reducibleCount = 0;

				fixed (HistogramNode* ptree = nodeBuffer)
				fixed (ReducibleNode* pweights = listBuffer.Span)
				fixed (float* gt = &LookupTables.SrgbGamma[0])
				{
					for (nuint i = 0; i < 64; i++)
						addReducibleNodes(ptree, pweights, gt, ptree + i, ref reducibleCount, 1, leafLevel - 1);
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

			makePalette(nodeBuffer);

			return isPaletteExact;
		}

		private void makePalette(Span<HistogramNode> nodeBuffer)
		{
			fixed (HistogramNode* ptree = nodeBuffer)
			fixed (uint* ppal = palBuffer, pilut = &LookupTables.OctreeIndexTable[0])
			fixed (byte* gt = &LookupTables.SrgbGammaUQ15[0])
			{
				nuint palidx = 0;
				for (nuint i = 0; i < 64; i++)
				{
					nuint idx = pilut[(i & 0b_11_00_00) << 2] | pilut[((i & 0b_11_00) << 4) + 256] | pilut[((i & 0b_11) << 6) + 512];
					populatePalette(ptree, gt, ppal, ptree + idx, leafLevel, 1, ref palidx);
				}

				for (nuint i = palidx + 1; palidx > 0 && i < maxPaletteSize; i++)
					ppal[i] = ppal[palidx - 1];

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

		private static void getNodeCounts(Span<HistogramNode> nodeBuffer, Span<int> counts)
		{
			fixed (HistogramNode* ptree = nodeBuffer)
			fixed (int* pcounts = counts)
			{
				for (nuint i = 0; i < maxHistogramSize; i++)
				{
					if (i < 64 && HistogramNode.HasChildren(ptree + i))
					{
						pcounts[1]++;
					}
					else
					{
						nuint level = HistogramNode.GetLevel(ptree + i);
						pcounts[level]++;
					}
				}

				pcounts[0] = 8;
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
			const int reserveNodes = 64;
			nint maxFree = listBuff.Length - reserveNodes;

			ref byte listStart = ref Unsafe.As<ushort, byte>(ref listBuff[0]), listPtr = ref listStart;
			ref byte listEnd = ref Unsafe.Add(ref listStart, maxFree * sizeof(ushort));

			if (Vector.IsHardwareAccelerated && maxFree > Vector<ushort>.Count)
			{
				var slots = (ReadOnlySpan<byte>)(new byte[] {
					64, 0, 65, 0, 66, 0, 67, 0, 68, 0, 69, 0, 70, 0, 71, 0,
					72, 0, 73, 0, 74, 0, 75, 0, 76, 0, 77, 0, 78, 0, 79, 0
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
				if (Sse41.IsSupported)
				{
					var vmsk = Unsafe.As<byte, Vector128<ushort>>(ref MemoryMarshal.GetReference(SumsMask));

#pragma warning disable IDE0075 // https://github.com/dotnet/runtime/issues/4207
					return Sse41.TestZ(vmsk, Sse2.LoadVector128(children)) ? false : true;
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

		private readonly struct ReducibleNode : IComparable<ReducibleNode>, IEquatable<ReducibleNode>
		{
			public readonly float Weight;
			public readonly ushort Parent;
			public readonly ushort Index;

			public ReducibleNode(float weight, ushort parent, ushort index) => (Weight, Parent, Index) = (weight, parent, index);

			int IComparable<ReducibleNode>.CompareTo(ReducibleNode other) => Weight.CompareTo(other.Weight);
			bool IEquatable<ReducibleNode>.Equals(ReducibleNode other) => Parent == other.Parent && Index == other.Index;
		}
	}
}
