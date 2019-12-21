using System.Numerics;
using System.Diagnostics;
using System.Runtime.CompilerServices;

using static PhotoSauce.MagicScaler.MathUtil;

using VectorF = System.Numerics.Vector<float>;

namespace PhotoSauce.MagicScaler
{
	internal class FloatConverter
	{
		public sealed class Widening : IConverter<byte, float>
		{
			public static readonly Widening InstanceFullRange = new Widening();
			public static readonly Widening InstanceVideoRange = new Widening(true);

			private static readonly WideningImpl3A processor3A = new WideningImpl3A();
			private static readonly WideningImpl3X processor3X = new WideningImpl3X();

			private readonly WideningImpl processor;

			private Widening(bool videoRange = false) => processor = new WideningImpl(videoRange);

			public IConversionProcessor<byte, float> Processor => processor;
			public IConversionProcessor<byte, float> Processor3A => processor3A;
			public IConversionProcessor<byte, float> Processor3X => processor3X;
		}

		public sealed class Narrowing : IConverter<float, byte>
		{
			public static readonly Narrowing Instance = new Narrowing();

			private static readonly NarrowingImpl processor = new NarrowingImpl();
			private static readonly NarrowingImpl3A processor3A = new NarrowingImpl3A();
			private static readonly NarrowingImpl3X processor3X = new NarrowingImpl3X();

			private Narrowing() { }

			public IConversionProcessor<float, byte> Processor => processor;
			public IConversionProcessor<float, byte> Processor3A => processor3A;
			public IConversionProcessor<float, byte> Processor3X => processor3X;
		}

		private sealed class WideningImpl : IConversionProcessor<byte, float>
		{
			private readonly float scale;
			private readonly float offset;
			private readonly float[] valueTable;

			public WideningImpl(bool videoRange = false)
			{
				scale = 1f / (videoRange ? VideoLumaScale : byte.MaxValue);
				offset = videoRange ? VideoLumaMin : 0f;
				valueTable = videoRange ? LookupTables.MakeVideoInverseGamma(LookupTables.Alpha) : LookupTables.Alpha;
			}

			unsafe void IConversionProcessor.ConvertLine(byte* ipstart, byte* opstart, int cb)
			{
				fixed (float* atstart = &valueTable[0])
				{
					byte* ip = ipstart, ipe = ipstart + cb;
					float* op = (float*)opstart, at = atstart;

#if VECTOR_CONVERT
					var vscal = new VectorF(scale);
					var voffs = new VectorF(offset);

					ipe -= Vector<byte>.Count;
					while (ip <= ipe)
					{
						var vb = Unsafe.ReadUnaligned<Vector<byte>>(ip);
						Vector.Widen(vb, out var vs0, out var vs1);
						Vector.Widen(vs0, out var vi0, out var vi1);
						Vector.Widen(vs1, out var vi2, out var vi3);

						var vf0 = Vector.ConvertToSingle(Vector.AsVectorInt32(vi0));
						var vf1 = Vector.ConvertToSingle(Vector.AsVectorInt32(vi1));
						var vf2 = Vector.ConvertToSingle(Vector.AsVectorInt32(vi2));
						var vf3 = Vector.ConvertToSingle(Vector.AsVectorInt32(vi3));

						vf0 = (vf0 - voffs) * vscal;
						vf1 = (vf1 - voffs) * vscal;
						vf2 = (vf2 - voffs) * vscal;
						vf3 = (vf3 - voffs) * vscal;

						Unsafe.WriteUnaligned(op, vf0);
						Unsafe.WriteUnaligned(op + VectorF.Count, vf1);
						Unsafe.WriteUnaligned(op + VectorF.Count * 2, vf2);
						Unsafe.WriteUnaligned(op + VectorF.Count * 3, vf3);

						ip += Vector<byte>.Count;
						op += Vector<byte>.Count;
					}
					ipe += Vector<byte>.Count;
#endif

					ipe -= 8;
					while (ip <= ipe)
					{
						float o0 = at[(uint)ip[0]];
						float o1 = at[(uint)ip[1]];
						float o2 = at[(uint)ip[2]];
						float o3 = at[(uint)ip[3]];
						float o4 = at[(uint)ip[4]];
						float o5 = at[(uint)ip[5]];
						float o6 = at[(uint)ip[6]];
						float o7 = at[(uint)ip[7]];

						op[0] = o0;
						op[1] = o1;
						op[2] = o2;
						op[3] = o3;
						op[4] = o4;
						op[5] = o5;
						op[6] = o6;
						op[7] = o7;

						ip += 8;
						op += 8;
					}
					ipe += 8;

					while (ip < ipe)
					{
						op[0] = at[(uint)ip[0]];
						ip++;
						op++;
					}
				}
			}
		}

		private sealed class WideningImpl3A : IConversionProcessor<byte, float>
		{
			unsafe void IConversionProcessor.ConvertLine(byte* ipstart, byte* opstart, int cb)
			{
				fixed (float* atstart = &LookupTables.Alpha[0])
				{
					byte* ip = ipstart, ipe = ipstart + cb - 4;
					float* op = (float*)opstart, at = atstart;

					while (ip <= ipe)
					{
						float o0 = at[(uint)ip[0]];
						float o1 = at[(uint)ip[1]];
						float o2 = at[(uint)ip[2]];
						float o3 = at[(uint)ip[3]];
						op[0] = o0 * o3;
						op[1] = o1 * o3;
						op[2] = o2 * o3;
						op[3] = o3;

						ip += 4;
						op += 4;
					}
				}
			}
		}

		private sealed class WideningImpl3X : IConversionProcessor<byte, float>
		{
			unsafe void IConversionProcessor.ConvertLine(byte* ipstart, byte* opstart, int cb)
			{
				fixed (float* atstart = &LookupTables.Alpha[0])
				{
					byte* ip = ipstart, ipe = ipstart + cb - 3;
					float* op = (float*)opstart, at = atstart;

					while (ip <= ipe)
					{
						float o0 = at[(uint)ip[0]];
						float o1 = at[(uint)ip[1]];
						float o2 = at[(uint)ip[2]];
						op[0] = o0;
						op[1] = o1;
						op[2] = o2;

						ip += 3;
						op += 4;
					}
				}
			}
		}

		private sealed class NarrowingImpl : IConversionProcessor<float, byte>
		{
			unsafe void IConversionProcessor.ConvertLine(byte* ipstart, byte* opstart, int cb)
			{
				float* ip = (float*)ipstart, ipe = (float*)(ipstart + cb);
				byte* op = opstart;

#if VECTOR_CONVERT
				int unrollCount = Vector<byte>.Count;
#else
				int unrollCount = VectorF.Count;
#endif

				var vmin = new VectorF(byte.MinValue);
				var vmax = new VectorF(byte.MaxValue);
				var vround = new VectorF(0.5f);

				ipe -= unrollCount;
				while (ip <= ipe)
				{
#if VECTOR_CONVERT
					var vf0 = Unsafe.ReadUnaligned<VectorF>(ip);
					var vf1 = Unsafe.ReadUnaligned<VectorF>(ip + VectorF.Count);
					var vf2 = Unsafe.ReadUnaligned<VectorF>(ip + VectorF.Count * 2);
					var vf3 = Unsafe.ReadUnaligned<VectorF>(ip + VectorF.Count * 3);

					vf0 = vf0 * vmax + vround;
					vf1 = vf1 * vmax + vround;
					vf2 = vf2 * vmax + vround;
					vf3 = vf3 * vmax + vround;

					vf0 = vf0.Clamp(vmin, vmax);
					vf1 = vf1.Clamp(vmin, vmax);
					vf2 = vf2.Clamp(vmin, vmax);
					vf3 = vf3.Clamp(vmin, vmax);

					var vi0 = Vector.AsVectorUInt32(Vector.ConvertToInt32(vf0));
					var vi1 = Vector.AsVectorUInt32(Vector.ConvertToInt32(vf1));
					var vi2 = Vector.AsVectorUInt32(Vector.ConvertToInt32(vf2));
					var vi3 = Vector.AsVectorUInt32(Vector.ConvertToInt32(vf3));

					var vs0 = Vector.Narrow(vi0, vi1);
					var vs1 = Vector.Narrow(vi2, vi3);
					var vb = Vector.Narrow(vs0, vs1);
					Unsafe.WriteUnaligned(op, vb);
#else
					var v = Unsafe.ReadUnaligned<VectorF>(ip) * vmax + vround;
					v = v.Clamp(vmin, vmax);

					op[0] = (byte)v[0];
					op[1] = (byte)v[1];
					op[2] = (byte)v[2];
					op[3] = (byte)v[3];

					if (VectorF.Count == 8)
					{
						op[4] = (byte)v[4];
						op[5] = (byte)v[5];
						op[6] = (byte)v[6];
						op[7] = (byte)v[7];
					}
#endif

					ip += unrollCount;
					op += unrollCount;
				}
				ipe += unrollCount;

				while (ip < ipe)
				{
					op[0] = FixToByte(ip[0]);
					ip++;
					op++;
				}
			}
		}

		private sealed class NarrowingImpl3A : IConversionProcessor<float, byte>
		{
			unsafe void IConversionProcessor.ConvertLine(byte* ipstart, byte* opstart, int cb)
			{
				float* ip = (float*)ipstart, ipe = (float*)(ipstart + cb) - 4;
				byte* op = opstart;
				float fmax = new Vector4(byte.MaxValue).X, fround = new Vector4(0.5f).X, fmin = fround / fmax;

				while (ip <= ipe)
				{
					float f3 = ip[3];
					if (f3 < fmin)
					{
						*(uint*)op = 0;
					}
					else
					{
						float f3i = fmax / f3;
						byte o0 = ClampToByte((int)(ip[0] * f3i + fround));
						byte o1 = ClampToByte((int)(ip[1] * f3i + fround));
						byte o2 = ClampToByte((int)(ip[2] * f3i + fround));
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

		private sealed class NarrowingImpl3X : IConversionProcessor<float, byte>
		{
			unsafe void IConversionProcessor.ConvertLine(byte* ipstart, byte* opstart, int cb)
			{
				float* ip = (float*)ipstart, ipe = (float*)(ipstart + cb) - VectorF.Count;
				byte* op = opstart;

				var vmin = new VectorF(byte.MinValue);
				var vmax = new VectorF(byte.MaxValue);
				var vround = new VectorF(0.5f);

				while (ip <= ipe)
				{
					var v = Unsafe.ReadUnaligned<VectorF>(ip) * vmax + vround;
					v = v.Clamp(vmin, vmax);

#if VECTOR_CONVERT
					var vi = Vector.ConvertToInt32(v);
#else
					var vi = v;
#endif

					op[0] = (byte)vi[0];
					op[1] = (byte)vi[1];
					op[2] = (byte)vi[2];

					if (VectorF.Count == 8)
					{
						op[3] = (byte)vi[4];
						op[4] = (byte)vi[5];
						op[5] = (byte)vi[6];
					}

					ip += VectorF.Count;
					op += VectorF.Count - VectorF.Count / 4;
				}

				ipe += VectorF.Count;
				while (ip < ipe)
				{
					op[0] = FixToByte(ip[0]);
					op[1] = FixToByte(ip[1]);
					op[2] = FixToByte(ip[2]);

					ip += 4;
					op += 3;
				}
			}
		}

		public static class Interpolating
		{
			unsafe public static void ConvertFloat(byte* ipstart, byte* opstart, float* lutstart, int lutmax, int cb)
			{
				Debug.Assert(ipstart == opstart);

				float* ip = (float*)ipstart, ipe = (float*)(ipstart + cb);
				float* lp = lutstart;

				var vlmax = new Vector4(lutmax);
				var vzero = Vector4.Zero;
				float fmin = vzero.X, fgmax = vlmax.X;

				ipe -= 4;
				while (ip <= ipe)
				{
					var vf = (Unsafe.ReadUnaligned<Vector4>(ip) * vlmax).Clamp(vzero, vlmax);

					float f0 = vf.X;
					float f1 = vf.Y;
					float f2 = vf.Z;
					float f3 = vf.W;

					uint i0 = (uint)f0;
					uint i1 = (uint)f1;
					uint i2 = (uint)f2;
					uint i3 = (uint)f3;

					ip[0] = Lerp(lp[i0], lp[i0 + 1], f0 - (int)i0);
					ip[1] = Lerp(lp[i1], lp[i1 + 1], f1 - (int)i1);
					ip[2] = Lerp(lp[i2], lp[i2 + 1], f2 - (int)i2);
					ip[3] = Lerp(lp[i3], lp[i3 + 1], f3 - (int)i3);

					ip += 4;
				}
				ipe += 4;

				while (ip < ipe)
				{
					float f = (*ip * fgmax).Clamp(fmin, fgmax);
					uint i = (uint)f;

					*ip++ = Lerp(lp[i], lp[i + 1], f - i);
				}
			}

			unsafe public static void ConvertFloat3A(byte* ipstart, byte* opstart, float* lutstart, int lutmax, int cb)
			{
				Debug.Assert(ipstart == opstart);

				float* ip = (float*)ipstart, ipe = (float*)(ipstart + cb);
				float* lp = lutstart;

				var vlmax = new Vector4(lutmax);
				var vzero = Vector4.Zero;
				float famin = new Vector4(1 / 1024f).X;

				while (ip < ipe)
				{
					var vf = Unsafe.ReadUnaligned<Vector4>(ip);

					float f3 = vf.W;
					if (f3 < famin)
					{
						Unsafe.WriteUnaligned(ip, vzero);
					}
					else
					{
						vf = (vf * vlmax / f3).Clamp(vzero, vlmax);

						float f0 = vf.X;
						float f1 = vf.Y;
						float f2 = vf.Z;

						uint i0 = (uint)f0;
						uint i1 = (uint)f1;
						uint i2 = (uint)f2;

						ip[0] = Lerp(lp[i0], lp[i0 + 1], f0 - (int)i0) * f3;
						ip[1] = Lerp(lp[i1], lp[i1 + 1], f1 - (int)i1) * f3;
						ip[2] = Lerp(lp[i2], lp[i2 + 1], f2 - (int)i2) * f3;
					}

					ip += 4;
				}
			}
		}
	}
}
