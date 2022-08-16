// Copyright © Clinton Ingram and Contributors.  Licensed under the MIT License.

using System.Numerics;
using System.Runtime.CompilerServices;

#if HWINTRINSICS
using System.Runtime.Intrinsics;
using System.Runtime.Intrinsics.X86;
#endif

using static PhotoSauce.MagicScaler.MathUtil;

using VectorF = System.Numerics.Vector<float>;

namespace PhotoSauce.MagicScaler.Converters;

internal sealed class ConverterToLinear<TFrom, TTo> : IConverter<TFrom, TTo> where TFrom : unmanaged where TTo : unmanaged
{
	public IConversionProcessor<TFrom, TTo> Processor { get; }
	public IConversionProcessor<TFrom, TTo> Processor3A { get; }
	public IConversionProcessor<TFrom, TTo> Processor3X { get; }

	public ConverterToLinear(TTo[] inverseGammaTable)
	{
		Processor = new Converter(inverseGammaTable);
		Processor3A = new Converter3A(inverseGammaTable);
		Processor3X = new Converter3X(inverseGammaTable);
	}

	private sealed unsafe class Converter : IConversionProcessor<TFrom, TTo>
	{
		private readonly TTo[] igt;

		public Converter(TTo[] inverseGammaTable) => igt = inverseGammaTable;

		void IConversionProcessor.ConvertLine(byte* istart, byte* ostart, nint cb)
		{
			fixed (TTo* igtstart = &igt.GetDataRef())
			{
				if (typeof(TFrom) == typeof(byte) && typeof(TTo) == typeof(ushort))
					convertUQ15(istart, ostart, (ushort*)igtstart, cb);
				else if (typeof(TFrom) == typeof(byte) && typeof(TTo) == typeof(float))
					convertFloat(istart, ostart, (float*)igtstart, cb);
				else if (typeof(TFrom) == typeof(float) && typeof(TTo) == typeof(float))
					FloatConverter.Interpolating.ConvertFloat(istart, ostart, (float*)igtstart, LookupTables.InverseGammaScale, cb);
			}
		}

		private static void convertUQ15(byte* istart, byte* ostart, ushort* igtstart, nint cb)
		{
			byte* ip = istart, ipe = istart + cb;
			ushort* op = (ushort*)ostart, igt = igtstart;

			ipe -= 8;
			while (ip <= ipe)
			{
				ushort o0 = igt[(nuint)ip[0]];
				ushort o1 = igt[(nuint)ip[1]];
				ushort o2 = igt[(nuint)ip[2]];
				ushort o3 = igt[(nuint)ip[3]];
				op[0] = o0;
				op[1] = o1;
				op[2] = o2;
				op[3] = o3;

				o0 = igt[(nuint)ip[4]];
				o1 = igt[(nuint)ip[5]];
				o2 = igt[(nuint)ip[6]];
				o3 = igt[(nuint)ip[7]];
				op[4] = o0;
				op[5] = o1;
				op[6] = o2;
				op[7] = o3;

				ip += 8;
				op += 8;
			}
			ipe += 8;

			while (ip < ipe)
			{
				op[0] = igt[(nuint)ip[0]];
				ip++;
				op++;
			}
		}

		private static void convertFloat(byte* istart, byte* ostart, float* igtstart, nint cb)
		{
			byte* ip = istart, ipe = istart + cb;
			float* op = (float*)ostart, igt = igtstart;

#if HWINTRINSICS
			if (Avx2.IsSupported && HWIntrinsics.HasFastGather && cb >= Vector256<byte>.Count)
				convertFloatAvx2(ip, ipe, op, igt);
			else
#endif
				convertFloatScalar(ip, ipe, op, igt);
		}

#if HWINTRINSICS
		[MethodImpl(MethodImplOptions.AggressiveOptimization)]
		private static void convertFloatAvx2(byte*ip, byte* ipe, float* op, float* igt)
		{
			ipe -= Vector256<byte>.Count;

			var vlast = Avx.LoadVector256(ipe);

			LoopTop:
			do
			{
				var vi0 = Avx2.ConvertToVector256Int32(ip);
				var vi1 = Avx2.ConvertToVector256Int32(ip + Vector256<int>.Count);
				var vi2 = Avx2.ConvertToVector256Int32(ip + Vector256<int>.Count * 2);
				var vi3 = Avx2.ConvertToVector256Int32(ip + Vector256<int>.Count * 3);
				ip += Vector256<byte>.Count;

				var vf0 = Avx2.GatherVector256(igt, vi0, sizeof(float));
				var vf1 = Avx2.GatherVector256(igt, vi1, sizeof(float));
				var vf2 = Avx2.GatherVector256(igt, vi2, sizeof(float));
				var vf3 = Avx2.GatherVector256(igt, vi3, sizeof(float));

				Avx.Store(op, vf0);
				Avx.Store(op + Vector256<float>.Count, vf1);
				Avx.Store(op + Vector256<float>.Count * 2, vf2);
				Avx.Store(op + Vector256<float>.Count * 3, vf3);
				op += Vector256<float>.Count * 4;
			}
			while (ip <= ipe);

			if (ip < ipe + Vector256<byte>.Count)
			{
				nuint offs = UnsafeUtil.ByteOffset(ipe, ip);
				ip = UnsafeUtil.SubtractOffset(ip, offs);
				op = UnsafeUtil.SubtractOffset(op, UnsafeUtil.ConvertOffset<byte, float>(offs));
				Avx.Store(ip, vlast);
				goto LoopTop;
			}
		}
#endif

		private static void convertFloatScalar(byte* ip, byte* ipe, float* op, float* igt)
		{
			ipe -= 8;
			while (ip <= ipe)
			{
				float o0 = igt[(nuint)ip[0]];
				float o1 = igt[(nuint)ip[1]];
				float o2 = igt[(nuint)ip[2]];
				float o3 = igt[(nuint)ip[3]];
				float o4 = igt[(nuint)ip[4]];
				float o5 = igt[(nuint)ip[5]];
				float o6 = igt[(nuint)ip[6]];
				float o7 = igt[(nuint)ip[7]];
				ip += 8;

				op[0] = o0;
				op[1] = o1;
				op[2] = o2;
				op[3] = o3;
				op[4] = o4;
				op[5] = o5;
				op[6] = o6;
				op[7] = o7;
				op += 8;
			}
			ipe += 8;

			while (ip < ipe)
			{
				op[0] = igt[(nuint)ip[0]];
				ip++;
				op++;
			}
		}
	}

	private sealed unsafe class Converter3A : IConversionProcessor<TFrom, TTo>
	{
		private readonly TTo[] igt;

		public Converter3A(TTo[] inverseGammaTable) => igt = inverseGammaTable;

		void IConversionProcessor.ConvertLine(byte* istart, byte* ostart, nint cb)
		{
			fixed (TTo* igtstart = &igt.GetDataRef())
			{
				if (typeof(TFrom) == typeof(byte) && typeof(TTo) == typeof(ushort))
					convertUQ15(istart, ostart, (ushort*)igtstart, cb);
				else if (typeof(TFrom) == typeof(byte) && typeof(TTo) == typeof(float))
					convertFloat(istart, ostart, (float*)igtstart, cb);
				else if (typeof(TFrom) == typeof(float) && typeof(TTo) == typeof(float))
					FloatConverter.Interpolating.ConvertFloat3A(istart, ostart, (float*)igtstart, LookupTables.InverseGammaScale, cb);
			}
		}

		private static void convertUQ15(byte* istart, byte* ostart, ushort* igtstart, nint cb)
		{
			byte* ip = istart, ipe = istart + cb;
			ushort* op = (ushort*)ostart, igt = igtstart;

			while (ip < ipe)
			{
				uint i0 = igt[(nuint)ip[0]];
				uint i1 = igt[(nuint)ip[1]];
				uint i2 = igt[(nuint)ip[2]];
				uint i3 = Fix15(ip[3]);
				ip += 4;

				i0 = UnFix15(i0 * i3);
				i1 = UnFix15(i1 * i3);
				i2 = UnFix15(i2 * i3);

				op[0] = (ushort)i0;
				op[1] = (ushort)i1;
				op[2] = (ushort)i2;
				op[3] = (ushort)i3;
				op += 4;
			}
		}

		private static void convertFloat(byte* istart, byte* ostart, float* igtstart, nint cb)
		{
			byte* ip = istart, ipe = istart + cb;
			float* op = (float*)ostart, igt = igtstart;

#if HWINTRINSICS
			if (Avx2.IsSupported && cb >= Vector256<byte>.Count)
				convertFloatAvx2(ip, ipe, op, igt);
			else
#endif
				convertFloatScalar(ip, ipe, op, igt);
		}

#if HWINTRINSICS
		[MethodImpl(MethodImplOptions.AggressiveOptimization)]
		private static void convertFloatAvx2(byte* ip, byte* ipe, float* op, float* igt)
		{
			var vscale = Vector256.Create(1f / byte.MaxValue);
			ipe -= Vector256<byte>.Count;

			var vlast = Avx.LoadVector256(ipe);

			LoopTop:
			do
			{
				var vi0 = Avx2.ConvertToVector256Int32(ip);
				var vi1 = Avx2.ConvertToVector256Int32(ip + Vector256<int>.Count);
				var vi2 = Avx2.ConvertToVector256Int32(ip + Vector256<int>.Count * 2);
				var vi3 = Avx2.ConvertToVector256Int32(ip + Vector256<int>.Count * 3);
				ip += Vector256<byte>.Count;

				var vf0 = Avx2.GatherVector256(igt, vi0, sizeof(float));
				var vf1 = Avx2.GatherVector256(igt, vi1, sizeof(float));
				var vf2 = Avx2.GatherVector256(igt, vi2, sizeof(float));
				var vf3 = Avx2.GatherVector256(igt, vi3, sizeof(float));

				var via0 = Avx2.Shuffle(vi0, HWIntrinsics.ShuffleMaskAlpha);
				var via1 = Avx2.Shuffle(vi1, HWIntrinsics.ShuffleMaskAlpha);
				var via2 = Avx2.Shuffle(vi2, HWIntrinsics.ShuffleMaskAlpha);
				var via3 = Avx2.Shuffle(vi3, HWIntrinsics.ShuffleMaskAlpha);

				var vfa0 = Avx.ConvertToVector256Single(via0);
				var vfa1 = Avx.ConvertToVector256Single(via1);
				var vfa2 = Avx.ConvertToVector256Single(via2);
				var vfa3 = Avx.ConvertToVector256Single(via3);

				vfa0 = Avx.Multiply(vfa0, vscale);
				vfa1 = Avx.Multiply(vfa1, vscale);
				vfa2 = Avx.Multiply(vfa2, vscale);
				vfa3 = Avx.Multiply(vfa3, vscale);

				vf0 = Avx.Multiply(vf0, vfa0);
				vf1 = Avx.Multiply(vf1, vfa1);
				vf2 = Avx.Multiply(vf2, vfa2);
				vf3 = Avx.Multiply(vf3, vfa3);

				vf0 = Avx.Blend(vf0, vfa0, HWIntrinsics.BlendMaskAlpha);
				vf1 = Avx.Blend(vf1, vfa1, HWIntrinsics.BlendMaskAlpha);
				vf2 = Avx.Blend(vf2, vfa2, HWIntrinsics.BlendMaskAlpha);
				vf3 = Avx.Blend(vf3, vfa3, HWIntrinsics.BlendMaskAlpha);

				Avx.Store(op, vf0);
				Avx.Store(op + Vector256<float>.Count, vf1);
				Avx.Store(op + Vector256<float>.Count * 2, vf2);
				Avx.Store(op + Vector256<float>.Count * 3, vf3);
				op += Vector256<float>.Count * 4;
			}
			while (ip <= ipe);

			if (ip < ipe + Vector256<byte>.Count)
			{
				nuint offs = UnsafeUtil.ByteOffset(ipe, ip);
				ip = UnsafeUtil.SubtractOffset(ip, offs);
				op = UnsafeUtil.SubtractOffset(op, UnsafeUtil.ConvertOffset<byte, float>(offs));
				Avx.Store(ip, vlast);
				goto LoopTop;
			}
		}
#endif

		private static void convertFloatScalar(byte* ip, byte* ipe, float* op, float* igt)
		{
			fixed (float* atstart = &LookupTables.Alpha.GetDataRef())
			{
				float* at = atstart;

				while (ip < ipe)
				{
					float o0 = igt[(nuint)ip[0]];
					float o1 = igt[(nuint)ip[1]];
					float o2 = igt[(nuint)ip[2]];
					float o3 =  at[(nuint)ip[3]];
					ip += 4;

					op[0] = o0 * o3;
					op[1] = o1 * o3;
					op[2] = o2 * o3;
					op[3] = o3;
					op += 4;
				}
			}
		}
	}

	private sealed unsafe class Converter3X : IConversionProcessor<TFrom, TTo>
	{
		private readonly TTo[] igt;

		public Converter3X(TTo[] inverseGammaTable) => igt = inverseGammaTable;

		void IConversionProcessor.ConvertLine(byte* istart, byte* ostart, nint cb)
		{
			fixed (TTo* igtstart = &igt.GetDataRef())
			{
				if (typeof(TFrom) == typeof(byte) && typeof(TTo) == typeof(float))
					convertFloat(istart, ostart, (float*)igtstart, cb);
			}
		}

#if HWINTRINSICS
		[MethodImpl(MethodImplOptions.AggressiveOptimization)]
#endif
		private static void convertFloat(byte* istart, byte* ostart, float* igtstart, nint cb)
		{
			byte* ip = istart, ipe = istart + cb;
			float* op = (float*)ostart, igt = igtstart;

			float z = Vector4.Zero.X;
			while (ip < ipe)
			{
				float o0 = igt[(nuint)ip[0]];
				float o1 = igt[(nuint)ip[1]];
				float o2 = igt[(nuint)ip[2]];
				ip += 3;

				op[0] = o0;
				op[1] = o1;
				op[2] = o2;
				op[3] = z;
				op += 4;
			}
		}
	}
}

internal sealed class ConverterFromLinear<TFrom, TTo> : IConverter<TFrom, TTo> where TFrom : unmanaged where TTo : unmanaged
{
	public IConversionProcessor<TFrom, TTo> Processor { get; }
	public IConversionProcessor<TFrom, TTo> Processor3A { get; }
	public IConversionProcessor<TFrom, TTo> Processor3X { get; }

	public ConverterFromLinear(TTo[] gammaTable)
	{
		Processor = new Converter(gammaTable);
		Processor3A = new Converter3A(gammaTable);
		Processor3X = new Converter3X(gammaTable);
	}

	private sealed unsafe class Converter : IConversionProcessor<TFrom, TTo>
	{
		private readonly TTo[] gt;

		public Converter(TTo[] gammaTable) => gt = gammaTable;

		void IConversionProcessor.ConvertLine(byte* istart, byte* ostart, nint cb)
		{
			fixed (TTo* gtstart = &gt.GetDataRef())
			{
				if (typeof(TFrom) == typeof(ushort) && typeof(TTo) == typeof(byte))
					convertUQ15(istart, ostart, (byte*)gtstart, cb);
				else if (typeof(TFrom) == typeof(float) && typeof(TTo) == typeof(byte))
					convertFloat(istart, ostart, (byte*)gtstart, cb);
				else if (typeof(TFrom) == typeof(float) && typeof(TTo) == typeof(float))
					FloatConverter.Interpolating.ConvertFloat(istart, ostart, (float*)gtstart, LookupTables.GammaScaleFloat, cb);
			}
		}

		private static void convertUQ15(byte* istart, byte* ostart, byte* gtstart, nint cb)
		{
			ushort* ip = (ushort*)istart, ipe = (ushort*)(istart + cb);
			byte* op = ostart, gt = gtstart;

			ipe -= 4;
			while (ip <= ipe)
			{
				uint i0 = ClampToUQ15One((uint)ip[0]);
				uint i1 = ClampToUQ15One((uint)ip[1]);
				uint i2 = ClampToUQ15One((uint)ip[2]);
				uint i3 = ClampToUQ15One((uint)ip[3]);
				ip += 4;

				op[0] = gt[i0];
				op[1] = gt[i1];
				op[2] = gt[i2];
				op[3] = gt[i3];
				op += 4;
			}
			ipe += 4;

			while (ip < ipe)
			{
				op[0] = gt[(nuint)ClampToUQ15One((uint)ip[0])];
				ip++;
				op++;
			}
		}

		private static void convertFloat(byte* istart, byte* ostart, byte* gtstart, nint cb)
		{
			float* ip = (float*)istart, ipe = (float*)(istart + cb);
			byte* op = ostart, gt = gtstart;

#if HWINTRINSICS
			if (Avx2.IsSupported && cb >= Vector256<byte>.Count * 4)
				convertFloatAvx2(ip, ipe, op, gt);
			else
#endif
			if (cb >= Vector<byte>.Count)
				convertFloatVector(ip, ipe, op, gt);
			else
				convertFloatScalar(ip, ipe, op, gt);
		}

#if HWINTRINSICS
		[MethodImpl(MethodImplOptions.AggressiveOptimization)]
		private static void convertFloatAvx2(float* ip, float* ipe, byte* op, byte* gt)
		{
			var vmin = Vector256<int>.Zero;
			var vscale = Vector256.Create((float)UQ15One);
			var vmaxuq15 = Vector256.Create((int)UQ15One);
			var vmaxbyte = Vector256.Create((int)byte.MaxValue);

			var vmaskp = Avx.LoadVector256((int*)HWIntrinsics.PermuteMaskDeinterleave8x32.GetAddressOf());
			ipe -= Vector256<float>.Count * 4;

			LoopTop:
			do
			{
				var vf0 = Avx.Multiply(vscale, Avx.LoadVector256(ip));
				var vf1 = Avx.Multiply(vscale, Avx.LoadVector256(ip + Vector256<float>.Count));
				var vf2 = Avx.Multiply(vscale, Avx.LoadVector256(ip + Vector256<float>.Count * 2));
				var vf3 = Avx.Multiply(vscale, Avx.LoadVector256(ip + Vector256<float>.Count * 3));
				ip += Vector256<float>.Count * 4;

				var vi0 = Avx.ConvertToVector256Int32(vf0);
				var vi1 = Avx.ConvertToVector256Int32(vf1);
				var vi2 = Avx.ConvertToVector256Int32(vf2);
				var vi3 = Avx.ConvertToVector256Int32(vf3);

				vi0 = Avx2.Min(Avx2.Max(vmin, vi0), vmaxuq15);
				vi1 = Avx2.Min(Avx2.Max(vmin, vi1), vmaxuq15);
				vi2 = Avx2.Min(Avx2.Max(vmin, vi2), vmaxuq15);
				vi3 = Avx2.Min(Avx2.Max(vmin, vi3), vmaxuq15);

				vi0 = Avx2.GatherVector256((int*)gt, vi0, sizeof(byte));
				vi1 = Avx2.GatherVector256((int*)gt, vi1, sizeof(byte));
				vi2 = Avx2.GatherVector256((int*)gt, vi2, sizeof(byte));
				vi3 = Avx2.GatherVector256((int*)gt, vi3, sizeof(byte));

				vi0 = Avx2.And(vi0, vmaxbyte);
				vi1 = Avx2.And(vi1, vmaxbyte);
				vi2 = Avx2.And(vi2, vmaxbyte);
				vi3 = Avx2.And(vi3, vmaxbyte);

				var vs0 = Avx2.PackSignedSaturate(vi0, vi1);
				var vs1 = Avx2.PackSignedSaturate(vi2, vi3);

				var vb0 = Avx2.PackUnsignedSaturate(vs0, vs1);
				vb0 = Avx2.PermuteVar8x32(vb0.AsInt32(), vmaskp).AsByte();

				Avx.Store(op, vb0);
				op += Vector256<byte>.Count;
			}
			while (ip <= ipe);

			if (ip < ipe + Vector256<float>.Count * 4)
			{
				nuint offs = UnsafeUtil.ByteOffset(ipe, ip);
				ip = UnsafeUtil.SubtractOffset(ip, offs);
				op = UnsafeUtil.SubtractOffset(op, UnsafeUtil.ConvertOffset<float, byte>(offs));
				goto LoopTop;
			}
		}
#endif

		private static void convertFloatVector(float* ip, float* ipe, byte* op, byte* gt)
		{
			var vmin = VectorF.Zero;
			var vmax = new VectorF(UQ15One);
			var vround = new VectorF(0.5f);
			ipe -= VectorF.Count;

			LoopTop:
			do
			{
				var v = Unsafe.ReadUnaligned<VectorF>(ip) * vmax + vround;
				v = v.Clamp(vmin, vmax);
				ip += VectorF.Count;

#if VECTOR_CONVERT
				var vi = Vector.AsVectorUInt32(Vector.ConvertToInt32(v));
#else
				var vi = v;
#endif

				byte o0 = gt[(nuint)vi[0]];
				byte o1 = gt[(nuint)vi[1]];
				byte o2 = gt[(nuint)vi[2]];
				byte o3 = gt[(nuint)vi[3]];
				op[0] = o0;
				op[1] = o1;
				op[2] = o2;
				op[3] = o3;

				if (VectorF.Count == 8)
				{
					o0 = gt[(nuint)vi[4]];
					o1 = gt[(nuint)vi[5]];
					o2 = gt[(nuint)vi[6]];
					o3 = gt[(nuint)vi[7]];
					op[4] = o0;
					op[5] = o1;
					op[6] = o2;
					op[7] = o3;
				}
				op += VectorF.Count;
			}
			while (ip <= ipe);

			if (ip < ipe + VectorF.Count)
			{
				nuint offs = UnsafeUtil.ByteOffset(ipe, ip);
				ip = UnsafeUtil.SubtractOffset(ip, offs);
				op = UnsafeUtil.SubtractOffset(op, UnsafeUtil.ConvertOffset<float, byte>(offs));
				goto LoopTop;
			}
		}

		private static void convertFloatScalar(float* ip, float* ipe, byte* op, byte* gt)
		{
			while (ip < ipe)
			{
				op[0] = gt[(nuint)FixToUQ15One(ip[0])];
				ip++;
				op++;
			}
		}
	}

	private sealed unsafe class Converter3A : IConversionProcessor<TFrom, TTo>
	{
		private readonly TTo[] gt;

		public Converter3A(TTo[] gammaTable) => gt = gammaTable;

		void IConversionProcessor.ConvertLine(byte* istart, byte* ostart, nint cb)
		{
			fixed (TTo* gtstart = &gt.GetDataRef())
			{
				if (typeof(TFrom) == typeof(ushort) && typeof(TTo) == typeof(byte))
					convertUQ15(istart, ostart, (byte*)gtstart, cb);
				else if (typeof(TFrom) == typeof(float) && typeof(TTo) == typeof(byte))
					convertFloat(istart, ostart, (byte*)gtstart, cb);
				else if (typeof(TFrom) == typeof(float) && typeof(TTo) == typeof(float))
					FloatConverter.Interpolating.ConvertFloat3A(istart, ostart, (float*)gtstart, LookupTables.GammaScaleFloat, cb);
			}
		}

		private static void convertUQ15(byte* istart, byte* ostart, byte* gtstart, nint cb)
		{
			ushort* ip = (ushort*)istart, ipe = (ushort*)(istart + cb);
			byte* op = ostart, gt = gtstart;

			while (ip < ipe)
			{
				uint i3 = ip[3];
				byte o3 = UnFix15ToByte(i3 * byte.MaxValue);
				if (o3 == 0)
				{
					*(uint*)op = 0;
				}
				else
				{
					uint o3i = UQ15One * UQ15One / i3;
					uint i0 = ip[0];
					uint i1 = ip[1];
					uint i2 = ip[2];

					byte o0 = gt[(nuint)UnFixToUQ15One(i0 * o3i)];
					byte o1 = gt[(nuint)UnFixToUQ15One(i1 * o3i)];
					byte o2 = gt[(nuint)UnFixToUQ15One(i2 * o3i)];

					op[0] = o0;
					op[1] = o1;
					op[2] = o2;
					op[3] = o3;
				}

				ip += 4;
				op += 4;
			}
		}

		private static void convertFloat(byte* istart, byte* ostart, byte* gtstart, nint cb)
		{
			float* ip = (float*)istart, ipe = (float*)(istart + cb);
			byte* op = ostart, gt = gtstart;

#if HWINTRINSICS
			if (Avx2.IsSupported && cb >= Vector256<byte>.Count * 4)
				convertFloatAvx2(ip, ipe, op, gt);
			else
#endif
				convertFloatScalar(ip, ipe, op, gt);
		}

#if HWINTRINSICS
		[MethodImpl(MethodImplOptions.AggressiveOptimization)]
		private static void convertFloatAvx2(float* ip, float* ipe, byte* op, byte* gt)
		{
			var vzero = Vector256<float>.Zero;
			var vmin = Vector256.Create(0.5f / byte.MaxValue);
			var vmsk = Vector256.Create((int)byte.MaxValue);

			var vmaskp = Avx.LoadVector256((int*)HWIntrinsics.PermuteMaskDeinterleave8x32.GetAddressOf());
			var vscalf = Avx.BroadcastVector128ToVector256((float*)HWIntrinsics.ScaleUQ15WithAlphaFloat.GetAddressOf());
			var vscali = Avx2.BroadcastVector128ToVector256((int*)HWIntrinsics.ScaleUQ15WithAlphaInt.GetAddressOf());
			ipe -= Vector256<float>.Count * 4;

			LoopTop:
			do
			{
				var vf0 = Avx.LoadVector256(ip);
				var vf1 = Avx.LoadVector256(ip + Vector256<float>.Count);
				var vf2 = Avx.LoadVector256(ip + Vector256<float>.Count * 2);
				var vf3 = Avx.LoadVector256(ip + Vector256<float>.Count * 3);
				ip += Vector256<float>.Count * 4;

				var vfa0 = Avx.Shuffle(vf0, vf0, HWIntrinsics.ShuffleMaskAlpha);
				var vfa1 = Avx.Shuffle(vf1, vf1, HWIntrinsics.ShuffleMaskAlpha);
				var vfa2 = Avx.Shuffle(vf2, vf2, HWIntrinsics.ShuffleMaskAlpha);
				var vfa3 = Avx.Shuffle(vf3, vf3, HWIntrinsics.ShuffleMaskAlpha);

				var vfr0 = Avx.AndNot(HWIntrinsics.AvxCompareLessThan(vfa0, vmin), Avx.Reciprocal(vfa0));
				var vfr1 = Avx.AndNot(HWIntrinsics.AvxCompareLessThan(vfa1, vmin), Avx.Reciprocal(vfa1));
				var vfr2 = Avx.AndNot(HWIntrinsics.AvxCompareLessThan(vfa2, vmin), Avx.Reciprocal(vfa2));
				var vfr3 = Avx.AndNot(HWIntrinsics.AvxCompareLessThan(vfa3, vmin), Avx.Reciprocal(vfa3));

				vf0 = Avx.Multiply(vf0, vfr0);
				vf1 = Avx.Multiply(vf1, vfr1);
				vf2 = Avx.Multiply(vf2, vfr2);
				vf3 = Avx.Multiply(vf3, vfr3);

				vf0 = Avx.Blend(vf0, vfa0, HWIntrinsics.BlendMaskAlpha);
				vf1 = Avx.Blend(vf1, vfa1, HWIntrinsics.BlendMaskAlpha);
				vf2 = Avx.Blend(vf2, vfa2, HWIntrinsics.BlendMaskAlpha);
				vf3 = Avx.Blend(vf3, vfa3, HWIntrinsics.BlendMaskAlpha);

				vf0 = Avx.Multiply(vf0, vscalf);
				vf1 = Avx.Multiply(vf1, vscalf);
				vf2 = Avx.Multiply(vf2, vscalf);
				vf3 = Avx.Multiply(vf3, vscalf);

				var vi0 = Avx.ConvertToVector256Int32(vf0);
				var vi1 = Avx.ConvertToVector256Int32(vf1);
				var vi2 = Avx.ConvertToVector256Int32(vf2);
				var vi3 = Avx.ConvertToVector256Int32(vf3);

				vi0 = Avx2.Min(Avx2.Max(vzero.AsInt32(), vi0), vscali);
				vi1 = Avx2.Min(Avx2.Max(vzero.AsInt32(), vi1), vscali);
				vi2 = Avx2.Min(Avx2.Max(vzero.AsInt32(), vi2), vscali);
				vi3 = Avx2.Min(Avx2.Max(vzero.AsInt32(), vi3), vscali);

				var vg0 = Avx2.GatherVector256((int*)gt, vi0, sizeof(byte));
				var vg1 = Avx2.GatherVector256((int*)gt, vi1, sizeof(byte));
				var vg2 = Avx2.GatherVector256((int*)gt, vi2, sizeof(byte));
				var vg3 = Avx2.GatherVector256((int*)gt, vi3, sizeof(byte));

				vi0 = Avx2.Blend(vg0, vi0, HWIntrinsics.BlendMaskAlpha);
				vi1 = Avx2.Blend(vg1, vi1, HWIntrinsics.BlendMaskAlpha);
				vi2 = Avx2.Blend(vg2, vi2, HWIntrinsics.BlendMaskAlpha);
				vi3 = Avx2.Blend(vg3, vi3, HWIntrinsics.BlendMaskAlpha);

				vi0 = Avx2.And(vi0, vmsk);
				vi1 = Avx2.And(vi1, vmsk);
				vi2 = Avx2.And(vi2, vmsk);
				vi3 = Avx2.And(vi3, vmsk);

				var vs0 = Avx2.PackSignedSaturate(vi0, vi1);
				var vs1 = Avx2.PackSignedSaturate(vi2, vi3);

				var vb0 = Avx2.PackUnsignedSaturate(vs0, vs1);
				vb0 = Avx2.PermuteVar8x32(vb0.AsInt32(), vmaskp).AsByte();

				Avx.Store(op, vb0);
				op += Vector256<byte>.Count;
			}
			while (ip <= ipe);

			if (ip < ipe + Vector256<float>.Count * 4)
			{
				nuint offs = UnsafeUtil.ByteOffset(ipe, ip);
				ip = UnsafeUtil.SubtractOffset(ip, offs);
				op = UnsafeUtil.SubtractOffset(op, UnsafeUtil.ConvertOffset<float, byte>(offs));
				goto LoopTop;
			}
		}
#endif

		private static void convertFloatScalar(float* ip, float* ipe, byte* op, byte* gt)
		{
			float fmax = new Vector4(byte.MaxValue).X, fround = new Vector4(0.5f).X, fmin = fround / fmax;

			while (ip < ipe)
			{
				float f3 = ip[3];
				if (f3 < fmin)
				{
					*(uint*)op = 0;
				}
				else
				{
					float f3i = UQ15One / f3;
					byte o0 = gt[(nuint)ClampToUQ15One((int)(ip[0] * f3i + fround))];
					byte o1 = gt[(nuint)ClampToUQ15One((int)(ip[1] * f3i + fround))];
					byte o2 = gt[(nuint)ClampToUQ15One((int)(ip[2] * f3i + fround))];
					byte o3 = ClampToByte((int)(f3 * fmax + fround));
					op[0] = o0;
					op[1] = o1;
					op[2] = o2;
					op[3] = o3;
				}

				ip += 4;
				op += 4;
			}
		}
	}

	private sealed unsafe class Converter3X : IConversionProcessor<TFrom, TTo>
	{
		private readonly TTo[] gt;

		public Converter3X(TTo[] gammaTable) => gt = gammaTable;

		void IConversionProcessor.ConvertLine(byte* istart, byte* ostart, nint cb)
		{
			fixed (TTo* gtstart = &gt.GetDataRef())
			{
				if (typeof(TFrom) == typeof(float) && typeof(TTo) == typeof(byte))
					convertFloat(istart, ostart, (byte*)gtstart, cb);
			}
		}

		private static void convertFloat(byte* istart, byte* ostart, byte* gtstart, nint cb)
		{
			float* ip = (float*)istart, ipe = (float*)(istart + cb);
			byte* op = ostart, gt = gtstart;

#if HWINTRINSICS
			if (Avx2.IsSupported && cb >= Vector256<byte>.Count * 4)
			{
				var vmin = Vector256<int>.Zero;
				var vscale = Vector256.Create((float)UQ15One);
				var vmaxuq15 = Vector256.Create((int)UQ15One);
				var vmaxbyte = Vector256.Create((int)byte.MaxValue);

				var vmaskg = Avx2.BroadcastVector128ToVector256((int*)HWIntrinsics.GatherMask3x.GetAddressOf());
				var vmaskp = Avx.LoadVector256((int*)HWIntrinsics.PermuteMaskDeinterleave8x32.GetAddressOf());
				var vmaskq = Avx.LoadVector256((int*)HWIntrinsics.PermuteMask3xTo3Chan.GetAddressOf());
				var vmasks = Avx2.BroadcastVector128ToVector256(HWIntrinsics.ShuffleMask3xTo3Chan.GetAddressOf());

				ipe -= Vector256<float>.Count * 4;
				while (true)
				{
					var vf0 = Avx.Multiply(Avx.LoadVector256(ip), vscale);
					var vf1 = Avx.Multiply(Avx.LoadVector256(ip + Vector256<float>.Count), vscale);
					var vf2 = Avx.Multiply(Avx.LoadVector256(ip + Vector256<float>.Count * 2), vscale);
					var vf3 = Avx.Multiply(Avx.LoadVector256(ip + Vector256<float>.Count * 3), vscale);
					ip += Vector256<float>.Count * 4;

					var vi0 = Avx.ConvertToVector256Int32(vf0);
					var vi1 = Avx.ConvertToVector256Int32(vf1);
					var vi2 = Avx.ConvertToVector256Int32(vf2);
					var vi3 = Avx.ConvertToVector256Int32(vf3);

					var vl0 = Avx2.Min(Avx2.Max(vmin, vi0), vmaxuq15);
					var vl1 = Avx2.Min(Avx2.Max(vmin, vi1), vmaxuq15);
					var vl2 = Avx2.Min(Avx2.Max(vmin, vi2), vmaxuq15);
					var vl3 = Avx2.Min(Avx2.Max(vmin, vi3), vmaxuq15);

					vi0 = Avx2.GatherMaskVector256(vi0, (int*)gt, vl0, vmaskg, sizeof(byte));
					vi1 = Avx2.GatherMaskVector256(vi1, (int*)gt, vl1, vmaskg, sizeof(byte));
					vi2 = Avx2.GatherMaskVector256(vi2, (int*)gt, vl2, vmaskg, sizeof(byte));
					vi3 = Avx2.GatherMaskVector256(vi3, (int*)gt, vl3, vmaskg, sizeof(byte));

					vi0 = Avx2.And(vi0, vmaxbyte);
					vi1 = Avx2.And(vi1, vmaxbyte);
					vi2 = Avx2.And(vi2, vmaxbyte);
					vi3 = Avx2.And(vi3, vmaxbyte);

					var vs0 = Avx2.PackSignedSaturate(vi0, vi1);
					var vs1 = Avx2.PackSignedSaturate(vi2, vi3);

					var vb0 = Avx2.PackUnsignedSaturate(vs0, vs1);
					vb0 = Avx2.PermuteVar8x32(vb0.AsInt32(), vmaskp).AsByte();

					vb0 = Avx2.Shuffle(vb0, vmasks);
					vb0 = Avx2.PermuteVar8x32(vb0.AsInt32(), vmaskq).AsByte();

					if (ip >= ipe)
						goto LastBlock;

					Avx.Store(op, vb0);
					op += Vector256<byte>.Count * 3 / 4;
					continue;

					LastBlock:
					Sse2.Store(op, vb0.GetLower());
					Sse2.StoreScalar((long*)(op + Vector128<byte>.Count), vb0.GetUpper().AsInt64());
					op += Vector256<byte>.Count * 3 / 4;
					break;
				}
				ipe += Vector256<float>.Count * 4;
			}
			else
#endif
			{
				var vmin = VectorF.Zero;
				var vmax = new VectorF(UQ15One);
				var vround = new VectorF(0.5f);

				ipe -= VectorF.Count;
				while (ip <= ipe)
				{
					var v = Unsafe.ReadUnaligned<VectorF>(ip) * vmax + vround;
					v = v.Clamp(vmin, vmax);
					ip += VectorF.Count;

#if VECTOR_CONVERT
					var vi = Vector.ConvertToInt32(v);
#else
					var vi = v;
#endif

					byte o0 = gt[(nuint)vi[0]];
					byte o1 = gt[(nuint)vi[1]];
					byte o2 = gt[(nuint)vi[2]];
					op[0] = o0;
					op[1] = o1;
					op[2] = o2;

					if (VectorF.Count == 8)
					{
						o0 = gt[(nuint)vi[4]];
						o1 = gt[(nuint)vi[5]];
						o2 = gt[(nuint)vi[6]];
						op[3] = o0;
						op[4] = o1;
						op[5] = o2;
					}
					op += VectorF.Count * 3 / 4;
				}
				ipe += VectorF.Count;
			}

			while (ip < ipe)
			{
				op[0] = gt[(nuint)FixToUQ15One(ip[0])];
				op[1] = gt[(nuint)FixToUQ15One(ip[1])];
				op[2] = gt[(nuint)FixToUQ15One(ip[2])];

				ip += 4;
				op += 3;
			}
		}
	}
}
