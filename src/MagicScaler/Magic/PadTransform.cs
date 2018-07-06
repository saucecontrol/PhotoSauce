using System;
using System.Drawing;

#if DRAWING_SHIM
using System.Drawing.Temp;
#endif

using PhotoSauce.MagicScaler.Interop;

namespace PhotoSauce.MagicScaler
{
	internal class PadTransform : PixelSource
	{
		private readonly int Bpp;
		private readonly uint bgra;
		private readonly byte fillB;
		private readonly byte fillG;
		private readonly byte fillR;
		private readonly Rectangle irect;

		public PadTransform(PixelSource source, Color color, Rectangle innerRect, Rectangle outerRect) : base(source)
		{
			Bpp = Format.BitsPerPixel / 8;

			if (Format.NumericRepresentation != PixelNumericRepresentation.UnsignedInteger || Format.ChannelCount != Bpp)
				throw new NotSupportedException("Pixel format not supported.");

			bgra = (uint)color.ToArgb();
			fillB = color.B;
			fillG = color.G;
			fillR = color.R;

			irect = innerRect;

			Width = (uint)outerRect.Width;
			Height = (uint)outerRect.Height;
		}

		unsafe protected override void CopyPixelsInternal(WICRect prc, uint cbStride, uint cbBufferSize, IntPtr pbBuffer)
		{
			var trect = new WICRect { X = Math.Max(prc.X - irect.X, 0), Height = 1 };
			trect.Width = Math.Min(prc.Width, Math.Min(Math.Max(prc.X + prc.Width - irect.X, 0), irect.Width - trect.X));
			int cx = Math.Max(irect.X - prc.X, 0);

			for (int y = 0; y < prc.Height; y++)
			{
				int cy = prc.Y + y;
				byte* pb = (byte*)(pbBuffer + y * (int)cbStride);

				if (trect.Width < prc.Width || cy < irect.Y || cy >= irect.Bottom)
				{
					if (Bpp == 1)
						new Span<byte>(pb, prc.Width).Fill(fillB);
					else if (Bpp == 4)
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
					Source.CopyPixels(trect, cbStride, cbBufferSize, (IntPtr)(pb + cx * Bpp));
					Timer.Start();
				}
			}
		}
	}
}
