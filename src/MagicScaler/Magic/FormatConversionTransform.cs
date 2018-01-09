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

			return UnFixToUQ15(r * rY + g * gY + b * bY);
		}
	}

	internal class FormatConversionTransform : PixelSource, IDisposable
	{
		protected byte[] LineBuff;
		protected PixelFormat InFormat;

		public FormatConversionTransform(PixelSource source, Guid dstFormat) : base(source)
		{
			InFormat = source.Format;
			Format = PixelFormat.Cache[dstFormat];
			LineBuff = ArrayPool<byte>.Shared.Rent((int)BufferStride);
		}

		unsafe protected override void CopyPixelsInternal(WICRect prc, uint cbStride, uint cbBufferSize, IntPtr pbBuffer)
		{
			fixed (byte* bstart = LineBuff)
			{
				int oh = prc.Height, oy = prc.Y;

				prc.Height = 1;
				int cb = (prc.Width * InFormat.BitsPerPixel + 7) / 8;
				for (int y = 0; y < oh; y++)
				{
					prc.Y = oy + y;
					Timer.Stop();
					Source.CopyPixels(prc, BufferStride, BufferStride, (IntPtr)bstart);
					Timer.Start();

					byte* op = (byte*)pbBuffer + y * cbStride;

					if (InFormat.ColorRepresentation == PixelColorRepresentation.Cmyk && InFormat.BitsPerPixel == 64 && Format.BitsPerPixel == 32)
					{
						mapValuesShortToByte(bstart, op, cb);
					}
					else if (InFormat.Colorspace == PixelColorspace.sRgb && Format.Colorspace == PixelColorspace.LinearRgb && Format.NumericRepresentation == PixelNumericRepresentation.Fixed)
					{
						if (InFormat.AlphaRepresentation != PixelAlphaRepresentation.None)
							mapValuesByteToUQ15LinearWithAlpha(bstart, op, cb);
						else
							mapValuesByteToUQ15Linear(bstart, op, cb);
					}
					else if (InFormat.Colorspace == PixelColorspace.LinearRgb && Format.Colorspace == PixelColorspace.sRgb && InFormat.NumericRepresentation == PixelNumericRepresentation.Fixed)
					{
						if (InFormat.AlphaRepresentation == PixelAlphaRepresentation.Associated)
							mapValuesUQ15LinearToByteWithAssociatedAlpha(bstart, op, cb);
						else if (InFormat.AlphaRepresentation == PixelAlphaRepresentation.Unassociated)
							mapValuesUQ15LinearToByteWithAlpha(bstart, op, cb);
						else
							mapValuesUQ15LinearToByte(bstart, op, cb);
					}
					else if (InFormat.Colorspace == PixelColorspace.sRgb && Format.Colorspace == PixelColorspace.LinearRgb && Format.NumericRepresentation == PixelNumericRepresentation.Float)
					{
						if (InFormat.AlphaRepresentation == PixelAlphaRepresentation.Associated)
							mapValuesByteToFloatLinearWithAssociatedAlpha(bstart, op, cb);
						else if (InFormat.AlphaRepresentation == PixelAlphaRepresentation.Unassociated)
							mapValuesByteToFloatLinearWithAlpha(bstart, op, cb);
						else if (InFormat.AlphaRepresentation == PixelAlphaRepresentation.None && InFormat.ChannelCount == 3 && Format.ChannelCount == 4)
							mapValuesByteToFloatLinearWithNullAlpha(bstart, op, cb);
						else
							mapValuesByteToFloatLinear(bstart, op, cb);
					}
					else if (InFormat.Colorspace == PixelColorspace.LinearRgb && Format.Colorspace == PixelColorspace.sRgb && InFormat.NumericRepresentation == PixelNumericRepresentation.Float)
					{
						if (Format.AlphaRepresentation == PixelAlphaRepresentation.Unassociated)
							mapValuesFloatLinearToByteWithAlpha(bstart, op, cb);
						else if (InFormat.AlphaRepresentation == PixelAlphaRepresentation.None && InFormat.ChannelCount == 4 && Format.ChannelCount == 3)
							mapValuesFloatLinearWithNullAlphaToByte(bstart, op, cb);
						else
							mapValuesFloatLinearToByte(bstart, op, cb);
					}
					else if (Format.NumericRepresentation == PixelNumericRepresentation.Float)
					{
						if (InFormat.AlphaRepresentation == PixelAlphaRepresentation.Unassociated)
							mapValuesByteToFloatWithAlpha(bstart, op, cb);
						else if (InFormat.AlphaRepresentation == PixelAlphaRepresentation.None && InFormat.ChannelCount == 3 && Format.ChannelCount == 4)
							mapValuesByteToFloatWithNullAlpha(bstart, op, cb);
						else
							mapValuesByteToFloat(bstart, op, cb);
					}
					else if (InFormat.NumericRepresentation == PixelNumericRepresentation.Float)
					{
						if (Format.AlphaRepresentation == PixelAlphaRepresentation.Unassociated)
							mapValuesFloatToByteWithAlpha(bstart, op, cb);
						else if (InFormat.AlphaRepresentation == PixelAlphaRepresentation.None && InFormat.ChannelCount == 4 && Format.ChannelCount == 3)
							mapValuesFloatWithNullAlphaToByte(bstart, op, cb);
						else
							mapValuesFloatToByte(bstart, op, cb);
					}
					else
						throw new NotSupportedException("Unsupported pixel format");
				}
			}
		}

		unsafe private static void mapValuesShortToByte(byte* ipstart, byte* opstart, int cb)
		{
			ushort* ip = (ushort*)ipstart + 4, ipe = (ushort*)(ipstart + cb);
			byte* op = opstart + 4;

			while (ip <= ipe)
			{
				byte o0 = (byte)(ip[-4] >> 8);
				byte o1 = (byte)(ip[-3] >> 8);
				byte o2 = (byte)(ip[-2] >> 8);
				byte o3 = (byte)(ip[-1] >> 8);
				op[-4] = o0;
				op[-3] = o1;
				op[-2] = o2;
				op[-1] = o3;

				ip += 4;
				op += 4;
			}
		}

		unsafe private static void mapValuesByteToUQ15Linear(byte* ipstart, byte* opstart, int cb)
		{
			fixed (ushort* igtstart = LookupTables.InverseGammaUQ15)
			{
				byte* ip = ipstart + 8, ipe = ipstart + cb;
				ushort* op = (ushort*)opstart + 8, igt = igtstart;

				while (ip <= ipe)
				{
					ushort o0 = igt[(uint)ip[-8]];
					ushort o1 = igt[(uint)ip[-7]];
					ushort o2 = igt[(uint)ip[-6]];
					ushort o3 = igt[(uint)ip[-5]];
					op[-8] = o0;
					op[-7] = o1;
					op[-6] = o2;
					op[-5] = o3;

					o0 = igt[(uint)ip[-4]];
					o1 = igt[(uint)ip[-3]];
					o2 = igt[(uint)ip[-2]];
					o3 = igt[(uint)ip[-1]];
					op[-4] = o0;
					op[-3] = o1;
					op[-2] = o2;
					op[-1] = o3;

					ip += 8;
					op += 8;
				}

				ip -= 8;
				op -= 8;
				while (ip < ipe)
				{
					op[0] = igt[ip[0]];
					ip++;
					op++;
				}
			}
		}

		unsafe private static void mapValuesByteToUQ15LinearWithAlpha(byte* ipstart, byte* opstart, int cb)
		{
			fixed (ushort* igtstart = LookupTables.InverseGammaUQ15, atstart = LookupTables.AlphaUQ15)
			{
				byte* ip = ipstart + 4, ipe = ipstart + cb;
				ushort* op = (ushort*)opstart + 4, igt = igtstart, at = atstart;

				while (ip <= ipe)
				{
					ushort o0 = igt[(uint)ip[-4]];
					ushort o1 = igt[(uint)ip[-3]];
					ushort o2 = igt[(uint)ip[-2]];
					ushort o3 = at[(uint)ip[-1]];
					op[-4] = o0;
					op[-3] = o1;
					op[-2] = o2;
					op[-1] = o3;

					ip += 4;
					op += 4;
				}
			}
		}

		unsafe private static void mapValuesUQ15LinearToByte(byte* ipstart, byte* opstart, int cb)
		{
			fixed (byte* gtstart = LookupTables.Gamma)
			{
				ushort* ip = (ushort*)ipstart + 8, ipe = (ushort*)(ipstart + cb);
				byte* op = opstart + 8, gt = gtstart;

				while (ip <= ipe)
				{
					byte o0 = gt[(uint)ip[-8]];
					byte o1 = gt[(uint)ip[-7]];
					byte o2 = gt[(uint)ip[-6]];
					byte o3 = gt[(uint)ip[-5]];
					op[-8] = o0;
					op[-7] = o1;
					op[-6] = o2;
					op[-5] = o3;

					o0 = gt[(uint)ip[-4]];
					o1 = gt[(uint)ip[-3]];
					o2 = gt[(uint)ip[-2]];
					o3 = gt[(uint)ip[-1]];
					op[-4] = o0;
					op[-3] = o1;
					op[-2] = o2;
					op[-1] = o3;

					ip += 8;
					op += 8;
				}

				ip -= 8;
				op -= 8;
				while (ip < ipe)
				{
					op[0] = gt[ip[0]];
					ip++;
					op++;
				}
			}
		}

		unsafe private static void mapValuesUQ15LinearToByteWithAlpha(byte* ipstart, byte* opstart, int cb)
		{
			fixed (byte* gtstart = LookupTables.Gamma)
			{
				ushort* ip = (ushort*)ipstart + 4, ipe = (ushort*)(ipstart + cb);
				byte* op = opstart + 4, gt = gtstart;

				while (ip <= ipe)
				{
					byte o3 = UnFix15ToByte(ip[-1] * byte.MaxValue);
					if (o3 == 0)
					{
						op[-4] = 0;
						op[-3] = 0;
						op[-2] = 0;
						op[-1] = 0;
					}
					else
					{
						byte o0 = gt[(uint)ip[-4]];
						byte o1 = gt[(uint)ip[-3]];
						byte o2 = gt[(uint)ip[-2]];
						op[-4] = o0;
						op[-3] = o1;
						op[-2] = o2;
						op[-1] = o3;
					}

					ip += 4;
					op += 4;
				}
			}
		}

		unsafe private static void mapValuesUQ15LinearToByteWithAssociatedAlpha(byte* ipstart, byte* opstart, int cb)
		{
			fixed (byte* gtstart = LookupTables.Gamma)
			{
				ushort* ip = (ushort*)ipstart + 4, ipe = (ushort*)(ipstart + cb);
				byte* op = opstart + 4, gt = gtstart;

				while (ip <= ipe)
				{
					byte o3 = UnFix15ToByte(ip[-1] * byte.MaxValue);
					if (o3 == 0)
					{
						op[-4] = 0;
						op[-3] = 0;
						op[-2] = 0;
						op[-1] = 0;
					}
					else
					{
						int o3i = (UQ15One << 15) / o3;
						byte o0 = gt[UnFixToUQ15(ip[-4] * o3i)];
						byte o1 = gt[UnFixToUQ15(ip[-3] * o3i)];
						byte o2 = gt[UnFixToUQ15(ip[-2] * o3i)];
						op[-4] = o0;
						op[-3] = o1;
						op[-2] = o2;
						op[-1] = o3;
					}

					ip += 4;
					op += 4;
				}
			}
		}

		unsafe private static void mapValuesByteToFloatLinear(byte* ipstart, byte* opstart, int cb)
		{
			fixed (float* igtstart = LookupTables.InverseGammaFloat)
			{
				byte* ip = ipstart + 8, ipe = ipstart + cb;
				float* op = (float*)opstart + 8, igt = igtstart;

				while (ip <= ipe)
				{
					float o0 = igt[(uint)ip[-8]];
					float o1 = igt[(uint)ip[-7]];
					float o2 = igt[(uint)ip[-6]];
					float o3 = igt[(uint)ip[-5]];
					float o4 = igt[(uint)ip[-4]];
					float o5 = igt[(uint)ip[-3]];
					float o6 = igt[(uint)ip[-2]];
					float o7 = igt[(uint)ip[-1]];

					op[-8] = o0;
					op[-7] = o1;
					op[-6] = o2;
					op[-5] = o3;
					op[-4] = o4;
					op[-3] = o5;
					op[-2] = o6;
					op[-1] = o7;

					ip += 8;
					op += 8;
				}

				ip -= 8;
				op -= 8;
				while (ip < ipe)
				{
					op[0] = igt[ip[0]];
					ip++;
					op++;
				}
			}
		}

		unsafe private static void mapValuesByteToFloatLinearWithAlpha(byte* ipstart, byte* opstart, int cb)
		{
			fixed (float* igtstart = LookupTables.InverseGammaFloat, atstart = LookupTables.AlphaFloat)
			{
				byte* ip = ipstart + 4, ipe = ipstart + cb;
				float* op = (float*)opstart + 4, igt = igtstart, at = atstart;

				while (ip <= ipe)
				{
					float o0 = igt[(uint)ip[-4]];
					float o1 = igt[(uint)ip[-3]];
					float o2 = igt[(uint)ip[-2]];
					float o3 = at[(uint)ip[-1]];
					op[-4] = o0 * o3;
					op[-3] = o1 * o3;
					op[-2] = o2 * o3;
					op[-1] = o3;

					ip += 4;
					op += 4;
				}
			}
		}

		unsafe private static void mapValuesByteToFloatLinearWithAssociatedAlpha(byte* ipstart, byte* opstart, int cb)
		{
			fixed (float* igtstart = LookupTables.InverseGammaFloat, atstart = LookupTables.AlphaFloat)
			{
				byte* ip = ipstart + 4, ipe = ipstart + cb;
				float* op = (float*)opstart + 4, igt = igtstart, at = atstart;

				while (ip <= ipe)
				{
					float o0 = igt[(uint)ip[-4]];
					float o1 = igt[(uint)ip[-3]];
					float o2 = igt[(uint)ip[-2]];
					float o3 = at[(uint)ip[-1]];
					op[-4] = o0;
					op[-3] = o1;
					op[-2] = o2;
					op[-1] = o3;

					ip += 4;
					op += 4;
				}
			}
		}

		unsafe private static void mapValuesByteToFloatLinearWithNullAlpha(byte* ipstart, byte* opstart, int cb)
		{
			fixed (float* igtstart = LookupTables.InverseGammaFloat)
			{
				byte* ip = ipstart + 3, ipe = ipstart + cb;
				float* op = (float*)opstart + 4, igt = igtstart;

				while (ip <= ipe)
				{
					float o0 = igt[(uint)ip[-3]];
					float o1 = igt[(uint)ip[-2]];
					float o2 = igt[(uint)ip[-1]];
					op[-4] = o0;
					op[-3] = o1;
					op[-2] = o2;

					ip += 3;
					op += 4;
				}
			}
		}

		unsafe private static void mapValuesFloatLinearToByte(byte* ipstart, byte* opstart, int cb)
		{
			fixed (byte* gtstart = LookupTables.Gamma)
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

					//TODO future JIT versions will auto-unroll loops over vector elements
					byte o0 = gt[(uint)v[0]];
					byte o1 = gt[(uint)v[1]];
					byte o2 = gt[(uint)v[2]];
					byte o3 = gt[(uint)v[3]];
					op[0] = o0;
					op[1] = o1;
					op[2] = o2;
					op[3] = o3;

					if (VectorF.Count == 8)
					{
						o0 = gt[(uint)v[4]];
						o1 = gt[(uint)v[5]];
						o2 = gt[(uint)v[6]];
						o3 = gt[(uint)v[7]];
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
					op[0] = gt[FixToUQ15(ip[0])];
					ip++;
					op++;
				}
			}
		}

		unsafe private static void mapValuesFloatLinearToByteWithAlpha(byte* ipstart, byte* opstart, int cb)
		{
			fixed (byte* gtstart = LookupTables.Gamma)
			{
				float* ip = (float*)ipstart + 4, ipe = (float*)(ipstart + cb);
				byte* op = opstart + 4, gt = gtstart;
				float fmax = new Vector4(byte.MaxValue).X, fround = new Vector4(FloatRound).X, fmin = fround / fmax;

				while (ip <= ipe)
				{
					float f3 = ip[-1];
					if (f3 < fmin)
					{
						op[-4] = 0;
						op[-3] = 0;
						op[-2] = 0;
						op[-1] = 0;
					}
					else
					{
						float f3i = FloatScale / f3;
						byte o0 = gt[ClampToUQ15((int)(ip[-4] * f3i + fround))];
						byte o1 = gt[ClampToUQ15((int)(ip[-3] * f3i + fround))];
						byte o2 = gt[ClampToUQ15((int)(ip[-2] * f3i + fround))];
						byte o3 = ClampToByte((int)(f3 * fmax + fround));
						op[-4] = o0;
						op[-3] = o1;
						op[-2] = o2;
						op[-1] = o3;
					}

					ip += 4;
					op += 4;
				}
			}
		}

		unsafe private static void mapValuesFloatLinearWithNullAlphaToByte(byte* ipstart, byte* opstart, int cb)
		{
			fixed (byte* gtstart = LookupTables.Gamma)
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

					byte o0 = gt[(uint)v[0]];
					byte o1 = gt[(uint)v[1]];
					byte o2 = gt[(uint)v[2]];
					op[0] = o0;
					op[1] = o1;
					op[2] = o2;

					if (VectorF.Count == 8)
					{
						o0 = gt[(uint)v[4]];
						o1 = gt[(uint)v[5]];
						o2 = gt[(uint)v[6]];
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
					op[0] = gt[FixToUQ15(ip[0])];
					op[1] = gt[FixToUQ15(ip[1])];
					op[2] = gt[FixToUQ15(ip[2])];

					ip += 4;
					op += 3;
				}
			}
		}

		unsafe private static void mapValuesByteToFloat(byte* ipstart, byte* opstart, int cb)
		{
			fixed (float* atstart = LookupTables.AlphaFloat)
			{
				byte* ip = ipstart + 8, ipe = ipstart + cb;
				float* op = (float*)opstart + 8, at = atstart;

				while (ip <= ipe)
				{
					float o0 = at[(uint)ip[-8]];
					float o1 = at[(uint)ip[-7]];
					float o2 = at[(uint)ip[-6]];
					float o3 = at[(uint)ip[-5]];
					float o4 = at[(uint)ip[-4]];
					float o5 = at[(uint)ip[-3]];
					float o6 = at[(uint)ip[-2]];
					float o7 = at[(uint)ip[-1]];

					op[-8] = o0;
					op[-7] = o1;
					op[-6] = o2;
					op[-5] = o3;
					op[-4] = o4;
					op[-3] = o5;
					op[-2] = o6;
					op[-1] = o7;

					ip += 8;
					op += 8;
				}

				ip -= 8;
				op -= 8;
				while (ip < ipe)
				{
					op[0] = at[ip[0]];
					ip++;
					op++;
				}
			}
		}

		unsafe private static void mapValuesByteToFloatWithAlpha(byte* ipstart, byte* opstart, int cb)
		{
			fixed (float* atstart = LookupTables.AlphaFloat)
			{
				byte* ip = ipstart + 4, ipe = ipstart + cb;
				float* op = (float*)opstart + 4, at = atstart;

				while (ip <= ipe)
				{
					float o0 = at[(uint)ip[-4]];
					float o1 = at[(uint)ip[-3]];
					float o2 = at[(uint)ip[-2]];
					float o3 = at[(uint)ip[-1]];
					op[-4] = o0 * o3;
					op[-3] = o1 * o3;
					op[-2] = o2 * o3;
					op[-1] = o3;

					ip += 4;
					op += 4;
				}
			}
		}

		unsafe private static void mapValuesByteToFloatWithNullAlpha(byte* ipstart, byte* opstart, int cb)
		{
			fixed (float* atstart = LookupTables.AlphaFloat)
			{
				byte* ip = ipstart + 3, ipe = ipstart + cb;
				float* op = (float*)opstart + 4, at = atstart;

				while (ip <= ipe)
				{
					float o0 = at[(uint)ip[-3]];
					float o1 = at[(uint)ip[-2]];
					float o2 = at[(uint)ip[-1]];
					op[-4] = o0;
					op[-3] = o1;
					op[-2] = o2;

					ip += 3;
					op += 4;
				}
			}
		}

		unsafe private static void mapValuesFloatToByte(byte* ipstart, byte* opstart, int cb)
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
				op[3] = (byte)v[3];

				if (VectorF.Count == 8)
				{
					op[4] = (byte)v[4];
					op[5] = (byte)v[5];
					op[6] = (byte)v[6];
					op[7] = (byte)v[7];
				}

				ip += VectorF.Count;
				op += VectorF.Count;
			}

			ipe += VectorF.Count;
			while (ip < ipe)
			{
				op[0] = FixToByte(ip[0]);
				ip++;
				op++;
			}
		}

		unsafe private static void mapValuesFloatToByteWithAlpha(byte* ipstart, byte* opstart, int cb)
		{
			float* ip = (float*)ipstart + 4, ipe = (float*)(ipstart + cb);
			byte* op = opstart + 4;
			float fmax = new Vector4(byte.MaxValue).X, fround = new Vector4(FloatRound).X, fmin = fround / fmax;

			while (ip <= ipe)
			{
				float f3 = ip[-1];
				if (f3 < fmin)
				{
					op[-4] = 0;
					op[-3] = 0;
					op[-2] = 0;
					op[-1] = 0;
				}
				else
				{
					float f3i = byte.MaxValue / f3;
					byte o0 = ClampToByte((int)(ip[-4] * f3i + fround));
					byte o1 = ClampToByte((int)(ip[-3] * f3i + fround));
					byte o2 = ClampToByte((int)(ip[-2] * f3i + fround));
					byte o3 = ClampToByte((int)(f3 * fmax + fround));
					op[-4] = o0;
					op[-3] = o1;
					op[-2] = o2;
					op[-1] = o3;
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

		unsafe public static void ConvertBgrToGreyByte(byte* ipstart, byte* opstart, int cb)
		{
			byte* ip = ipstart + 3, ipe = ipstart + cb;
			byte* op = opstart + 1;

			while (ip <= ipe)
			{
				byte y = Rec601.LumaFromBgr(ip[-3], ip[-2], ip[-1]);
				op[-1] = y;

				ip += 3;
				op++;
			}
		}

		unsafe public static void ConvertBgrToGreyUQ15(byte* ipstart, byte* opstart, int cb)
		{
			fixed (byte* gtstart = LookupTables.Gamma)
			{
				ushort* ip = (ushort*)ipstart + 3, ipe = (ushort*)(ipstart + cb);
				byte* op = opstart + 1, gt = gtstart;

				while (ip <= ipe)
				{
					ushort y = Rec709.LumaFromBgr(ip[-3], ip[-2], ip[-1]);
					op[-1] = gt[ClampToUQ15(y)];

					ip += 3;
					op++;
				}
			}
		}

		unsafe public static void ConvertBgrToGreyFloat(byte* ipstart, byte* opstart, int cb, bool linear)
		{
			float* ip = (float*)ipstart, ipe = (float*)(ipstart + cb);
			float* op = (float*)opstart;

			var clum = linear ? Rec709.Coefficients : Rec601.Coefficients;
			float cbl = clum.X, cgl = clum.Y, crl = clum.Z;

			while (ip < ipe)
			{
				float c0 = ip[0] * cbl, c1 = ip[1] * cgl, c2 = ip[2] * crl;
				op[0] = c0 + c1 + c2;

				ip += 3;
				op++;
			}
		}

		unsafe public static void ConvertBgrxToGreyByte(byte* ipstart, byte* opstart, int cb)
		{
			byte* ip = ipstart + 4, ipe = ipstart + cb;
			byte* op = opstart + 1;

			while (ip <= ipe)
			{
				byte y = Rec601.LumaFromBgr(ip[-4], ip[-3], ip[-2]);
				op[-1] = y;

				ip += 4;
				op++;
			}
		}

		unsafe public static void ConvertBgrxToGreyUQ15(byte* ipstart, byte* opstart, int cb)
		{
			fixed (byte* gtstart = LookupTables.Gamma)
			{
				ushort* ip = (ushort*)ipstart + 4, ipe = (ushort*)(ipstart + cb), op = (ushort*)opstart + 1;
				byte* gt = gtstart;

				while (ip <= ipe)
				{
					uint y = Rec709.LumaFromBgr(ip[-4], ip[-3], ip[-2]);
					op[-1] = gt[y];

					ip += 4;
					op++;
				}
			}
		}

		unsafe public static void ConvertBgrxToGreyFloat(byte* ipstart, byte* opstart, int cb, bool linear)
		{
			float* ip = (float*)ipstart, ipe = (float*)(ipstart + cb);
			float* op = (float*)opstart;

			var clum = linear ? Rec709.Coefficients : Rec601.Coefficients;
			float cbl = clum.X, cgl = clum.Y, crl = clum.Z;

			while (ip < ipe)
			{
				float c0 = ip[0] * cbl, c1 = ip[1] * cgl, c2 = ip[2] * crl;
				op[0] = c0 + c1 + c2;

				ip += 4;
				op++;
			}
		}

		unsafe public static void ConvertGreyLinearToGreyUQ15(byte* ipstart, byte* opstart, int cb)
		{
			fixed (byte* gtstart = LookupTables.Gamma)
			{
				ushort* ip = (ushort*)ipstart, ipe = (ushort*)(ipstart + cb), op = (ushort*)opstart;
				byte* gt = gtstart;

				while (ip < ipe)
					*op++ = gt[(uint)*ip++];
			}
		}

		unsafe public static void ConvertGreyLinearToGreyFloat(byte* ipstart, byte* opstart, int cb)
		{
			float* ip = (float*)ipstart, ipe = (float*)(ipstart + cb) - VectorF.Count;
			float* op = (float*)opstart;

			while (ip <= ipe)
			{
				var v = Unsafe.Read<VectorF>(ip);
				Unsafe.Write(op, Vector.SquareRoot(v));

				ip += VectorF.Count;
				op += VectorF.Count;
			}

			ipe += VectorF.Count;
			while (ip < ipe)
				*op++ = (*ip++).Sqrt();
		}

		public void Dispose()
		{
			ArrayPool<byte>.Shared.Return(LineBuff ?? Array.Empty<byte>());
			LineBuff = null;
		}

		public override string ToString() => $"{base.ToString()}: {InFormat.Name}->{Format.Name}";
	}
}
