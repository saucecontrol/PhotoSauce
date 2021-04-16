// Copyright © Clinton Ingram and Contributors.  Licensed under the MIT License.

using System;
using System.Numerics;
using System.Runtime.CompilerServices;

using VectorF = System.Numerics.Vector<float>;

namespace PhotoSauce.MagicScaler.Transforms
{
	internal sealed partial class Convolver4ChanVector : IConvolver
	{
		unsafe void IConvolver.SharpenLine(byte* cstart, byte* ystart, byte* bstart, byte* ostart, int ox, int ow, float amt, float thresh, bool gamma)
		{
			float* ip = (float*)cstart + (uint)ox * channels, yp = (float*)ystart + (uint)ox, bp = (float*)bstart, op = (float*)ostart;
			float* ipe = ip + (uint)ow * channels;

			bool threshold = thresh > 0f;

			var vmin = Vector4.Zero;
			var vamt = new Vector4(amt) { W = 0f };

			while (ip < ipe)
			{
				float dif = *yp++ - *bp++;
				var v0 = Unsafe.ReadUnaligned<Vector4>(ip);
				ip += vector4Count;

				if (!threshold || Math.Abs(dif) > thresh)
				{
					var vd = new Vector4(dif) * vamt;

					if (gamma)
					{
						v0 = Vector4.SquareRoot(Vector4.Max(v0, vmin));
						v0 = Vector4.Max(v0 + vd, vmin);
						v0 *= v0;
					}
					else
					{
						v0 += vd;
					}
				}

				Unsafe.WriteUnaligned(op, v0);
				op += vector4Count;
			}
		}
	}

	internal sealed partial class Convolver3ChanVector : IConvolver
	{
		unsafe void IConvolver.SharpenLine(byte* cstart, byte* ystart, byte* bstart, byte* ostart, int ox, int ow, float amt, float thresh, bool gamma) => throw new NotImplementedException();
	}

	internal sealed partial class Convolver1ChanVector : IConvolver
	{
		unsafe void IConvolver.SharpenLine(byte* cstart, byte* ystart, byte* bstart, byte* ostart, int ox, int ow, float amt, float thresh, bool gamma)
		{
			float* ip = (float*)cstart + (uint)ox * channels, yp = (float*)ystart + (uint)ox, bp = (float*)bstart, op = (float*)ostart;
			float* ipe = ip + (uint)ow * channels;

			bool threshold = thresh > 0f;

			var vmin = VectorF.Zero;
			var vthresh = new VectorF(threshold ? thresh : -1f);
			var vamt = new VectorF(amt);
			float fmin = vmin[0];

			ipe -= VectorF.Count;
			while (ip <= ipe)
			{
				var vd = Unsafe.ReadUnaligned<VectorF>(yp) - Unsafe.ReadUnaligned<VectorF>(bp);
				yp += VectorF.Count;
				bp += VectorF.Count;

				if (threshold)
				{
					var sm = Vector.GreaterThan(Vector.Abs(vd), vthresh);
					vd = Vector.ConditionalSelect(sm, vd, vmin);
				}
				vd *= vamt;

				var v0 = Unsafe.ReadUnaligned<VectorF>(ip);
				ip += VectorF.Count;

				if (gamma)
				{
					v0 = Vector.SquareRoot(Vector.Max(v0, vmin));
					v0 = Vector.Max(v0 + vd, vmin);
					v0 *= v0;
				}
				else
				{
					v0 += vd;
				}

				Unsafe.WriteUnaligned(op, v0);
				op += VectorF.Count;
			}
			ipe += VectorF.Count;

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
}
