using System;
using System.Drawing;

using PhotoSauce.Interop.Wic;

namespace PhotoSauce.MagicScaler
{
	internal readonly struct PixelArea : IEquatable<PixelArea>
	{
		public readonly int X;
		public readonly int Y;
		public readonly int Width;
		public readonly int Height;

		public static PixelArea FromGdiRect(Rectangle r) => new PixelArea(r.X, r.Y, r.Width, r.Height);

		public static PixelArea FromGdiSize(Size s) => new PixelArea(0, 0, s.Width, s.Height);

		public static PixelArea FromWicRect(WICRect r) => new PixelArea(r.X, r.Y, r.Width, r.Height);

		public PixelArea(int x, int y, int width, int height)
		{
			static void throwArgException(string name) => throw new ArgumentOutOfRangeException(name, "Value cannot be negative");

			if (x < 0) throwArgException(nameof(x));
			if (y < 0) throwArgException(nameof(y));
			if (width < 0) throwArgException(nameof(width));
			if (height < 0) throwArgException(nameof(height));

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

		public Rectangle ToGdiRect() => new Rectangle(X, Y, Width, Height);

		public WICRect ToWicRect() => new WICRect { X = X, Y = Y, Width = Width, Height = Height };

		public bool Equals(PixelArea other) => X == other.X && Y == other.Y && Width == other.Width && Height == other.Height;
		public override bool Equals(object? obj) => obj is PixelArea other && Equals(other);

		public override int GetHashCode() => (X, Y, Width, Height).GetHashCode();
		public override string ToString() => (X, Y, Width, Height).ToString();

		public static bool operator ==(in PixelArea left, in PixelArea right) => left.Equals(right);
		public static bool operator !=(in PixelArea left, in PixelArea right) => !left.Equals(right);
	}
}
