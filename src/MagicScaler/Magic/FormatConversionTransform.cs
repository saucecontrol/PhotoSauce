using System;
using System.Buffers;

namespace PhotoSauce.MagicScaler
{
	internal interface IConverter
	{
		unsafe void ConvertLine(byte* istart, byte* ostart, int cb);
	}

	internal interface IConverter<TFrom, TTo> : IConverter where TFrom : unmanaged where TTo : unmanaged { }

	internal class FormatConversionTransformInternal : PixelSource, IDisposable
	{
		private readonly PixelFormat srcFormat;
		private readonly IConverter processor;
		private readonly int bpp;

		private byte[] lineBuff;

		public FormatConversionTransformInternal(PixelSource source, ColorProfile? sourceProfile, ColorProfile? destProfile, Guid destFormat) : base(source)
		{
			var srcProfile = sourceProfile as CurveProfile ?? ColorProfile.sRGB;
			var dstProfile = destProfile as CurveProfile ?? ColorProfile.sRGB;

			srcFormat = source.Format;
			bpp = srcFormat.BitsPerPixel;

			Format = PixelFormat.FromGuid(destFormat);
			lineBuff = ArrayPool<byte>.Shared.Rent(BufferStride);
			lineBuff.AsSpan().Clear();
			processor = null!;

			if (srcFormat.ColorRepresentation == PixelColorRepresentation.Cmyk && bpp == 64 && bpp == 32)
			{
				processor = NarrowingConverter.Instance;
			}
			else if (srcFormat.Colorspace == PixelColorspace.sRgb && Format.Colorspace == PixelColorspace.LinearRgb)
			{
				if (Format.NumericRepresentation == PixelNumericRepresentation.Fixed)
					if (srcFormat.AlphaRepresentation != PixelAlphaRepresentation.None)
						processor = srcProfile.ConverterByteToUQ15.Processor3A;
					else
						processor = srcProfile.ConverterByteToUQ15.Processor;
				else if (Format.NumericRepresentation == PixelNumericRepresentation.Float)
					// TODO if srcProfile.IsLinear, we can use normal byte to float
					if (srcFormat.AlphaRepresentation != PixelAlphaRepresentation.None)
						processor = srcProfile.ConverterByteToFloat.Processor3A;
					else if (srcFormat.ChannelCount == 3 && Format.ChannelCount == 4)
						processor = srcProfile.ConverterByteToFloat.Processor3X;
					else
						processor = srcProfile.ConverterByteToFloat.Processor;
			}
			else if (srcFormat.Colorspace == PixelColorspace.LinearRgb && Format.Colorspace == PixelColorspace.sRgb && !dstProfile.IsLinear)
			{
				// TODO add normal UQ15/Float to byte for when dstProfile.IsLinear?
				if (srcFormat.NumericRepresentation == PixelNumericRepresentation.Fixed)
					if (srcFormat.AlphaRepresentation != PixelAlphaRepresentation.None)
						processor = dstProfile.ConverterUQ15ToByte.Processor3A;
					else
						processor = dstProfile.ConverterUQ15ToByte.Processor;
				else if (srcFormat.NumericRepresentation == PixelNumericRepresentation.Float)
					if (srcFormat.AlphaRepresentation != PixelAlphaRepresentation.None)
						processor = dstProfile.ConverterFloatToByte.Processor3A;
					else if (srcFormat.ChannelCount == 4 && Format.ChannelCount == 3)
						processor = dstProfile.ConverterFloatToByte.Processor3X;
					else
						processor = dstProfile.ConverterFloatToByte.Processor;
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
			fixed (byte* bstart = &lineBuff[0])
			{
				int oh = prc.Height, oy = prc.Y;
				int cb = MathUtil.DivCeiling(prc.Width * bpp, 8);

				for (int y = 0; y < oh; y++)
				{
					Profiler.PauseTiming();
					Source.CopyPixels(new PixelArea(prc.X, oy + y, prc.Width, 1), BufferStride, BufferStride, (IntPtr)bstart);
					Profiler.ResumeTiming();

					byte* op = (byte*)pbBuffer + y * cbStride;
					processor.ConvertLine(bstart, op, cb);
				}
			}
		}

		public void Dispose()
		{
			ArrayPool<byte>.Shared.Return(lineBuff ?? Array.Empty<byte>());
			lineBuff = null!;
		}

		public override string ToString() => $"{base.ToString()}: {srcFormat.Name}->{Format.Name}";
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
				ctx.Source = ctx.AddDispose(new FormatConversionTransformInternal(ctx.Source, null, null, outFormat));

			Source = ctx.Source;
		}
	}
}
