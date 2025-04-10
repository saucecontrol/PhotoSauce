// Copyright © Clinton Ingram and Contributors
// SPDX-License-Identifier: MIT

using System;

using PhotoSauce.MagicScaler.Converters;

namespace PhotoSauce.MagicScaler.Transforms;

internal sealed class ConversionTransform : ChainedPixelSource
{
	private readonly IConversionProcessor processor;

	public override PixelFormat Format { get; }

	public ConversionTransform(PixelSource source, PixelFormat destFormat, ColorProfile? sourceProfile = null, ColorProfile? destProfile = null) : base(source)
	{
		Format = destFormat;
		processor = CreateProcessor(source.Format, destFormat, sourceProfile, destProfile);
	}

	public static IConversionProcessor CreateProcessor(PixelFormat srcFormat, PixelFormat destFormat, ColorProfile? sourceProfile = null, ColorProfile? destProfile = null)
	{
		IConversionProcessor? processor = null;

		if (srcFormat.ColorRepresentation == PixelColorRepresentation.Cmyk && srcFormat.BitsPerPixel == 64 && destFormat.BitsPerPixel == 32)
		{
			processor = NarrowingConverter.Instance;
		}
		else if (srcFormat.Range != destFormat.Range && srcFormat.BitsPerPixel == 8 && destFormat.BitsPerPixel == 8)
		{
			if (srcFormat.Range == PixelValueRange.Video)
				processor = srcFormat.Encoding == PixelValueEncoding.Chroma ? VideoChromaConverter.VideoToFullRangeProcessor.Instance : VideoLumaConverter.VideoToFullRangeProcessor.Instance;
			else
				processor = srcFormat.Encoding == PixelValueEncoding.Chroma ? VideoChromaConverter.FullRangeToVideoProcessor.Instance : VideoLumaConverter.FullRangeToVideoProcessor.Instance;
		}
		else if (srcFormat.Encoding == PixelValueEncoding.Companded && destFormat.Encoding == PixelValueEncoding.Linear)
		{
			var srcProfile = sourceProfile as CurveProfile ?? ColorProfile.sRGB;
			if (destFormat.NumericRepresentation == PixelNumericRepresentation.Fixed)
			{
				var conv = srcFormat.Range == PixelValueRange.Video
					? srcProfile.GetConverter<byte, ushort, EncodingType.Companded, EncodingRange.Video>()
					: srcProfile.GetConverter<byte, ushort, EncodingType.Companded>();
				if (srcFormat.AlphaRepresentation != PixelAlphaRepresentation.None)
					processor = conv.Processor3A;
				else
					processor = conv.Processor;
			}
			else if (srcFormat.NumericRepresentation == PixelNumericRepresentation.UnsignedInteger && destFormat.NumericRepresentation == PixelNumericRepresentation.Float)
			{
				var conv = srcFormat.Range == PixelValueRange.Video
					? srcProfile.GetConverter<byte, float, EncodingType.Companded, EncodingRange.Video>()
					: srcProfile.GetConverter<byte, float, EncodingType.Companded>();
				if (srcFormat.AlphaRepresentation != PixelAlphaRepresentation.None)
					processor = conv.Processor3A;
				else if (srcFormat.ChannelCount == 3 && destFormat.ChannelCount == 4)
					processor = conv.Processor3X;
				else
					processor = conv.Processor;
			}
			else if (destFormat.NumericRepresentation == PixelNumericRepresentation.Float)
			{
				var conv = srcProfile.GetConverter<float, float, EncodingType.Companded>();
				if (srcFormat.AlphaRepresentation != PixelAlphaRepresentation.None)
					processor = conv.Processor3A;
				else
					processor = conv.Processor;
			}
		}
		else if (srcFormat.Encoding == PixelValueEncoding.Linear && destFormat.Encoding == PixelValueEncoding.Companded)
		{
			var dstProfile = destProfile as CurveProfile ?? ColorProfile.sRGB;
			if (srcFormat.NumericRepresentation == PixelNumericRepresentation.Fixed)
			{
				var conv = destFormat.Range == PixelValueRange.Video
					? dstProfile.GetConverter<ushort, byte, EncodingType.Linear, EncodingRange.Video>()
					: dstProfile.GetConverter<ushort, byte, EncodingType.Linear>();
				if (srcFormat.AlphaRepresentation != PixelAlphaRepresentation.None)
					processor = conv.Processor3A;
				else
					processor = conv.Processor;
			}
			else if (srcFormat.NumericRepresentation == PixelNumericRepresentation.Float && destFormat.NumericRepresentation == PixelNumericRepresentation.UnsignedInteger)
			{
				var conv = destFormat.Range == PixelValueRange.Video
					? dstProfile.GetConverter<float, byte, EncodingType.Linear, EncodingRange.Video>()
					: dstProfile.GetConverter<float, byte, EncodingType.Linear>();
				if (srcFormat.AlphaRepresentation != PixelAlphaRepresentation.None)
					processor = conv.Processor3A;
				else if (srcFormat.ChannelCount == 4 && destFormat.ChannelCount == 3)
					processor = conv.Processor3X;
				else
					processor = conv.Processor;
			}
			else if (srcFormat.NumericRepresentation == PixelNumericRepresentation.Float)
			{
				var conv = dstProfile.GetConverter<float, float, EncodingType.Linear>();
				if (srcFormat.AlphaRepresentation != PixelAlphaRepresentation.None)
					processor = conv.Processor3A;
				else
					processor = conv.Processor;
			}
		}
		else if (srcFormat.NumericRepresentation == PixelNumericRepresentation.UnsignedInteger && destFormat.NumericRepresentation == PixelNumericRepresentation.Float)
		{
			if (srcFormat.AlphaRepresentation == PixelAlphaRepresentation.Unassociated)
				processor = FloatConverter.Widening.InstanceFullRange.Processor3A;
			else if (srcFormat.AlphaRepresentation == PixelAlphaRepresentation.None && srcFormat.ChannelCount == 3 && destFormat.ChannelCount == 4)
				processor = FloatConverter.Widening.InstanceFullRange.Processor3X;
			else if (srcFormat.Encoding == PixelValueEncoding.Chroma)
				processor = srcFormat.Range == PixelValueRange.Full ? FloatConverter.Widening.InstanceFullChroma.Processor : FloatConverter.Widening.InstanceVideoChroma.Processor;
			else
				processor = srcFormat.Range == PixelValueRange.Full ? FloatConverter.Widening.InstanceFullRange.Processor : FloatConverter.Widening.InstanceVideoRange.Processor;
		}
		else if (srcFormat.NumericRepresentation == PixelNumericRepresentation.Float && destFormat.NumericRepresentation == PixelNumericRepresentation.UnsignedInteger)
		{
			if (destFormat.AlphaRepresentation != PixelAlphaRepresentation.None)
				processor = FloatConverter.Narrowing.InstanceFullRange.Processor3A;
			else if (srcFormat.AlphaRepresentation == PixelAlphaRepresentation.None && srcFormat.ChannelCount == 4 && destFormat.ChannelCount == 3)
				processor = FloatConverter.Narrowing.InstanceFullRange.Processor3X;
			else if (destFormat.Encoding == PixelValueEncoding.Chroma)
				processor = destFormat.Range == PixelValueRange.Full ? FloatConverter.Narrowing.InstanceFullChroma.Processor : FloatConverter.Narrowing.InstanceVideoChroma.Processor;
			else
				processor = destFormat.Range == PixelValueRange.Full ? FloatConverter.Narrowing.InstanceFullRange.Processor : FloatConverter.Narrowing.InstanceVideoRange.Processor;
		}
		else if (srcFormat.NumericRepresentation == destFormat.NumericRepresentation)
		{
			if ((srcFormat.ColorRepresentation is PixelColorRepresentation.Bgr && destFormat.ColorRepresentation is PixelColorRepresentation.Rgb) ||
					(srcFormat.ColorRepresentation is PixelColorRepresentation.Rgb && destFormat.ColorRepresentation is PixelColorRepresentation.Bgr)
				)
			{
				if (srcFormat.NumericRepresentation == PixelNumericRepresentation.Float)
					processor = Swizzlers<float>.GetSwapConverter(srcFormat.ChannelCount, destFormat.ChannelCount);
				else if (srcFormat.NumericRepresentation == PixelNumericRepresentation.Fixed)
					processor = Swizzlers<ushort>.GetSwapConverter(srcFormat.ChannelCount, destFormat.ChannelCount);
				else
					processor = Swizzlers<byte>.GetSwapConverter(srcFormat.ChannelCount, destFormat.ChannelCount);
			}
			else if (srcFormat.ChannelCount != destFormat.ChannelCount)
			{
				if (srcFormat.NumericRepresentation == PixelNumericRepresentation.Float)
					processor = Swizzlers<float>.GetConverter(srcFormat.ChannelCount, destFormat.ChannelCount);
				else if (srcFormat.NumericRepresentation == PixelNumericRepresentation.Fixed)
					processor = Swizzlers<ushort>.GetConverter(srcFormat.ChannelCount, destFormat.ChannelCount);
				else
					processor = Swizzlers<byte>.GetConverter(srcFormat.ChannelCount, destFormat.ChannelCount);
			}
			else if (srcFormat.IsColorCompatibleWith(destFormat))
			{
				processor = NoopConverter.Instance.Processor;
			}
		}

		if (processor is null)
			throw new NotSupportedException($"Unsupported conversion: {srcFormat.Name}->{destFormat.Name}");

		return processor;
	}

	public override bool IsCompatible(PixelSource newSource) => PrevSource.Format == newSource.Format;

	protected override unsafe void CopyPixelsInternal(in PixelArea prc, int cbStride, int cbBufferSize, byte* pbBuffer)
	{
		if (PrevSource.Format.BitsPerPixel > Format.BitsPerPixel)
			copyPixelsBuffered(prc, cbStride, pbBuffer);
		else
			copyPixelsDirect(prc, cbStride, pbBuffer);
	}

	private unsafe void copyPixelsBuffered(in PixelArea prc, int cbStride, byte* pbBuffer)
	{
		int buffStride = BufferStride;
		using var buff = BufferPool.RentLocalAligned<byte>(buffStride);

		fixed (byte* bstart = buff.Span)
		{
			int cb = MathUtil.DivCeiling(prc.Width * PrevSource.Format.BitsPerPixel, 8);

			for (int y = 0; y < prc.Height; y++)
			{
				Profiler.PauseTiming();
				PrevSource.CopyPixels(prc.Slice(y, 1), buffStride, buffStride, bstart);
				Profiler.ResumeTiming();

				byte* op = pbBuffer + y * cbStride;
				processor.ConvertLine(bstart, op, cb);
			}
		}
	}

	private unsafe void copyPixelsDirect(in PixelArea prc, int cbStride, byte* pbBuffer)
	{
		int cbi = MathUtil.DivCeiling(prc.Width * PrevSource.Format.BitsPerPixel, 8);
		int cbo = MathUtil.DivCeiling(prc.Width * Format.BitsPerPixel, 8);

		for (int y = 0; y < prc.Height; y++)
		{
			byte* op = pbBuffer + y * cbStride;
			byte* ip = op + cbo - cbi;

			Profiler.PauseTiming();
			PrevSource.CopyPixels(prc.Slice(y, 1), cbStride, cbi, ip);
			Profiler.ResumeTiming();

			processor.ConvertLine(ip, op, cbi);
		}
	}

	public override string ToString() => $"{nameof(ConversionTransform)}: {PrevSource.Format.Name}->{Format.Name}";
}

/// <summary>Converts an image to an alternate pixel format.</summary>
public sealed class FormatConversionTransform : PixelTransformInternalBase
{
	private readonly Guid outFormat;

	/// <summary>Constructs a new <see cref="FormatConversionTransform" /> using the specified <paramref name="outFormat" />.</summary>
	/// <param name="outFormat">The desired output format.  Must be a member of <see cref="PixelFormats" />.</param>
	public FormatConversionTransform(Guid outFormat)
	{
		if (outFormat != PixelFormats.Grey8bpp && outFormat != PixelFormats.Bgr24bpp && outFormat != PixelFormats.Bgra32bpp)
			throw new NotSupportedException("Unsupported pixel format");

		this.outFormat = outFormat;
	}

	internal override void Init(PipelineContext ctx)
	{
		MagicTransforms.AddExternalFormatConverter(ctx);

		if (ctx.Source.Format.FormatGuid != outFormat)
			ctx.Source = ctx.AddProfiler(new ConversionTransform(ctx.Source, PixelFormat.FromGuid(outFormat)));

		Source = ctx.Source;
	}
}
