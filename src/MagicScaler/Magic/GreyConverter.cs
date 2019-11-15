using System;
using System.Numerics;
using System.Runtime.CompilerServices;

using PhotoSauce.Interop.Wic;
using static PhotoSauce.MagicScaler.MathUtil;

using VectorF = System.Numerics.Vector<float>;

namespace PhotoSauce.MagicScaler
{
	internal static class Rec601
	{
		public const double R = 0.299;
		public const double B = 0.114;
		public const double G = 1 - R - B;
		public static Vector3 Coefficients = new Vector3((float)B, (float)G, (float)R);

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
		public const double R = 0.2126;
		public const double B = 0.0722;
		public const double G = 1 - R - B;
		public static Vector3 Coefficients = new Vector3((float)B, (float)G, (float)R);

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static ushort LumaFromBgr(ushort b, ushort g, ushort r)
		{
			const int rY = (ushort)(R * DoubleScale + DoubleRound);
			const int gY = (ushort)(G * DoubleScale + DoubleRound);
			const int bY = (ushort)(B * DoubleScale + DoubleRound);

			return UnFixToUQ15One(r * rY + g * gY + b * bY);
		}
	}

	internal static class GreyConverter
	{
		unsafe public static void ConvertLine(Guid inFormat, byte* ipstart, byte* opstart, int cbIn, int cbOut)
		{
			if (inFormat == PixelFormat.Grey32BppLinearFloat.FormatGuid || inFormat == PixelFormat.Y32BppLinearFloat.FormatGuid)
				greyLinearToGreyFloat(ipstart, opstart, cbIn);
			else if (inFormat == PixelFormat.Grey16BppLinearUQ15.FormatGuid || inFormat == PixelFormat.Y16BppLinearUQ15.FormatGuid)
				greyLinearToGreyUQ15(ipstart, opstart, cbIn);
			else if (inFormat == Consts.GUID_WICPixelFormat24bppBGR)
				bgrToGreyByte(ipstart, opstart, cbIn);
			else if (inFormat == PixelFormat.Bgr48BppLinearUQ15.FormatGuid)
				bgrToGreyUQ15(ipstart, opstart, cbIn);
			else if (inFormat == Consts.GUID_WICPixelFormat32bppBGR || inFormat == Consts.GUID_WICPixelFormat32bppBGRA || inFormat == Consts.GUID_WICPixelFormat32bppPBGRA)
				bgrxToGreyByte(ipstart, opstart, cbIn);
			else if (inFormat == PixelFormat.Pbgra64BppLinearUQ15.FormatGuid)
				bgrxToGreyUQ15(ipstart, opstart, cbIn);
			else if (inFormat == PixelFormat.Bgr96BppFloat.FormatGuid)
				bgrToGreyFloat(ipstart, opstart, cbIn, false);
			else if (inFormat == PixelFormat.Bgrx128BppFloat.FormatGuid || inFormat == PixelFormat.Pbgra128BppFloat.FormatGuid)
				bgrxToGreyFloat(ipstart, opstart, cbIn, false);
			else if (inFormat == PixelFormat.Bgr96BppLinearFloat.FormatGuid)
			{
				bgrToGreyFloat(ipstart, opstart, cbIn, true);
				greyLinearToGreyFloat(opstart, opstart, cbOut);
			}
			else if (inFormat == PixelFormat.Bgrx128BppLinearFloat.FormatGuid || inFormat == PixelFormat.Pbgra128BppLinearFloat.FormatGuid)
			{
				bgrxToGreyFloat(ipstart, opstart, cbIn, true);
				greyLinearToGreyFloat(opstart, opstart, cbOut);
			}
			else
				throw new NotSupportedException("Unsupported pixel format");
		}

		unsafe private static void bgrToGreyByte(byte* ipstart, byte* opstart, int cb)
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

		unsafe private static void bgrToGreyUQ15(byte* ipstart, byte* opstart, int cb)
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

		unsafe private static void bgrToGreyFloat(byte* ipstart, byte* opstart, int cb, bool linear)
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

		unsafe private static void bgrxToGreyByte(byte* ipstart, byte* opstart, int cb)
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

		unsafe private static void bgrxToGreyUQ15(byte* ipstart, byte* opstart, int cb)
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

		unsafe private static void bgrxToGreyFloat(byte* ipstart, byte* opstart, int cb, bool linear)
		{
			float* ip = (float*)ipstart, ipe = (float*)(ipstart + cb) - 4, op = (float*)opstart;
			var clum = new Vector4(linear ? Rec709.Coefficients : Rec601.Coefficients, 0f);

			while (ip <= ipe)
			{
				*op++ = Vector4.Dot(Unsafe.ReadUnaligned<Vector4>(ip), clum);
				ip += 4;
			}
		}

		unsafe private static void greyLinearToGreyUQ15(byte* ipstart, byte* opstart, int cb)
		{
			fixed (byte* gtstart = &LookupTables.SrgbGamma[0])
			{
				ushort* ip = (ushort*)ipstart, ipe = (ushort*)(ipstart + cb), op = (ushort*)opstart;
				byte* gt = gtstart;

				while (ip < ipe)
					*op++ = gt[ClampToUQ15One(*ip++)];
			}
		}

		unsafe private static void greyLinearToGreyFloat(byte* ipstart, byte* opstart, int cb)
		{
			float* ip = (float*)ipstart, ipe = (float*)(ipstart + cb) - VectorF.Count, op = (float*)opstart;
			var vmin = Vector<float>.Zero;
			float fmin = vmin[0];

			while (ip <= ipe)
			{
				var v = Unsafe.ReadUnaligned<VectorF>(ip);
				Unsafe.WriteUnaligned(op, Vector.SquareRoot(Vector.Max(v, vmin)));

				ip += VectorF.Count;
				op += VectorF.Count;
			}

			ipe += VectorF.Count;
			while (ip < ipe)
				*op++ = MaxF(*ip++, fmin).Sqrt();
		}
	}
}
