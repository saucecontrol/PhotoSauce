// Copyright © Clinton Ingram and Contributors.  Licensed under the MIT License.

using System;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Runtime.CompilerServices;

namespace PhotoSauce.MagicScaler;

/// <summary>A base <see cref="IPixelSource" /> implementation for wrapping a fully-decoded image bitmap in memory.</summary>
public abstract class BitmapPixelSource : IPixelSource, IDisposable
{
	/// <inheritdoc />
	public virtual Guid Format { get; }
	/// <inheritdoc />
	public virtual int Width { get; }
	/// <inheritdoc />
	public virtual int Height { get; }

	/// <summary>The number of bytes between pixels in adjacent bitmap rows.</summary>
	protected virtual int Stride { get; }

	/// <summary>Exposes the pixel data in the backing bitmap.</summary>
	/// <value>A <see cref="ReadOnlySpan{T}" /> instance that exposes the pixel data in memory.</value>
	protected abstract ReadOnlySpan<byte> Span { get; }

	/// <summary>Sets base properties of the <see cref="BitmapPixelSource" /> implementation.</summary>
	/// <param name="format">The format of the bitmap pixels.</param>
	/// <param name="width">The bitmap width, in pixels.</param>
	/// <param name="height">The bitmap height, in pixels.</param>
	/// <param name="stride">The number of bytes between pixels in adjacent bitmap rows.</param>
	protected BitmapPixelSource(Guid format, int width, int height, int stride)
	{
		Format = format;
		Width = width;
		Height = height;
		Stride = stride;
	}

	/// <inheritdoc />
	public virtual void CopyPixels(Rectangle sourceArea, int cbStride, Span<byte> buffer)
	{
		var (rx, ry, rw, rh) = (sourceArea.X, sourceArea.Y, sourceArea.Width, sourceArea.Height);
		int bpp = PixelFormat.FromGuid(Format).BytesPerPixel;
		int cb = rw * bpp;

		if (rx < 0 || ry < 0 || rw < 0 || rh < 0 || rx + rw > Width || ry + rh > Height)
			throw new ArgumentOutOfRangeException(nameof(sourceArea), "Requested area does not fall within the image bounds");

		if (cb > cbStride)
			throw new ArgumentOutOfRangeException(nameof(cbStride), "Stride is too small for the requested area");

		if ((rh - 1) * cbStride + cb > buffer.Length)
			throw new ArgumentOutOfRangeException(nameof(buffer), "Buffer is too small for the requested area");

		ref byte pixRef = ref Unsafe.Add(ref MemoryMarshal.GetReference(Span), ry * Stride + rx * bpp);
		for (int y = 0; y < rh; y++)
			Unsafe.CopyBlockUnaligned(ref buffer[y * cbStride], ref Unsafe.Add(ref pixRef, y * Stride), (uint)cb);
	}

	/// <inheritdoc />
	public void Dispose()
	{
		Dispose(true);
		GC.SuppressFinalize(this);
	}

	/// <inheritdoc cref="IDisposable.Dispose" />
	/// <param name="disposing">True if the method is being invoked from a call to <see cref="IDisposable.Dispose"/>, false if it is invoked from a finalizer.</param>
	protected virtual void Dispose(bool disposing) { }
}