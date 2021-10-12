// Copyright © Clinton Ingram and Contributors.  Licensed under the MIT License.

#if NETFRAMEWORK || GDIPROCESSOR
#pragma warning disable CS1591
using System.IO;
using System.Drawing;
using System.Drawing.Imaging;
using System.Drawing.Drawing2D;

using GdiPixelFormat = System.Drawing.Imaging.PixelFormat;

namespace PhotoSauce.MagicScaler
{
#if GDIPROCESSOR
	public
#else
	internal
#endif
	static class GdiUtil
	{
		public static void CreateBrokenImage(Stream ostm, ProcessImageSettings s)
		{
			s = s.Clone();
			if (s.Width == 0 && s.Height == 0)
				s.Height = s.Width = 100;

			s.Fixup(s.Width > 0 ? s.Width : s.Height, s.Height > 0 ? s.Height : s.Width);

			using var bmp = new Bitmap(s.Width, s.Height, GdiPixelFormat.Format24bppRgb);
			using var gfx = Graphics.FromImage(bmp);
			using var pen = new Pen(Brushes.White, 1.75f);

			gfx.Clear(Color.Gainsboro);
			gfx.SmoothingMode = SmoothingMode.AntiAlias;
			gfx.PixelOffsetMode = PixelOffsetMode.Half;
			gfx.CompositingQuality = CompositingQuality.GammaCorrected;

			float l = 0.5f, t = 0.5f, r = s.Width - 0.5f, b = s.Height - 0.5f;
			gfx.DrawLines(pen, new[] { new PointF(l, t), new PointF(r, b), new PointF(l, b), new PointF(r, t) });
			gfx.DrawLines(pen, new[] { new PointF(l, b), new PointF(l, t), new PointF(r, t), new PointF(r, b) });
			bmp.Save(ostm, ImageFormat.Png);
		}
	}
}
#endif