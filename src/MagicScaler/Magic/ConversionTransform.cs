using System;
using System.Buffers;

namespace PhotoSauce.MagicScaler
{
	internal interface IConverter
	{
		unsafe void ConvertLine(byte* istart, byte* ostart, int cb);
	}

	internal interface IConverter<TFrom, TTo> : IConverter where TFrom : unmanaged where TTo : unmanaged { }

	internal class ConversionTransform : PixelSource, IDisposable
	{
		private readonly IConverter processor;

		private byte[]? lineBuff;

		public ConversionTransform(PixelSource source, ColorProfile? sourceProfile, ColorProfile? destProfile, Guid destFormat) : base(source)
		{
			var srcProfile = sourceProfile as CurveProfile ?? ColorProfile.sRGB;
			var dstProfile = destProfile as CurveProfile ?? ColorProfile.sRGB;
			var srcFormat = source.Format;

			processor = null!;

			Format = PixelFormat.FromGuid(destFormat);
			if (srcFormat.BitsPerPixel != Format.BitsPerPixel)
			{
				lineBuff = ArrayPool<byte>.Shared.Rent(BufferStride);
				lineBuff.AsSpan().Clear();
			}

			if (srcFormat.ColorRepresentation == PixelColorRepresentation.Cmyk && srcFormat.BitsPerPixel == 64 && Format.BitsPerPixel == 32)
			{
				processor = NarrowingConverter.Instance;
			}
			else if (srcFormat.Colorspace == PixelColorspace.sRgb && Format.Colorspace == PixelColorspace.LinearRgb && !srcProfile.IsLinear)
			{
				if (Format.NumericRepresentation == PixelNumericRepresentation.Fixed)
					if (srcFormat.AlphaRepresentation != PixelAlphaRepresentation.None)
						processor = srcProfile.ConverterByteToUQ15.Processor3A;
					else
						processor = srcProfile.ConverterByteToUQ15.Processor;
				else if (srcFormat.NumericRepresentation == PixelNumericRepresentation.UnsignedInteger && Format.NumericRepresentation == PixelNumericRepresentation.Float)
					if (srcFormat.AlphaRepresentation != PixelAlphaRepresentation.None)
						processor = srcProfile.ConverterByteToFloat.Processor3A;
					else if (srcFormat.ChannelCount == 3 && Format.ChannelCount == 4)
						processor = srcProfile.ConverterByteToFloat.Processor3X;
					else
						processor = srcProfile.ConverterByteToFloat.Processor;
				else if (Format.NumericRepresentation == PixelNumericRepresentation.Float)
					if (srcFormat.AlphaRepresentation != PixelAlphaRepresentation.None)
						processor = srcProfile.ConverterFloatToFloatLinear.Processor3A;
					else
						processor = srcProfile.ConverterFloatToFloatLinear.Processor;
			}
			else if (srcFormat.Colorspace == PixelColorspace.LinearRgb && Format.Colorspace == PixelColorspace.sRgb && !dstProfile.IsLinear)
			{
				if (srcFormat.NumericRepresentation == PixelNumericRepresentation.Fixed)
					if (srcFormat.AlphaRepresentation != PixelAlphaRepresentation.None)
						processor = dstProfile.ConverterUQ15ToByte.Processor3A;
					else
						processor = dstProfile.ConverterUQ15ToByte.Processor;
				else if (srcFormat.NumericRepresentation == PixelNumericRepresentation.Float && Format.NumericRepresentation == PixelNumericRepresentation.UnsignedInteger)
					if (srcFormat.AlphaRepresentation != PixelAlphaRepresentation.None)
						processor = dstProfile.ConverterFloatToByte.Processor3A;
					else if (srcFormat.ChannelCount == 4 && Format.ChannelCount == 3)
						processor = dstProfile.ConverterFloatToByte.Processor3X;
					else
						processor = dstProfile.ConverterFloatToByte.Processor;
				else if (srcFormat.NumericRepresentation == PixelNumericRepresentation.Float)
					if (srcFormat.AlphaRepresentation != PixelAlphaRepresentation.None)
						processor = dstProfile.ConverterFloatLinearToFloat.Processor3A;
					else
						processor = dstProfile.ConverterFloatLinearToFloat.Processor;
			}
			else if (srcFormat.NumericRepresentation == PixelNumericRepresentation.UnsignedInteger && Format.NumericRepresentation == PixelNumericRepresentation.Float)
			{
				if (srcFormat.AlphaRepresentation == PixelAlphaRepresentation.Unassociated)
					processor = FloatConverter.Widening3A.Instance;
				else if (srcFormat.AlphaRepresentation == PixelAlphaRepresentation.None && srcFormat.ChannelCount == 3 && Format.ChannelCount == 4)
					processor = FloatConverter.Widening3X.Instance;
				else
					processor = FloatConverter.Widening.Instance;
			}
			else if (srcFormat.NumericRepresentation == PixelNumericRepresentation.Float && Format.NumericRepresentation == PixelNumericRepresentation.UnsignedInteger)
			{
				if (Format.AlphaRepresentation != PixelAlphaRepresentation.None)
					processor = FloatConverter.Narrowing3A.Instance;
				else if (srcFormat.AlphaRepresentation == PixelAlphaRepresentation.None && srcFormat.ChannelCount == 4 && Format.ChannelCount == 3)
					processor = FloatConverter.Narrowing3X.Instance;
				else
					processor = FloatConverter.Narrowing.Instance;
			}
			else if (srcFormat.NumericRepresentation == Format.NumericRepresentation && srcFormat.ChannelCount != Format.ChannelCount)
			{
				if (srcFormat.NumericRepresentation == PixelNumericRepresentation.Float)
					processor = ChannelChanger<float>.GetConverter(srcFormat.ChannelCount, Format.ChannelCount);
				else if (srcFormat.NumericRepresentation == PixelNumericRepresentation.Fixed)
					processor = ChannelChanger<ushort>.GetConverter(srcFormat.ChannelCount, Format.ChannelCount);
				else
					processor = ChannelChanger<byte>.GetConverter(srcFormat.ChannelCount, Format.ChannelCount);
			}

			if (processor is null)
				throw new NotSupportedException("Unsupported pixel format");
		}

		unsafe protected override void CopyPixelsInternal(in PixelArea prc, int cbStride, int cbBufferSize, IntPtr pbBuffer)
		{
			if (lineBuff != null)
				copyPixelsBuffered(prc, cbStride, cbBufferSize, pbBuffer);
			else
				copyPixelsDirect(prc, cbStride, cbBufferSize, pbBuffer);
		}

		unsafe private void copyPixelsBuffered(in PixelArea prc, int cbStride, int cbBufferSize, IntPtr pbBuffer)
		{
			fixed (byte* bstart = &lineBuff![0])
			{
				int cb = MathUtil.DivCeiling(prc.Width * Source.Format.BitsPerPixel, 8);

				for (int y = 0; y < prc.Height; y++)
				{
					Profiler.PauseTiming();
					Source.CopyPixels(new PixelArea(prc.X, prc.Y + y, prc.Width, 1), BufferStride, BufferStride, (IntPtr)bstart);
					Profiler.ResumeTiming();

					byte* op = (byte*)pbBuffer + y * cbStride;
					processor.ConvertLine(bstart, op, cb);
				}
			}
		}

		unsafe private void copyPixelsDirect(in PixelArea prc, int cbStride, int cbBufferSize, IntPtr pbBuffer)
		{
			int cb = MathUtil.DivCeiling(prc.Width * Source.Format.BitsPerPixel, 8);

			for (int y = 0; y < prc.Height; y++)
			{
				byte* op = (byte*)pbBuffer + y * cbStride;

				Profiler.PauseTiming();
				Source.CopyPixels(new PixelArea(prc.X, prc.Y + y, prc.Width, 1), cbStride, cb, (IntPtr)op);
				Profiler.ResumeTiming();

				processor.ConvertLine(op, op, cb);
			}
		}

		public void Dispose()
		{
			ArrayPool<byte>.Shared.Return(lineBuff ?? Array.Empty<byte>());
			lineBuff = null;
		}

		public override string ToString() => $"{base.ToString()}: {Source.Format.Name}->{Format.Name}";
	}

	/// <summary>Converts an image to an alternate pixel format.</summary>
	public sealed class FormatConversionTransform : PixelTransform, IPixelTransformInternal
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

		void IPixelTransformInternal.Init(PipelineContext ctx)
		{
			MagicTransforms.AddExternalFormatConverter(ctx);

			if (ctx.Source.Format.FormatGuid != outFormat)
				ctx.Source = ctx.AddDispose(new ConversionTransform(ctx.Source, null, null, outFormat));

			Source = ctx.Source;
		}
	}
}
