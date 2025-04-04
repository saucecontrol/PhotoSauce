// Copyright © Clinton Ingram and Contributors
// SPDX-License-Identifier: MIT

using PhotoSauce.MagicScaler.Converters;

using System;

namespace PhotoSauce.MagicScaler.Experimental;

/// <summary>Provides a mechanism for processing raw pixel data using caller-provided buffers.</summary>
public interface IPixelProcessor
{
	/// <summary>Processes the pixels in <paramref name="source"/> and stores the results in <paramref name="dest"/>.</summary>
	/// <param name="dest">A target memory buffer that will receive the pixel data.</param>
	/// <param name="source">A source memory buffer that pixel data will be read from.</param>
	void Process(Span<byte> dest, ReadOnlySpan<byte> source);
}

internal static class PixelProcessor
{
	public static IPixelProcessor FromConversionProcessor(IConversionProcessor processor)
	{
		return new ConversionProcessorWrapper(processor);
	}

	private sealed class ConversionProcessorWrapper(IConversionProcessor processor) : IPixelProcessor
	{
		private readonly IConversionProcessor processor = processor;

		public unsafe void Process(Span<byte> dest, ReadOnlySpan<byte> source)
		{
			// TODO: argument validation? how to validate that dest has enough bytes for source?

			fixed (byte* op = dest)
			fixed (byte* ip = source)
			{
				processor.ConvertLine(ip, op, source.Length);
			}
		}
	}
}
