using System;
using System.Runtime.CompilerServices;

namespace PhotoSauce.MagicScaler
{
	unsafe internal static class ChannelChanger<T> where T : unmanaged
	{
		private static readonly T maxalpha;

		unsafe static ChannelChanger()
		{
			var one = default(T);
			if (typeof(T) == typeof(float))
				Unsafe.Write(&one, 1.0f);
			else if (typeof(T) == typeof(ushort))
				Unsafe.Write(&one, MathUtil.UQ15One);
			else if (typeof(T) == typeof(byte))
				Unsafe.Write(&one, byte.MaxValue);
			else
				throw new NotSupportedException($"{nameof(T)} must be float, ushort, or byte");

			maxalpha = one;
		}

		public static void Change1to3Chan(byte* ipstart, byte* opstart, int cb)
		{
			T* ip = (T*)ipstart, ipe = (T*)(ipstart + cb), op = (T*)opstart;

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

		public static void Change1to4Chan(byte* ipstart, byte* opstart, int cb)
		{
			T* ip = (T*)ipstart, ipe = (T*)(ipstart + cb), op = (T*)opstart;
			var alpha = maxalpha;

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

		public static void Change3to1Chan(byte* ipstart, byte* opstart, int cb)
		{
			T* ip = (T*)ipstart, ipe = (T*)(ipstart + cb) - 3, op = (T*)opstart;

			while (ip <= ipe)
			{
				op[0] = ip[0];

				ip += 3;
				op++;
			}
		}

		public static void Change3to4Chan(byte* ipstart, byte* opstart, int cb)
		{
			T* ip = (T*)ipstart, ipe = (T*)(ipstart + cb) - 3, op = (T*)opstart;
			var alpha = maxalpha;

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

		public static void Change4to1Chan(byte* ipstart, byte* opstart, int cb)
		{
			T* ip = (T*)ipstart, ipe = (T*)(ipstart + cb) - 4, op = (T*)opstart;

			while (ip <= ipe)
			{
				op[0] = ip[0];

				ip += 4;
				op++;
			}
		}

		public static void Change4to3Chan(byte* ipstart, byte* opstart, int cb)
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
	}
}
