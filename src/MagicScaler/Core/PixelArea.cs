// Copyright © Clinton Ingram and Contributors.  Licensed under the MIT License.

using System;
using System.Drawing;
using System.Diagnostics;
using System.Runtime.CompilerServices;

namespace PhotoSauce.MagicScaler;

internal readonly record struct PixelArea
{
	public static PixelArea Default => default;

	public readonly int X;
	public readonly int Y;
	public readonly int Width;
	public readonly int Height;

	public PixelArea(int x, int y, int width, int height)
	{
		ThrowHelper.ThrowIfNegative(x);
		ThrowHelper.ThrowIfNegative(y);
		ThrowHelper.ThrowIfNegative(width);
		ThrowHelper.ThrowIfNegative(height);

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

		return new(x, y, width, height);
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

		return new(x, y, width, height);
	}

	public PixelArea SnapTo(int incX, int incY, int maxWidth, int maxHeight)
	{
		int x = MathUtil.PowerOfTwoFloor(X, incX);
		int y = MathUtil.PowerOfTwoFloor(Y, incY);
		int width = Math.Min(MathUtil.PowerOfTwoCeiling(Width + X - x, incX), maxWidth - x);
		int height = Math.Min(MathUtil.PowerOfTwoCeiling(Height + Y - y, incY), maxHeight - y);

		return new(x, y, width, height);
	}

	public PixelArea ScaleTo(int ratioX, int ratioY, int maxWidth, int maxHeight)
	{
		int x = X / ratioX;
		int y = Y / ratioY;
		int width = Math.Min(MathUtil.DivCeiling(Width, ratioX), maxWidth);
		int height = Math.Min(MathUtil.DivCeiling(Height, ratioY), maxHeight);

		return new(x, y, width, height);
	}

	public PixelArea Intersect(PixelArea other)
	{
		int x1 = Math.Max(X, other.X);
		int x2 = Math.Min(X + Width, other.X + other.Width);
		int y1 = Math.Max(Y, other.Y);
		int y2 = Math.Min(Y + Height, other.Y + other.Height);

		if (x2 >= x1 && y2 >= y1)
			return new(x1, y1, x2 - x1, y2 - y1);

		return default;
	}

	public PixelArea RelativeTo(PixelArea other)
	{
		int x = X - other.X;
		int y = Y - other.Y;

		return new(x, y, Width, Height);
	}

	public PixelArea Slice(int y)
	{
		Debug.Assert(y < Height);

		return new(X, Y + y, Width, Height - y);
	}

	public PixelArea Slice(int y, int height)
	{
		Debug.Assert(y + height <= Height);

		return new(X, Y + y, Width, height);
	}

	public PixelArea SliceMax(int y, int height)
	{
		height = Math.Min(height, Height - y);

		return new(X, Y + y, Width, height);
	}

	public static PixelArea FromSize(int width, int height) => new(0, 0, width, height);

	public static implicit operator PixelArea(in Rectangle r) => new(r.X, r.Y, r.Width, r.Height);
	public static implicit operator Rectangle(in PixelArea a) => Unsafe.As<PixelArea, Rectangle>(ref Unsafe.AsRef(in a));

	public static implicit operator PixelArea(Size s) => FromSize(s.Width, s.Height);
}
