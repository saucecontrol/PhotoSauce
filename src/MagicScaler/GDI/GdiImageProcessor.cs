// Copyright © Clinton Ingram and Contributors.  Licensed under the MIT License.

#if GDIPROCESSOR
#pragma warning disable CS1591
using System;
using System.IO;
using System.Linq;
using System.Drawing;
using System.Drawing.Imaging;
using System.Drawing.Drawing2D;

using GdiPixelFormat = System.Drawing.Imaging.PixelFormat;

namespace PhotoSauce.MagicScaler
{
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

			var prop = img.GetPropertyItem(exifOrientationID)!;
			int val = BitConverter.ToUInt16(prop.Value!, 0);
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
			if (s.HybridScaleRatio == 1 || (mode != InterpolationMode.HighQualityBicubic && mode != InterpolationMode.HighQualityBilinear))
				return img;

			int intw = (int)Math.Ceiling((double)img.Width / s.HybridScaleRatio);
			int inth = (int)Math.Ceiling((double)img.Height / s.HybridScaleRatio);

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

		public static ProcessImageResult ProcessImage(string imgPath!!, Stream outStream!!, ProcessImageSettings settings!!)
		{
			using var fs = File.OpenRead(imgPath);
			return ProcessImage(fs, outStream, settings);
		}

		public static unsafe ProcessImageResult ProcessImage(ReadOnlySpan<byte> imgBuffer, Stream outStream!!, ProcessImageSettings settings!!)
		{
			fixed (byte* pbBuffer = imgBuffer)
			using (var ms = new UnmanagedMemoryStream(pbBuffer, imgBuffer.Length, imgBuffer.Length, FileAccess.Read))
				return ProcessImage(ms, outStream, settings);
		}

		public static ProcessImageResult ProcessImage(Stream imgStream!!, Stream outStream!!, ProcessImageSettings settings!!) => processImage(imgStream, outStream, settings);

		private static ProcessImageResult processImage(Stream istm, Stream ostm, ProcessImageSettings s)
		{
			using var img = Image.FromStream(istm, s.ColorProfileMode != ColorProfileMode.Ignore, false);

			if (s.DecoderOptions is IMultiFrameDecoderOptions opt)
			{
				var fd = img.RawFormat.Guid == ImageFormat.Gif.Guid ? FrameDimension.Time : FrameDimension.Page;
				int fi = opt.FrameRange.GetOffsetAndLength(img.GetFrameCount(fd)).Offset;

				img.SelectActiveFrame(fd, fi);
			}

			if (s.OrientationMode == OrientationMode.Normalize)
				img.ExifRotate();

			s = s.Clone();
			s.Fixup(img.Width, img.Height);
			var usedSettings = s.Clone();

			bool alpha = ((ImageFlags)img.Flags).HasFlag(ImageFlags.HasAlpha);
			var pixelFormat = alpha && s.MatteColor.IsTransparent() ? GdiPixelFormat.Format32bppArgb : GdiPixelFormat.Format24bppRgb;
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

			if ((alpha || s.InnerSize != s.OuterSize) && !s.MatteColor.IsEmpty)
			{
				gfx.Clear(s.MatteColor);
				gfx.CompositingMode = CompositingMode.SourceOver;
				gfx.CompositingQuality = CompositingQuality.GammaCorrected;
			}

			gfx.DrawImage(src, s.InnerRect, s.Crop.X, s.Crop.Y, s.Crop.Width, s.Crop.Height, GraphicsUnit.Pixel, iat);

			var enc = s.EncoderInfo ?? CodecManager.FallbackEncoder;
			if (enc.SupportsMimeType(ImageMimeTypes.Bmp))
			{
				bmp.Save(ostm, ImageFormat.Bmp);
			}
			else if (enc.SupportsMimeType(ImageMimeTypes.Tiff))
			{
				using var param = new EncoderParameter(Encoder.Compression, (long)EncoderValue.CompressionNone);
				using var encoderParams = new EncoderParameters(1);
				encoderParams.Param[0] = param;
				bmp.Save(ostm, tiffCodec, encoderParams);
			}
			else if (enc.SupportsMimeType(ImageMimeTypes.Jpeg))
			{
				using var param = new EncoderParameter(Encoder.Quality, s.LossyQuality);
				using var encoderParams = new EncoderParameters(1);
				encoderParams.Param[0] = param;
				bmp.Save(ostm, jpegCodec, encoderParams);
			}
			else if (enc.SupportsMimeType(ImageMimeTypes.Gif) || s.EncoderOptions is IIndexedEncoderOptions)
			{
				bmp.Save(ostm, ImageFormat.Gif);
			}
			else
			{
				bmp.Save(ostm, ImageFormat.Png);
			}

			return new ProcessImageResult(usedSettings, Enumerable.Empty<PixelSourceStats>());
		}
	}
}
#endif