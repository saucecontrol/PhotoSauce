using System;
using System.Buffers;
using System.Numerics;
using System.Runtime.CompilerServices;

using PhotoSauce.MagicScaler.Interop;
using static PhotoSauce.MagicScaler.MathUtil;

using VectorF = System.Numerics.Vector<float>;

namespace PhotoSauce.MagicScaler
{
	internal static class Rec601
	{
		public const float R = 0.299f;
		public const float G = 0.587f;
		public const float B = 0.114f;
		public static Vector3 Coefficients = new Vector3(B, G, R);

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static byte LumaFromBgr(byte b, byte g, byte r)
		{
			const int rY = (ushort)(R * DoubleScale + DoubleRound);
			const int gY = (ushort)(G * DoubleScale + DoubleRound);
			const int bY = (ushort)(B * DoubleScale + DoubleRound);

			return UnFix15ToByte(r * rY + g * gY + b * bY);
		}
	}

	internal static class Rec709
	{
		public const float R = 0.2126f;
		public const float G = 0.7152f;
		public const float B = 0.0722f;
		public static Vector3 Coefficients = new Vector3(B, G, R);

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static ushort LumaFromBgr(ushort b, ushort g, ushort r)
		{
			const int rY = (ushort)(R * DoubleScale + DoubleRound);
			const int gY = (ushort)(G * DoubleScale + DoubleRound);
			const int bY = (ushort)(B * DoubleScale + DoubleRound);

			return UnFixToUQ15One(r * rY + g * gY + b * bY);
		}
	}

	internal class FormatConversionTransform : PixelSource, IDisposable
	{
		protected readonly PixelFormat InFormat;
		protected readonly ColorProfile SourceProfile;
		protected readonly ColorProfile DestProfile;

		protected byte[] LineBuff;

		public FormatConversionTransform(PixelSource source, ColorProfile sourceProfile, ColorProfile destProfile, Guid destFormat) : base(source)
		{
			InFormat = source.Format;
			Format = PixelFormat.Cache[destFormat];
			SourceProfile = sourceProfile ?? ColorProfile.sRGB;
			DestProfile = destProfile ?? ColorProfile.sRGB;
			LineBuff = ArrayPool<byte>.Shared.Rent((int)BufferStride);
		}

		unsafe protected override void CopyPixelsInternal(WICRect prc, uint cbStride, uint cbBufferSize, IntPtr pbBuffer)
		{
			fixed (byte* bstart = &LineBuff[0])
			{
				int oh = prc.Height, oy = prc.Y;

				var irc = new WICRect { X = prc.X, Width = prc.Width, Height = 1 };
				int cb = (irc.Width * InFormat.BitsPerPixel + 7) / 8;
				for (int y = 0; y < oh; y++)
				{
					irc.Y = oy + y;
					Timer.Stop();
					Source.CopyPixels(irc, BufferStride, BufferStride, (IntPtr)bstart);
					Timer.Start();

					byte* op = (byte*)pbBuffer + y * cbStride;

					if (InFormat.ColorRepresentation == PixelColorRepresentation.Cmyk && InFormat.BitsPerPixel == 64 && Format.BitsPerPixel == 32)
					{
						mapValuesShortToByte(bstart, op, cb);
					}
					else if (InFormat.Colorspace == PixelColorspace.sRgb && Format.Colorspace == PixelColorspace.LinearRgb && Format.NumericRepresentation == PixelNumericRepresentation.Fixed)
					{
						var lut = SourceProfile.IsLinear ? LookupTables.AlphaUQ15 : SourceProfile.InverseGammaUQ15;
						fixed (ushort* igtstart = &lut[0])
						{
							if (InFormat.AlphaRepresentation != PixelAlphaRepresentation.None)
								mapValuesByteToUQ15LinearWithAlpha(bstart, op, igtstart, cb);
							else
								mapValuesByteToUQ15Linear(bstart, op, igtstart, cb);
						}
					}
					else if (InFormat.Colorspace == PixelColorspace.LinearRgb && Format.Colorspace == PixelColorspace.sRgb && InFormat.NumericRepresentation == PixelNumericRepresentation.Fixed && !DestProfile.IsLinear)
					{
						fixed (byte* gtstart = &DestProfile.Gamma[0])
						{
							if (InFormat.AlphaRepresentation != PixelAlphaRepresentation.None)
								mapValuesUQ15LinearToByteWithAlpha(bstart, op, gtstart, cb);
							else
								mapValuesUQ15LinearToByte(bstart, op, gtstart, cb);
						}
					}
					else if (InFormat.Colorspace == PixelColorspace.sRgb && Format.Colorspace == PixelColorspace.LinearRgb && Format.NumericRepresentation == PixelNumericRepresentation.Float)
					{
						var lut = SourceProfile.IsLinear ? LookupTables.AlphaFloat : SourceProfile.InverseGammaFloat;
						fixed (float* igtstart = &lut[0])
						{
							if (InFormat.AlphaRepresentation != PixelAlphaRepresentation.None)
								mapValuesByteToFloatLinearWithAlpha(bstart, op, igtstart, cb);
							else if (InFormat.ChannelCount == 3 && Format.ChannelCount == 4)
								mapValuesByteToFloatLinearWithNullAlpha(bstart, op, igtstart, cb);
							else
								mapValuesByteToFloatLinear(bstart, op, igtstart, cb);
						}
					}
					else if (InFormat.Colorspace == PixelColorspace.LinearRgb && Format.Colorspace == PixelColorspace.sRgb && InFormat.NumericRepresentation == PixelNumericRepresentation.Float && !DestProfile.IsLinear)
					{
						fixed (byte* gtstart = &DestProfile.Gamma[0])
						{
							if (InFormat.AlphaRepresentation != PixelAlphaRepresentation.None)
								mapValuesFloatLinearToByteWithAlpha(bstart, op, gtstart, cb);
							else if (InFormat.ChannelCount == 4 && Format.ChannelCount == 3)
								mapValuesFloatLinearWithNullAlphaToByte(bstart, op, gtstart, cb);
							else
								mapValuesFloatLinearToByte(bstart, op, gtstart, cb);
						}
					}
					else if (InFormat.NumericRepresentation == PixelNumericRepresentation.UnsignedInteger && Format.NumericRepresentation == PixelNumericRepresentation.Float)
					{
						if (InFormat.AlphaRepresentation == PixelAlphaRepresentation.Unassociated)
							mapValuesByteToFloatWithAlpha(bstart, op, cb);
						else if (InFormat.AlphaRepresentation == PixelAlphaRepresentation.None && InFormat.ChannelCount == 3 && Format.ChannelCount == 4)
							mapValuesByteToFloatWithNullAlpha(bstart, op, cb);
						else
							mapValuesByteToFloat(bstart, op, cb);
					}
					else if (InFormat.NumericRepresentation == PixelNumericRepresentation.Float && Format.NumericRepresentation == PixelNumericRepresentation.UnsignedInteger)
					{
						if (Format.AlphaRepresentation != PixelAlphaRepresentation.None)
							mapValuesFloatToByteWithAlpha(bstart, op, cb);
						else if (InFormat.AlphaRepresentation == PixelAlphaRepresentation.None && InFormat.ChannelCount == 4 && Format.ChannelCount == 3)
							mapValuesFloatWithNullAlphaToByte(bstart, op, cb);
						else
							mapValuesFloatToByte(bstart, op, cb);
					}
					else if (InFormat.NumericRepresentation == Format.NumericRepresentation && InFormat.ChannelCount == 4 && Format.ChannelCount == 3)
					{
						if (InFormat.NumericRepresentation == PixelNumericRepresentation.Float)
							mapValues4to3Chan<float>(bstart, op, cb);
						else if (InFormat.NumericRepresentation == PixelNumericRepresentation.Fixed)
							mapValues4to3Chan<ushort>(bstart, op, cb);
						else
							mapValues4to3Chan<byte>(bstart, op, cb);
					}
					else
						throw new NotSupportedException("Unsupported pixel format");
				}
			}
		}

		unsafe private static void mapValuesShortToByte(byte* ipstart, byte* opstart, int cb)
		{
			ushort* ip = (ushort*)ipstart, ipe = (ushort*)(ipstart + cb) - 4;
			byte* op = opstart;

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

		unsafe private static void mapValuesByteToUQ15Linear(byte* ipstart, byte* opstart, ushort* igtstart, int cb)
		{
			byte* ip = ipstart, ipe = ipstart + cb - 8;
			ushort* op = (ushort*)opstart, igt = igtstart;

			while (ip <= ipe)
			{
				ushort o0 = igt[(uint)ip[0]];
				ushort o1 = igt[(uint)ip[1]];
				ushort o2 = igt[(uint)ip[2]];
				ushort o3 = igt[(uint)ip[3]];
				op[0] = o0;
				op[1] = o1;
				op[2] = o2;
				op[3] = o3;

				o0 = igt[(uint)ip[4]];
				o1 = igt[(uint)ip[5]];
				o2 = igt[(uint)ip[6]];
				o3 = igt[(uint)ip[7]];
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
				op[0] = igt[(uint)ip[0]];
				ip++;
				op++;
			}
		}

		unsafe private static void mapValuesByteToUQ15LinearWithAlpha(byte* ipstart, byte* opstart, ushort* igtstart, int cb)
		{
			fixed (ushort* atstart = &LookupTables.AlphaUQ15[0])
			{
				byte* ip = ipstart, ipe = ipstart + cb - 4;
				ushort* op = (ushort*)opstart, igt = igtstart, at = atstart;

				while (ip <= ipe)
				{
					int i0 = igt[(uint)ip[0]];
					int i1 = igt[(uint)ip[1]];
					int i2 = igt[(uint)ip[2]];
					int i3 =  at[(uint)ip[3]];

					i0 = UnFix15(i0 * i3);
					i1 = UnFix15(i1 * i3);
					i2 = UnFix15(i2 * i3);

					op[0] = (ushort)i0;
					op[1] = (ushort)i1;
					op[2] = (ushort)i2;
					op[3] = (ushort)i3;

					ip += 4;
					op += 4;
				}
			}
		}

		unsafe private static void mapValuesUQ15LinearToByte(byte* ipstart, byte* opstart, byte* gtstart, int cb)
		{
			ushort* ip = (ushort*)ipstart, ipe = (ushort*)(ipstart + cb) - 4;
			byte* op = opstart, gt = gtstart;

			while (ip <= ipe)
			{
				uint i0 = ClampToUQ15One(ip[0]);
				uint i1 = ClampToUQ15One(ip[1]);
				uint i2 = ClampToUQ15One(ip[2]);
				uint i3 = ClampToUQ15One(ip[3]);
				op[0] = gt[i0];
				op[1] = gt[i1];
				op[2] = gt[i2];
				op[3] = gt[i3];

				ip += 4;
				op += 4;
			}

			ipe += 4;
			while (ip < ipe)
			{
				op[0] = gt[ClampToUQ15One(ip[0])];
				ip++;
				op++;
			}
		}

		unsafe private static void mapValuesUQ15LinearToByteWithAlpha(byte* ipstart, byte* opstart, byte* gtstart, int cb)
		{
			ushort* ip = (ushort*)ipstart, ipe = (ushort*)(ipstart + cb) - 4;
			byte* op = opstart, gt = gtstart;

			while (ip <= ipe)
			{
				ushort i3 = ip[3];
				byte o3 = UnFix15ToByte(i3 * byte.MaxValue);
				if (o3 == 0)
				{
					*(uint*)op = 0;
				}
				else
				{
					int o3i = (UQ15One << 15) / i3;
					int i0 = ip[0];
					int i1 = ip[1];
					int i2 = ip[2];

					byte o0 = gt[UnFixToUQ15One(i0 * o3i)];
					byte o1 = gt[UnFixToUQ15One(i1 * o3i)];
					byte o2 = gt[UnFixToUQ15One(i2 * o3i)];

					op[0] = o0;
					op[1] = o1;
					op[2] = o2;
					op[3] = o3;
				}

				ip += 4;
				op += 4;
			}
		}

		unsafe private static void mapValuesByteToFloatLinear(byte* ipstart, byte* opstart, float* igtstart, int cb)
		{
			byte* ip = ipstart, ipe = ipstart + cb - 8;
			float* op = (float*)opstart, igt = igtstart;

			while (ip <= ipe)
			{
				float o0 = igt[(uint)ip[0]];
				float o1 = igt[(uint)ip[1]];
				float o2 = igt[(uint)ip[2]];
				float o3 = igt[(uint)ip[3]];
				float o4 = igt[(uint)ip[4]];
				float o5 = igt[(uint)ip[5]];
				float o6 = igt[(uint)ip[6]];
				float o7 = igt[(uint)ip[7]];

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
				op[0] = igt[(uint)ip[0]];
				ip++;
				op++;
			}
		}

		unsafe private static void mapValuesByteToFloatLinearWithAlpha(byte* ipstart, byte* opstart, float* igtstart, int cb)
		{
			fixed (float* atstart = &LookupTables.AlphaFloat[0])
			{
				byte* ip = ipstart, ipe = ipstart + cb - 4;
				float* op = (float*)opstart, igt = igtstart, at = atstart;

				while (ip <= ipe)
				{
					float o0 = igt[(uint)ip[0]];
					float o1 = igt[(uint)ip[1]];
					float o2 = igt[(uint)ip[2]];
					float o3 =  at[(uint)ip[3]];
					op[0] = o0 * o3;
					op[1] = o1 * o3;
					op[2] = o2 * o3;
					op[3] = o3;

					ip += 4;
					op += 4;
				}
			}
		}

		unsafe private static void mapValuesByteToFloatLinearWithNullAlpha(byte* ipstart, byte* opstart, float* igtstart, int cb)
		{
			byte* ip = ipstart, ipe = ipstart + cb - 3;
			float* op = (float*)opstart, igt = igtstart;

			while (ip <= ipe)
			{
				float o0 = igt[(uint)ip[0]];
				float o1 = igt[(uint)ip[1]];
				float o2 = igt[(uint)ip[2]];
				op[0] = o0;
				op[1] = o1;
				op[2] = o2;

				ip += 3;
				op += 4;
			}
		}

		unsafe private static void mapValuesFloatLinearToByte(byte* ipstart, byte* opstart, byte* gtstart, int cb)
		{
			float* ip = (float*)ipstart, ipe = (float*)(ipstart + cb) - VectorF.Count;
			byte* op = opstart, gt = gtstart;

			var vmin = VectorF.Zero;
			var vmax = new VectorF(UQ15One);
			var vscale = new VectorF(FloatScale);
			var vround = new VectorF(FloatRound);

			while (ip <= ipe)
			{
				var v = Unsafe.Read<VectorF>(ip) * vscale + vround;
				v = v.Clamp(vmin, vmax);

#if VECTOR_CONVERT
				var vi = Vector.ConvertToInt32(v);
#else
				var vi = v;
#endif

				byte o0 = gt[(uint)vi[0]];
				byte o1 = gt[(uint)vi[1]];
				byte o2 = gt[(uint)vi[2]];
				byte o3 = gt[(uint)vi[3]];
				op[0] = o0;
				op[1] = o1;
				op[2] = o2;
				op[3] = o3;

				if (VectorF.Count == 8)
				{
					o0 = gt[(uint)vi[4]];
					o1 = gt[(uint)vi[5]];
					o2 = gt[(uint)vi[6]];
					o3 = gt[(uint)vi[7]];
					op[4] = o0;
					op[5] = o1;
					op[6] = o2;
					op[7] = o3;
				}

				ip += VectorF.Count;
				op += VectorF.Count;
			}

			ipe += VectorF.Count;
			while (ip < ipe)
			{
				op[0] = gt[FixToUQ15One(ip[0])];
				ip++;
				op++;
			}
		}

		unsafe private static void mapValuesFloatLinearToByteWithAlpha(byte* ipstart, byte* opstart, byte* gtstart, int cb)
		{
			float* ip = (float*)ipstart, ipe = (float*)(ipstart + cb) - 4;
			byte* op = opstart, gt = gtstart;
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
					float f3i = FloatScale / f3;
					byte o0 = gt[ClampToUQ15One((int)(ip[0] * f3i + fround))];
					byte o1 = gt[ClampToUQ15One((int)(ip[1] * f3i + fround))];
					byte o2 = gt[ClampToUQ15One((int)(ip[2] * f3i + fround))];
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

		unsafe private static void mapValuesFloatLinearWithNullAlphaToByte(byte* ipstart, byte* opstart, byte* gtstart, int cb)
		{
			float* ip = (float*)ipstart, ipe = (float*)(ipstart + cb) - VectorF.Count;
			byte* op = opstart, gt = gtstart;

			var vmin = VectorF.Zero;
			var vmax = new VectorF(UQ15One);
			var vscale = new VectorF(FloatScale);
			var vround = new VectorF(FloatRound);

			while (ip <= ipe)
			{
				var v = Unsafe.Read<VectorF>(ip) * vscale + vround;
				v = v.Clamp(vmin, vmax);

#if VECTOR_CONVERT
				var vi = Vector.ConvertToInt32(v);
#else
				var vi = v;
#endif

				byte o0 = gt[(uint)vi[0]];
				byte o1 = gt[(uint)vi[1]];
				byte o2 = gt[(uint)vi[2]];
				op[0] = o0;
				op[1] = o1;
				op[2] = o2;

				if (VectorF.Count == 8)
				{
					o0 = gt[(uint)vi[4]];
					o1 = gt[(uint)vi[5]];
					o2 = gt[(uint)vi[6]];
					op[3] = o0;
					op[4] = o1;
					op[5] = o2;
				}

				ip += VectorF.Count;
				op += VectorF.Count - VectorF.Count / 4;
			}

			ipe += VectorF.Count;
			while (ip < ipe)
			{
				op[0] = gt[FixToUQ15One(ip[0])];
				op[1] = gt[FixToUQ15One(ip[1])];
				op[2] = gt[FixToUQ15One(ip[2])];

				ip += 4;
				op += 3;
			}
		}

		unsafe private static void mapValuesByteToFloat(byte* ipstart, byte* opstart, int cb)
		{
#if VECTOR_CONVERT
			int UnrollCount = Vector<byte>.Count;
			var vscale = new VectorF(1f / byte.MaxValue);
#else
			const int UnrollCount = 8;
#endif

			fixed (float* atstart = &LookupTables.AlphaFloat[0])
			{
				byte* ip = ipstart, ipe = ipstart + cb - UnrollCount;
				float* op = (float*)opstart, at = atstart;

				while (ip <= ipe)
				{
#if VECTOR_CONVERT
					var vb = Unsafe.Read<Vector<byte>>(ip);
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

					Unsafe.Write(op, vf0);
					Unsafe.Write(op + VectorF.Count, vf1);
					Unsafe.Write(op + VectorF.Count * 2, vf2);
					Unsafe.Write(op + VectorF.Count * 3, vf3);
#else
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
#endif

					ip += UnrollCount;
					op += UnrollCount;
				}

				ipe += UnrollCount;
				while (ip < ipe)
				{
					op[0] = at[(uint)ip[0]];
					ip++;
					op++;
				}
			}
		}

		unsafe private static void mapValuesByteToFloatWithAlpha(byte* ipstart, byte* opstart, int cb)
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

		unsafe private static void mapValuesByteToFloatWithNullAlpha(byte* ipstart, byte* opstart, int cb)
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

		unsafe private static void mapValuesFloatToByte(byte* ipstart, byte* opstart, int cb)
		{
#if VECTOR_CONVERT
			int UnrollCount = Vector<byte>.Count;
#else
			int UnrollCount = VectorF.Count;
#endif

			float* ip = (float*)ipstart, ipe = (float*)(ipstart + cb) - UnrollCount;
			byte* op = opstart;

			var vmin = new VectorF(byte.MinValue);
			var vmax = new VectorF(byte.MaxValue);
			var vround = new VectorF(FloatRound);

			while (ip <= ipe)
			{
#if VECTOR_CONVERT
				var vf0 = Unsafe.Read<VectorF>(ip);
				var vf1 = Unsafe.Read<VectorF>(ip + VectorF.Count);
				var vf2 = Unsafe.Read<VectorF>(ip + VectorF.Count * 2);
				var vf3 = Unsafe.Read<VectorF>(ip + VectorF.Count * 3);

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
				Unsafe.Write(op, vb);
#else
				var v = Unsafe.Read<VectorF>(ip) * vmax + vround;
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

				ip += UnrollCount;
				op += UnrollCount;
			}

			ipe += UnrollCount;
			while (ip < ipe)
			{
				op[0] = FixToByte(ip[0]);
				ip++;
				op++;
			}
		}

		unsafe private static void mapValuesFloatToByteWithAlpha(byte* ipstart, byte* opstart, int cb)
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

		unsafe private static void mapValuesFloatWithNullAlphaToByte(byte* ipstart, byte* opstart, int cb)
		{
			float* ip = (float*)ipstart, ipe = (float*)(ipstart + cb) - VectorF.Count;
			byte* op = opstart;

			var vmin = new VectorF(byte.MinValue);
			var vmax = new VectorF(byte.MaxValue);
			var vround = new VectorF(FloatRound);

			while (ip <= ipe)
			{
				var v = Unsafe.Read<VectorF>(ip) * vmax + vround;
				v = v.Clamp(vmin, vmax);

				op[0] = (byte)v[0];
				op[1] = (byte)v[1];
				op[2] = (byte)v[2];

				if (VectorF.Count == 8)
				{
					op[3] = (byte)v[4];
					op[4] = (byte)v[5];
					op[5] = (byte)v[6];
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

		unsafe private static void mapValues4to3Chan<T>(byte* ipstart, byte* opstart, int cb) where T : unmanaged
		{
			T* ip = (T*)ipstart, ipe = (T*)(ipstart + cb) - 4, op = (T*)opstart;

			while (ip <= ipe)
			{
				op[0] = ip[0];
				op[1] = ip[1];
				op[2] = ip[2];

				ip += 4;
				op += 3;
			}
		}

		unsafe public static void ConvertBgrToGreyByte(byte* ipstart, byte* opstart, int cb)
		{
			byte* ip = ipstart, ipe = ipstart + cb - 3, op = opstart;

			while (ip <= ipe)
			{
				byte y = Rec601.LumaFromBgr(ip[0], ip[1], ip[2]);
				op[0] = y;

				ip += 3;
				op++;
			}
		}

		unsafe public static void ConvertBgrToGreyUQ15(byte* ipstart, byte* opstart, int cb)
		{
			fixed (byte* gtstart = &LookupTables.SrgbGamma[0])
			{
				ushort* ip = (ushort*)ipstart, ipe = (ushort*)(ipstart + cb) - 3, op = (ushort*)opstart;
				byte* gt = gtstart;

				while (ip <= ipe)
				{
					uint y = Rec709.LumaFromBgr(ip[0], ip[1], ip[2]);
					op[0] = gt[y];

					ip += 3;
					op++;
				}
			}
		}

		unsafe public static void ConvertBgrToGreyFloat(byte* ipstart, byte* opstart, int cb, bool linear)
		{
			float* ip = (float*)ipstart, ipe = (float*)(ipstart + cb) - 3, op = (float*)opstart;
			var clum = linear ? Rec709.Coefficients : Rec601.Coefficients;
			float cbl = clum.X, cgl = clum.Y, crl = clum.Z;

			while (ip <= ipe)
			{
				float c0 = ip[0] * cbl, c1 = ip[1] * cgl, c2 = ip[2] * crl;
				op[0] = c0 + c1 + c2;

				ip += 3;
				op++;
			}
		}

		unsafe public static void ConvertBgrxToGreyByte(byte* ipstart, byte* opstart, int cb)
		{
			byte* ip = ipstart, ipe = ipstart + cb - 4, op = opstart;

			while (ip <= ipe)
			{
				byte y = Rec601.LumaFromBgr(ip[0], ip[1], ip[2]);
				op[0] = y;

				ip += 4;
				op++;
			}
		}

		unsafe public static void ConvertBgrxToGreyUQ15(byte* ipstart, byte* opstart, int cb)
		{
			fixed (byte* gtstart = &LookupTables.SrgbGamma[0])
			{
				ushort* ip = (ushort*)ipstart, ipe = (ushort*)(ipstart + cb) - 4, op = (ushort*)opstart;
				byte* gt = gtstart;

				while (ip <= ipe)
				{
					uint y = Rec709.LumaFromBgr(ip[0], ip[1], ip[2]);
					op[0] = gt[y];

					ip += 4;
					op++;
				}
			}
		}

		unsafe public static void ConvertBgrxToGreyFloat(byte* ipstart, byte* opstart, int cb, bool linear)
		{
			float* ip = (float*)ipstart, ipe = (float*)(ipstart + cb) - 4, op = (float*)opstart;
			var clum = new Vector4(linear ? Rec709.Coefficients : Rec601.Coefficients, 0f);

			while (ip <= ipe)
			{
				*op++ = Vector4.Dot(Unsafe.Read<Vector4>(ip), clum);
				ip += 4;
			}
		}

		unsafe public static void ConvertGreyLinearToGreyUQ15(byte* ipstart, byte* opstart, int cb)
		{
			fixed (byte* gtstart = &LookupTables.SrgbGamma[0])
			{
				ushort* ip = (ushort*)ipstart, ipe = (ushort*)(ipstart + cb), op = (ushort*)opstart;
				byte* gt = gtstart;

				while (ip < ipe)
					*op++ = gt[ClampToUQ15One(*ip++)];
			}
		}

		unsafe public static void ConvertGreyLinearToGreyFloat(byte* ipstart, byte* opstart, int cb)
		{
			float* ip = (float*)ipstart, ipe = (float*)(ipstart + cb) - VectorF.Count, op = (float*)opstart;
			var vmin = Vector<float>.Zero;
			float fmin = vmin[0];

			while (ip <= ipe)
			{
				var v = Unsafe.Read<VectorF>(ip);
				Unsafe.Write(op, Vector.SquareRoot(Vector.Max(v, vmin)));

				ip += VectorF.Count;
				op += VectorF.Count;
			}

			ipe += VectorF.Count;
			while (ip < ipe)
				*op++ = MaxF(*ip++, fmin).Sqrt();
		}

		public void Dispose()
		{
			ArrayPool<byte>.Shared.Return(LineBuff ?? Array.Empty<byte>());
			LineBuff = null;
		}

		public override string ToString() => $"{base.ToString()}: {InFormat.Name}->{Format.Name}";
	}
}
