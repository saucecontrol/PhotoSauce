using System;
using System.Drawing;

namespace PhotoSauce.MagicScaler
{
	internal class PadTransformInternal : PixelSource
	{
		private readonly int bytesPerPixel;
		private readonly uint bgra;
		private readonly byte fillB;
		private readonly byte fillG;
		private readonly byte fillR;
		private readonly Rectangle irect;

		public PadTransformInternal(PixelSource source, Color color, Rectangle innerRect, Rectangle outerRect) : base(source)
		{
			bytesPerPixel = Format.BitsPerPixel / 8;

			if (Format.NumericRepresentation != PixelNumericRepresentation.UnsignedInteger || Format.ChannelCount != bytesPerPixel)
				throw new NotSupportedException("Pixel format not supported.");

			bgra = (uint)color.ToArgb();
			fillB = color.B;
			fillG = color.G;
			fillR = color.R;

			irect = innerRect;
			Width = (uint)outerRect.Width;
			Height = (uint)outerRect.Height;
		}

		unsafe protected override void CopyPixelsInternal(in Rectangle prc, uint cbStride, uint cbBufferSize, IntPtr pbBuffer)
		{
			var trect = new Rectangle { X = Math.Max(prc.X - irect.X, 0), Height = 1 };
			trect.Width = Math.Min(prc.Width, Math.Min(Math.Max(prc.X + prc.Width - irect.X, 0), irect.Width - trect.X));
			int cx = Math.Max(irect.X - prc.X, 0);

			for (int y = 0; y < prc.Height; y++)
			{
				int cy = prc.Y + y;
				byte* pb = (byte*)(pbBuffer + y * (int)cbStride);

				if (trect.Width < prc.Width || cy < irect.Y || cy >= irect.Bottom)
				{
					if (bytesPerPixel == 1)
						new Span<byte>(pb, prc.Width).Fill(fillB);
					else if (bytesPerPixel == 4)
						new Span<uint>(pb, prc.Width).Fill(bgra);
					else
					{
						byte* pp = pb, pe = pb + prc.Width * 3;
						while (pp < pe)
						{
							pp[0] = fillB;
							pp[1] = fillG;
							pp[2] = fillR;
							pp += 3;
						}
					}
				}

				if (trect.Width > 0 && cy >= irect.Y && cy < irect.Bottom)
				{
					trect.Y = cy - irect.Y;
					Timer.Stop();
					Source.CopyPixels(trect, cbStride, cbBufferSize, (IntPtr)(pb + cx * bytesPerPixel));
					Timer.Start();
				}
			}
		}
	}

	/// <summary>Adds solid-colored padding pixels to an image.</summary>
	public sealed class PadTransform : PixelTransform, IPixelTransformInternal
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
			void throwOutOfRange(string name) =>
				throw new ArgumentOutOfRangeException(nameof(left), $"Value must be between 0 and {short.MaxValue}");

			if ((uint)top > short.MaxValue) throwOutOfRange(nameof(top));
			if ((uint)right > short.MaxValue) throwOutOfRange(nameof(right));
			if ((uint)bottom > short.MaxValue) throwOutOfRange(nameof(bottom));
			if ((uint)left > short.MaxValue) throwOutOfRange(nameof(left));

			padColor = color;
			padRect = Rectangle.FromLTRB(left, top, right, bottom);
		}

		void IPixelTransformInternal.Init(WicProcessingContext ctx)
		{
			if (!padRect.IsEmpty)
			{
				MagicTransforms.AddExternalFormatConverter(ctx);

				var innerRect = new Rectangle(padRect.Left, padRect.Top, (int)ctx.Source.Width, (int)ctx.Source.Height);
				var outerRect = Rectangle.FromLTRB(0, 0, innerRect.Right + padRect.Right, innerRect.Bottom + padRect.Bottom);
				ctx.Source = new PadTransformInternal(ctx.Source, padColor, innerRect, outerRect);
			}

			Source = ctx.Source;
		}
	}
}
