// Copyright © Clinton Ingram and Contributors.  Licensed under the MIT License.

using System;

#if HWINTRINSICS
using System.Runtime.Intrinsics;
using System.Runtime.Intrinsics.X86;
using System.Runtime.InteropServices;
using System.Runtime.CompilerServices;
#endif

namespace PhotoSauce.MagicScaler.Transforms
{
	internal static class ChannelChanger<T> where T : unmanaged
	{
		private static readonly T maxalpha = getOneValue();

		private static T getOneValue()
		{
			if (typeof(T) == typeof(float))
				return (T)(object)1.0f;
			if (typeof(T) == typeof(ushort))
				return (T)(object)MathUtil.UQ15One;
			if (typeof(T) == typeof(byte))
				return (T)(object)byte.MaxValue;

			throw new NotSupportedException(nameof(T) + " must be float, ushort, or byte");
		}

		public static IConversionProcessor<T, T> GetConverter(int chanIn, int chanOut)
		{
			if (chanIn == 1 && chanOut == 3)
				return Change1to3Chan.Instance;
			if (chanIn == 1 && chanOut == 4)
				return Change1to4Chan.Instance;
			else if (chanIn == 3 && chanOut == 1)
				return Change3to1Chan.Instance;
			else if (chanIn == 3 && chanOut == 4)
				return Change3to4Chan.Instance;
			else if (chanIn == 4 && chanOut == 1)
				return Change4to1Chan.Instance;
			else if (chanIn == 4 && chanOut == 3)
				return Change4to3Chan.Instance;

			throw new NotSupportedException("Unsupported pixel format");
		}

		private sealed class Change1to3Chan : IConversionProcessor<T, T>
		{
			public static readonly Change1to3Chan Instance = new();

			private Change1to3Chan() { }

			unsafe void IConversionProcessor.ConvertLine(byte* ipstart, byte* opstart, nint cb)
			{
				T* ip = (T*)ipstart, ipe = (T*)(ipstart + cb), op = (T*)opstart;

#if HWINTRINSICS
				if (typeof(T) == typeof(byte) && Ssse3.IsSupported && cb > Vector128<byte>.Count)
				{
					var mask = (ReadOnlySpan<byte>)(new byte[] {
						 0,  0,  0,  1,  1,  1,  2,  2,  2,  3,  3,  3,  4,  4,  4,  5,
						 5,  5,  6,  6,  6,  7,  7,  7,  8,  8,  8,  9,  9,  9, 10, 10,
						10, 11, 11, 11, 12, 12, 12, 13, 13, 13, 14, 14, 14, 15, 15, 15
					});
					byte* pmask = (byte*)Unsafe.AsPointer(ref MemoryMarshal.GetReference(mask));

					var vmask0 = Sse2.LoadVector128(pmask);
					var vmask1 = Sse2.LoadVector128(pmask + Vector128<byte>.Count);
					var vmask2 = Sse2.LoadVector128(pmask + Vector128<byte>.Count * 2);

					ipe -= Vector128<byte>.Count;
					do
					{
						var v0 = Sse2.LoadVector128((byte*)ip);
						ip += Vector128<byte>.Count;

						Sse2.Store((byte*)op, Ssse3.Shuffle(v0, vmask0));
						Sse2.Store((byte*)op + Vector128<byte>.Count, Ssse3.Shuffle(v0, vmask1));
						Sse2.Store((byte*)op + Vector128<byte>.Count * 2, Ssse3.Shuffle(v0, vmask2));
						op += Vector128<byte>.Count * 3;

					} while (ip <= ipe);
					ipe += Vector128<byte>.Count;
				}
#endif

				while (ip < ipe)
				{
					var i0 = *ip;
					op[0] = i0;
					op[1] = i0;
					op[2] = i0;

					ip++;
					op += 3;
				}
			}
		}

		private sealed class Change1to4Chan : IConversionProcessor<T, T>
		{
			public static readonly Change1to4Chan Instance = new();

			private Change1to4Chan() { }

			unsafe void IConversionProcessor.ConvertLine(byte* ipstart, byte* opstart, nint cb)
			{
				T* ip = (T*)ipstart, ipe = (T*)(ipstart + cb), op = (T*)opstart;
				var alpha = maxalpha;

#if HWINTRINSICS
				if (typeof(T) == typeof(byte) && Sse2.IsSupported && cb > Vector128<byte>.Count)
				{
					var vfill = Vector128.Create(byte.MaxValue);

					ipe -= Vector128<byte>.Count;
					do
					{
						var v0 = Sse2.LoadVector128((byte*)ip);
						ip += Vector128<byte>.Count;

						var vll = Sse2.UnpackLow(v0, v0);
						var vlh = Sse2.UnpackHigh(v0, v0);
						var vhl = Sse2.UnpackLow(v0, vfill);
						var vhh = Sse2.UnpackHigh(v0, vfill);

						Sse2.Store((byte*)op, Sse2.UnpackLow(vll, vhl));
						Sse2.Store((byte*)op + Vector128<byte>.Count, Sse2.UnpackHigh(vll, vhl));
						Sse2.Store((byte*)op + Vector128<byte>.Count * 2, Sse2.UnpackLow(vlh, vhh));
						Sse2.Store((byte*)op + Vector128<byte>.Count * 3, Sse2.UnpackHigh(vlh, vhh));
						op += Vector128<byte>.Count * 4;

					} while (ip <= ipe);
					ipe += Vector128<byte>.Count;
				}
#endif

				while (ip < ipe)
				{
					var i0 = *ip;
					op[0] = i0;
					op[1] = i0;
					op[2] = i0;
					op[3] = alpha;

					ip++;
					op += 4;
				}
			}
		}

		private sealed class Change3to1Chan : IConversionProcessor<T, T>
		{
			public static readonly Change3to1Chan Instance = new();

			private Change3to1Chan() { }

			unsafe void IConversionProcessor.ConvertLine(byte* ipstart, byte* opstart, nint cb)
			{
				T* ip = (T*)ipstart, ipe = (T*)(ipstart + cb), op = (T*)opstart;

#if HWINTRINSICS
				if (typeof(T) == typeof(byte) && Ssse3.IsSupported && cb > Vector128<byte>.Count * 3)
				{
					var mask = (ReadOnlySpan<byte>)(new byte[] {
						   0,    3,    6,    9,   12,  15,  0x80, 0x80, 0x80, 0x80, 0x80, 0x80, 0x80, 0x80, 0x80, 0x80,
						0x80, 0x80, 0x80, 0x80, 0x80, 0x80,    2,    5,    8,   11,   14, 0x80, 0x80, 0x80, 0x80, 0x80,
						0x80, 0x80, 0x80, 0x80, 0x80, 0x80, 0x80, 0x80, 0x80, 0x80, 0x80,    1,    4,    7,   10,   13
					});
					byte* pmask = (byte*)Unsafe.AsPointer(ref MemoryMarshal.GetReference(mask));

					var vmask0 = Sse2.LoadVector128(pmask);
					var vmask1 = Sse2.LoadVector128(pmask + Vector128<byte>.Count);
					var vmask2 = Sse2.LoadVector128(pmask + Vector128<byte>.Count * 2);

					ipe -= Vector128<byte>.Count * 3;
					do
					{
						var v0 = Sse2.LoadVector128((byte*)ip);
						var v1 = Sse2.LoadVector128((byte*)ip + Vector128<byte>.Count);
						var v2 = Sse2.LoadVector128((byte*)ip + Vector128<byte>.Count * 2);
						ip += Vector128<byte>.Count * 3;

						v0 = Sse2.Or(Sse2.Or(Ssse3.Shuffle(v0, vmask0), Ssse3.Shuffle(v1, vmask1)), Ssse3.Shuffle(v2, vmask2));

						Sse2.Store((byte*)op, v0);
						op += Vector128<byte>.Count;

					} while (ip <= ipe);
					ipe += Vector128<byte>.Count * 3;
				}
#endif

				ipe -= 3;
				while (ip <= ipe)
				{
					op[0] = ip[0];

					ip += 3;
					op++;
				}
			}
		}

		private sealed class Change3to4Chan : IConversionProcessor<T, T>
		{
			public static readonly Change3to4Chan Instance = new();

			private Change3to4Chan() { }

			unsafe void IConversionProcessor.ConvertLine(byte* ipstart, byte* opstart, nint cb)
			{
				T* ip = (T*)ipstart, ipe = (T*)(ipstart + cb), op = (T*)opstart;
				var alpha = maxalpha;

#if HWINTRINSICS
				if (typeof(T) == typeof(byte) && Ssse3.IsSupported && cb > Vector128<byte>.Count * 3)
				{
					var vmask = Sse2.LoadVector128((byte*)Unsafe.AsPointer(ref MemoryMarshal.GetReference(HWIntrinsics.ShuffleMask3To3xChan)));
					var vfill = Vector128.Create(0xff000000u).AsByte();

					ipe -= Vector128<byte>.Count * 3;
					do
					{
						var v0 = Sse2.LoadVector128((byte*)ip);
						var v1 = Sse2.LoadVector128((byte*)ip + Vector128<byte>.Count);
						var v2 = Sse2.LoadVector128((byte*)ip + Vector128<byte>.Count * 2);
						var v3 = Sse2.ShiftRightLogical128BitLane(v2, 4);
						ip += Vector128<byte>.Count * 3;

						v2 = Ssse3.AlignRight(v2, v1, 8);
						v1 = Ssse3.AlignRight(v1, v0, 12);

						v0 = Sse2.Or(Ssse3.Shuffle(v0, vmask), vfill);
						v1 = Sse2.Or(Ssse3.Shuffle(v1, vmask), vfill);
						v2 = Sse2.Or(Ssse3.Shuffle(v2, vmask), vfill);
						v3 = Sse2.Or(Ssse3.Shuffle(v3, vmask), vfill);

						Sse2.Store((byte*)op, v0);
						Sse2.Store((byte*)op + Vector128<byte>.Count, v1);
						Sse2.Store((byte*)op + Vector128<byte>.Count * 2, v2);
						Sse2.Store((byte*)op + Vector128<byte>.Count * 3, v3);
						op += Vector128<byte>.Count * 4;

					} while (ip <= ipe);
					ipe += Vector128<byte>.Count * 3;
				}
#endif

				ipe -= 3;
				while (ip <= ipe)
				{
					op[0] = ip[0];
					op[1] = ip[1];
					op[2] = ip[2];
					op[3] = alpha;

					ip += 3;
					op += 4;
				}
			}
		}

		private sealed class Change4to1Chan : IConversionProcessor<T, T>
		{
			public static readonly Change4to1Chan Instance = new();

			private Change4to1Chan() { }

			unsafe void IConversionProcessor.ConvertLine(byte* ipstart, byte* opstart, nint cb)
			{
				T* ip = (T*)ipstart, ipe = (T*)(ipstart + cb), op = (T*)opstart;

#if HWINTRINSICS
				if (typeof(T) == typeof(byte) && Ssse3.IsSupported && cb > Vector128<byte>.Count * 4)
				{
					var mask = (ReadOnlySpan<byte>)(new byte[] { 0, 4, 8, 12, 0x80, 0x80, 0x80, 0x80, 0x80, 0x80, 0x80, 0x80, 0x80, 0x80, 0x80, 0x80 });
					var vmask = Sse2.LoadVector128((byte*)Unsafe.AsPointer(ref MemoryMarshal.GetReference(mask)));

					ipe -= Vector128<byte>.Count * 4;
					do
					{
						var v0 = Sse2.LoadVector128((byte*)ip);
						var v1 = Sse2.LoadVector128((byte*)ip + Vector128<byte>.Count);
						var v2 = Sse2.LoadVector128((byte*)ip + Vector128<byte>.Count * 2);
						var v3 = Sse2.LoadVector128((byte*)ip + Vector128<byte>.Count * 3);
						ip += Vector128<byte>.Count * 4;

						v0 = Ssse3.Shuffle(v0, vmask);
						v1 = Ssse3.Shuffle(v1, vmask);
						v2 = Ssse3.Shuffle(v2, vmask);
						v3 = Ssse3.Shuffle(v3, vmask);

						var vl = Sse2.UnpackLow(v0.AsUInt32(), v1.AsUInt32()).AsUInt64();
						var vh = Sse2.UnpackLow(v2.AsUInt32(), v3.AsUInt32()).AsUInt64();

						Sse2.Store((byte*)op, Sse2.UnpackLow(vl, vh).AsByte());
						op += Vector128<byte>.Count;

					} while (ip <= ipe);
					ipe += Vector128<byte>.Count * 4;
				}
#endif

				ipe -= 4;
				while (ip <= ipe)
				{
					op[0] = ip[0];

					ip += 4;
					op++;
				}
			}
		}

		private sealed class Change4to3Chan : IConversionProcessor<T, T>
		{
			public static readonly Change4to3Chan Instance = new();

			private Change4to3Chan() { }

			unsafe void IConversionProcessor.ConvertLine(byte* ipstart, byte* opstart, nint cb)
			{
				T* ip = (T*)ipstart, ipe = (T*)(ipstart + cb), op = (T*)opstart;

#if HWINTRINSICS
				if (typeof(T) == typeof(byte) && Ssse3.IsSupported && cb > Vector128<byte>.Count * 4)
				{
					var vmasko = Sse2.LoadVector128((byte*)Unsafe.AsPointer(ref MemoryMarshal.GetReference(HWIntrinsics.ShuffleMask3xTo3Chan)));
					var vmaske = Ssse3.AlignRight(vmasko, vmasko, 12).AsByte();

					ipe -= Vector128<byte>.Count * 4;
					do
					{
						var v0 = Sse2.LoadVector128((byte*)ip);
						var v1 = Sse2.LoadVector128((byte*)ip + Vector128<byte>.Count);
						var v2 = Sse2.LoadVector128((byte*)ip + Vector128<byte>.Count * 2);
						var v3 = Sse2.LoadVector128((byte*)ip + Vector128<byte>.Count * 3);
						ip += Vector128<byte>.Count * 4;

						v0 = Ssse3.Shuffle(v0, vmaske);
						v1 = Ssse3.Shuffle(v1, vmasko);
						v2 = Ssse3.Shuffle(v2, vmaske);
						v3 = Ssse3.Shuffle(v3, vmasko);

						v0 = Ssse3.AlignRight(v1, v0, 4);
						v1 = Sse2.Or(Sse2.ShiftRightLogical128BitLane(v1, 4), Sse2.ShiftLeftLogical128BitLane(v2, 4));
						v2 = Ssse3.AlignRight(v3, v2, 12);

						Sse2.Store((byte*)op, v0);
						Sse2.Store((byte*)op + Vector128<byte>.Count, v1);
						Sse2.Store((byte*)op + Vector128<byte>.Count * 2, v2);
						op += Vector128<byte>.Count * 3;

					} while (ip <= ipe);
					ipe += Vector128<byte>.Count * 4;
				}
#endif

				ipe -= 4;
				while (ip <= ipe)
				{
					op[0] = ip[0];
					op[1] = ip[1];
					op[2] = ip[2];

					ip += 4;
					op += 3;
				}
			}
		}
	}
}
