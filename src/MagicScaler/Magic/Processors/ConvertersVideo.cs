// Copyright © Clinton Ingram and Contributors.  Licensed under the MIT License.

using System;
using System.Diagnostics;

#if HWINTRINSICS
using System.Runtime.Intrinsics;
using System.Runtime.Intrinsics.X86;
using System.Runtime.CompilerServices;
#endif

namespace PhotoSauce.MagicScaler.Converters;

internal static class VideoLumaConverter
{
	public sealed unsafe class VideoToFullRangeProcessor : IConversionProcessor<byte, byte>
	{
		public static readonly byte[] unvideoTable = makeTable();

		private static byte[] makeTable()
		{
			const int offs = -16;
			const int rnd = 109;

			var tbl = new byte[256];

			for (int i = 0; i < tbl.Length; i++)
			{
				int v = i + offs;
				uint vu = (uint)Math.Max(v, 0);

				vu = (vu * 255 + rnd) / 219;
				vu = Math.Min(vu, byte.MaxValue);

				tbl[i] = (byte)vu;
			}

			return tbl;
		}

		public static readonly VideoToFullRangeProcessor Instance = new();

		private VideoToFullRangeProcessor() { }

		void IConversionProcessor.ConvertLine(byte* istart, byte* ostart, nint cb)
		{
			Debug.Assert(istart == ostart);

			byte* ip = istart, ipe = istart + cb;

#if HWINTRINSICS
			if (HWIntrinsics.IsSupported && cb >= HWIntrinsics.VectorCount<byte>())
				convertIntrinsic(ip, ipe);
			else
#endif
				LutConverter.Convert(ip, ipe, unvideoTable);
		}

#if HWINTRINSICS
		[MethodImpl(MethodImplOptions.AggressiveOptimization)]
		private static void convertIntrinsic(byte* ip, byte* ipe)
		{
			if (Avx2.IsSupported)
			{
				var vrnd = Vector256.Create((ushort)3971);
				var vdiv = Vector256.Create((ushort)11073);
				var voff = Vector256.Create((byte)16);
				var vzer = Vector256<byte>.Zero;
				ipe -= Vector256<byte>.Count;

				var vlast = Avx.LoadVector256(ipe);

				LoopTop:
				do
				{
					var vi = Avx2.Max(voff, Avx.LoadVector256(ip));

					var vil = Avx2.UnpackLow(vi, vzer).AsUInt16();
					var vih = Avx2.UnpackHigh(vi, vzer).AsUInt16();
					vil = Avx2.Subtract(Avx2.ShiftLeftLogical(vil, 8), Avx2.Add(vil, vrnd));
					vih = Avx2.Subtract(Avx2.ShiftLeftLogical(vih, 8), Avx2.Add(vih, vrnd));

					var vml = Avx2.MultiplyHigh(vil, vdiv);
					var vmh = Avx2.MultiplyHigh(vih, vdiv);
					vil = Avx2.ShiftRightLogical(Avx2.Subtract(vil, vml), 1);
					vih = Avx2.ShiftRightLogical(Avx2.Subtract(vih, vmh), 1);
					vil = Avx2.ShiftRightLogical(Avx2.Add(vil, vml), 7);
					vih = Avx2.ShiftRightLogical(Avx2.Add(vih, vmh), 7);

					vi = Avx2.PackUnsignedSaturate(vil.AsInt16(), vih.AsInt16());

					Avx.Store(ip, vi);
					ip += Vector256<byte>.Count;
				}
				while (ip <= ipe);

				if (ip < ipe + Vector256<byte>.Count)
				{
					nuint offs = UnsafeUtil.ByteOffset(ipe, ip);
					ip = UnsafeUtil.SubtractOffset(ip, offs);
					Avx.Store(ip, vlast);
					goto LoopTop;
				}
			}
			else
			{
				var vrnd = Vector128.Create((ushort)3971);
				var vdiv = Vector128.Create((ushort)11073);
				var voff = Vector128.Create((byte)16);
				var vzer = Vector128<byte>.Zero;
				ipe -= Vector128<byte>.Count;

				var vlast = Sse2.LoadVector128(ipe);

				LoopTop:
				do
				{
					var vi = Sse2.Max(voff, Sse2.LoadVector128(ip));

					var vil = Sse2.UnpackLow(vi, vzer).AsUInt16();
					var vih = Sse2.UnpackHigh(vi, vzer).AsUInt16();
					vil = Sse2.Subtract(Sse2.ShiftLeftLogical(vil, 8), Sse2.Add(vil, vrnd));
					vih = Sse2.Subtract(Sse2.ShiftLeftLogical(vih, 8), Sse2.Add(vih, vrnd));

					var vml = Sse2.MultiplyHigh(vil, vdiv);
					var vmh = Sse2.MultiplyHigh(vih, vdiv);
					vil = Sse2.ShiftRightLogical(Sse2.Subtract(vil, vml), 1);
					vih = Sse2.ShiftRightLogical(Sse2.Subtract(vih, vmh), 1);
					vil = Sse2.ShiftRightLogical(Sse2.Add(vil, vml), 7);
					vih = Sse2.ShiftRightLogical(Sse2.Add(vih, vmh), 7);

					vi = Sse2.PackUnsignedSaturate(vil.AsInt16(), vih.AsInt16());

					Sse2.Store(ip, vi);
					ip += Vector128<byte>.Count;
				}
				while (ip <= ipe);

				if (ip < ipe + Vector128<byte>.Count)
				{
					nuint offs = UnsafeUtil.ByteOffset(ipe, ip);
					ip = UnsafeUtil.SubtractOffset(ip, offs);
					Sse2.Store(ip, vlast);
					goto LoopTop;
				}
			}
		}
#endif
	}

	public sealed unsafe class FullRangeToVideoProcessor : IConversionProcessor<byte, byte>
	{
		public static readonly byte[] videoTable = makeTable();

		private static byte[] makeTable()
		{
			const int offs = -16;
			const int rnd = 127;

			var tbl = new byte[256];

			for (int i = 0; i < tbl.Length; i++)
			{
				uint v = (uint)i;
				v = (v * 219 + rnd) / 255;

				tbl[i] = (byte)(v - offs);
			}

			return tbl;
		}

		public static readonly FullRangeToVideoProcessor Instance = new();

		private FullRangeToVideoProcessor() { }

		void IConversionProcessor.ConvertLine(byte* istart, byte* ostart, nint cb)
		{
			Debug.Assert(istart == ostart);

			byte* ip = istart, ipe = istart + cb;

#if HWINTRINSICS
			if (HWIntrinsics.IsSupported && cb >= HWIntrinsics.VectorCount<byte>())
				convertIntrinsic(ip, ipe);
			else
#endif
				LutConverter.Convert(ip, ipe, videoTable);
		}

#if HWINTRINSICS
		[MethodImpl(MethodImplOptions.AggressiveOptimization)]
		private static void convertIntrinsic(byte* ip, byte* ipe)
		{
			if (Avx2.IsSupported)
			{
				var vrnd = Vector256.Create((byte)150);
				var vmul = Vector256.Create((ushort)0xdbdb);
				var voff = Vector256.Create((byte)16);
				ipe -= Vector256<byte>.Count;

				var vlast = Avx.LoadVector256(ipe);

				LoopTop:
				do
				{
					var vi = Avx.LoadVector256(ip);

					var vil = Avx2.UnpackLow(vrnd, vi).AsUInt16();
					var vih = Avx2.UnpackHigh(vrnd, vi).AsUInt16();
					vil = Avx2.MultiplyHigh(vil, vmul);
					vih = Avx2.MultiplyHigh(vih, vmul);
					vil = Avx2.ShiftRightLogical(vil, 8);
					vih = Avx2.ShiftRightLogical(vih, 8);

					vi = Avx2.PackUnsignedSaturate(vil.AsInt16(), vih.AsInt16());
					vi = Avx2.Add(vi, voff);

					Avx.Store(ip, vi);
					ip += Vector256<byte>.Count;
				}
				while (ip <= ipe);

				if (ip < ipe + Vector256<byte>.Count)
				{
					nuint offs = UnsafeUtil.ByteOffset(ipe, ip);
					ip = UnsafeUtil.SubtractOffset(ip, offs);
					Avx.Store(ip, vlast);
					goto LoopTop;
				}
			}
			else
			{
				var vrnd = Vector128.Create((byte)150);
				var vmul = Vector128.Create((ushort)0xdbdb);
				var voff = Vector128.Create((byte)16);
				ipe -= Vector128<byte>.Count;

				var vlast = Sse2.LoadVector128(ipe);

				LoopTop:
				do
				{
					var vi = Sse2.LoadVector128(ip);

					var vil = Sse2.UnpackLow(vrnd, vi).AsUInt16();
					var vih = Sse2.UnpackHigh(vrnd, vi).AsUInt16();
					vil = Sse2.MultiplyHigh(vil, vmul);
					vih = Sse2.MultiplyHigh(vih, vmul);
					vil = Sse2.ShiftRightLogical(vil, 8);
					vih = Sse2.ShiftRightLogical(vih, 8);

					vi = Sse2.PackUnsignedSaturate(vil.AsInt16(), vih.AsInt16());
					vi = Sse2.Add(vi, voff);

					Sse2.Store(ip, vi);
					ip += Vector128<byte>.Count;
				}
				while (ip <= ipe);

				if (ip < ipe + Vector128<byte>.Count)
				{
					nuint offs = UnsafeUtil.ByteOffset(ipe, ip);
					ip = UnsafeUtil.SubtractOffset(ip, offs);
					Sse2.Store(ip, vlast);
					goto LoopTop;
				}
			}
		}
#endif
	}
}

internal static class VideoChromaConverter
{
	public sealed unsafe class VideoToFullRangeProcessor : IConversionProcessor<byte, byte>
	{
		public static readonly byte[] unvideoTable = makeTable();

		private static byte[] makeTable()
		{
			const int offs = -128;
			const int rnd = 56;

			var tbl = new byte[256];

			for (int i = 0; i < tbl.Length; i++)
			{
				int v = i + offs;
				int vr = v >= 0 ? rnd : -rnd;

				v = (v * 127 + vr) / 112;
				v = Math.Min(Math.Max(v - offs, 0), byte.MaxValue);

				tbl[i] = (byte)v;
			}

			return tbl;
		}

		public static readonly VideoToFullRangeProcessor Instance = new();

		private VideoToFullRangeProcessor() { }

		void IConversionProcessor.ConvertLine(byte* istart, byte* ostart, nint cb)
		{
			Debug.Assert(istart == ostart);

			byte* ip = istart, ipe = istart + cb;

#if HWINTRINSICS
			if (HWIntrinsics.IsSupported && cb >= HWIntrinsics.VectorCount<byte>())
				convertIntrinsic(ip, ipe);
			else
#endif
				LutConverter.Convert(ip, ipe, unvideoTable);
		}

#if HWINTRINSICS
		[MethodImpl(MethodImplOptions.AggressiveOptimization)]
		private static void convertIntrinsic(byte* ip, byte* ipe)
		{
			if (Avx2.IsSupported)
			{
				var vrndn = Vector256.Create((sbyte)-72);
				var vrndp = Vector256.Create((sbyte)+72);
				var vrndh = Vector256.Create((sbyte)-64);
				var vdiv = Vector256.Create((short)9363);
				var voff = Vector256.Create((byte)128);
				var vzer = Vector256<byte>.Zero;
				ipe -= Vector256<byte>.Count;

				var vlast = Avx.LoadVector256(ipe);

				LoopTop:
				do
				{
					var vi = Avx.LoadVector256(ip);
					var vm = Avx2.CompareGreaterThan(vzer.AsSByte(), vi.AsSByte());
					var vr = Avx2.BlendVariable(vrndp, vrndn, vm);

					var vil = Avx2.UnpackLow(vi, vzer).AsInt16();
					var vih = Avx2.UnpackHigh(vi, vzer).AsInt16();
					vil = Avx2.Subtract(Avx2.ShiftLeftLogical(vil, 7), vil);
					vih = Avx2.Subtract(Avx2.ShiftLeftLogical(vih, 7), vih);

					var vrl = Avx2.UnpackLow(vr, vrndh).AsInt16();
					var vrh = Avx2.UnpackHigh(vr, vrndh).AsInt16();
					vil = Avx2.Add(vil, vrl);
					vih = Avx2.Add(vih, vrh);

					vil = Avx2.MultiplyHigh(vil, vdiv);
					vih = Avx2.MultiplyHigh(vih, vdiv);
					vil = Avx2.Add(Avx2.ShiftRightLogical(vil, 15), Avx2.ShiftRightArithmetic(vil, 4));
					vih = Avx2.Add(Avx2.ShiftRightLogical(vih, 15), Avx2.ShiftRightArithmetic(vih, 4));

					vi = Avx2.PackSignedSaturate(vil, vih).AsByte();
					vi = Avx2.Add(vi, voff);

					Avx.Store(ip, vi);
					ip += Vector256<byte>.Count;
				}
				while (ip <= ipe);

				if (ip < ipe + Vector256<byte>.Count)
				{
					nuint offs = UnsafeUtil.ByteOffset(ipe, ip);
					ip = UnsafeUtil.SubtractOffset(ip, offs);
					Avx.Store(ip, vlast);
					goto LoopTop;
				}
			}
			else
			{
				var vrndn = Vector128.Create((sbyte)-72).AsByte();
				var vrndp = Vector128.Create((sbyte)+72).AsByte();
				var vrndh = Vector128.Create((sbyte)-64);
				var vdiv = Vector128.Create((short)9363);
				var voff = Vector128.Create((byte)128);
				var vzer = Vector128<byte>.Zero;
				ipe -= Vector128<byte>.Count;

				var vlast = Sse2.LoadVector128(ipe);

				LoopTop:
				do
				{
					var vi = Sse2.LoadVector128(ip);
					var vm = Sse2.CompareGreaterThan(vzer.AsSByte(), vi.AsSByte()).AsByte();
					var vr = HWIntrinsics.BlendVariable(vrndp, vrndn, vm).AsSByte();

					var vil = Sse2.UnpackLow(vi, vzer).AsInt16();
					var vih = Sse2.UnpackHigh(vi, vzer).AsInt16();
					vil = Sse2.Subtract(Sse2.ShiftLeftLogical(vil, 7), vil);
					vih = Sse2.Subtract(Sse2.ShiftLeftLogical(vih, 7), vih);

					var vrl = Sse2.UnpackLow(vr, vrndh).AsInt16();
					var vrh = Sse2.UnpackHigh(vr, vrndh).AsInt16();
					vil = Sse2.Add(vil, vrl);
					vih = Sse2.Add(vih, vrh);

					vil = Sse2.MultiplyHigh(vil, vdiv);
					vih = Sse2.MultiplyHigh(vih, vdiv);
					vil = Sse2.Add(Sse2.ShiftRightLogical(vil, 15), Sse2.ShiftRightArithmetic(vil, 4));
					vih = Sse2.Add(Sse2.ShiftRightLogical(vih, 15), Sse2.ShiftRightArithmetic(vih, 4));

					vi = Sse2.PackSignedSaturate(vil, vih).AsByte();
					vi = Sse2.Add(vi, voff);

					Sse2.Store(ip, vi);
					ip += Vector128<byte>.Count;
				}
				while (ip <= ipe);

				if (ip < ipe + Vector128<byte>.Count)
				{
					nuint offs = UnsafeUtil.ByteOffset(ipe, ip);
					ip = UnsafeUtil.SubtractOffset(ip, offs);
					Sse2.Store(ip, vlast);
					goto LoopTop;
				}
			}
		}
#endif
	}

	public sealed unsafe class FullRangeToVideoProcessor : IConversionProcessor<byte, byte>
	{
		public static readonly byte[] videoTable = makeTable();

		private static byte[] makeTable()
		{
			const int offs = -128;
			const int rnd = 63;

			var tbl = new byte[256];

			for (int i = 0; i < tbl.Length; i++)
			{
				int v = i + offs;

				int vr = v >= 0 ? rnd : -rnd;
				v = (v * 112 + vr) / 127;

				tbl[i] = (byte)(v - offs);
			}

			return tbl;
		}

		public static readonly FullRangeToVideoProcessor Instance = new();

		private FullRangeToVideoProcessor() { }

		void IConversionProcessor.ConvertLine(byte* istart, byte* ostart, nint cb)
		{
			Debug.Assert(istart == ostart);

			byte* ip = istart, ipe = istart + cb;

#if HWINTRINSICS
			if (HWIntrinsics.IsSupported && cb >= HWIntrinsics.VectorCount<byte>())
				convertIntrinsic(ip, ipe);
			else
#endif
				LutConverter.Convert(ip, ipe, videoTable);
		}

#if HWINTRINSICS
		[MethodImpl(MethodImplOptions.AggressiveOptimization)]
		private static void convertIntrinsic(byte* ip, byte* ipe)
		{
			if (Avx2.IsSupported)
			{
				var vrndp = Vector256.Create((short)(+63 - (112 << 7)));
				var vrndn = Vector256.Create((short)(-63 - (112 << 7)));
				var vmul = Vector256.Create((short)112);
				var vdiv = Vector256.Create((short)8257);
				var voff = Vector256.Create((byte)128);
				var vzer = Vector256<byte>.Zero;
				ipe -= Vector256<byte>.Count;

				var vlast = Avx.LoadVector256(ipe);

				LoopTop:
				do
				{
					var vi = Avx.LoadVector256(ip);
					var vp = Avx2.CompareGreaterThan(vzer.AsSByte(), vi.AsSByte());

					var vil = Avx2.UnpackLow(vi, vzer).AsInt16();
					var vih = Avx2.UnpackHigh(vi, vzer).AsInt16();
					vil = Avx2.MultiplyLow(vil, vmul);
					vih = Avx2.MultiplyLow(vih, vmul);

					var vml = Avx2.UnpackLow(vp, vp);
					var vmh = Avx2.UnpackHigh(vp, vp);
					var vrl = Avx2.BlendVariable(vrndn.AsByte(), vrndp.AsByte(), vml.AsByte()).AsInt16();
					var vrh = Avx2.BlendVariable(vrndn.AsByte(), vrndp.AsByte(), vmh.AsByte()).AsInt16();

					vil = Avx2.Add(vil, vrl);
					vih = Avx2.Add(vih, vrh);
					vil = Avx2.MultiplyHigh(vil, vdiv);
					vih = Avx2.MultiplyHigh(vih, vdiv);

					vil = Avx2.Add(Avx2.ShiftRightLogical(vil, 15), Avx2.ShiftRightArithmetic(vil, 4));
					vih = Avx2.Add(Avx2.ShiftRightLogical(vih, 15), Avx2.ShiftRightArithmetic(vih, 4));

					vi = Avx2.PackSignedSaturate(vil, vih).AsByte();
					vi = Avx2.Add(vi, voff);

					Avx.Store(ip, vi);
					ip += Vector256<byte>.Count;
				}
				while (ip <= ipe);

				if (ip < ipe + Vector256<byte>.Count)
				{
					nuint offs = UnsafeUtil.ByteOffset(ipe, ip);
					ip = UnsafeUtil.SubtractOffset(ip, offs);
					Avx.Store(ip, vlast);
					goto LoopTop;
				}
			}
			else
			{
				var vrndp = Vector128.Create((short)(+63 - (112 << 7)));
				var vrndn = Vector128.Create((short)(-63 - (112 << 7)));
				var vmul = Vector128.Create((short)112);
				var vdiv = Vector128.Create((short)8257);
				var voff = Vector128.Create((byte)128);
				var vzer = Vector128<byte>.Zero;
				ipe -= Vector128<byte>.Count;

				var vlast = Sse2.LoadVector128(ipe);

				LoopTop:
				do
				{
					var vi = Sse2.LoadVector128(ip);
					var vp = Sse2.CompareGreaterThan(vzer.AsSByte(), vi.AsSByte());

					var vil = Sse2.UnpackLow(vi, vzer).AsInt16();
					var vih = Sse2.UnpackHigh(vi, vzer).AsInt16();
					vil = Sse2.MultiplyLow(vil, vmul);
					vih = Sse2.MultiplyLow(vih, vmul);

					var vml = Sse2.UnpackLow(vp, vp);
					var vmh = Sse2.UnpackHigh(vp, vp);
					var vrl = Sse41.BlendVariable(vrndn.AsByte(), vrndp.AsByte(), vml.AsByte()).AsInt16();
					var vrh = Sse41.BlendVariable(vrndn.AsByte(), vrndp.AsByte(), vmh.AsByte()).AsInt16();

					vil = Sse2.Add(vil, vrl);
					vih = Sse2.Add(vih, vrh);
					vil = Sse2.MultiplyHigh(vil, vdiv);
					vih = Sse2.MultiplyHigh(vih, vdiv);

					vil = Sse2.Add(Sse2.ShiftRightLogical(vil, 15), Sse2.ShiftRightArithmetic(vil, 4));
					vih = Sse2.Add(Sse2.ShiftRightLogical(vih, 15), Sse2.ShiftRightArithmetic(vih, 4));

					vi = Sse2.PackSignedSaturate(vil, vih).AsByte();
					vi = Sse2.Add(vi, voff);

					Sse2.Store(ip, vi);
					ip += Vector128<byte>.Count;
				}
				while (ip <= ipe);

				if (ip < ipe + Vector128<byte>.Count)
				{
					nuint offs = UnsafeUtil.ByteOffset(ipe, ip);
					ip = UnsafeUtil.SubtractOffset(ip, offs);
					Sse2.Store(ip, vlast);
					goto LoopTop;
				}
			}
		}
#endif
	}
}

internal static unsafe class LutConverter
{
	public static void Convert(byte* ip, byte* ipe, byte[] lut)
	{
		fixed (byte* plut = &lut.GetDataRef())
		{
			ipe -= 4;
			while (ip <= ipe)
			{
				byte b0 = plut[(nuint)ip[0]];
				byte b1 = plut[(nuint)ip[1]];
				byte b2 = plut[(nuint)ip[2]];
				byte b3 = plut[(nuint)ip[3]];

				ip[0] = b0;
				ip[1] = b1;
				ip[2] = b2;
				ip[3] = b3;
				ip += 4;
			}
			ipe += 4;

			while (ip < ipe)
			{
				ip[0] = plut[(nuint)ip[0]];
				ip++;
			}
		}
	}
}
