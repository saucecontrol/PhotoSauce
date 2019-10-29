#pragma warning disable CS1591 // XML Comments

#if SYSTEM_DRAWING
using System;
using System.IO;
using System.Linq;
using System.ComponentModel;
using System.Drawing;
using System.Drawing.Imaging;
using System.Drawing.Drawing2D;

using GdiPixelFormat = System.Drawing.Imaging.PixelFormat;

namespace PhotoSauce.MagicScaler
{
	[Obsolete("This class is meant only for testing/benchmarking and will be removed in a future version"), EditorBrowsable(EditorBrowsableState.Never)]
	public static class GdiImageProcessor
	{
		private const int exifOrientationID = 274;
		private static readonly ImageCodecInfo[] codecs = ImageCodecInfo.GetImageEncoders();
		private static readonly ImageCodecInfo jpegCodec = codecs.First(c => c.FormatID == ImageFormat.Jpeg.Guid);
		private static readonly ImageCodecInfo tiffCodec = codecs.First(c => c.FormatID == ImageFormat.Tiff.Guid);

		internal static void ExifRotate(this Image img)
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

		internal static Image HybridScale(this Image img, ProcessImageSettings s, InterpolationMode mode)
		{
			if (s.HybridScaleRatio == 1d || (mode != InterpolationMode.HighQualityBicubic && mode != InterpolationMode.HighQualityBilinear))
				return img;

			int intw = (int)Math.Ceiling(img.Width / s.HybridScaleRatio);
			int inth = (int)Math.Ceiling(img.Height / s.HybridScaleRatio);

			var bmp = new Bitmap(intw, inth);
			using (var gfx = Graphics.FromImage(bmp))
			{
				gfx.PixelOffsetMode = PixelOffsetMode.Half;
				gfx.CompositingMode = CompositingMode.SourceCopy;
				gfx.DrawImage(img, new Rectangle(0, 0, intw, inth), s.Crop.X, s.Crop.Y, s.Crop.Width, s.Crop.Height, GraphicsUnit.Pixel);
			}

			img.Dispose();
			s.Crop = new Rectangle(0, 0, intw, inth);

			return bmp;
		}

		public static ProcessImageResult ProcessImage(string imgPath, Stream outStream, ProcessImageSettings settings)
		{
			using var fs = new FileStream(imgPath, FileMode.Open, FileAccess.Read, FileShare.Read);
			return ProcessImage(fs, outStream, settings);
		}

		unsafe public static ProcessImageResult ProcessImage(ReadOnlySpan<byte> imgBuffer, Stream outStream, ProcessImageSettings settings)
		{
			fixed (byte* pbBuffer = imgBuffer)
			using (var ms = new UnmanagedMemoryStream(pbBuffer, imgBuffer.Length, imgBuffer.Length, FileAccess.Read))
				return ProcessImage(ms, outStream, settings);
		}

		public static ProcessImageResult ProcessImage(Stream imgStream, Stream outStream, ProcessImageSettings settings) => processImage(imgStream, outStream, settings);

		public static void CreateBrokenImage(Stream outStream, ProcessImageSettings settings) => createBrokenImage(outStream, settings);

		private static ProcessImageResult processImage(Stream istm, Stream ostm, ProcessImageSettings s)
		{
			using var img = Image.FromStream(istm, s.ColorProfileMode <= ColorProfileMode.NormalizeAndEmbed, false);

			if (s.FrameIndex > 0)
			{
				var fd = img.RawFormat.Guid == ImageFormat.Gif.Guid ? FrameDimension.Time : FrameDimension.Page;
				if (img.GetFrameCount(fd) > s.FrameIndex)
					img.SelectActiveFrame(fd, s.FrameIndex);
				else
					throw new ArgumentOutOfRangeException("Invalid Frame Index");
			}

			if (s.OrientationMode == OrientationMode.Normalize)
				img.ExifRotate();

			s = s.Clone();
			s.Fixup(img.Width, img.Height);
			var usedSettings = s.Clone();

			bool alpha = ((ImageFlags)img.Flags).HasFlag(ImageFlags.HasAlpha);
			var pixelFormat = alpha && s.MatteColor.A < byte.MaxValue ? GdiPixelFormat.Format32bppArgb : GdiPixelFormat.Format24bppRgb;
			var mode = s.Interpolation.WeightingFunction.Support < 0.1 ? InterpolationMode.NearestNeighbor :
			           s.Interpolation.WeightingFunction.Support < 1.0 ? s.ScaleRatio > 1.0 ? InterpolationMode.Bilinear : InterpolationMode.NearestNeighbor :
			           s.Interpolation.WeightingFunction.Support > 1.0 ? s.ScaleRatio > 1.0 || s.Interpolation.Blur > 1.0 ? InterpolationMode.HighQualityBicubic : InterpolationMode.Bicubic :
			           s.ScaleRatio > 1.0 ? InterpolationMode.HighQualityBilinear : InterpolationMode.Bilinear;

			using var src = img.HybridScale(s, mode);
			using var iat = new ImageAttributes();
			using var bmp = new Bitmap(s.Width, s.Height, pixelFormat);
			using var gfx = Graphics.FromImage(bmp);

			iat.SetWrapMode(WrapMode.TileFlipXY);
			gfx.PixelOffsetMode = PixelOffsetMode.Half;
			gfx.CompositingMode = CompositingMode.SourceCopy;
			gfx.InterpolationMode = mode;

			if ((alpha || s.InnerRect != s.OuterRect) && !s.MatteColor.IsEmpty)
			{
				gfx.Clear(s.MatteColor);
				gfx.CompositingMode = CompositingMode.SourceOver;
				gfx.CompositingQuality = CompositingQuality.GammaCorrected;
			}

			gfx.DrawImage(src, s.InnerRect, s.Crop.X, s.Crop.Y, s.Crop.Width, s.Crop.Height, GraphicsUnit.Pixel, iat);

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

			return new ProcessImageResult(usedSettings, Enumerable.Empty<PixelSourceStats>());
		}

		private static void createBrokenImage(Stream ostm, ProcessImageSettings s)
		{
			s = s.Clone();
			if (s.Width == 0 && s.Height == 0)
				s.Height = s.Width = 100;

			s.Fixup(s.Width > 0 ? s.Width : s.Height, s.Height > 0 ? s.Height : s.Width);

			using var bmp = new Bitmap(s.Width, s.Height, GdiPixelFormat.Format24bppRgb);
			using var gfx = Graphics.FromImage(bmp);
			using var pen = new Pen(Brushes.White, 1.75f);

			gfx.FillRectangle(Brushes.Gainsboro, new Rectangle(0, 0, s.Width, s.Height));
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