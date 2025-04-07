// Copyright © Clinton Ingram and Contributors
// SPDX-License-Identifier: MIT

using PhotoSauce.MagicScaler.Converters;

using System;

namespace PhotoSauce.MagicScaler.Experimental;

/// <summary>Provides a mechanism for processing raw pixel data using caller-provided buffers.</summary>
[CLSCompliant(false)]
public interface IPixelProcessor
{
	/// <summary>Processes the pixel data in the input buffer and stores the results in the output buffer.</summary>
	/// <param name="istart">A pointer to the input buffer.</param>
	/// <param name="cbi">The size, in bytes, of the input buffer.</param>
	/// <param name="ostart">A pointer to the output buffer.</param>
	/// <param name="cbo">The size, in bytes, of the input buffer.</param>
	/// <exception cref="ArgumentException">Thrown when the output buffer is too small.</exception>
	unsafe void Process(byte* istart, nint cbi, byte* ostart, nint cbo);
}

internal static class PixelProcessor
{
	public static IPixelProcessor FromConversionProcessor(IConversionProcessor processor, PixelFormat sourceFormat, PixelFormat destFormat)
	{
		return new ConversionProcessorWrapper(processor, sourceFormat, destFormat);
	}

	private sealed class ConversionProcessorWrapper(IConversionProcessor processor, PixelFormat sourceFormat, PixelFormat destFormat) : IPixelProcessor
	{
		private readonly IConversionProcessor processor = processor;
		private readonly PixelFormat sourceFormat = sourceFormat;
		private readonly PixelFormat destFormat = destFormat;

		public unsafe void Process(byte* istart, nint cbi, byte* ostart, nint cbo)
		{
			nint icount = MathUtil.DivCeiling(cbi * 8, sourceFormat.BitsPerPixel);
			nint ocount = MathUtil.DivCeiling(cbo * 8, destFormat.BitsPerPixel);
			if (ocount < icount)
				throw new ArgumentException($"Output buffer is too small");

			if (sourceFormat.BitsPerPixel == destFormat.BitsPerPixel)
			{
				// Converters that operate on same-sized elements assume that istart==ostart, e.g. the conversion is only done in-place.
				// In order to avoid leaking that implementation detail, copy the input to output and then process output in-place.
				if (istart != ostart)
					Buffer.MemoryCopy(istart, ostart, cbo, cbi);
				processor.ConvertLine(ostart, ostart, cbi);
			}
			else
				processor.ConvertLine(istart, ostart, cbi);
		}
	}
}
