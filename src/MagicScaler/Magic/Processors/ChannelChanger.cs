// Copyright © Clinton Ingram and Contributors.  Licensed under the MIT License.

using System;

#if HWINTRINSICS
using System.Runtime.Intrinsics;
using System.Runtime.Intrinsics.X86;
using System.Runtime.CompilerServices;
#endif

namespace PhotoSauce.MagicScaler.Converters;

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

		throw new NotSupportedException($"{nameof(T)} must be float, ushort, or byte");
	}

	public static IConversionProcessor<T, T> AlphaExtractor => Change4to1Chan.InstanceAlpha;

	public static IConversionProcessor<T, T> GetConverter(int chanIn, int chanOut) =>
		(chanIn, chanOut) switch {
			(1, 3) => Change1to3Chan.Instance,
			(1, 4) => Change1to4Chan.Instance,
			(3, 1) => Change3to1Chan.Instance,
			(3, 4) => Change3to4Chan.Instance,
			(4, 1) => Change4to1Chan.Instance,
			(4, 3) => Change4to3Chan.Instance,
			_      => throw new NotSupportedException("Unsupported pixel format")
		};

	public static IConversionProcessor<T, T> GetSwapConverter(int chanIn, int chanOut) =>
		(chanIn, chanOut) switch {
			(3, 4) => Swap3to4Chan.Instance,
			(4, 3) => Swap4to3Chan.Instance,
			(3, 3) => Swap3Chan.Instance,
			(4, 4) => Swap4Chan.Instance,
			_      => throw new NotSupportedException("Unsupported pixel format")
		};

#if HWINTRINSICS
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	private static unsafe void change3to4Ssse3(ref T* ip, T* ipe, ref T* op, Vector128<byte> vmask)
	{
		var vfill = Vector128.Create(0xff000000u).AsByte();

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
		}
		while (ip <= ipe - Vector128<byte>.Count * 3);
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	private static unsafe void change4to3Ssse3(ref T* ip, T* ipe, ref T* op, Vector128<byte> vmasko)
	{
		var vmaske = Sse2.Shuffle(vmasko.AsUInt32(), 0b_10_01_00_11).AsByte();

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
		}
		while (ip <= ipe - Vector128<byte>.Count * 4);
	}
#endif

	private sealed class Change1to3Chan : IConversionProcessor<T, T>
	{
		public static readonly Change1to3Chan Instance = new();

		private Change1to3Chan() { }

		unsafe void IConversionProcessor.ConvertLine(byte* istart, byte* ostart, nint cb)
		{
			T* ip = (T*)istart, ipe = (T*)(istart + cb), op = (T*)ostart;

#if HWINTRINSICS
			if (typeof(T) == typeof(byte) && Ssse3.IsSupported && cb >= Vector128<byte>.Count)
			{
				var vmask0 = Vector128.Create((byte) 0,  0,  0,  1,  1,  1,  2,  2,  2,  3,  3,  3,  4,  4,  4,  5);
				var vmask1 = Vector128.Create((byte) 5,  5,  6,  6,  6,  7,  7,  7,  8,  8,  8,  9,  9,  9, 10, 10);
				var vmask2 = Vector128.Create((byte)10, 11, 11, 11, 12, 12, 12, 13, 13, 13, 14, 14, 14, 15, 15, 15);

				ipe -= Vector128<byte>.Count;
				do
				{
					var v0 = Sse2.LoadVector128((byte*)ip);
					ip += Vector128<byte>.Count;

					Sse2.Store((byte*)op, Ssse3.Shuffle(v0, vmask0));
					Sse2.Store((byte*)op + Vector128<byte>.Count, Ssse3.Shuffle(v0, vmask1));
					Sse2.Store((byte*)op + Vector128<byte>.Count * 2, Ssse3.Shuffle(v0, vmask2));
					op += Vector128<byte>.Count * 3;
				}
				while (ip <= ipe);
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

		unsafe void IConversionProcessor.ConvertLine(byte* istart, byte* ostart, nint cb)
		{
			T* ip = (T*)istart, ipe = (T*)(istart + cb), op = (T*)ostart;
			var alpha = maxalpha;

#if HWINTRINSICS
			if (typeof(T) == typeof(byte) && Sse2.IsSupported && cb >= Vector128<byte>.Count)
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
				}
				while (ip <= ipe);
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

		unsafe void IConversionProcessor.ConvertLine(byte* istart, byte* ostart, nint cb)
		{
			T* ip = (T*)istart, ipe = (T*)(istart + cb), op = (T*)ostart;

#if HWINTRINSICS
			if (typeof(T) == typeof(byte) && Ssse3.IsSupported && cb >= Vector128<byte>.Count * 3)
			{
				const byte _ = 0x80;
				var vmask0 = Vector128.Create(0, 3, 6, 9, 12, 15, _, _, _,  _,  _, _, _, _,  _,  _);
				var vmask1 = Vector128.Create(_, _, _, _,  _,  _, 2, 5, 8, 11, 14, _, _, _,  _,  _);
				var vmask2 = Vector128.Create(_, _, _, _,  _,  _, _, _, _,  _,  _, 1, 4, 7, 10, 13);

				ipe -= Vector128<byte>.Count * 3;
				do
				{
					var v0 = Sse2.LoadVector128((byte*)ip);
					var v1 = Sse2.LoadVector128((byte*)ip + Vector128<byte>.Count);
					var v2 = Sse2.LoadVector128((byte*)ip + Vector128<byte>.Count * 2);
					ip += Vector128<byte>.Count * 3;

					v0 = Ssse3.Shuffle(v0, vmask0);
					v1 = Ssse3.Shuffle(v1, vmask1);
					v2 = Ssse3.Shuffle(v2, vmask2);

					Sse2.Store((byte*)op, Sse2.Or(Sse2.Or(v0, v1), v2));
					op += Vector128<byte>.Count;
				}
				while (ip <= ipe);
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

		unsafe void IConversionProcessor.ConvertLine(byte* istart, byte* ostart, nint cb)
		{
			T* ip = (T*)istart, ipe = (T*)(istart + cb), op = (T*)ostart;
			var alpha = maxalpha;

#if HWINTRINSICS
			if (typeof(T) == typeof(byte) && Ssse3.IsSupported && cb >= Vector128<byte>.Count * 3)
			{
				const byte _ = 0x80;
				var vmask = Vector128.Create(0, 1, 2, _, 3, 4, 5, _, 6, 7, 8, _, 9, 10, 11, _);
				ChannelChanger<T>.change3to4Ssse3(ref ip, ipe, ref op, vmask);
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

	private sealed class Swap3to4Chan : IConversionProcessor<T, T>
	{
		public static readonly Swap3to4Chan Instance = new();

		private Swap3to4Chan() { }

		unsafe void IConversionProcessor.ConvertLine(byte* istart, byte* ostart, nint cb)
		{
			T* ip = (T*)istart, ipe = (T*)(istart + cb), op = (T*)ostart;
			var alpha = maxalpha;

#if HWINTRINSICS
			if (typeof(T) == typeof(byte) && Ssse3.IsSupported && cb >= Vector128<byte>.Count * 3)
			{
				const byte _ = 0x80;
				var vmask = Vector128.Create(2, 1, 0, _, 5, 4, 3, _, 8, 7, 6, _, 11, 10, 9, _ );
				ChannelChanger<T>.change3to4Ssse3(ref ip, ipe, ref op, vmask);
			}
#endif

			ipe -= 3;
			while (ip <= ipe)
			{
				var t = ip[0];
				op[0] = ip[2];
				op[1] = ip[1];
				op[2] = t;
				op[3] = alpha;

				ip += 3;
				op += 4;
			}
		}
	}

	private sealed class Change4to1Chan : IConversionProcessor<T, T>
	{
		public static readonly Change4to1Chan Instance = new(0);
		public static readonly Change4to1Chan InstanceAlpha = new(3);

		private readonly byte offset;

		private Change4to1Chan(byte offs) => offset = offs;

		unsafe void IConversionProcessor.ConvertLine(byte* istart, byte* ostart, nint cb)
		{
			T* ip = (T*)istart, ipe = (T*)(istart + cb), op = (T*)ostart;

#if HWINTRINSICS
			if (typeof(T) == typeof(byte) && Ssse3.IsSupported && cb >= Vector128<byte>.Count * 4)
			{
				const byte _ = 0x80;
				var vmask = Sse2.Add(Vector128.Create(offset), Vector128.Create(0, 4, 8, 12, _, _, _, _, _, _, _, _, _, _, _, _));

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
				}
				while (ip <= ipe);
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

		unsafe void IConversionProcessor.ConvertLine(byte* istart, byte* ostart, nint cb)
		{
			T* ip = (T*)istart, ipe = (T*)(istart + cb), op = (T*)ostart;

#if HWINTRINSICS
			if (typeof(T) == typeof(byte) && Ssse3.IsSupported && cb >= Vector128<byte>.Count * 4)
			{
				const byte _ = 0x80;
				var vmask = Vector128.Create(0, 1, 2, 4, 5, 6, 8, 9, 10, 12, 13, 14, _, _, _, _);
				ChannelChanger<T>.change4to3Ssse3(ref ip, ipe, ref op, vmask);
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

	private sealed class Swap4to3Chan : IConversionProcessor<T, T>
	{
		public static readonly Swap4to3Chan Instance = new();

		private Swap4to3Chan() { }

		unsafe void IConversionProcessor.ConvertLine(byte* istart, byte* ostart, nint cb)
		{
			T* ip = (T*)istart, ipe = (T*)(istart + cb), op = (T*)ostart;

#if HWINTRINSICS
			if (typeof(T) == typeof(byte) && Ssse3.IsSupported && cb >= Vector128<byte>.Count * 4)
			{
				const byte _ = 0x80;
				var vmask = Vector128.Create(2, 1, 0, 6, 5, 4, 10, 9, 8, 14, 13, 12, _, _, _, _);
				ChannelChanger<T>.change4to3Ssse3(ref ip, ipe, ref op, vmask);
			}
#endif

			ipe -= 4;
			while (ip <= ipe)
			{
				var t = ip[0];
				op[0] = ip[2];
				op[1] = ip[1];
				op[2] = t;

				ip += 4;
				op += 3;
			}
		}
	}

	private sealed class Swap3Chan : IConversionProcessor<T, T>
	{
		public static readonly Swap3Chan Instance = new();

		private Swap3Chan() { }

		unsafe void IConversionProcessor.ConvertLine(byte* istart, byte* ostart, nint cb)
		{
			T* ip = (T*)istart, ipe = (T*)(istart + cb), op = (T*)ostart;

#if HWINTRINSICS
			if (typeof(T) == typeof(byte) && Ssse3.IsSupported && cb >= Vector128<byte>.Count + sizeof(ulong))
			{
				const byte _ = 0x80;
				var vmask0 = Vector128.Create(2, 1, 0, 5, 4, 3, 8, 7, 6, 11, 10,  9, 14, 13, 12,  _);
				var vmask1 = Vector128.Create(_, 3, 2, 1, 6, 5, 4, 9, 8,  7, 12, 11, 10, 15, 14, 13);

				ipe -= Vector128<byte>.Count + sizeof(ulong);
				do
				{
					var v0 = Sse2.LoadVector128((byte*)ip);
					var v1 = Sse2.LoadVector128((byte*)ip + sizeof(ulong));
					ip += Vector128<byte>.Count + sizeof(ulong);

					v0 = Ssse3.Shuffle(v0, vmask0);
					v1 = Ssse3.Shuffle(v1, vmask1);

					Sse2.StoreScalar((ulong*)op, v0.AsUInt64());
					Sse2.Store((byte*)op + sizeof(ulong), Sse2.Or(v1, Sse2.ShiftRightLogical128BitLane(v0, 8)));
					op += Vector128<byte>.Count + sizeof(ulong);
				}
				while (ip <= ipe);
				ipe += Vector128<byte>.Count + sizeof(ulong);
			}
#endif

			ipe -= 3;
			if (ip == op)
			{
				while (ip <= ipe)
				{
					var t = ip[0];
					ip[0] = ip[2];
					ip[2] = t;

					ip += 3;
				}
				return;
			}

			while (ip <= ipe)
			{
				var t = ip[0];
				op[0] = ip[2];
				op[1] = ip[1];
				op[2] = t;

				ip += 3;
				op += 3;
			}
		}
	}

	private sealed class Swap4Chan : IConversionProcessor<T, T>
	{
		public static readonly Swap4Chan Instance = new();

		private Swap4Chan() { }

		unsafe void IConversionProcessor.ConvertLine(byte* istart, byte* ostart, nint cb)
		{
			T* ip = (T*)istart, ipe = (T*)(istart + cb), op = (T*)ostart;

#if HWINTRINSICS
			if (typeof(T) == typeof(byte) && Ssse3.IsSupported && cb >= Vector256<byte>.Count)
			{
				var vmask = Vector128.Create((byte)2, 1, 0, 3, 6, 5, 4, 7, 10, 9, 8, 11, 14, 13, 12, 15);

				if (Avx2.IsSupported)
				{
					var wmask = vmask.ToVector256Unsafe().WithUpper(vmask);

					ipe -= Vector256<byte>.Count;
					do
					{
						var v0 = Avx.LoadVector256((byte*)ip);
						ip += Vector256<byte>.Count;

						Avx.Store((byte*)op, Avx2.Shuffle(v0, wmask));
						op += Vector256<byte>.Count;
					}
					while (ip <= ipe);
					ipe += Vector256<byte>.Count;
				}
				else
				{
					ipe -= Vector128<byte>.Count;
					do
					{
						var v0 = Sse2.LoadVector128((byte*)ip);
						ip += Vector128<byte>.Count;

						Sse2.Store((byte*)op, Ssse3.Shuffle(v0, vmask));
						op += Vector128<byte>.Count;
					}
					while (ip <= ipe);
					ipe += Vector128<byte>.Count;
				}
			}
#endif

			ipe -= 4;
			if (ip == op)
			{
				while (ip <= ipe)
				{
					var t = ip[0];
					ip[0] = ip[2];
					ip[2] = t;

					ip += 4;
				}
				return;
			}

			while (ip <= ipe)
			{
				var t = ip[0];
				op[0] = ip[2];
				op[1] = ip[1];
				op[2] = t;
				op[3] = ip[3];

				ip += 4;
				op += 4;
			}
		}
	}
}
