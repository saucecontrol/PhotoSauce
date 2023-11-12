// Copyright © Clinton Ingram and Contributors.  Licensed under the MIT License.

using System;
using System.Drawing;

namespace PhotoSauce.MagicScaler;

/// <summary>A sample <see cref="IPixelSource" /> implementation.  Creates a test pattern that resembles an inverted <a href="https://en.wikipedia.org/wiki/Zone_plate">Fresnel zone plate</a>.</summary>
/// <remarks>This pixel source produces a complex pattern that is useful for detecting subtle errors in processing.  It is minimally optimized in order to emulate an expensive decoder.</remarks>
public sealed class ZonePlatePixelSource : IPixelSource, IProfileSource
{
	private static readonly Guid[] formats = [ default, PixelFormats.Grey8bpp, default, PixelFormats.Bgr24bpp, PixelFormats.Bgra32bpp ];

	private readonly int channels;
	private readonly double diameter;
	private readonly IProfiler profiler;

	/// <inheritdoc />
	public Guid Format => formats[channels];
	/// <inheritdoc />
	public int Width { get; }
	/// <inheritdoc />
	public int Height { get; }

	IProfiler IProfileSource.Profiler => profiler;

	/// <summary>Constructs a new <see cref="ZonePlatePixelSource" /> using the specified settings.</summary>
	/// <param name="width">The image width in pixels. This value should be odd to allow the pattern to be perfectly centered.</param>
	/// <param name="height">The image height in pixels. This value should be odd to allow the pattern to be perfectly centered.</param>
	/// <param name="pixelFormat">The pixel format of the image.  Must be a member of <see cref="PixelFormats" />.</param>
	/// <param name="scale">Determines the diameter of the pattern.  A value of 1.0 scales the pattern to fill to the edges of the image's smaller dimension.</param>
	/// <remarks><paramref name="width" /> and <paramref name="height" /> values up to 65535 are allowed, although not all encoders support imgages of that size.</remarks>
	public ZonePlatePixelSource(int width, int height, Guid pixelFormat, double scale = 1.0)
	{
		channels = Array.IndexOf(formats, pixelFormat);
		if (channels < 1 || channels == 2) throw new ArgumentException("Unsupported pixel format", nameof(pixelFormat));
		if (width < 1 || width > ushort.MaxValue) throw new ArgumentOutOfRangeException(nameof(width), $"Value must be between 1 and {ushort.MaxValue}");
		if (height < 1 || height > ushort.MaxValue) throw new ArgumentOutOfRangeException(nameof(height), $"Value must be between 1 and {ushort.MaxValue}");

		Width = width;
		Height = height;
		diameter = (Math.Min(Width, Height) & ~1) * scale;
		profiler = StatsManager.GetProfiler(this);
	}

	private void copyPixels(in PixelArea area, int cbStride, Span<byte> buffer)
	{
		int xo = area.X - (Width >> 1);
		int yo = area.Y - (Height >> 1);

		double rm = 0.5 * diameter;
		double km = 0.7 / diameter * Math.PI;
		double w = rm / 10.0;

		for (int y = 0; y < area.Height; y++)
		{
			var row = buffer.Slice(y * cbStride);
			double yd = (y + yo) * (y + yo);

			for (int x = 0; x < area.Width; x++)
			{
				var col = row.Slice(x * channels);
				double xd = (x + xo) * (x + xo);

				double d = xd + yd;
				double v = 127.5 * (1.0 + (1.0 + Math.Tanh((rm - Math.Sqrt(d)) / w)) * Math.Sin(km * d) * 0.5);

				byte bv = (byte)(v + 0.5);
				byte bi = (byte)(bv ^ byte.MaxValue);

				switch (channels)
				{
					case 3:
						col[2] = bi;
						col[1] = bi;
						col[0] = bv;
						break;
					case 4:
						col[3] = bv;
						col[2] = 0;
						col[1] = 0;
						col[0] = 0;
						break;
					default:
						col[0] = bi;
						break;
				}
			}
		}
	}

	/// <inheritdoc />
	public void CopyPixels(Rectangle sourceArea, int cbStride, Span<byte> buffer)
	{
		var (rx, ry, rw, rh) = (sourceArea.X, sourceArea.Y, sourceArea.Width, sourceArea.Height);
		int cb = rw * channels;

		if (rx < 0 || ry < 0 || rw < 0 || rh < 0 || rx + rw > Width || ry + rh > Height)
			throw new ArgumentOutOfRangeException(nameof(sourceArea), "Requested area does not fall within the image bounds");

		if (cb > cbStride)
			throw new ArgumentOutOfRangeException(nameof(cbStride), "Stride is too small for the requested area");

		if ((rh - 1) * cbStride + cb > buffer.Length)
			throw new ArgumentOutOfRangeException(nameof(buffer), "Buffer is too small for the requested area");

		var area = (PixelArea)sourceArea;
		profiler.ResumeTiming(area);
		copyPixels(area, cbStride, buffer);
		profiler.PauseTiming();
	}

	/// <inheritdoc />
	public override string ToString() => nameof(ZonePlatePixelSource);
}
