using System;
using System.Buffers;

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
			if (typeof(T) != typeof(int)) throw new NotSupportedException($"{nameof(T)} must be int");
		}

		private KernelMap(int inPixels, int outPixels, int samples, int channels)
		{
			InPixels = inPixels;
			OutPixels = outPixels;
			Samples = samples;
			Channels = channels;

			int mapLen = OutPixels * (Samples * Channels + 1) * sizeof(int);
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
							int a = 0;
							for (int j = 0; j <= o; j++)
								a += mpw[j * chan + k];

							mpw[k] = a;

							for (int j = 1; j < samp; j++)
								mpw[j * chan + k] = j < samp - o ? mpw[j * chan + o * chan + k] : 0;
						}
					}
					else if (start + samp > ipix)
					{
						int ns = ipix - samp, last = samp - 1, o = start - ns;

						*mp = ns;
						int* mpw = mp + 1;
						for (int k = 0; k < chan; k++)
						{
							int a = 0;
							for (int j = 0; j <= o; j++)
								a += mpw[last * chan - j * chan + k];

							mpw[last * chan + k] = a;

							for (int j = last - 1; j >= 0; j--)
								mpw[j * chan + k] = j >= o ? mpw[j * chan - o * chan + k] : 0;
						}
					}

					mp += (samp * chan + 1);
				}
			}

			return this;
		}

		unsafe public static KernelMap<T> MakeScaleMap(uint isize, uint osize, uint colorChannels, bool alphaChannel, InterpolationSettings interpolator)
		{
			var ainterpolator = interpolator.WeightingFunction.Support > 1d ? InterpolationSettings.Hermite : interpolator;

			double offs = interpolator.WeightingFunction.Support < 0.1 ? 0.5 : 0.0;
			double blur = interpolator.WeightingFunction.Support > 0.5 ? interpolator.Blur : 1.0;
			double ablur = ainterpolator.WeightingFunction.Support > 0.5 ? ainterpolator.Blur : 1.0;

			double wfactor = Math.Min((double)osize / isize, 1d);
			double wdist = Math.Min(interpolator.WeightingFunction.Support / wfactor * blur, isize / 2d);
			int wsize = (int)Math.Ceiling(wdist * 2d);
			int channels = (int)colorChannels + (alphaChannel ? 1 : 0);

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
						double weight = interpolator.WeightingFunction.GetValue(Math.Abs(((double)start + j - spoint) * wfactor / blur));
						weightsum += weight;
						wp[j] = weight;
					}
					weightsum = 1d / weightsum;
					for (int j = 0; j < wsize; j++)
						wp[j] = wp[j] * weightsum;

					if (alphaChannel)
					{
						double aweightsum = 0d;
						for (int j = 0; j < wsize; j++)
						{
							double weight = ainterpolator.WeightingFunction.GetValue(Math.Abs(((double)start + j - spoint) * wfactor / ablur));
							aweightsum += weight;
							awp[j] = weight;
						}
						aweightsum = 1d / aweightsum;
						for (int j = 0; j < wsize; j++)
							awp[j] = awp[j] * aweightsum;
					}

					for (int j = 0; j < wsize; j++)
					{
						for (int k = 0; k < colorChannels; k++)
							*mp++ = MathUtil.ScaleToInt32(wp[j]);

						if (alphaChannel)
							*mp++ = MathUtil.ScaleToInt32(awp[j]);
					}

					spoint += inc;
				}
			}

			return map.clamp();
		}

		unsafe public static KernelMap<T> MakeBlurMap(uint size, double radius, uint colorChannels, bool alphaChannel)
		{
			double[] blurkernel = new MathUtil.GaussianFactory(radius).MakeKernel();
			int blurdist = blurkernel.Length;
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
					for (int k = 0; k < channels; k++)
							*mp++ = MathUtil.ScaleToInt32(kp[j]);
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