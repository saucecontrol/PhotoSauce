// Copyright © Clinton Ingram and Contributors.  Licensed under the MIT License.

using System;
using System.Numerics;
using System.Runtime.CompilerServices;

#if HWINTRINSICS
using System.Runtime.Intrinsics;
using System.Runtime.Intrinsics.X86;
#endif

using static PhotoSauce.MagicScaler.MathUtil;

using VectorF = System.Numerics.Vector<float>;

namespace PhotoSauce.MagicScaler
{
	internal static class Rec601Luma
	{
		public const double R = 0.299;
		public const double B = 0.114;
		public const double G = 1 - R - B;
		public static readonly Vector3 Coefficients = new((float)B, (float)G, (float)R);

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static byte FromBgr(byte b, byte g, byte r)
		{
			const uint rY = (ushort)(R * UQ15One + 0.5);
			const uint gY = (ushort)(G * UQ15One + 0.5);
			const uint bY = (ushort)(B * UQ15One + 0.5);

			return UnFix15ToByte(r * rY + g * gY + b * bY);
		}
	}

	internal static class Rec709Luma
	{
		public const double R = 0.2126;
		public const double B = 0.0722;
		public const double G = 1 - R - B;
		public static readonly Vector3 Coefficients = new((float)B, (float)G, (float)R);

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static ushort FromBgr(ushort b, ushort g, ushort r)
		{
			const uint rY = (ushort)(R * UQ15One + 0.5);
			const uint gY = (ushort)(G * UQ15One + 0.5);
			const uint bY = (ushort)(B * UQ15One + 0.5);

			return UnFixToUQ15One(r * rY + g * gY + b * bY);
		}
	}

	internal static unsafe class GreyConverter
	{
		public static void ConvertLine(PixelFormat inFormat, byte* ipstart, byte* opstart, nint cb)
		{
			if (inFormat == PixelFormat.Grey32FloatLinear || inFormat == PixelFormat.Y32FloatLinear)
				greyLinearToGreyFloat(ipstart, opstart, cb);
			else if (inFormat == PixelFormat.Grey16UQ15Linear || inFormat == PixelFormat.Y16UQ15Linear)
				greyLinearToGreyUQ15(ipstart, opstart, cb);
			else if (inFormat == PixelFormat.Bgr24)
				bgrToGreyByte(ipstart, opstart, cb);
			else if (inFormat == PixelFormat.Bgr48UQ15Linear)
				bgrToGreyUQ15(ipstart, opstart, cb);
			else if (inFormat == PixelFormat.Bgrx32 || inFormat == PixelFormat.Bgra32 || inFormat == PixelFormat.Pbgra32)
				bgrxToGreyByte(ipstart, opstart, cb);
			else if (inFormat == PixelFormat.Pbgra64UQ15Linear)
				bgrxToGreyUQ15(ipstart, opstart, cb);
			else if (inFormat == PixelFormat.Bgr96Float)
				bgrToGreyFloat(ipstart, opstart, cb, false);
			else if (inFormat == PixelFormat.Bgrx128Float || inFormat == PixelFormat.Pbgra128Float)
				bgrxToGreyFloat(ipstart, opstart, cb, false);
			else if (inFormat == PixelFormat.Bgr96FloatLinear)
				bgrToGreyFloat(ipstart, opstart, cb, true);
			else if (inFormat == PixelFormat.Bgrx128FloatLinear || inFormat == PixelFormat.Pbgra128FloatLinear)
				bgrxToGreyFloat(ipstart, opstart, cb, true);
			else
				throw new NotSupportedException("Unsupported pixel format");
		}

		private static void bgrToGreyByte(byte* ipstart, byte* opstart, nint cb)
		{
			byte* ip = ipstart, ipe = ipstart + cb - 3, op = opstart;

			while (ip <= ipe)
			{
				byte y = Rec601Luma.FromBgr(ip[0], ip[1], ip[2]);
				op[0] = y;

				ip += 3;
				op++;
			}
		}

		private static void bgrToGreyUQ15(byte* ipstart, byte* opstart, nint cb)
		{
			fixed (byte* gtstart = &LookupTables.SrgbGammaUQ15[0])
			{
				ushort* ip = (ushort*)ipstart, ipe = (ushort*)(ipstart + cb) - 3, op = (ushort*)opstart;
				byte* gt = gtstart;

				while (ip <= ipe)
				{
					uint y = Rec709Luma.FromBgr(ip[0], ip[1], ip[2]);
					op[0] = gt[y];

					ip += 3;
					op++;
				}
			}
		}

		private static void bgrToGreyFloat(byte* ipstart, byte* opstart, nint cb, bool linear)
		{
			float* ip = (float*)ipstart, ipe = (float*)(ipstart + cb) - 3, op = (float*)opstart;
			var clum = linear ? Rec709Luma.Coefficients : Rec601Luma.Coefficients;
			var vzero = Vector4.Zero;
			float cbl = clum.X, cgl = clum.Y, crl = clum.Z;

			while (ip <= ipe)
			{
				float c0 = ip[0] * cbl, c1 = ip[1] * cgl, c2 = ip[2] * crl;
				ip += 3;

				float f0 = c0 + c1 + c2;

				if (linear)
				{
					var vt = default(Vector4);
					vt.X = f0;
					f0 = Vector4.SquareRoot(Vector4.Max(vt, vzero)).X;
				}

				*op++ = f0;
			}
		}

		private static void bgrxToGreyByte(byte* ipstart, byte* opstart, nint cb)
		{
			byte* ip = ipstart, ipe = ipstart + cb - 4, op = opstart;

			while (ip <= ipe)
			{
				byte y = Rec601Luma.FromBgr(ip[0], ip[1], ip[2]);
				op[0] = y;

				ip += 4;
				op++;
			}
		}

		private static void bgrxToGreyUQ15(byte* ipstart, byte* opstart, nint cb)
		{
			fixed (byte* gtstart = &LookupTables.SrgbGammaUQ15[0])
			{
				ushort* ip = (ushort*)ipstart, ipe = (ushort*)(ipstart + cb) - 4, op = (ushort*)opstart;
				byte* gt = gtstart;

				while (ip <= ipe)
				{
					uint y = Rec709Luma.FromBgr(ip[0], ip[1], ip[2]);
					op[0] = gt[y];

					ip += 4;
					op++;
				}
			}
		}

		private static void bgrxToGreyFloat(byte* ipstart, byte* opstart, nint cb, bool linear)
		{
			float* ip = (float*)ipstart, ipe = (float*)(ipstart + cb), op = (float*)opstart;
			var clum = linear ? Rec709Luma.Coefficients : Rec601Luma.Coefficients;

#if HWINTRINSICS
			if (Avx.IsSupported && cb >= Vector256<byte>.Count * 4)
			{
				var vcbl = Vector256.Create(clum.X);
				var vcgl = Vector256.Create(clum.Y);
				var vcrl = Vector256.Create(clum.Z);
				var vzero = Vector256<float>.Zero;
				var vmaskp = Avx.LoadVector256((int*)HWIntrinsics.PermuteMaskDeinterleave8x32.GetAddressOf());
				ipe -= Vector256<float>.Count * 4;

				LoopTop:
				do
				{
					var v0 = Avx.LoadVector256(ip);
					var v1 = Avx.LoadVector256(ip + Vector256<float>.Count);
					var v2 = Avx.LoadVector256(ip + Vector256<float>.Count * 2);
					var v3 = Avx.LoadVector256(ip + Vector256<float>.Count * 3);
					ip += Vector256<float>.Count * 4;

					var vl0 = Avx.UnpackLow(v0, v1).AsDouble();
					var vh0 = Avx.UnpackHigh(v0, v1).AsDouble();
					var vl1 = Avx.UnpackLow(v2, v3).AsDouble();
					var vh1 = Avx.UnpackHigh(v2, v3).AsDouble();

					var vb = Avx.UnpackLow(vl0, vl1).AsSingle();
					var vg = Avx.UnpackHigh(vl0, vl1).AsSingle();
					var vr = Avx.UnpackLow(vh0, vh1).AsSingle();

					vb = Avx.Multiply(vb, vcbl);
					vg = Avx.Multiply(vg, vcgl);
					vb = HWIntrinsics.MultiplyAdd(vb, vr, vcrl);
					vb = Avx2.PermuteVar8x32(Avx.Add(vb, vg), vmaskp);

					if (linear)
						vb = Avx.Sqrt(Avx.Max(vb, vzero));

					Avx.Store(op, vb);
					op += Vector256<float>.Count;
				}
				while (ip <= ipe);

				if (ip < ipe + Vector256<float>.Count * 4)
				{
					nint offs = UnsafeUtil.ByteOffset(ipe, ip);
					ip = UnsafeUtil.SubtractOffset(ip, offs);
					op = UnsafeUtil.SubtractOffset(op, offs / 4);
					goto LoopTop;
				}

				return;
			}
#endif

			var vzero4 = Vector4.Zero;
			var vlum = new Vector4(clum, vzero4.X);

			while (ip < ipe)
			{
				float f0 = Vector4.Dot(Unsafe.ReadUnaligned<Vector4>(ip), vlum);
				ip += 4;

				if (linear)
				{
					var vt = default(Vector4);
					vt.X = f0;
					f0 = Vector4.SquareRoot(Vector4.Max(vt, vzero4)).X;
				}

				*op++ = f0;
			}
		}

		private static void greyLinearToGreyUQ15(byte* ipstart, byte* opstart, nint cb)
		{
			fixed (byte* gtstart = &LookupTables.SrgbGammaUQ15[0])
			{
				ushort* ip = (ushort*)ipstart, ipe = (ushort*)(ipstart + cb), op = (ushort*)opstart;
				byte* gt = gtstart;

				while (ip < ipe)
					*op++ = gt[(nuint)ClampToUQ15One((uint)*ip++)];
			}
		}

#if HWINTRINSICS
		[MethodImpl(MethodImplOptions.AggressiveOptimization)]
#endif
		private static void greyLinearToGreyFloat(byte* ipstart, byte* opstart, nint cb)
		{
			float* ip = (float*)ipstart, ipe = (float*)(ipstart + cb), op = (float*)opstart;

#if HWINTRINSICS
			if (Avx.IsSupported)
			{
				var vzero = Vector256<float>.Zero;

				ipe -= Vector256<float>.Count;
				while (ip <= ipe)
				{
					var v = Avx.Max(vzero, Avx.LoadVector256(ip));
					ip += Vector256<float>.Count;

					v = Avx.Sqrt(v);

					Avx.Store(op, v);
					op += Vector256<float>.Count;
				}
				ipe += Vector256<float>.Count;
			}
			else
#endif
			{
				var vzero = VectorF.Zero;

				ipe -= VectorF.Count;
				while (ip <= ipe)
				{
					var v = Unsafe.ReadUnaligned<VectorF>(ip);
					ip += VectorF.Count;

					v = Vector.SquareRoot(Vector.Max(v, vzero));

					Unsafe.WriteUnaligned(op, v);
					op += VectorF.Count;
				}
				ipe += VectorF.Count;
			}

			float fmin = Vector4.Zero.X;
			while (ip < ipe)
				*op++ = FastMax(*ip++, fmin).Sqrt();
		}
	}
}
