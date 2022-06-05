// Copyright © Clinton Ingram and Contributors.  Licensed under the MIT License.

using System;
using System.Numerics;
using System.Collections.Generic;
using System.Runtime.CompilerServices;

#if HWINTRINSICS
using System.Runtime.Intrinsics;
using System.Runtime.Intrinsics.X86;
#endif

using static PhotoSauce.MagicScaler.MathUtil;

using VectorF = System.Numerics.Vector<float>;

namespace PhotoSauce.MagicScaler.Converters
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
		private static readonly Dictionary<PixelFormat, IConversionProcessor> processorMap = new() {
			[PixelFormat.Bgr24              ] = Processor3.Instance,
			[PixelFormat.Bgra32             ] = Processor3X.Instance,
			[PixelFormat.Bgrx32             ] = Processor3X.Instance,
			[PixelFormat.Pbgra32            ] = Processor3X.Instance,
			[PixelFormat.Bgr48UQ15Linear    ] = UQ15Processor3.Instance,
			[PixelFormat.Pbgra64UQ15Linear  ] = UQ15Processor3X.Instance,
			[PixelFormat.Grey16UQ15Linear   ] = UQ15Processor.Instance,
			[PixelFormat.Y16UQ15Linear      ] = UQ15Processor.Instance,
			[PixelFormat.Grey32FloatLinear  ] = FloatProcessor.Instance,
			[PixelFormat.Y32FloatLinear     ] = FloatProcessor.Instance,
			[PixelFormat.Bgr96Float         ] = FloatProcessor3<EncodingType.Companded>.Instance,
			[PixelFormat.Bgr96FloatLinear   ] = FloatProcessor3<EncodingType.Linear>.Instance,
			[PixelFormat.Pbgra128Float      ] = FloatProcessor3X<EncodingType.Companded>.Instance,
			[PixelFormat.Pbgra128FloatLinear] = FloatProcessor3X<EncodingType.Linear>.Instance,
			[PixelFormat.Bgrx128Float       ] = FloatProcessor3X<EncodingType.Companded>.Instance,
			[PixelFormat.Bgrx128FloatLinear ] = FloatProcessor3X<EncodingType.Linear>.Instance,
		};

		public static IConversionProcessor GetProcessor(PixelFormat format)
		{
			if (processorMap.TryGetValue(format, out var processor))
				return processor;

			throw new NotSupportedException("Unsupported pixel format");
		}

		private sealed class Processor3 : IConversionProcessor<byte, byte>
		{
			private const int channels = 3;

			public static readonly Processor3 Instance = new();

			void IConversionProcessor.ConvertLine(byte* istart, byte* ostart, nint cb)
			{
				byte* ip = istart, ipe = istart + cb - channels, op = ostart;

				while (ip <= ipe)
				{
					byte y = Rec601Luma.FromBgr(ip[0], ip[1], ip[2]);
					op[0] = y;

					ip += channels;
					op++;
				}
			}
		}

		private sealed class UQ15Processor3 : IConversionProcessor<ushort, ushort>
		{
			private const int channels = 3;

			public static readonly UQ15Processor3 Instance = new();

			void IConversionProcessor.ConvertLine(byte* istart, byte* ostart, nint cb)
			{
				fixed (byte* gtstart = &LookupTables.SrgbGammaUQ15.GetDataRef())
				{
					ushort* ip = (ushort*)istart, ipe = (ushort*)(istart + cb) - channels, op = (ushort*)ostart;
					byte* gt = gtstart;

					while (ip <= ipe)
					{
						uint y = Rec709Luma.FromBgr(ip[0], ip[1], ip[2]);
						op[0] = gt[y];

						ip += channels;
						op++;
					}
				}
			}
		}

		private sealed class FloatProcessor3<TEnc> : IConversionProcessor<float, float> where TEnc : struct, EncodingType
		{
			private const int channels = 3;

			public static readonly FloatProcessor3<TEnc> Instance = new();

			void IConversionProcessor.ConvertLine(byte* istart, byte* ostart, nint cb)
			{
				bool linear = typeof(TEnc) == typeof(EncodingType.Linear);

				float* ip = (float*)istart, ipe = (float*)(istart + cb) - channels, op = (float*)ostart;
				var clum = linear ? Rec709Luma.Coefficients : Rec601Luma.Coefficients;
				var vzero = Vector4.Zero;
				float cbl = clum.X, cgl = clum.Y, crl = clum.Z;

				while (ip <= ipe)
				{
					float c0 = ip[0] * cbl, c1 = ip[1] * cgl, c2 = ip[2] * crl;
					ip += channels;

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
		}

		private sealed class Processor3X : IConversionProcessor<byte, byte>
		{
			private const int channels = 4;

			public static readonly Processor3X Instance = new();

			void IConversionProcessor.ConvertLine(byte* istart, byte* ostart, nint cb)
			{
				byte* ip = istart, ipe = istart + cb - channels, op = ostart;

				while (ip <= ipe)
				{
					byte y = Rec601Luma.FromBgr(ip[0], ip[1], ip[2]);
					op[0] = y;

					ip += channels;
					op++;
				}
			}
		}

		private sealed class UQ15Processor3X : IConversionProcessor<ushort, ushort>
		{
			private const int channels = 4;

			public static readonly UQ15Processor3X Instance = new();

			void IConversionProcessor.ConvertLine(byte* istart, byte* ostart, nint cb)
			{
				fixed (byte* gtstart = &LookupTables.SrgbGammaUQ15.GetDataRef())
				{
					ushort* ip = (ushort*)istart, ipe = (ushort*)(istart + cb) - channels, op = (ushort*)ostart;
					byte* gt = gtstart;

					while (ip <= ipe)
					{
						uint y = Rec709Luma.FromBgr(ip[0], ip[1], ip[2]);
						op[0] = gt[y];

						ip += channels;
						op++;
					}
				}
			}
		}

		private sealed class FloatProcessor3X<TEnc> : IConversionProcessor<float, float> where TEnc : struct, EncodingType
		{
			private const int channels = 4;

			public static readonly FloatProcessor3X<TEnc> Instance = new();

			void IConversionProcessor.ConvertLine(byte* istart, byte* ostart, nint cb)
			{
				bool linear = typeof(TEnc) == typeof(EncodingType.Linear);

				float* ip = (float*)istart, ipe = (float*)(istart + cb), op = (float*)ostart;
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

						var vl0 = Avx.UnpackLow (v0, v1).AsDouble();
						var vh0 = Avx.UnpackHigh(v0, v1).AsDouble();
						var vl1 = Avx.UnpackLow (v2, v3).AsDouble();
						var vh1 = Avx.UnpackHigh(v2, v3).AsDouble();

						var vb = Avx.UnpackLow (vl0, vl1).AsSingle();
						var vg = Avx.UnpackHigh(vl0, vl1).AsSingle();
						var vr = Avx.UnpackLow (vh0, vh1).AsSingle();

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
						nuint offs = UnsafeUtil.ByteOffset(ipe, ip);
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
					ip += channels;

					if (linear)
					{
						var vt = default(Vector4);
						vt.X = f0;
						f0 = Vector4.SquareRoot(Vector4.Max(vt, vzero4)).X;
					}

					*op++ = f0;
				}
			}
		}

		private sealed class UQ15Processor : IConversionProcessor<ushort, ushort>
		{
			public static readonly UQ15Processor Instance = new();

			void IConversionProcessor.ConvertLine(byte* istart, byte* ostart, nint cb)
			{
				fixed (byte* gtstart = &LookupTables.SrgbGammaUQ15.GetDataRef())
				{
					ushort* ip = (ushort*)istart, ipe = (ushort*)(istart + cb), op = (ushort*)ostart;
					byte* gt = gtstart;

					while (ip < ipe)
						*op++ = gt[(nuint)ClampToUQ15One((uint)*ip++)];
				}
			}
		}

		private sealed class FloatProcessor : IConversionProcessor<float, float>
		{
			public static readonly FloatProcessor Instance = new();

#if HWINTRINSICS
			[MethodImpl(MethodImplOptions.AggressiveOptimization)]
#endif
			void IConversionProcessor.ConvertLine(byte* istart, byte* ostart, nint cb)
			{
				float* ip = (float*)istart, ipe = (float*)(istart + cb), op = (float*)ostart;

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
}
