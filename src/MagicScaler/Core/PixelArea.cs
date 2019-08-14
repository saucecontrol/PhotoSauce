using System;
using System.Drawing;

namespace PhotoSauce.MagicScaler
{
	internal readonly struct PixelArea : IEquatable<PixelArea>
	{
		public readonly int X;
		public readonly int Y;
		public readonly int Width;
		public readonly int Height;

		public static PixelArea FromGdiRect(Rectangle r) => new PixelArea(r.X, r.Y, r.Width, r.Height);

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

		public Rectangle ToGdiRect() => new Rectangle(X, Y, Width, Height);

		public bool Equals(PixelArea other) => X == other.X && Y == other.Y && Width == other.Width && Height == other.Height;
		public override bool Equals(object? obj) => obj is PixelArea other && Equals(other);

		public override int GetHashCode() => (X, Y, Width, Height).GetHashCode();
		public override string ToString() => (X, Y, Width, Height).ToString();

		public static bool operator ==(in PixelArea left, in PixelArea right) => left.Equals(right);
		public static bool operator !=(in PixelArea left, in PixelArea right) => !left.Equals(right);
	}
}
