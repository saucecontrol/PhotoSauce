using System;
using System.Diagnostics.Contracts;

using static System.Math;

namespace PhotoSauce.MagicScaler.Interpolators
{
	public interface IInterpolator
	{
		double Support { get; }
		double GetValue(double distance);
	}

	//http://www.imagemagick.org/Usage/filter/#point
	public class PointInterpolator : IInterpolator
	{
		public double Support => 0.000001;
		public double GetValue(double d) => 1.0;
	}

	//http://www.imagemagick.org/Usage/filter/#box
	public class BoxInterpolator : IInterpolator
	{
		public double Support => 0.5;
		public double GetValue(double d) => d <= 0.5 ? 1.0 : 0.0;
	}

	//http://www.imagemagick.org/Usage/filter/#triangle
	public class LinearInterpolator : IInterpolator
	{
		public double Support => 1.0;
		public double GetValue(double d) => d < 1.0 ? 1.0 - d : 0.0;
	}

	//http://www.imagemagick.org/Usage/filter/#gaussian
	public class GaussianInterpolator : IInterpolator
	{
		private readonly double sigma, support;
		private readonly MathUtil.GaussianFactory gauss;

		public GaussianInterpolator(double sigma)
		{
			Contract.Requires<ArgumentException>(sigma > 0.0, "Sigma must be greater than 0");

			this.sigma = sigma;
			gauss = new MathUtil.GaussianFactory(sigma);
			support = gauss.Support;
		}

		public double Support => support;
		public double GetValue(double d) => (d < support) ? gauss.GetValue(d) : 0.0;
		public override string ToString() => $"{base.ToString()}({sigma})";
	}

	//http://neildodgson.com/pubs/quad.pdf
	public class QuadraticInterpolator : IInterpolator
	{
		private readonly double r;

		public QuadraticInterpolator(double r = 1.0)
		{
			Contract.Requires<ArgumentException>(r >= 0.5 && r <= 1.0, "r must be between 0.5 and 1.0");

			this.r = r;
		}

		public double Support => 1.5;
		public double GetValue(double d)
		{
			if (d < 0.5)
				return (-2.0 * r) * (d * d)                        + 0.50 * (r + 1.0);

			if (d < 1.5)
				return         r  * (d * d) + (-2.0 * r - 0.5) * d + 0.75 * (r + 1.0);

			return 0.0;
		}

		public override string ToString() => $"{base.ToString()}({r})";
	}

	//http://www.imagemagick.org/Usage/filter/#cubics
	public class CubicInterpolator : IInterpolator
	{
		private readonly double support;
		private readonly double b, c, p0, p2, p3, q0, q1, q2, q3;

		public CubicInterpolator(double b = 0.0, double c = 0.5)
		{
			Contract.Requires<ArgumentException>(b >= 0.0 && c >= 0.0, "B and C values must be greater than or equal to 0");

			this.b = b; this.c = c;
			support = b == 0.0 && c == 0.0 ? 1.0 : 2.0;

			p0 = (  6.0 -  2.0 * b           ) / 6.0;
			p2 = (-18.0 + 12.0 * b + c *  6.0) / 6.0;
			p3 = ( 12.0 -  9.0 * b - c *  6.0) / 6.0;
			q0 = (         8.0 * b + c * 24.0) / 6.0;
			q1 = (       -12.0 * b - c * 48.0) / 6.0;
			q2 = (         6.0 * b + c * 30.0) / 6.0;
			q3 = (              -b - c *  6.0) / 6.0;
		}

		public double Support => support;
		public double GetValue(double d)
		{
			if (d < 1.0)
				return p0 + d *       d * (p2 + d * p3);

			if (support > 1.0 && d < 2.0)
				return q0 + d * (q1 + d * (q2 + d * q3));

			return 0.0;
		}

		public override string ToString() => $"{base.ToString()}({b}, {c})";
	}

	//http://en.wikipedia.org/wiki/Lanczos_resampling
	public class LanczosInterpolator : IInterpolator
	{
		private readonly double support;

		public LanczosInterpolator(int lobes = 3)
		{
			Contract.Requires<ArgumentException>(lobes > 0, "lobe count must be greater than 0");

			support = lobes;
		}

		public double Support => support;
		public double GetValue(double d)
		{
			if (d == 0.0)
				return 1.0;

			if (d < support)
			{
				d *= PI;
				return (support * Sin(d) * Sin(d / support)) / (d * d);
			}

			return 0.0;
		}

		public override string ToString() => $"{base.ToString()}({support})";
	}

	//http://www.panotools.org/dersch/interpolator/interpolator.html
	public class Spline36Interpolator : IInterpolator
	{
		public double Support => 3.0;
		public double GetValue(double d)
		{
			if (d < 1.0)
				return (( 13.0/11.0 * d - 453.0/209.0) * d -   3.0/209.0) * d + 1.0;

			if (d < 2.0)
			{
				d -= 1.0;
				return ((- 6.0/11.0 * d + 270.0/209.0) * d - 156.0/209.0) * d;
			}

			if (d < 3.0)
			{
				d -= 2.0;
				return ((  1.0/11.0 * d -  45.0/209.0) * d +  26.0/209.0) * d;
			}

			return 0.0;
		}
	}
}