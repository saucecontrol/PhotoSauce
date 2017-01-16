using System;
using System.Linq;

namespace PhotoSauce.MagicScaler
{
	internal class KernelMap
	{
		public int InPixelCount { get; private set; }
		public int OutPixelCount { get; private set; }
		public int SampleCount { get; private set; }
		public int[] Map { get; private set; }

		public KernelMap(int inPixels, int outPixels, int samples)
		{
			InPixelCount = inPixels;
			OutPixelCount = outPixels;
			SampleCount = samples;
			Map = new int[outPixels * (samples + 1)];
		}

		unsafe private KernelMap clamp()
		{
			fixed (int* mstart = Map)
			{
				int* mp = mstart;
				int* mpe = mstart + Map.Length;
				int samp = SampleCount, ipix = InPixelCount;
				while (mp < mpe)
				{
					int start = *mp;
					if (start < 0)
					{
						*mp = 0;
						int* mpw = mp + 1;
						int o = 0 - start;
						int a = 0;
						for (int j = 0; j <= o; j++)
							a += mpw[j];

						mpw[0] = a;
						for (int j = 1; j < samp; j++)
							mpw[j] = j < samp - o ? mpw[j + o] : 0;
					}
					else if (start + samp > ipix)
					{
						int ns = ipix - samp;
						int last = samp - 1;

						*mp = ns;
						int* mpw = mp + 1;
						int o = start - ns;
						int a = 0;
						for (int j = 0; j <= o; j++)
							a += mpw[last - j];

						mpw[last] = a;
						for (int j = last - 1; j >= 0; j--)
							mpw[j] = j >= o ? mpw[j - o] : 0;
					}

					mp += samp + 1;
				}
			}

			return this;
		}

		unsafe public KernelMap MakeAlphaMap()
		{
			if (InPixelCount == OutPixelCount)
				return this;

			var interpolator = InterpolationSettings.Hermite.WeightingFunction;
			double blur = 0.9;

			double wfactor = Math.Min((double)OutPixelCount / InPixelCount, 1d);
			double wdist = Math.Min(interpolator.Support / wfactor * blur, InPixelCount / 2d);
			int wsize = (int)Math.Ceiling(wdist * 2d);

			if (wsize >= SampleCount)
				return this;

			wsize = SampleCount;
			int osize = OutPixelCount;
			var map = new KernelMap(InPixelCount, OutPixelCount, SampleCount);

			fixed (int* istart = Map, ostart = map.Map)
			{
				double* wp = stackalloc double[wsize];
				int* ip = istart, op = ostart;

				double inc = (double)InPixelCount / OutPixelCount;
				double midpoint = ((double)InPixelCount - OutPixelCount) / (OutPixelCount * 2d);

				for (int i = 0; i < osize; i++)
				{
					int start = *ip++;
					*op++ = start;

					double weightsum = 0d;
					for (int j = 0; j < wsize; j++)
					{
						double weight = interpolator.GetValue(Math.Abs(((double)start + j - midpoint) * wfactor / blur));
						weightsum += weight;
						wp[j] = weight;
					}

					for (int j = 0; j < wsize; j++, ip++)
						*op++ = MathUtil.ScaleToInt32(wp[j] / weightsum);

					midpoint += inc;
				}
			}

			return map.clamp();
		}

		unsafe public static KernelMap MakeScaleMap(uint isize, uint osize, InterpolationSettings interpolator)
		{
			double offs = interpolator.WeightingFunction.Support <= 0.1 ? 0.5 : 0.0;
			double blur = interpolator.Blur;
			if (interpolator.WeightingFunction.Support <= 0.5)
				blur = 1d;

			double wfactor = Math.Min((double)osize / isize, 1d);
			double wdist = Math.Min(interpolator.WeightingFunction.Support / wfactor * blur, isize / 2d);
			int wsize = (int)Math.Ceiling(wdist * 2d);

			var map = new KernelMap((int)isize, (int)osize, wsize);
			fixed (int* mapstart = map.Map)
			{
				double* wp = stackalloc double[wsize];
				int* mp = mapstart;

				double inc = (double)isize / osize;
				double midpoint = ((double)isize - osize) / (osize * 2d) + offs;

				for (int i = 0; i < osize; i++)
				{
					int end = (int)(midpoint + wdist);
					int start = end - wsize + 1;

					*mp++ = start;

					double weightsum = 0d;
					for (int j = 0; j < wsize; j++)
					{
						double weight = interpolator.WeightingFunction.GetValue(Math.Abs(((double)start + j - midpoint) * wfactor / blur));
						weightsum += weight;
						wp[j] = weight;
					}

					for (int j = 0; j < wsize; j++)
						*mp++ = MathUtil.ScaleToInt32(wp[j] / weightsum);

					midpoint += inc;
				}
			}

			return map.clamp();
		}

		unsafe public static KernelMap MakeBlurMap(uint size, double radius)
		{
			int[] blurkernel = new MathUtil.GaussianFactory(radius).MakeKernel().Select(f => MathUtil.ScaleToInt32(f)).ToArray();
			int blurdist = blurkernel.Length;

			var map = new KernelMap((int)size, (int)size, blurdist);
			fixed (int* mstart = map.Map, kstart = blurkernel)
			{
				int* mp = mstart, kp = kstart;

				for (int i = 0; i < size; i++)
				{
					int end = i + blurdist / 2;
					int start = end - blurdist + 1;

					*mp++ = start;

					for (int j = 0; j < blurdist; j++)
						*mp++ = kp[j];
				}
			}

			return map.clamp();
		}
	}
}