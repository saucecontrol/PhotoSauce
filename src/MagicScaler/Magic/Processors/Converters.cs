using System.Numerics;
using System.Runtime.CompilerServices;

using static PhotoSauce.MagicScaler.MathUtil;

using VectorF = System.Numerics.Vector<float>;

namespace PhotoSauce.MagicScaler
{
	internal sealed class NarrowingConverter : IConverter<ushort, byte>
	{
		public static NarrowingConverter Instance = new NarrowingConverter();

		private NarrowingConverter() { }

		unsafe void IConverter.ConvertLine(byte* istart, byte* ostart, int cb)
		{
			ushort* ip = (ushort*)istart, ipe = (ushort*)(istart + cb) - 4;
			byte* op = ostart;

			while (ip <= ipe)
			{
				byte o0 = (byte)(ip[0] >> 8);
				byte o1 = (byte)(ip[1] >> 8);
				byte o2 = (byte)(ip[2] >> 8);
				byte o3 = (byte)(ip[3] >> 8);
				op[0] = o0;
				op[1] = o1;
				op[2] = o2;
				op[3] = o3;

				ip += 4;
				op += 4;
			}
		}
	}

	internal class FloatConverter
	{
		public sealed class Widening : IConverter<byte, float>
		{
			public static Widening Instance = new Widening();

			private Widening() { }

			unsafe void IConverter.ConvertLine(byte* ipstart, byte* opstart, int cb)
			{
				fixed (float* atstart = &LookupTables.AlphaFloat[0])
				{
					byte* ip = ipstart, ipe = ipstart + cb;
					float* op = (float*)opstart, at = atstart;

#if VECTOR_CONVERT
					var vscale = new VectorF(1f / byte.MaxValue);

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

						vf0 *= vscale;
						vf1 *= vscale;
						vf2 *= vscale;
						vf3 *= vscale;

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

		public sealed class Widening3A : IConverter<byte, float>
		{
			public static Widening3A Instance = new Widening3A();

			private Widening3A() { }

			unsafe void IConverter.ConvertLine(byte* ipstart, byte* opstart, int cb)
			{
				fixed (float* atstart = &LookupTables.AlphaFloat[0])
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

		public sealed class Widening3X : IConverter<byte, float>
		{
			public static Widening3X Instance = new Widening3X();

			private Widening3X() { }

			unsafe void IConverter.ConvertLine(byte* ipstart, byte* opstart, int cb)
			{
				fixed (float* atstart = &LookupTables.AlphaFloat[0])
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

		public sealed class Narrowing : IConverter<float, byte>
		{
			public static Narrowing Instance = new Narrowing();

			private Narrowing() { }

			unsafe void IConverter.ConvertLine(byte* ipstart, byte* opstart, int cb)
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
				var vround = new VectorF(FloatRound);

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

		public sealed class Narrowing3A : IConverter<float, byte>
		{
			public static Narrowing3A Instance = new Narrowing3A();

			private Narrowing3A() { }

			unsafe void IConverter.ConvertLine(byte* ipstart, byte* opstart, int cb)
			{
				float* ip = (float*)ipstart, ipe = (float*)(ipstart + cb) - 4;
				byte* op = opstart;
				float fmax = new Vector4(byte.MaxValue).X, fround = new Vector4(FloatRound).X, fmin = fround / fmax;

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

		public sealed class Narrowing3X : IConverter<float, byte>
		{
			public static Narrowing3X Instance = new Narrowing3X();

			private Narrowing3X() { }

			unsafe void IConverter.ConvertLine(byte* ipstart, byte* opstart, int cb)
			{
				float* ip = (float*)ipstart, ipe = (float*)(ipstart + cb) - VectorF.Count;
				byte* op = opstart;

				var vmin = new VectorF(byte.MinValue);
				var vmax = new VectorF(byte.MaxValue);
				var vround = new VectorF(FloatRound);

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
	}
}
