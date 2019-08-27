using System;
using System.Drawing;

using PhotoSauce.MagicScaler.Interop;

namespace PhotoSauce.MagicScaler
{
	internal readonly struct PixelArea : IEquatable<PixelArea>
	{
		public readonly int X;
		public readonly int Y;
		public readonly int Width;
		public readonly int Height;

		public static PixelArea FromGdiRect(Rectangle r) => new PixelArea(r.X, r.Y, r.Width, r.Height);

		public static PixelArea FromWicRect(WICRect r) => new PixelArea(r.X, r.Y, r.Width, r.Height);

		public PixelArea(int x, int y, int width, int height)
		{
			if (x < 0) throwArgException(nameof(x));
			if (y < 0) throwArgException(nameof(y));
			if (width < 0) throwArgException(nameof(width));
			if (height < 0) throwArgException(nameof(height));

			X = x;
			Y = y;
			Width = width;
			Height = height;

			static void throwArgException(string name) => throw new ArgumentOutOfRangeException(name, "Value cannot be negative");
		}

		public void Deconstruct(out int x, out int y, out int width, out int height)
		{
			x = X;
			y = Y;
			width = Width;
			height = Height;
		}

		public PixelArea DeOrient(Orientation orientation, uint targetWidth, uint targetHeight)
		{
			var (x, y, width, height) = this;

			if (orientation.SwapsDimensions())
				(x, y, width, height) = (y, x, height, width);

			if (orientation.FlipsX())
				x = (int)targetWidth - width - x;

			if (orientation.FlipsY())
				y = (int)targetHeight - height - y;

			return new PixelArea(x, y, width, height);
		}

		public PixelArea ReOrient(Orientation orientation, uint targetWidth, uint targetHeight)
		{
			if (orientation.SwapsDimensions())
				(targetWidth, targetHeight) = (targetHeight, targetWidth);

			return DeOrient(orientation.Invert(), targetWidth, targetHeight);
		}

		public PixelArea ProportionalScale(uint sourceWidth, uint sourceHeight, uint targetWidth, uint targetHeight)
		{
			double xRatio = (double)sourceWidth / targetWidth;
			double yRatio = (double)sourceHeight / targetHeight;

			int x = (int)Math.Floor(X / xRatio);
			int y = (int)Math.Floor(Y / yRatio);
			int width = Math.Min((int)Math.Ceiling(Width / xRatio), (int)targetWidth - x);
			int height = Math.Min((int)Math.Ceiling(Height / yRatio), (int)targetHeight - y);

			return new PixelArea(x, y, width, height);
		}

		public Rectangle ToGdiRect() => new Rectangle(X, Y, Width, Height);

		public WICRect ToWicRect() => new WICRect { X = X, Y = Y, Width = Width, Height = Height };

		public WICRect CopyToWicRect(WICRect wr)
		{
			wr.X = X;
			wr.Y = Y;
			wr.Width = Width;
			wr.Height = Height;

			return wr;
		}

		public bool Equals(PixelArea other) => X == other.X && Y == other.Y && Width == other.Width && Height == other.Height;
		public override bool Equals(object? obj) => obj is PixelArea other && Equals(other);

		public override int GetHashCode() => (X, Y, Width, Height).GetHashCode();
		public override string ToString() => (X, Y, Width, Height).ToString();

		public static bool operator ==(in PixelArea left, in PixelArea right) => left.Equals(right);
		public static bool operator !=(in PixelArea left, in PixelArea right) => !left.Equals(right);
	}
}
