// Copyright © Clinton Ingram and Contributors.  Licensed under the MIT License.

using System;
using System.Drawing;
using System.Runtime.CompilerServices;

namespace PhotoSauce.MagicScaler
{
	internal readonly record struct PixelArea
	{
		public static PixelArea Default => default;

		public readonly int X;
		public readonly int Y;
		public readonly int Width;
		public readonly int Height;

		public PixelArea(int x, int y, int width, int height)
		{
			Guard.NonNegative(x);
			Guard.NonNegative(y);
			Guard.NonNegative(width);
			Guard.NonNegative(height);

			(X, Y, Width, Height) = (x, y, width, height);
		}

		public bool IsEmpty => this == default;

		public void Deconstruct(out int x, out int y, out int w, out int h) => (x, y, w, h) = (X, Y, Width, Height);

		public PixelArea DeOrient(Orientation orientation, int targetWidth, int targetHeight)
		{
			var (x, y, width, height) = this;

			if (orientation.SwapsDimensions())
				(x, y, width, height) = (y, x, height, width);

			if (orientation.FlipsX())
				x = targetWidth - width - x;

			if (orientation.FlipsY())
				y = targetHeight - height - y;

			return new PixelArea(x, y, width, height);
		}

		public PixelArea ReOrient(Orientation orientation, int targetWidth, int targetHeight)
		{
			if (orientation.SwapsDimensions())
				(targetWidth, targetHeight) = (targetHeight, targetWidth);

			return DeOrient(orientation.Invert(), targetWidth, targetHeight);
		}

		public PixelArea ProportionalScale(int sourceWidth, int sourceHeight, int targetWidth, int targetHeight)
		{
			double xRatio = (double)sourceWidth / targetWidth;
			double yRatio = (double)sourceHeight / targetHeight;

			int x = (int)Math.Floor(X / xRatio);
			int y = (int)Math.Floor(Y / yRatio);
			int width = (int)Math.Min(Math.Ceiling(Width / xRatio), targetWidth - x);
			int height = (int)Math.Min(Math.Ceiling(Height / yRatio), targetHeight - y);

			return new PixelArea(x, y, width, height);
		}

		public static implicit operator PixelArea(in Rectangle r) => new(r.X, r.Y, r.Width, r.Height);
		public static implicit operator Rectangle(in PixelArea a) => Unsafe.As<PixelArea, Rectangle>(ref Unsafe.AsRef(a));

		public static implicit operator PixelArea(Size s) => new(0, 0, s.Width, s.Height);
	}
}
