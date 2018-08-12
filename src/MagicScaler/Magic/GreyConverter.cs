using System.Numerics;
using System.Runtime.CompilerServices;

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

	internal static class GreyConverter
	{
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
	}
}
