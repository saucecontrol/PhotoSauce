using System;

namespace PhotoSauce.MagicScaler
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
			public static Change1to3Chan Instance = new Change1to3Chan();

			private Change1to3Chan() { }

			unsafe void IConversionProcessor.ConvertLine(byte* ipstart, byte* opstart, int cb)
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
		}

		private sealed class Change1to4Chan : IConversionProcessor<T, T>
		{
			public static Change1to4Chan Instance = new Change1to4Chan();

			private Change1to4Chan() { }

			unsafe void IConversionProcessor.ConvertLine(byte* ipstart, byte* opstart, int cb)
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
		}

		private sealed class Change3to1Chan : IConversionProcessor<T, T>
		{
			public static Change3to1Chan Instance = new Change3to1Chan();

			private Change3to1Chan() { }

			unsafe void IConversionProcessor.ConvertLine(byte* ipstart, byte* opstart, int cb)
			{
				T* ip = (T*)ipstart, ipe = (T*)(ipstart + cb) - 3, op = (T*)opstart;

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
			public static Change3to4Chan Instance = new Change3to4Chan();

			private Change3to4Chan() { }

			unsafe void IConversionProcessor.ConvertLine(byte* ipstart, byte* opstart, int cb)
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
		}

		private sealed class Change4to1Chan : IConversionProcessor<T, T>
		{
			public static Change4to1Chan Instance = new Change4to1Chan();

			private Change4to1Chan() { }

			unsafe void IConversionProcessor.ConvertLine(byte* ipstart, byte* opstart, int cb)
			{
				T* ip = (T*)ipstart, ipe = (T*)(ipstart + cb) - 4, op = (T*)opstart;

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
			public static Change4to3Chan Instance = new Change4to3Chan();

			private Change4to3Chan() { }

			unsafe void IConversionProcessor.ConvertLine(byte* ipstart, byte* opstart, int cb)
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
}
