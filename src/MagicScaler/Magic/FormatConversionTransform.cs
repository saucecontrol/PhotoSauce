using System;
using System.Buffers;
using System.Numerics;
using System.Runtime.CompilerServices;

using PhotoSauce.MagicScaler.Interop;
using static PhotoSauce.MagicScaler.MathUtil;

namespace PhotoSauce.MagicScaler
{
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
				int cb = (prc.Width * InFormat.BitsPerPixel + 7 & ~7) / 8;
				for (int y = 0; y < oh; y++)
				{
					prc.Y = oy + y;
					Timer.Stop();
					Source.CopyPixels(prc, BufferStride, BufferStride, (IntPtr)bstart);
					Timer.Start();

					byte* op = (byte*)pbBuffer + y * cbStride;

					if (InFormat.Colorspace == PixelColorspace.sRgb && Format.Colorspace == PixelColorspace.LinearRgb && Format.NumericRepresentation == PixelNumericRepresentation.Fixed)
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
						else
							mapValuesByteToFloatLinear(bstart, op, cb);
					}
					else if (InFormat.Colorspace == PixelColorspace.LinearRgb && Format.Colorspace == PixelColorspace.sRgb && InFormat.NumericRepresentation == PixelNumericRepresentation.Float)
					{
						if (Format.AlphaRepresentation == PixelAlphaRepresentation.Unassociated)
							mapValuesFloatLinearToByteWithAlpha(bstart, op, cb);
						else
							mapValuesFloatLinearToByte(bstart, op, cb);
					}
					else if (Format.NumericRepresentation == PixelNumericRepresentation.Float)
					{
						if (InFormat.AlphaRepresentation == PixelAlphaRepresentation.Unassociated)
							mapValuesByteToFloatWithAlpha(bstart, op, cb);
						else
							mapValuesByteToFloat(bstart, op, cb);
					}
					else if (InFormat.NumericRepresentation == PixelNumericRepresentation.Float)
					{
						if (Format.AlphaRepresentation == PixelAlphaRepresentation.Unassociated)
							mapValuesFloatToByteWithAlpha(bstart, op, cb);
						else
							mapValuesFloatToByte(bstart, op, cb);
					}
					else
						throw new NotSupportedException("Unsupported pixel format");
				}
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
					ushort o0 = igt[ip[-8]];
					ushort o1 = igt[ip[-7]];
					ushort o2 = igt[ip[-6]];
					ushort o3 = igt[ip[-5]];
					op[-8] = o0;
					op[-7] = o1;
					op[-6] = o2;
					op[-5] = o3;

					o0 = igt[ip[-4]];
					o1 = igt[ip[-3]];
					o2 = igt[ip[-2]];
					o3 = igt[ip[-1]];
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
					ushort o0 = igt[ip[-4]];
					ushort o1 = igt[ip[-3]];
					ushort o2 = igt[ip[-2]];
					ushort o3 = at[ip[-1]];
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
					byte o0 = gt[ip[-8]];
					byte o1 = gt[ip[-7]];
					byte o2 = gt[ip[-6]];
					byte o3 = gt[ip[-5]];
					op[-8] = o0;
					op[-7] = o1;
					op[-6] = o2;
					op[-5] = o3;

					o0 = gt[ip[-4]];
					o1 = gt[ip[-3]];
					o2 = gt[ip[-2]];
					o3 = gt[ip[-1]];
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
					byte o0 = gt[ip[-4]];
					byte o1 = gt[ip[-3]];
					byte o2 = gt[ip[-2]];
					byte o3 = UnscaleToByte(ip[-1] * byte.MaxValue);
					op[-4] = o0;
					op[-3] = o1;
					op[-2] = o2;
					op[-1] = o3;

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
					ushort o3 = ip[-1];
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
						byte o0 = gt[UnscaleToUQ15(ip[-4] * o3i)];
						byte o1 = gt[UnscaleToUQ15(ip[-3] * o3i)];
						byte o2 = gt[UnscaleToUQ15(ip[-2] * o3i)];
						op[-4] = o0;
						op[-3] = o1;
						op[-2] = o2;
						op[-1] = UnscaleToByte(o3 * byte.MaxValue);
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
					float o0 = igt[ip[-8]];
					float o1 = igt[ip[-7]];
					float o2 = igt[ip[-6]];
					float o3 = igt[ip[-5]];
					float o4 = igt[ip[-4]];
					float o5 = igt[ip[-3]];
					float o6 = igt[ip[-2]];
					float o7 = igt[ip[-1]];

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
					float o0 = igt[ip[-4]];
					float o1 = igt[ip[-3]];
					float o2 = igt[ip[-2]];
					float o3 = at[ip[-1]];
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
					float o0 = igt[ip[-8]];
					float o1 = igt[ip[-7]];
					float o2 = igt[ip[-6]];
					float o3 = at[ip[-5]];
					op[-4] = o0;
					op[-3] = o1;
					op[-2] = o2;
					op[-1] = o3;

					ip += 4;
					op += 4;
				}
			}
		}


		unsafe private static void mapValuesFloatLinearToByte(byte* ipstart, byte* opstart, int cb)
		{
			fixed (byte* gtstart = LookupTables.Gamma)
			{
				float* ip = (float*)ipstart, ipe = (float*)(ipstart + cb) - Vector<float>.Count;
				byte* op = opstart, gt = gtstart;

				var vmin = Vector<float>.Zero;
				var vmax = new Vector<float>(UQ15Max);
				var vscale = new Vector<float>(FloatScale);
				var vround = new Vector<float>(FloatRound);

				while (ip <= ipe)
				{
					var v = Unsafe.Read<Vector<float>>(ip) * vscale + vround;
					v = v.Clamp(vmin, vmax);

					//TODO future JIT versions will auto-unroll loops over vector elements
					byte o0 = gt[(ushort)v[0]];
					byte o1 = gt[(ushort)v[1]];
					byte o2 = gt[(ushort)v[2]];
					byte o3 = gt[(ushort)v[3]];
					op[0] = o0;
					op[1] = o1;
					op[2] = o2;
					op[3] = o3;

					if (Vector<float>.Count == 8)
					{
						o0 = gt[(ushort)v[4]];
						o1 = gt[(ushort)v[5]];
						o2 = gt[(ushort)v[6]];
						o3 = gt[(ushort)v[7]];
						op[4] = o0;
						op[5] = o1;
						op[6] = o2;
						op[7] = o3;
					}

					ip += Vector<float>.Count;
					op += Vector<float>.Count;
				}

				ipe += Vector<float>.Count;
				while (ip < ipe)
				{
					op[0] = gt[ScaleToUQ15(ip[0])];
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

				while (ip <= ipe)
				{
					float f3 = ip[-1];
					float f3i = FloatScale / f3;
					byte o0 = gt[ClampToUQ15((int)(ip[-4] * f3i + FloatRound))];
					byte o1 = gt[ClampToUQ15((int)(ip[-3] * f3i + FloatRound))];
					byte o2 = gt[ClampToUQ15((int)(ip[-2] * f3i + FloatRound))];
					byte o3 = ScaleToByte(f3);
					op[-4] = o0;
					op[-3] = o1;
					op[-2] = o2;
					op[-1] = o3;

					ip += 4;
					op += 4;
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
					float o0 = at[ip[-8]];
					float o1 = at[ip[-7]];
					float o2 = at[ip[-6]];
					float o3 = at[ip[-5]];
					float o4 = at[ip[-4]];
					float o5 = at[ip[-3]];
					float o6 = at[ip[-2]];
					float o7 = at[ip[-1]];

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
			fixed (float* igtstart = LookupTables.InverseGammaFloat, atstart = LookupTables.AlphaFloat)
			{
				byte* ip = ipstart + 4, ipe = ipstart + cb;
				float* op = (float*)opstart + 4, at = atstart;

				while (ip <= ipe)
				{
					float o0 = at[ip[-4]];
					float o1 = at[ip[-3]];
					float o2 = at[ip[-2]];
					float o3 = at[ip[-1]];
					op[-4] = o0 * o3;
					op[-3] = o1 * o3;
					op[-2] = o2 * o3;
					op[-1] = o3;

					ip += 4;
					op += 4;
				}
			}
		}

		unsafe private static void mapValuesFloatToByte(byte* ipstart, byte* opstart, int cb)
		{
			float* ip = (float*)ipstart, ipe = (float*)(ipstart + cb) - Vector<float>.Count;
			byte* op = opstart;

			var vmin = new Vector<float>(byte.MinValue);
			var vmax = new Vector<float>(byte.MaxValue);
			var vround = new Vector<float>(FloatRound);

			while (ip <= ipe)
			{
				var v = Unsafe.Read<Vector<float>>(ip) * vmax + vround;
				v = v.Clamp(vmin, vmax);

				op[0] = (byte)v[0];
				op[1] = (byte)v[1];
				op[2] = (byte)v[2];
				op[3] = (byte)v[3];

				if (Vector<float>.Count == 8)
				{
					op[4] = (byte)v[4];
					op[5] = (byte)v[5];
					op[6] = (byte)v[6];
					op[7] = (byte)v[7];
				}

				ip += Vector<float>.Count;
				op += Vector<float>.Count;
			}

			ipe += Vector<float>.Count;
			while (ip < ipe)
			{
				op[0] = ScaleToByte(ip[0]);
				ip++;
				op++;
			}
		}

		unsafe private static void mapValuesFloatToByteWithAlpha(byte* ipstart, byte* opstart, int cb)
		{
			float* ip = (float*)ipstart + 4, ipe = (float*)(ipstart + cb);
			byte* op = opstart + 4;

			while (ip <= ipe)
			{
				float f3 = ip[-1];
				float f3i = byte.MaxValue / f3;
				byte o0 = ClampToByte((int)(ip[-4] * f3i + FloatRound));
				byte o1 = ClampToByte((int)(ip[-3] * f3i + FloatRound));
				byte o2 = ClampToByte((int)(ip[-2] * f3i + FloatRound));
				byte o3 = ScaleToByte(f3);
				op[-4] = o0;
				op[-3] = o1;
				op[-2] = o2;
				op[-1] = o3;

				ip += 4;
				op += 4;
			}
		}

		public void Dispose()
		{
			ArrayPool<byte>.Shared.Return(LineBuff ?? Array.Empty<byte>());
			LineBuff = null;
		}

		public override string ToString() => $"{base.ToString()}: {InFormat.Name}->{Format.Name}";
	}
}
