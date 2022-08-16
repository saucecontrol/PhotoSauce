// Copyright © Clinton Ingram and Contributors.  Licensed under the MIT License.

#if HWINTRINSICS
using System;
using System.Runtime.Intrinsics;
using System.Runtime.Intrinsics.X86;
using System.Runtime.CompilerServices;

using VectorAvx = System.Runtime.Intrinsics.Vector256<float>;
using VectorSse = System.Runtime.Intrinsics.Vector128<float>;

namespace PhotoSauce.MagicScaler.Transforms;

internal sealed partial class Convolver4ChanIntrinsic : IConvolver
{
	[MethodImpl(MethodImplOptions.AggressiveOptimization)]
	unsafe void IConvolver.SharpenLine(byte* cstart, byte* ystart, byte* bstart, byte* ostart, int ox, int ow, float amt, float thresh, bool gamma)
	{
		float* ip = (float*)cstart + (uint)ox * channels, yp = (float*)ystart + (uint)ox, bp = (float*)bstart, op = (float*)ostart;
		float* ipe = ip + (uint)ow * channels;

		bool threshold = thresh > 0f;

		var vmin = VectorSse.Zero;
		var vamt = Vector128.Create(amt, amt, amt, 0f);

		while (ip < ipe)
		{
			float dif = *yp++ - *bp++;
			var v0 = Sse.LoadVector128(ip);
			ip += VectorSse.Count;

			if (!threshold || Math.Abs(dif) > thresh)
			{
				var vd = Sse.Multiply(Vector128.Create(dif), vamt);

				if (gamma)
				{
					v0 = Sse.Max(v0, vmin);
					v0 = Sse.Multiply(v0, Sse.ReciprocalSqrt(v0));
					v0 = Sse.Add(v0, vd);
					v0 = Sse.Max(v0, vmin);
					v0 = Sse.Multiply(v0, v0);
				}
				else
				{
					v0 = Sse.Add(v0, vd);
				}
			}

			Sse.Store(op, v0);
			op += VectorSse.Count;
		}
	}
}

internal sealed partial class Convolver3ChanIntrinsic : IConvolver
{
	unsafe void IConvolver.SharpenLine(byte* cstart, byte* ystart, byte* bstart, byte* ostart, int ox, int ow, float amt, float thresh, bool gamma) =>
		throw new NotImplementedException();
}

internal sealed partial class Convolver1ChanIntrinsic : IConvolver
{
	[MethodImpl(MethodImplOptions.AggressiveOptimization)]
	unsafe void IConvolver.SharpenLine(byte* cstart, byte* ystart, byte* bstart, byte* ostart, int ox, int ow, float amt, float thresh, bool gamma)
	{
		float* ip = (float*)cstart + (uint)ox * channels, yp = (float*)ystart + (uint)ox, bp = (float*)bstart, op = (float*)ostart;
		float* ipe = ip + (uint)ow * channels;

		bool threshold = thresh > 0f;

		if (Avx.IsSupported && ip <= ipe - VectorAvx.Count)
		{
			var vthresh = Vector256.Create(threshold ? thresh : -1f);
			var vmsk = Vector256.Create(0x7fffffff).AsSingle();
			var vamt = Vector256.Create(amt);
			var vmin = VectorAvx.Zero;

			ipe -= VectorAvx.Count;
			do
			{
				var vd = Avx.Subtract(Avx.LoadVector256(yp), Avx.LoadVector256(bp));
				yp += VectorAvx.Count;
				bp += VectorAvx.Count;

				if (threshold)
				{
#if NET5_0_OR_GREATER
					var sm = Avx.CompareGreaterThan(Avx.And(vd, vmsk), vthresh);
#else
					var sm = Avx.Compare(Avx.And(vd, vmsk), vthresh, FloatComparisonMode.OrderedGreaterThanSignaling); // https://github.com/dotnet/runtime/issues/31193
#endif
					vd = Avx.And(vd, sm);
				}
				vd = Avx.Multiply(vd, vamt);

				var v0 = Avx.LoadVector256(ip);
				ip += VectorAvx.Count;

				if (gamma)
				{
					v0 = Avx.Max(v0, vmin);
					v0 = Avx.Multiply(v0, Avx.ReciprocalSqrt(v0));
					v0 = Avx.Add(v0, vd);
					v0 = Avx.Max(v0, vmin);
					v0 = Avx.Multiply(v0, v0);
				}
				else
				{
					v0 = Avx.Add(v0, vd);
				}

				Avx.Store(op, v0);
				op += VectorAvx.Count;
			}
			while (ip <= ipe);
			ipe += VectorAvx.Count;
		}
		else if (ip <= ipe - VectorSse.Count)
		{
			var vthresh = Vector128.Create(threshold ? thresh : -1f);
			var vmsk = Vector128.Create(0x7fffffff).AsSingle();
			var vamt = Vector128.Create(amt);
			var vmin = VectorSse.Zero;

			ipe -= VectorSse.Count;
			do
			{
				var vd = Sse.Subtract(Sse.LoadVector128(yp), Sse.LoadVector128(bp));
				yp += VectorSse.Count;
				bp += VectorSse.Count;

				if (threshold)
				{
					var sm = Sse.CompareGreaterThan(Sse.And(vd, vmsk), vthresh);
					vd = Sse.And(vd, sm);
				}
				vd = Sse.Multiply(vd, vamt);

				var v0 = Sse.LoadVector128(ip);
				ip += VectorSse.Count;

				if (gamma)
				{
					v0 = Sse.Max(v0, vmin);
					v0 = Sse.Multiply(v0, Sse.ReciprocalSqrt(v0));
					v0 = Sse.Add(v0, vd);
					v0 = Sse.Max(v0, vmin);
					v0 = Sse.Multiply(v0, v0);
				}
				else
				{
					v0 = Sse.Add(v0, vd);
				}

				Sse.Store(op, v0);
				op += VectorSse.Count;
			}
			while (ip <= ipe);
			ipe += VectorSse.Count;
		}

		float fmin = VectorSse.Zero.ToScalar();

		while (ip < ipe)
		{
			float dif = *yp++ - *bp++;
			float c0 = *ip++;

			if (!threshold || Math.Abs(dif) > thresh)
			{
				dif *= amt;

				if (gamma)
				{
					c0 = MathUtil.FastMax(c0, fmin).Sqrt();
					c0 = MathUtil.FastMax(c0 + dif, fmin);
					c0 *= c0;
				}
				else
				{
					c0 += dif;
				}
			}

			*op++ = c0;
		}
	}
}
#endif
