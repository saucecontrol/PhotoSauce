using System;
using System.Drawing;

namespace PhotoSauce.MagicScaler.Transforms
{
	internal class PadTransformInternal : PixelSource
	{
		private readonly int bytesPerPixel;
		private readonly uint fill;
		private readonly PixelArea inner;

		public PadTransformInternal(PixelSource source, Color color, PixelArea innerArea, PixelArea outerArea) : base(source)
		{
			bytesPerPixel = Format.BitsPerPixel / 8;

			if (Format.NumericRepresentation != PixelNumericRepresentation.UnsignedInteger || Format.ChannelCount != bytesPerPixel)
				throw new NotSupportedException("Pixel format not supported.");

			fill = (uint)color.ToArgb();

			inner = innerArea;
			Width = outerArea.Width;
			Height = outerArea.Height;
		}

		unsafe protected override void CopyPixelsInternal(in PixelArea prc, int cbStride, int cbBufferSize, IntPtr pbBuffer)
		{
			int tx = Math.Max(prc.X - inner.X, 0);
			int tw = Math.Min(prc.Width, Math.Min(Math.Max(prc.X + prc.Width - inner.X, 0), inner.Width - tx));
			int cx = Math.Max(inner.X - prc.X, 0);
			byte* pb = (byte*)pbBuffer;

			for (int y = 0; y < prc.Height; y++)
			{
				int cy = prc.Y + y;

				if (tw < prc.Width || cy < inner.Y || cy >= inner.Y + inner.Height)
				{
					switch (bytesPerPixel)
					{
						case 1:
							new Span<byte>(pb, prc.Width).Fill((byte)fill);
							break;
						case 3:
							new Span<triple>(pb, prc.Width).Fill((triple)fill);
							break;
						case 4:
							new Span<uint>(pb, prc.Width).Fill(fill);
							break;
					}
				}

				if (tw > 0 && cy >= inner.Y && cy < inner.Y + inner.Height)
				{
					Profiler.PauseTiming();
					Source.CopyPixels(new PixelArea(tx, cy - inner.Y, tw, 1), cbStride, cbBufferSize, (IntPtr)(pb + cx * bytesPerPixel));
					Profiler.ResumeTiming();
				}

				pb += cbStride;
			}
		}
	}

	/// <summary>Adds solid-colored padding pixels to an image.</summary>
	public sealed class PadTransform : PixelTransformInternalBase, IPixelTransformInternal
	{
		private readonly Color padColor;
		private readonly Rectangle padRect;

		/// <summary>Constructs a new <see cref="PadTransform" /> using the specified <see cref="Color" /> and sizes.</summary>
		/// <param name="color">The <see cref="Color" /> of the padding pixels.</param>
		/// <param name="top">The number of pixels to add to the image top.</param>
		/// <param name="right">The number of pixels to add to the image right.</param>
		/// <param name="bottom">The number of pixels to add to the image bottom.</param>
		/// <param name="left">The number of pixels to add to the image left.</param>
		public PadTransform(Color color, int top, int right, int bottom, int left)
		{
			static void throwOutOfRange(string name) =>
				throw new ArgumentOutOfRangeException(name, $"Value must be between 0 and {short.MaxValue}");

			if ((uint)top > short.MaxValue) throwOutOfRange(nameof(top));
			if ((uint)right > short.MaxValue) throwOutOfRange(nameof(right));
			if ((uint)bottom > short.MaxValue) throwOutOfRange(nameof(bottom));
			if ((uint)left > short.MaxValue) throwOutOfRange(nameof(left));

			padColor = color;
			padRect = Rectangle.FromLTRB(left, top, right, bottom);
		}

		void IPixelTransformInternal.Init(PipelineContext ctx)
		{
			if (!padRect.IsEmpty)
			{
				MagicTransforms.AddExternalFormatConverter(ctx);

				var innerRect = new Rectangle(padRect.Left, padRect.Top, ctx.Source.Width, ctx.Source.Height);
				var outerRect = Rectangle.FromLTRB(0, 0, innerRect.Right + padRect.Right, innerRect.Bottom + padRect.Bottom);
				ctx.Source = new PadTransformInternal(ctx.Source, padColor, PixelArea.FromGdiRect(innerRect), PixelArea.FromGdiRect(outerRect));
			}

			Source = ctx.Source;
		}
	}
}
