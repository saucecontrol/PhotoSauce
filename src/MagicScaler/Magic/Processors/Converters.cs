using System;

#if HWINTRINSICS
using System.Runtime.Intrinsics;
using System.Runtime.Intrinsics.X86;
using System.Runtime.CompilerServices;
#endif

using static PhotoSauce.MagicScaler.MathUtil;

namespace PhotoSauce.MagicScaler
{
	internal enum ConverterDirection { ToLinear, FromLinear }

	internal interface IConversionProcessor
	{
		unsafe void ConvertLine(byte* istart, byte* ostart, int cb);
	}

	internal interface IConversionProcessor<TFrom, TTo> : IConversionProcessor where TFrom : unmanaged where TTo : unmanaged { }

	internal interface IConverter { }

	internal interface IConverter<TFrom, TTo> : IConverter where TFrom : unmanaged where TTo : unmanaged
	{
		public IConversionProcessor<TFrom, TTo> Processor { get; }
		public IConversionProcessor<TFrom, TTo> Processor3A { get; }
		public IConversionProcessor<TFrom, TTo> Processor3X { get; }
	}

	internal sealed class NarrowingConverter : IConversionProcessor<ushort, byte>
	{
		public static readonly NarrowingConverter Instance = new NarrowingConverter();

		private NarrowingConverter() { }

#if HWINTRINSICS
		[MethodImpl(MethodImplOptions.AggressiveOptimization)]
#endif
		unsafe void IConversionProcessor.ConvertLine(byte* istart, byte* ostart, int cb)
		{
			ushort* ip = (ushort*)istart, ipe = (ushort*)(istart + cb);
			byte* op = ostart;

#if HWINTRINSICS
			if (Avx2.IsSupported)
			{
				ipe -= Vector256<byte>.Count;
				while (ip <= ipe)
				{
					var vs0 = Avx.LoadVector256(ip);
					var vs1 = Avx.LoadVector256(ip + Vector256<ushort>.Count);
					ip += Vector256<byte>.Count;

					vs0 = Avx2.ShiftRightLogical(vs0, 8);
					vs1 = Avx2.ShiftRightLogical(vs1, 8);

					var vb0 = Avx2.PackUnsignedSaturate(vs0.AsInt16(), vs1.AsInt16());
					vb0 = Avx2.Permute4x64(vb0.AsInt64(), HWIntrinsics.PermuteMaskDeinterleave4x64).AsByte();

					Avx.Store(op, vb0);
					op += Vector256<byte>.Count;
				}
				ipe += Vector256<byte>.Count;
			}
			else if (Sse2.IsSupported)
			{
				ipe -= Vector128<byte>.Count;
				while (ip <= ipe)
				{
					var vs0 = Sse2.LoadVector128(ip);
					var vs1 = Sse2.LoadVector128(ip + Vector128<ushort>.Count);
					ip += Vector128<byte>.Count;

					vs0 = Sse2.ShiftRightLogical(vs0, 8);
					vs1 = Sse2.ShiftRightLogical(vs1, 8);

					var vb0 = Sse2.PackUnsignedSaturate(vs0.AsInt16(), vs1.AsInt16());

					Sse2.Store(op, vb0);
					op += Vector128<byte>.Count;
				}
				ipe += Vector128<byte>.Count;
			}
#endif

			while (ip < ipe)
			{
				byte o0 = (byte)(ip[0] >> 8);
				byte o1 = (byte)(ip[1] >> 8);
				byte o2 = (byte)(ip[2] >> 8);
				byte o3 = (byte)(ip[3] >> 8);
				ip += 4;

				op[0] = o0;
				op[1] = o1;
				op[2] = o2;
				op[3] = o3;
				op += 4;
			}
		}
	}

	internal sealed class VideoLevelsConverter : IConversionProcessor<byte, byte>
	{
		public static readonly VideoLevelsConverter Instance = new VideoLevelsConverter();

		private VideoLevelsConverter() { }

		unsafe void IConversionProcessor.ConvertLine(byte* istart, byte* ostart, int cb)
		{
			byte* ip = istart, ipe = istart + cb - 4;
			byte* op = ostart;

			while (ip <= ipe)
			{
				byte o0 = ScaleFromVideoLevels(ip[0]);
				byte o1 = ScaleFromVideoLevels(ip[1]);
				byte o2 = ScaleFromVideoLevels(ip[2]);
				byte o3 = ScaleFromVideoLevels(ip[3]);
				ip += 4;

				op[0] = o0;
				op[1] = o1;
				op[2] = o2;
				op[3] = o3;
				op += 4;
			}
			ipe += 4;

			while (ip < ipe)
			{
				op[0] = ScaleFromVideoLevels(ip[0]);
				ip++;
				op++;
			}
		}
	}

	internal sealed class NoopConverter : IConverter<float, float>
	{
		public static readonly NoopConverter Instance = new NoopConverter();

		private static readonly NoopProcessor processor = new NoopProcessor();

		private NoopConverter() { }

		public IConversionProcessor<float, float> Processor => processor;
		public IConversionProcessor<float, float> Processor3A => processor;
		public IConversionProcessor<float, float> Processor3X => processor;

		private sealed class NoopProcessor : IConversionProcessor<float, float>
		{
			unsafe void IConversionProcessor.ConvertLine(byte* istart, byte* ostart, int cb) { }
		}
	}

	internal sealed class UQ15Converter : IConverter<ushort, byte>
	{
		public static readonly IConverter<ushort, byte> Instance = new UQ15Converter();

		private static readonly UQ15Processor processor = new UQ15Processor();
		private static readonly UQ15Processor3A processor3A = new UQ15Processor3A();

		private UQ15Converter() { }

		public IConversionProcessor<ushort, byte> Processor => processor;
		public IConversionProcessor<ushort, byte> Processor3A => processor3A;
		public IConversionProcessor<ushort, byte> Processor3X => throw new NotImplementedException();

		private sealed class UQ15Processor : IConversionProcessor<ushort, byte>
		{
			unsafe void IConversionProcessor.ConvertLine(byte* ipstart, byte* opstart, int cb)
			{
				ushort* ip = (ushort*)ipstart, ipe = (ushort*)(ipstart + cb) - 4;
				byte* op = opstart;

				while (ip <= ipe)
				{
					byte i0 = UnFix15ToByte((uint)ip[0] * byte.MaxValue);
					byte i1 = UnFix15ToByte((uint)ip[1] * byte.MaxValue);
					byte i2 = UnFix15ToByte((uint)ip[2] * byte.MaxValue);
					byte i3 = UnFix15ToByte((uint)ip[3] * byte.MaxValue);
					ip += 4;

					op[0] = i0;
					op[1] = i1;
					op[2] = i2;
					op[3] = i3;
					op += 4;
				}
				ipe += 4;

				while (ip < ipe)
				{
					op[0] = UnFix15ToByte((uint)ip[0] * byte.MaxValue);
					ip++;
					op++;
				}
			}
		}

		private sealed class UQ15Processor3A : IConversionProcessor<ushort, byte>
		{
			unsafe void IConversionProcessor.ConvertLine(byte* ipstart, byte* opstart, int cb)
			{
				ushort* ip = (ushort*)ipstart, ipe = (ushort*)(ipstart + cb);
				byte* op = opstart;

				while (ip < ipe)
				{
					uint i3 = ip[3];
					if (i3 < (UQ15Round >> 8))
					{
						*(uint*)op = 0;
					}
					else
					{
						uint o3i = UQ15One * byte.MaxValue / i3;
						uint i0 = ip[0];
						uint i1 = ip[1];
						uint i2 = ip[2];

						byte o0 = UnFix15ToByte(i0 * o3i);
						byte o1 = UnFix15ToByte(i1 * o3i);
						byte o2 = UnFix15ToByte(i2 * o3i);
						byte o3 = UnFix15ToByte(i3 * byte.MaxValue);
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
	}
}
