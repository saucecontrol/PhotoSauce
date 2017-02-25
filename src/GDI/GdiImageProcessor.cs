#if NET46
using System;
using System.IO;
using System.Linq;
using System.Drawing;
using System.Drawing.Imaging;
using System.Drawing.Drawing2D;

namespace PhotoSauce.MagicScaler
{
	public static class GdiImageProcessor
	{
		private const int exifOrientationID = 274;
		private static readonly ImageCodecInfo[] codecs = ImageCodecInfo.GetImageEncoders();
		private static readonly ImageCodecInfo jpegCodec = codecs.First(c => c.FormatID == ImageFormat.Jpeg.Guid);
		private static readonly ImageCodecInfo tiffCodec = codecs.First(c => c.FormatID == ImageFormat.Tiff.Guid);

		public static void ExifRotate(this Image img)
		{
			if (!img.PropertyIdList.Contains(exifOrientationID))
				return;

			var prop = img.GetPropertyItem(exifOrientationID);
			int val = BitConverter.ToUInt16(prop.Value, 0);
			var rot = RotateFlipType.RotateNoneFlipNone;

			if (val == 3 || val == 4)
				rot = RotateFlipType.Rotate180FlipNone;
			else if (val == 5 || val == 6)
				rot = RotateFlipType.Rotate90FlipNone;
			else if (val == 7 || val == 8)
				rot = RotateFlipType.Rotate270FlipNone;

			if (val == 2 || val == 4 || val == 5 || val == 7)
				rot |= RotateFlipType.RotateNoneFlipX;

			if (rot != RotateFlipType.RotateNoneFlipNone)
				img.RotateFlip(rot);
		}

		public static void ProcessImage(string imgPath, Stream ostm, ProcessImageSettings s)
		{
			using (var fs = new FileStream(imgPath, FileMode.Open, FileAccess.Read, FileShare.Read))
				ProcessImage(fs, ostm, s);
		}

		public static void ProcessImage(byte[] img, Stream ostm, ProcessImageSettings s)
		{
			using (var ms = new MemoryStream(img, false))
				ProcessImage(ms, ostm, s);
		}

		public static void ProcessImage(Stream istm, Stream ostm, ProcessImageSettings s)
		{
			using (var img = Image.FromStream(istm, true, false))
			{
				if (s.FrameIndex > 0)
				{
					var fd = img.RawFormat.Guid == ImageFormat.Gif.Guid ? FrameDimension.Time : FrameDimension.Page;
					if (img.GetFrameCount(fd) > s.FrameIndex)
						img.SelectActiveFrame(fd, s.FrameIndex);
					else
						throw new ArgumentOutOfRangeException("Invalid Frame Index");
				}

				img.ExifRotate();
				s.Fixup(img.Width, img.Height);
				bool alpha = ((ImageFlags)img.Flags & ImageFlags.HasAlpha) == ImageFlags.HasAlpha;

				var src = img;
				var crop = s.Crop;
				if (s.HybridScaleRatio > 1d)
				{
					int intw = (int)Math.Ceiling(img.Width / s.HybridScaleRatio);
					int inth = (int)Math.Ceiling(img.Height / s.HybridScaleRatio);

					var bmp = new Bitmap(intw, inth);
					using (var gfx = Graphics.FromImage(bmp))
					{
						gfx.PixelOffsetMode = PixelOffsetMode.Half;
						gfx.CompositingMode = CompositingMode.SourceCopy;
						gfx.DrawImage(img, Rectangle.FromLTRB(0, 0, intw, inth), crop.X, crop.Y, crop.Width, crop.Height, GraphicsUnit.Pixel);
					}

					img.Dispose();
					src = bmp;
					crop = new Rectangle(0, 0, intw, inth);
				}

				using (src)
				using (var iat = new ImageAttributes())
				using (var bmp = new Bitmap(s.Width, s.Height, alpha ? PixelFormat.Format32bppArgb : PixelFormat.Format24bppRgb))
				using (var gfx = Graphics.FromImage(bmp))
				{
					iat.SetWrapMode(WrapMode.TileFlipXY);
					gfx.PixelOffsetMode = PixelOffsetMode.Half;
					gfx.CompositingMode = CompositingMode.SourceCopy;
					gfx.InterpolationMode = InterpolationMode.HighQualityBicubic;

					if (alpha && !s.MatteColor.IsEmpty)
					{
						gfx.Clear(s.MatteColor);
						gfx.CompositingMode = CompositingMode.SourceOver;
						gfx.CompositingQuality = CompositingQuality.GammaCorrected;
					}

					gfx.DrawImage(src, Rectangle.FromLTRB(0, 0, s.Width, s.Height), crop.X, crop.Y, crop.Width, crop.Height, GraphicsUnit.Pixel, iat);

					switch (s.SaveFormat)
					{
						case FileFormat.Bmp:
							bmp.Save(ostm, ImageFormat.Bmp);
							break;
						case FileFormat.Tiff:
							using (var encoderParams = new EncoderParameters(1))
							using (var param = new EncoderParameter(Encoder.Compression, (long)EncoderValue.CompressionNone))
							{
								encoderParams.Param[0] = param;
								bmp.Save(ostm, tiffCodec, encoderParams);
							}
							break;
						case FileFormat.Jpeg:
							using (var encoderParams = new EncoderParameters(1))
							using (var param = new EncoderParameter(Encoder.Quality, s.JpegQuality))
							{
								encoderParams.Param[0] = param;
								bmp.Save(ostm, jpegCodec, encoderParams);
							}
							break;
						default:
							if (s.IndexedColor)
								bmp.Save(ostm, ImageFormat.Gif);
							else
								bmp.Save(ostm, ImageFormat.Png);
							break;
					}
				}
			}
		}

		public static Stream CreateBrokenImage(ProcessImageSettings s)
		{
			s.Fixup(s.Width > 0 ? s.Width : s.Height, s.Height > 0 ? s.Height : s.Width);
			if (s.Width <= 0)
				s.Height = s.Width = 100;

			using (var bmp = new Bitmap(s.Width, s.Height, PixelFormat.Format24bppRgb))
			using (var gfx = Graphics.FromImage(bmp))
			using (var pen = new Pen(Brushes.White, 1.75f))
			using (var ms = new MemoryStream(8192))
			{
				gfx.FillRectangle(Brushes.Gainsboro, Rectangle.FromLTRB(0, 0, s.Width, s.Height));
				gfx.SmoothingMode = SmoothingMode.AntiAlias;
				gfx.PixelOffsetMode = PixelOffsetMode.Half;
				gfx.CompositingQuality = CompositingQuality.GammaCorrected;

				float l = 0.5f, t = 0.5f, r = s.Width - 0.5f, b = s.Height - 0.5f;
				gfx.DrawLines(pen, new PointF[] { new PointF(l, t), new PointF(r, b), new PointF(l, b), new PointF(r, t) });
				gfx.DrawLines(pen, new PointF[] { new PointF(l, b), new PointF(l, t), new PointF(r, t), new PointF(r, b) });
				bmp.Save(ms, ImageFormat.Png);

				ms.Seek(0, SeekOrigin.Begin);
				return ms;
			}
		}
	}
}
#endif