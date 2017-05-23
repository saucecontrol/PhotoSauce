using System;
using System.Buffers;
using System.Numerics;
using System.Runtime.CompilerServices;

namespace PhotoSauce.MagicScaler
{
	internal class KernelMap<T> : IDisposable where T : struct
	{
		public int InPixels { get; private set; }
		public int OutPixels { get; private set; }
		public int Samples { get; private set; }
		public int Channels { get; private set; }
		public ArraySegment<byte> Map { get; private set; }

		static KernelMap()
		{
			if (typeof(T) != typeof(int) && typeof(T) != typeof(float)) throw new NotSupportedException($"{nameof(T)} must be int or float");
			if (Unsafe.SizeOf<T>() != sizeof(int)) throw new NotSupportedException($"sizeof({nameof(T)}) and sizeof(int) must be equal");
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		unsafe private static T convertWeight(double d)
		{
			T v = default(T);
			if (typeof(T) == typeof(int))
				Unsafe.Write(Unsafe.AsPointer(ref v), MathUtil.Fix15(d));
			else if (typeof(T) == typeof(float))
				Unsafe.Write(Unsafe.AsPointer(ref v), (float)d);

			return v;
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		unsafe private static void addWeight(ref T a, void* b)
		{
			void* ap = Unsafe.AsPointer(ref a);
			if (typeof(T) == typeof(int))
				Unsafe.Write(ap, Unsafe.Read<int>(ap) + Unsafe.Read<int>(b));
			else if (typeof(T) == typeof(float))
				Unsafe.Write(ap, Unsafe.Read<float>(ap) + Unsafe.Read<float>(b));
		}

		private KernelMap(int inPixels, int outPixels, int samples, int channels)
		{
			InPixels = inPixels;
			OutPixels = outPixels;
			Samples = samples;
			Channels = channels;

			int mapLen = OutPixels * (Samples * Channels * Unsafe.SizeOf<T>() + sizeof(int));
			Map = new ArraySegment<byte>(ArrayPool<byte>.Shared.Rent(mapLen), 0, mapLen);
		}

		unsafe private KernelMap<T> clamp()
		{
			fixed (byte* mstart = Map.Array)
			{
				int samp = Samples, ipix = InPixels, chan = Channels;

				int* mp = (int*)mstart;
				int* mpe = (int*)(mstart + Map.Count);
				while (mp < mpe)
				{
					int start = *mp;
					if (start < 0)
					{
						int o = 0 - start;

						*mp = 0;
						int* mpw = mp + 1;
						for (int k = 0; k < chan; k++)
						{
							T a = default(T);
							for (int j = 0; j <= o; j++)
								addWeight(ref a, mpw + j * chan + k);

							Unsafe.Write(mpw + k, a);

							for (int j = 1; j < samp; j++)
								Unsafe.Write(mpw + j * chan + k, j < samp - o ? Unsafe.Read<T>(mpw + j * chan + o * chan + k) : default(T));
						}
					}
					else if (start + samp > ipix)
					{
						int ns = ipix - samp, last = samp - 1, o = start - ns;

						*mp = ns;
						int* mpw = mp + 1;
						for (int k = 0; k < chan; k++)
						{
							T a = default(T);
							for (int j = 0; j <= o; j++)
								addWeight(ref a, mpw + last * chan - j * chan + k);

							Unsafe.Write(mpw + last * chan + k, a);

							for (int j = last - 1; j >= 0; j--)
								Unsafe.Write(mpw + j * chan + k, j >= o ? Unsafe.Read<T>(mpw + j * chan - o * chan + k) : default(T));
						}
					}

					mp += (samp * chan + 1);
				}
			}

			return this;
		}

		unsafe public static KernelMap<T> MakeScaleMap(uint isize, uint osize, int colorChannels, bool alphaChannel, bool vectored, InterpolationSettings interpolator)
		{
			var ainterpolator = interpolator.WeightingFunction.Support > 1d ? InterpolationSettings.Hermite : interpolator;

			double offs = interpolator.WeightingFunction.Support < 0.1 ? 0.5 : 0.0;
			double wfactor = Math.Min((double)osize / isize, 1d);
			double wscale = wfactor / interpolator.Blur;
			double awscale = wfactor / ainterpolator.Blur;
			double wdist = Math.Min(interpolator.WeightingFunction.Support / wscale, isize / 2d);

			int channels = colorChannels + (alphaChannel ? 1 : 0);
			int wsize = (int)Math.Ceiling(wdist * 2d);
			if (vectored && wsize * channels % (Vector<T>.Count * channels) >= (Vector<T>.Count * channels) / 2)
				wsize = (wsize * channels + (Vector<T>.Count * channels - 1) & ~(Vector<T>.Count * channels - 1)) / channels;
			wsize = Math.Min(wsize, (int)isize);

			var map = new KernelMap<T>((int)isize, (int)osize, wsize, channels);
			fixed (byte* mapstart = map.Map.Array)
			{
				double* wp = stackalloc double[wsize];
				double* awp = stackalloc double[wsize];
				int* mp = (int*)mapstart;

				double inc = (double)isize / osize;
				double spoint = ((double)isize - osize) / (osize * 2d) + offs;

				for (int i = 0; i < osize; i++)
				{
					int start = (int)(spoint + wdist) - wsize + 1;
					*mp++ = start;

					double weightsum = 0d;
					for (int j = 0; j < wsize; j++)
					{
						double weight = interpolator.WeightingFunction.GetValue(Math.Abs(((double)start + j - spoint) * wscale));
						weightsum += weight;
						wp[j] = weight;
					}
					weightsum = 1d / weightsum;
					for (int j = 0; j < wsize; j++)
						wp[j] *= weightsum;

					if (alphaChannel)
					{
						double aweightsum = 0d;
						for (int j = 0; j < wsize; j++)
						{
							double weight = ainterpolator.WeightingFunction.GetValue(Math.Abs(((double)start + j - spoint) * awscale));
							aweightsum += weight;
							awp[j] = weight;
						}
						aweightsum = 1d / aweightsum;
						for (int j = 0; j < wsize; j++)
							awp[j] *= aweightsum;
					}

					for (int j = 0; j < wsize; j++)
					{
						T w = convertWeight(wp[j]);
						for (int k = 0; k < colorChannels; k++)
							Unsafe.Write(mp++, w);

						if (alphaChannel)
							Unsafe.Write(mp++, convertWeight(awp[j]));
					}

					spoint += inc;
				}
			}

			return map.clamp();
		}

		unsafe public static KernelMap<T> MakeBlurMap(uint size, double radius, uint colorChannels, bool alphaChannel)
		{
			double[] blurkernel = new MathUtil.GaussianFactory(radius).MakeKernel();
			int blurdist = Math.Min(blurkernel.Length, (int)size);
			int channels = (int)colorChannels + (alphaChannel ? 1 : 0);

			var map = new KernelMap<T>((int)size, (int)size, blurdist, channels);
			fixed (byte* mstart = map.Map.Array)
			fixed (double* kstart = blurkernel)
			{
				int* mp = (int*)mstart;
				double* kp = kstart;

				for (int i = 0; i < size; i++)
				{
					int start = i - blurdist / 2;
					*mp++ = start;

					for (int j = 0; j < blurdist; j++)
					{
						T w = convertWeight(kp[j]);
						for (int k = 0; k < channels; k++)
							Unsafe.Write(mp++, w);
					}
				}
			}

			return map.clamp();
		}

		public void Dispose()
		{
			ArrayPool<byte>.Shared.Return(Map.Array ?? Array.Empty<byte>());
			Map = default(ArraySegment<byte>);
		}
	}
}