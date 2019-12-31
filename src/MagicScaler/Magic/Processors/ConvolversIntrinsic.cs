using System;

#if HWINTRINSICS
using System.Runtime.Intrinsics;
using System.Runtime.Intrinsics.X86;
using System.Runtime.CompilerServices;
#endif

namespace PhotoSauce.MagicScaler
{
	internal interface IVectorConvolver
	{
		IConvolver IntrinsicImpl { get; }
	}

#if HWINTRINSICS
	internal sealed partial class Convolver4ChanFloat : IVectorConvolver
	{
		IConvolver IVectorConvolver.IntrinsicImpl => HWIntrinsics.IsSupported ? Convolver4ChanIntrinsic.Instance : (IConvolver)this;
	}
	internal sealed partial class Convolver3ChanFloat : IVectorConvolver
	{
		IConvolver IVectorConvolver.IntrinsicImpl => HWIntrinsics.IsSupported ? Convolver3ChanIntrinsic.Instance : (IConvolver)this;
	}
	internal sealed partial class Convolver3XChanFloat : IVectorConvolver
	{
		IConvolver IVectorConvolver.IntrinsicImpl => HWIntrinsics.IsSupported ? Convolver4ChanIntrinsic.Instance : (IConvolver)this;
	}
	internal sealed partial class Convolver1ChanFloat : IVectorConvolver
	{
		IConvolver IVectorConvolver.IntrinsicImpl => HWIntrinsics.IsSupported ? Convolver1ChanIntrinsic.Instance : (IConvolver)this;
	}

	internal sealed class Convolver4ChanIntrinsic : IConvolver
	{
		private const int channels = 4;

		public static readonly Convolver4ChanIntrinsic Instance = new Convolver4ChanIntrinsic();

		private Convolver4ChanIntrinsic() { }

		int IConvolver.Channels => channels;
		int IConvolver.MapChannels => channels;

		[MethodImpl(MethodImplOptions.AggressiveOptimization)]
		unsafe void IConvolver.ConvolveSourceLine(byte* istart, byte* tstart, int cb, byte* mapxstart, int smapx, int smapy)
		{
			float* tp = (float*)tstart, tpe = (float*)(tstart + cb);
			float* pmapx = (float*)mapxstart;
			int kstride = smapx * channels;
			int tstride = smapy * channels;
			int vcnt = smapx / Vector128<float>.Count;

			while (tp < tpe)
			{
				int ix = *(int*)pmapx++;
				int lcnt = vcnt;

				float* ip = (float*)istart + ix * channels;
				float* mp = pmapx;
				pmapx += kstride;

				Vector128<float> av0;

				if (Avx.IsSupported && lcnt >= 2)
				{
					var ax0 = Vector256<float>.Zero;

					for (; lcnt >= 2; lcnt -= 2)
					{
						var iv0 = Avx.LoadVector256(ip);
						var iv1 = Avx.LoadVector256(ip + Vector256<float>.Count);
						var iv2 = Avx.LoadVector256(ip + Vector256<float>.Count * 2);
						var iv3 = Avx.LoadVector256(ip + Vector256<float>.Count * 3);
						ip += Vector256<int>.Count * channels;

						if (Fma.IsSupported)
						{
							ax0 = Fma.MultiplyAdd(Avx.LoadVector256(mp), iv0, ax0);
							ax0 = Fma.MultiplyAdd(Avx.LoadVector256(mp + Vector256<float>.Count), iv1, ax0);
							ax0 = Fma.MultiplyAdd(Avx.LoadVector256(mp + Vector256<float>.Count * 2), iv2, ax0);
							ax0 = Fma.MultiplyAdd(Avx.LoadVector256(mp + Vector256<float>.Count * 3), iv3, ax0);
						}
						else
						{
							ax0 = Avx.Add(ax0, Avx.Multiply(iv0, Avx.LoadVector256(mp)));
							ax0 = Avx.Add(ax0, Avx.Multiply(iv1, Avx.LoadVector256(mp + Vector256<float>.Count)));
							ax0 = Avx.Add(ax0, Avx.Multiply(iv2, Avx.LoadVector256(mp + Vector256<float>.Count * 2)));
							ax0 = Avx.Add(ax0, Avx.Multiply(iv3, Avx.LoadVector256(mp + Vector256<float>.Count * 3)));
						}
						mp += Vector256<float>.Count * channels;
					}

					av0 = Sse.Add(ax0.GetLower(), ax0.GetUpper());
				}
				else
				{
					av0 = Vector128<float>.Zero;
				}

				for (; lcnt != 0; lcnt--)
				{
					var iv0 = Sse.LoadVector128(ip);
					var iv1 = Sse.LoadVector128(ip + Vector128<float>.Count);
					var iv2 = Sse.LoadVector128(ip + Vector128<float>.Count * 2);
					var iv3 = Sse.LoadVector128(ip + Vector128<float>.Count * 3);
					ip += Vector128<float>.Count * channels;

					if (Fma.IsSupported)
					{
						av0 = Fma.MultiplyAdd(Sse.LoadVector128(mp), iv0, av0);
						av0 = Fma.MultiplyAdd(Sse.LoadVector128(mp + Vector128<float>.Count), iv1, av0);
						av0 = Fma.MultiplyAdd(Sse.LoadVector128(mp + Vector128<float>.Count * 2), iv2, av0);
						av0 = Fma.MultiplyAdd(Sse.LoadVector128(mp + Vector128<float>.Count * 3), iv3, av0);
					}
					else
					{
						av0 = Sse.Add(av0, Sse.Multiply(iv0, Sse.LoadVector128(mp)));
						av0 = Sse.Add(av0, Sse.Multiply(iv1, Sse.LoadVector128(mp + Vector128<float>.Count)));
						av0 = Sse.Add(av0, Sse.Multiply(iv2, Sse.LoadVector128(mp + Vector128<float>.Count * 2)));
						av0 = Sse.Add(av0, Sse.Multiply(iv3, Sse.LoadVector128(mp + Vector128<float>.Count * 3)));
					}
					mp += Vector128<float>.Count * channels;
				}

				tp[0] = av0.ToScalar();
				tp[1] = Sse.Shuffle(av0, av0, 0b_11_10_01_01).ToScalar();
				tp[2] = Sse.UnpackHigh(av0, av0).ToScalar();
				tp[3] = Sse.Shuffle(av0, av0, 0b_11_10_01_11).ToScalar();
				tp += tstride;
			}
		}

		[MethodImpl(MethodImplOptions.AggressiveOptimization)]
		unsafe void IConvolver.WriteDestLine(byte* tstart, byte* ostart, int ox, int ow, byte* pmapy, int smapy)
		{
			float* op = (float*)ostart;
			int xc = ox + ow, tstride = smapy * channels;
			int vcnt = smapy / Vector128<float>.Count;

			while (ox < xc)
			{
				int lcnt = vcnt;

				float* tp = (float*)tstart + ox * tstride;
				float* mp = (float*)pmapy;

				Vector128<float> av0;

				if (Avx.IsSupported && lcnt >= 2)
				{
					var ax0 = Vector256<float>.Zero;

					for (; lcnt >= 2; lcnt -= 2)
					{
						var iv0 = Avx.LoadVector256(tp);
						var iv1 = Avx.LoadVector256(tp + Vector256<float>.Count);
						var iv2 = Avx.LoadVector256(tp + Vector256<float>.Count * 2);
						var iv3 = Avx.LoadVector256(tp + Vector256<float>.Count * 3);
						tp += Vector256<int>.Count * channels;

						if (Fma.IsSupported)
						{
							ax0 = Fma.MultiplyAdd(Avx.LoadVector256(mp), iv0, ax0);
							ax0 = Fma.MultiplyAdd(Avx.LoadVector256(mp + Vector256<float>.Count), iv1, ax0);
							ax0 = Fma.MultiplyAdd(Avx.LoadVector256(mp + Vector256<float>.Count * 2), iv2, ax0);
							ax0 = Fma.MultiplyAdd(Avx.LoadVector256(mp + Vector256<float>.Count * 3), iv3, ax0);
						}
						else
						{
							ax0 = Avx.Add(ax0, Avx.Multiply(iv0, Avx.LoadVector256(mp)));
							ax0 = Avx.Add(ax0, Avx.Multiply(iv1, Avx.LoadVector256(mp + Vector256<float>.Count)));
							ax0 = Avx.Add(ax0, Avx.Multiply(iv2, Avx.LoadVector256(mp + Vector256<float>.Count * 2)));
							ax0 = Avx.Add(ax0, Avx.Multiply(iv3, Avx.LoadVector256(mp + Vector256<float>.Count * 3)));
						}
						mp += Vector256<float>.Count * channels;
					}

					av0 = Sse.Add(ax0.GetLower(), ax0.GetUpper());
				}
				else
				{
					av0 = Vector128<float>.Zero;
				}

				for (; lcnt != 0; lcnt--)
				{
					var iv0 = Sse.LoadVector128(tp);
					var iv1 = Sse.LoadVector128(tp + Vector128<float>.Count);
					var iv2 = Sse.LoadVector128(tp + Vector128<float>.Count * 2);
					var iv3 = Sse.LoadVector128(tp + Vector128<float>.Count * 3);
					tp += Vector128<float>.Count * channels;

					if (Fma.IsSupported)
					{
						av0 = Fma.MultiplyAdd(Sse.LoadVector128(mp), iv0, av0);
						av0 = Fma.MultiplyAdd(Sse.LoadVector128(mp + Vector128<float>.Count), iv1, av0);
						av0 = Fma.MultiplyAdd(Sse.LoadVector128(mp + Vector128<float>.Count * 2), iv2, av0);
						av0 = Fma.MultiplyAdd(Sse.LoadVector128(mp + Vector128<float>.Count * 3), iv3, av0);
					}
					else
					{
						av0 = Sse.Add(av0, Sse.Multiply(iv0, Sse.LoadVector128(mp)));
						av0 = Sse.Add(av0, Sse.Multiply(iv1, Sse.LoadVector128(mp + Vector128<float>.Count)));
						av0 = Sse.Add(av0, Sse.Multiply(iv2, Sse.LoadVector128(mp + Vector128<float>.Count * 2)));
						av0 = Sse.Add(av0, Sse.Multiply(iv3, Sse.LoadVector128(mp + Vector128<float>.Count * 3)));
					}
					mp += Vector128<float>.Count * channels;
				}

				op[0] = av0.ToScalar();
				op[1] = Sse.Shuffle(av0, av0, 0b_11_10_01_01).ToScalar();
				op[2] = Sse.UnpackHigh(av0, av0).ToScalar();
				op[3] = Sse.Shuffle(av0, av0, 0b_11_10_01_11).ToScalar();
				op += channels;
				ox++;
			}
		}

		[MethodImpl(MethodImplOptions.AggressiveOptimization)]
		unsafe void IConvolver.SharpenLine(byte* cstart, byte* ystart, byte* bstart, byte* ostart, int ox, int ow, float amt, float thresh, bool gamma)
		{
			float* ip = (float*)cstart + ox * channels, yp = (float*)ystart + ox, bp = (float*)bstart, op = (float*)ostart;
			float* ipe = ip + ow * channels;

			bool threshold = thresh > 0f;

			var vmin = Vector128<float>.Zero;
			var vamt = Vector128.Create(amt, amt, amt, 0f);

			while (ip < ipe)
			{
				float dif = *yp++ - *bp++;
				var v0 = Sse.LoadVector128(ip);
				ip += Vector128<float>.Count;

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
				op += Vector128<float>.Count;
			}
		}
	}

	internal sealed class Convolver3ChanIntrinsic : IConvolver
	{
		private const int channels = 3;

		public static readonly Convolver3ChanIntrinsic Instance = new Convolver3ChanIntrinsic();

		private Convolver3ChanIntrinsic() { }

		int IConvolver.Channels => channels;
		int IConvolver.MapChannels => channels;

		[MethodImpl(MethodImplOptions.AggressiveOptimization)]
		unsafe void IConvolver.ConvolveSourceLine(byte* istart, byte* tstart, int cb, byte* mapxstart, int smapx, int smapy)
		{
			float* tp = (float*)tstart, tpe = (float*)(tstart + cb);
			float* pmapx = (float*)mapxstart;
			int kstride = smapx * channels;
			int tstride = smapy * 4;
			int vcnt = smapx / Vector128<float>.Count;

			while (tp < tpe)
			{
				int ix = *(int*)pmapx++;
				int lcnt = vcnt;

				float* ip = (float*)istart + ix * channels;
				float* mp = pmapx;
				pmapx += kstride;

				Vector128<float> av0, av1, av2;

				if (Avx.IsSupported && lcnt >= 2)
				{
					Vector256<float> ax0 = Vector256<float>.Zero, ax1 = ax0, ax2 = ax0;

					for (; lcnt >= 2; lcnt -= 2)
					{
						var iv0 = Avx.LoadVector256(ip);
						var iv1 = Avx.LoadVector256(ip + Vector256<float>.Count);
						var iv2 = Avx.LoadVector256(ip + Vector256<float>.Count * 2);
						ip += Vector256<int>.Count * channels;

						if (Fma.IsSupported)
						{
							ax0 = Fma.MultiplyAdd(Avx.LoadVector256(mp), iv0, ax0);
							ax1 = Fma.MultiplyAdd(Avx.LoadVector256(mp + Vector256<float>.Count), iv1, ax1);
							ax2 = Fma.MultiplyAdd(Avx.LoadVector256(mp + Vector256<float>.Count * 2), iv2, ax2);
						}
						else
						{
							ax0 = Avx.Add(ax0, Avx.Multiply(iv0, Avx.LoadVector256(mp)));
							ax1 = Avx.Add(ax1, Avx.Multiply(iv1, Avx.LoadVector256(mp + Vector256<float>.Count)));
							ax2 = Avx.Add(ax2, Avx.Multiply(iv2, Avx.LoadVector256(mp + Vector256<float>.Count * 2)));
						}
						mp += Vector256<float>.Count * channels;
					}

					av0 = Sse.Add(ax0.GetLower(), ax1.GetUpper());
					av1 = Sse.Add(ax0.GetUpper(), ax2.GetLower());
					av2 = Sse.Add(ax1.GetLower(), ax2.GetUpper());
				}
				else
				{
					av0 = av1 = av2 = Vector128<float>.Zero;
				}

				for (; lcnt != 0; lcnt--)
				{
					var iv0 = Sse.LoadVector128(ip);
					var iv1 = Sse.LoadVector128(ip + Vector128<float>.Count);
					var iv2 = Sse.LoadVector128(ip + Vector128<float>.Count * 2);
					ip += Vector128<float>.Count * channels;

					if (Fma.IsSupported)
					{
						av0 = Fma.MultiplyAdd(Sse.LoadVector128(mp), iv0, av0);
						av1 = Fma.MultiplyAdd(Sse.LoadVector128(mp + Vector128<float>.Count), iv1, av1);
						av2 = Fma.MultiplyAdd(Sse.LoadVector128(mp + Vector128<float>.Count * 2), iv2, av2);
					}
					else
					{
						av0 = Sse.Add(av0, Sse.Multiply(iv0, Sse.LoadVector128(mp)));
						av1 = Sse.Add(av1, Sse.Multiply(iv1, Sse.LoadVector128(mp + Vector128<float>.Count)));
						av2 = Sse.Add(av2, Sse.Multiply(iv2, Sse.LoadVector128(mp + Vector128<float>.Count * 2)));
					}
					mp += Vector128<float>.Count * channels;
				}

				var avs0 = Sse.Add(Sse.Add(
					Sse.Shuffle(av0, av0, 0b_00_10_01_11),
					Sse.Shuffle(av1, av1, 0b_00_01_11_10)),
					Sse.Shuffle(av2, av2, 0b_00_11_10_01)
				);
				var avs1 = Sse3.IsSupported ?
					Sse3.MoveHighAndDuplicate(avs0) :
					Sse.Shuffle(avs0, avs0, 0b_11_11_01_01);
				var avs2 = Sse.UnpackHigh(avs0, avs0);

				tp[0] = Sse.AddScalar(av0, avs0).ToScalar();
				tp[1] = Sse.AddScalar(av1, avs1).ToScalar();
				tp[2] = Sse.AddScalar(av2, avs2).ToScalar();
				tp += tstride;
			}
		}

		unsafe void IConvolver.WriteDestLine(byte* tstart, byte* ostart, int ox, int ow, byte* pmapy, int smapy) => throw new NotImplementedException();

		unsafe void IConvolver.SharpenLine(byte* cstart, byte* ystart, byte* bstart, byte* ostart, int ox, int ow, float amt, float thresh, bool gamma) => throw new NotImplementedException();
	}

	internal sealed class Convolver1ChanIntrinsic : IConvolver
	{
		public static readonly Convolver1ChanIntrinsic Instance = new Convolver1ChanIntrinsic();

		private Convolver1ChanIntrinsic() { }

		int IConvolver.Channels => 1;
		int IConvolver.MapChannels => 1;

		[MethodImpl(MethodImplOptions.AggressiveOptimization)]
		unsafe void IConvolver.ConvolveSourceLine(byte* istart, byte* tstart, int cb, byte* mapxstart, int smapx, int smapy)
		{
			float* tp = (float*)tstart, tpe = (float*)(tstart + cb);
			float* pmapx = (float*)mapxstart;
			int kstride = smapx;
			int tstride = smapy;
			int vcnt = smapx / Vector128<float>.Count;

			while (tp < tpe)
			{
				int ix = *(int*)pmapx++;
				int lcnt = vcnt;

				float* ip = (float*)istart + ix;
				float* mp = pmapx;
				pmapx += kstride;

				Vector128<float> av0;

				if (Avx.IsSupported && lcnt >= 2)
				{
					var ax0 = Vector256<float>.Zero;

					for (; lcnt >= 8; lcnt -= 8)
					{
						var iv0 = Avx.LoadVector256(ip);
						var iv1 = Avx.LoadVector256(ip + Vector256<float>.Count);
						var iv2 = Avx.LoadVector256(ip + Vector256<float>.Count * 2);
						var iv3 = Avx.LoadVector256(ip + Vector256<float>.Count * 3);
						ip += Vector256<float>.Count * 4;

						if (Fma.IsSupported)
						{
							ax0 = Fma.MultiplyAdd(Avx.LoadVector256(mp), iv0, ax0);
							ax0 = Fma.MultiplyAdd(Avx.LoadVector256(mp + Vector256<float>.Count), iv1, ax0);
							ax0 = Fma.MultiplyAdd(Avx.LoadVector256(mp + Vector256<float>.Count * 2), iv2, ax0);
							ax0 = Fma.MultiplyAdd(Avx.LoadVector256(mp + Vector256<float>.Count * 3), iv3, ax0);
						}
						else
						{
							ax0 = Avx.Add(ax0, Avx.Multiply(iv0, Avx.LoadVector256(mp)));
							ax0 = Avx.Add(ax0, Avx.Multiply(iv1, Avx.LoadVector256(mp + Vector256<float>.Count)));
							ax0 = Avx.Add(ax0, Avx.Multiply(iv2, Avx.LoadVector256(mp + Vector256<float>.Count * 2)));
							ax0 = Avx.Add(ax0, Avx.Multiply(iv3, Avx.LoadVector256(mp + Vector256<float>.Count * 3)));
						}
						mp += Vector256<float>.Count * 4;
					}

					if (lcnt >= 6)
					{
						lcnt -= 6;

						var iv0 = Avx.LoadVector256(ip);
						var iv1 = Avx.LoadVector256(ip + Vector256<float>.Count);
						var iv2 = Avx.LoadVector256(ip + Vector256<float>.Count * 2);
						ip += Vector256<float>.Count * 3;

						if (Fma.IsSupported)
						{
							ax0 = Fma.MultiplyAdd(Avx.LoadVector256(mp), iv0, ax0);
							ax0 = Fma.MultiplyAdd(Avx.LoadVector256(mp + Vector256<float>.Count), iv1, ax0);
							ax0 = Fma.MultiplyAdd(Avx.LoadVector256(mp + Vector256<float>.Count * 2), iv2, ax0);
						}
						else
						{
							ax0 = Avx.Add(ax0, Avx.Multiply(iv0, Avx.LoadVector256(mp)));
							ax0 = Avx.Add(ax0, Avx.Multiply(iv1, Avx.LoadVector256(mp + Vector256<float>.Count)));
							ax0 = Avx.Add(ax0, Avx.Multiply(iv2, Avx.LoadVector256(mp + Vector256<float>.Count * 2)));
						}
						mp += Vector256<float>.Count * 3;
					}
					else if (lcnt >= 4)
					{
						lcnt -= 4;

						var iv0 = Avx.LoadVector256(ip);
						var iv1 = Avx.LoadVector256(ip + Vector256<float>.Count);
						ip += Vector256<float>.Count * 2;

						if (Fma.IsSupported)
						{
							ax0 = Fma.MultiplyAdd(Avx.LoadVector256(mp), iv0, ax0);
							ax0 = Fma.MultiplyAdd(Avx.LoadVector256(mp + Vector256<float>.Count), iv1, ax0);
						}
						else
						{
							ax0 = Avx.Add(ax0, Avx.Multiply(iv0, Avx.LoadVector256(mp)));
							ax0 = Avx.Add(ax0, Avx.Multiply(iv1, Avx.LoadVector256(mp + Vector256<float>.Count)));
						}
						mp += Vector256<float>.Count * 2;
					}
					else if (lcnt >= 2)
					{
						lcnt -= 2;

						var iv0 = Avx.LoadVector256(ip);
						ip += Vector256<float>.Count;

						if (Fma.IsSupported)
							ax0 = Fma.MultiplyAdd(Avx.LoadVector256(mp), iv0, ax0);
						else
							ax0 = Avx.Add(ax0, Avx.Multiply(iv0, Avx.LoadVector256(mp)));

						mp += Vector256<float>.Count;
					}

					av0 = Sse.Add(ax0.GetLower(), ax0.GetUpper());
				}
				else
				{
					av0 = Vector128<float>.Zero;
				}

				for (; lcnt != 0; lcnt--)
				{
					var iv0 = Sse.LoadVector128(ip);
					ip += Vector128<float>.Count;

					if (Fma.IsSupported)
						av0 = Fma.MultiplyAdd(Sse.LoadVector128(mp), iv0, av0);
					else
						av0 = Sse.Add(av0, Sse.Multiply(iv0, Sse.LoadVector128(mp)));

					mp += Vector128<float>.Count;
				}

				tp[0] = av0.HorizontalAdd();
				tp += tstride;
			}
		}

		[MethodImpl(MethodImplOptions.AggressiveOptimization)]
		unsafe void IConvolver.WriteDestLine(byte* tstart, byte* ostart, int ox, int ow, byte* pmapy, int smapy)
		{
			float* op = (float*)ostart;
			int xc = ox + ow, tstride = smapy;
			int vcnt = smapy / Vector128<float>.Count;

			while (ox < xc)
			{
				int lcnt = vcnt;

				float* tp = (float*)tstart + ox * tstride;
				float* mp = (float*)pmapy;

				Vector128<float> av0;

				if (Avx.IsSupported && lcnt >= 2)
				{
					var ax0 = Vector256<float>.Zero;

					for (; lcnt >= 4; lcnt -= 4)
					{
						var iv0 = Avx.LoadVector256(tp);
						var iv1 = Avx.LoadVector256(tp + Vector256<float>.Count);
						tp += Vector256<float>.Count * 2;

						if (Fma.IsSupported)
						{
							ax0 = Fma.MultiplyAdd(Avx.LoadVector256(mp), iv0, ax0);
							ax0 = Fma.MultiplyAdd(Avx.LoadVector256(mp + Vector256<float>.Count), iv1, ax0);
						}
						else
						{
							ax0 = Avx.Add(ax0, Avx.Multiply(iv0, Avx.LoadVector256(mp)));
							ax0 = Avx.Add(ax0, Avx.Multiply(iv1, Avx.LoadVector256(mp + Vector256<float>.Count)));
						}
						mp += Vector256<float>.Count * 2;
					}

					if (lcnt >= 2)
					{
						lcnt -= 2;

						var iv0 = Avx.LoadVector256(tp);
						tp += Vector256<float>.Count;

						if (Fma.IsSupported)
							ax0 = Fma.MultiplyAdd(Avx.LoadVector256(mp), iv0, ax0);
						else
							ax0 = Avx.Add(ax0, Avx.Multiply(iv0, Avx.LoadVector256(mp)));

						mp += Vector256<float>.Count;
					}

					av0 = Sse.Add(ax0.GetLower(), ax0.GetUpper());
				}
				else
				{
					av0 = Vector128<float>.Zero;
				}

				for (; lcnt != 0; lcnt--)
				{
					var iv0 = Sse.LoadVector128(tp);
					tp += Vector128<float>.Count;

					if (Fma.IsSupported)
						av0 = Fma.MultiplyAdd(Sse.LoadVector128(mp), iv0, av0);
					else
						av0 = Sse.Add(av0, Sse.Multiply(iv0, Sse.LoadVector128(mp)));

					mp += Vector128<float>.Count;
				}

				*op++ = av0.HorizontalAdd();
				ox++;
			}
		}

		[MethodImpl(MethodImplOptions.AggressiveOptimization)]
		unsafe void IConvolver.SharpenLine(byte* cstart, byte* ystart, byte* bstart, byte* ostart, int ox, int ow, float amt, float thresh, bool gamma)
		{
			float* ip = (float*)cstart + ox, yp = (float*)ystart + ox, bp = (float*)bstart, op = (float*)ostart;
			float* ipe = ip + ow;

			bool threshold = thresh > 0f;

			if (Avx.IsSupported && ip <= ipe - Vector256<float>.Count)
			{
				var vthresh = Vector256.Create(threshold ? thresh : -1f);
				var vmsk = Vector256.Create(0x7fffffff).AsSingle();
				var vamt = Vector256.Create(amt);
				var vmin = Vector256<float>.Zero;

				ipe -= Vector256<float>.Count;
				do
				{
					var vd = Avx.Subtract(Avx.LoadVector256(yp), Avx.LoadVector256(bp));
					yp += Vector256<float>.Count;
					bp += Vector256<float>.Count;

					if (threshold)
					{
						var sm = HWIntrinsics.AvxCompareGreaterThan(Avx.And(vd, vmsk), vthresh);
						vd = Avx.And(vd, sm);
					}
					vd = Avx.Multiply(vd, vamt);

					var v0 = Avx.LoadVector256(ip);
					ip += Vector256<float>.Count;

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
					op += Vector256<float>.Count;
				} while (ip <= ipe);
				ipe += Vector256<float>.Count;
			}
			else if (ip <= ipe - Vector128<float>.Count)
			{
				var vthresh = Vector128.Create(threshold ? thresh : -1f);
				var vmsk = Vector128.Create(0x7fffffff).AsSingle();
				var vamt = Vector128.Create(amt);
				var vmin = Vector128<float>.Zero;

				ipe -= Vector128<float>.Count;
				do
				{
					var vd = Sse.Subtract(Sse.LoadVector128(yp), Sse.LoadVector128(bp));
					yp += Vector128<float>.Count;
					bp += Vector128<float>.Count;

					if (threshold)
					{
						var sm = Sse.CompareGreaterThan(Sse.And(vd, vmsk), vthresh);
						vd = Sse.And(vd, sm);
					}
					vd = Sse.Multiply(vd, vamt);

					var v0 = Sse.LoadVector128(ip);
					ip += Vector128<float>.Count;

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
					op += Vector128<float>.Count;
				} while (ip <= ipe);
				ipe += Vector128<float>.Count;
			}

			float fmin = Vector128<float>.Zero.ToScalar();

			while (ip < ipe)
			{
				float dif = *yp++ - *bp++;
				float c0 = *ip++;

				if (!threshold || Math.Abs(dif) > thresh)
				{
					dif *= amt;

					if (gamma)
					{
						c0 = MathUtil.MaxF(c0, fmin).Sqrt();
						c0 = MathUtil.MaxF(c0 + dif, fmin);
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
}
