// Copyright © Clinton Ingram and Contributors.  Licensed under the MIT License.

using System;

namespace PhotoSauce.MagicScaler.Transforms
{
	internal sealed class ConversionTransform : ChainedPixelSource, IDisposable
	{
		private readonly IConversionProcessor processor;

		private ArraySegment<byte> lineBuff;

		public override PixelFormat Format { get; }

		public ConversionTransform(PixelSource source, ColorProfile? sourceProfile, ColorProfile? destProfile, PixelFormat destFormat, bool videoLevels = false) : base(source)
		{
			var srcProfile = sourceProfile as CurveProfile ?? ColorProfile.sRGB;
			var dstProfile = destProfile as CurveProfile ?? ColorProfile.sRGB;
			var srcFormat = source.Format;

			processor = null!;

			Format = destFormat;
			if (srcFormat.BitsPerPixel != Format.BitsPerPixel)
			{
				lineBuff = BufferPool.Rent(BufferStride, true);
				lineBuff.AsSpan().Clear();
			}

			if (srcFormat.ColorRepresentation == PixelColorRepresentation.Cmyk && srcFormat.BitsPerPixel == 64 && Format.BitsPerPixel == 32)
			{
				processor = NarrowingConverter.Instance;
			}
			else if (videoLevels && srcFormat.BitsPerPixel == 8 && Format.BitsPerPixel == 8)
			{
				processor = VideoLevelsConverter.Instance;
			}
			else if (srcFormat.Encoding == PixelValueEncoding.Companded && Format.Encoding == PixelValueEncoding.Linear)
			{
				if (Format.NumericRepresentation == PixelNumericRepresentation.Fixed)
					if (srcFormat.AlphaRepresentation != PixelAlphaRepresentation.None)
						processor = srcProfile.GetConverter<byte, ushort>(ConverterDirection.ToLinear).Processor3A;
					else
						processor = srcProfile.GetConverter<byte, ushort>(ConverterDirection.ToLinear, videoLevels).Processor;
				else if (srcFormat.NumericRepresentation == PixelNumericRepresentation.UnsignedInteger && Format.NumericRepresentation == PixelNumericRepresentation.Float)
					if (srcFormat.AlphaRepresentation != PixelAlphaRepresentation.None)
						processor = srcProfile.GetConverter<byte, float>(ConverterDirection.ToLinear).Processor3A;
					else if (srcFormat.ChannelCount == 3 && Format.ChannelCount == 4)
						processor = srcProfile.GetConverter<byte, float>(ConverterDirection.ToLinear).Processor3X;
					else
						processor = srcProfile.GetConverter<byte, float>(ConverterDirection.ToLinear, videoLevels).Processor;
				else if (Format.NumericRepresentation == PixelNumericRepresentation.Float)
					if (srcFormat.AlphaRepresentation != PixelAlphaRepresentation.None)
						processor = srcProfile.GetConverter<float, float>(ConverterDirection.ToLinear).Processor3A;
					else
						processor = srcProfile.GetConverter<float, float>(ConverterDirection.ToLinear).Processor;
			}
			else if (srcFormat.Encoding == PixelValueEncoding.Linear && Format.Encoding == PixelValueEncoding.Companded)
			{
				if (srcFormat.NumericRepresentation == PixelNumericRepresentation.Fixed)
					if (srcFormat.AlphaRepresentation != PixelAlphaRepresentation.None)
						processor = dstProfile.GetConverter<ushort, byte>(ConverterDirection.FromLinear).Processor3A;
					else
						processor = dstProfile.GetConverter<ushort, byte>(ConverterDirection.FromLinear).Processor;
				else if (srcFormat.NumericRepresentation == PixelNumericRepresentation.Float && Format.NumericRepresentation == PixelNumericRepresentation.UnsignedInteger)
					if (srcFormat.AlphaRepresentation != PixelAlphaRepresentation.None)
						processor = dstProfile.GetConverter<float, byte>(ConverterDirection.FromLinear).Processor3A;
					else if (srcFormat.ChannelCount == 4 && Format.ChannelCount == 3)
						processor = dstProfile.GetConverter<float, byte>(ConverterDirection.FromLinear).Processor3X;
					else
						processor = dstProfile.GetConverter<float, byte>(ConverterDirection.FromLinear).Processor;
				else if (srcFormat.NumericRepresentation == PixelNumericRepresentation.Float)
					if (srcFormat.AlphaRepresentation != PixelAlphaRepresentation.None)
						processor = dstProfile.GetConverter<float, float>(ConverterDirection.FromLinear).Processor3A;
					else
						processor = dstProfile.GetConverter<float, float>(ConverterDirection.FromLinear).Processor;
			}
			else if (srcFormat.NumericRepresentation == PixelNumericRepresentation.UnsignedInteger && Format.NumericRepresentation == PixelNumericRepresentation.Float)
			{
				if (srcFormat.AlphaRepresentation == PixelAlphaRepresentation.Unassociated)
					processor = FloatConverter.Widening.InstanceFullRange.Processor3A;
				else if (srcFormat.AlphaRepresentation == PixelAlphaRepresentation.None && srcFormat.ChannelCount == 3 && Format.ChannelCount == 4)
					processor = FloatConverter.Widening.InstanceFullRange.Processor3X;
				else
					processor = videoLevels ? FloatConverter.Widening.InstanceVideoRange.Processor : FloatConverter.Widening.InstanceFullRange.Processor;
			}
			else if (srcFormat.NumericRepresentation == PixelNumericRepresentation.Float && Format.NumericRepresentation == PixelNumericRepresentation.UnsignedInteger)
			{
				if (Format.AlphaRepresentation != PixelAlphaRepresentation.None)
					processor = FloatConverter.Narrowing.Instance.Processor3A;
				else if (srcFormat.AlphaRepresentation == PixelAlphaRepresentation.None && srcFormat.ChannelCount == 4 && Format.ChannelCount == 3)
					processor = FloatConverter.Narrowing.Instance.Processor3X;
				else
					processor = FloatConverter.Narrowing.Instance.Processor;
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

		protected override void CopyPixelsInternal(in PixelArea prc, int cbStride, int cbBufferSize, IntPtr pbBuffer)
		{
			if (PrevSource.Format.BitsPerPixel != Format.BitsPerPixel)
				copyPixelsBuffered(prc, cbStride, cbBufferSize, pbBuffer);
			else
				copyPixelsDirect(prc, cbStride, cbBufferSize, pbBuffer);
		}

		private unsafe void copyPixelsBuffered(in PixelArea prc, int cbStride, int cbBufferSize, IntPtr pbBuffer)
		{
			if (lineBuff.Array is null) throw new ObjectDisposedException(nameof(ConversionTransform));

			fixed (byte* bstart = &lineBuff.Array[lineBuff.Offset])
			{
				int cb = MathUtil.DivCeiling(prc.Width * PrevSource.Format.BitsPerPixel, 8);

				for (int y = 0; y < prc.Height; y++)
				{
					Profiler.PauseTiming();
					PrevSource.CopyPixels(new PixelArea(prc.X, prc.Y + y, prc.Width, 1), lineBuff.Count, lineBuff.Count, (IntPtr)bstart);
					Profiler.ResumeTiming();

					byte* op = (byte*)pbBuffer + y * cbStride;
					processor.ConvertLine(bstart, op, cb);
				}
			}
		}

		private unsafe void copyPixelsDirect(in PixelArea prc, int cbStride, int cbBufferSize, IntPtr pbBuffer)
		{
			int cb = MathUtil.DivCeiling(prc.Width * PrevSource.Format.BitsPerPixel, 8);

			for (int y = 0; y < prc.Height; y++)
			{
				byte* op = (byte*)pbBuffer + y * cbStride;

				Profiler.PauseTiming();
				PrevSource.CopyPixels(new PixelArea(prc.X, prc.Y + y, prc.Width, 1), cbStride, cb, (IntPtr)op);
				Profiler.ResumeTiming();

				processor.ConvertLine(op, op, cb);
			}
		}

		public void Dispose()
		{
			BufferPool.Return(lineBuff);
			lineBuff = default;
		}

		public override string ToString() => $"{nameof(ConversionTransform)}: {PrevSource.Format.Name}->{Format.Name}";
	}

	/// <summary>Converts an image to an alternate pixel format.</summary>
	public sealed class FormatConversionTransform : PixelTransformInternalBase, IPixelTransformInternal
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
				ctx.Source = ctx.AddDispose(new ConversionTransform(ctx.Source, null, null, PixelFormat.FromGuid(outFormat)));

			Source = ctx.Source;
		}
	}
}
