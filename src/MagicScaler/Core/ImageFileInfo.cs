using System;
using System.IO;

using PhotoSauce.MagicScaler.Interop;

namespace PhotoSauce.MagicScaler
{
	internal class ImageFileInfo
	{
		public struct FrameInfo
		{
			public int Width { get; private set; }
			public int Height { get; private set; }
			public bool Rotated90 { get; private set; }
			public bool HasAlpha { get; private set; }

			public FrameInfo(int width, int height, bool rotated, bool alpha)
			{
				Width = width;
				Height = height;
				Rotated90 = rotated;
				HasAlpha = alpha;
			}
		}

		public long FileSize { get; private set; }
		public DateTime FileDate { get; private set; }
		public FileFormat ContainerType { get; private set; }
		public FrameInfo[] Frames { get; private set; }

		public ImageFileInfo(string imgPath)
		{
			var fi = new FileInfo(imgPath);
			if (!fi.Exists)
				throw new FileNotFoundException("File not found", imgPath);

			using (var ctx = new WicProcessingContext(new ProcessImageSettings()))
			using (var dec = new WicDecoder(imgPath, ctx))
				loadInfo(dec, ctx);

			FileSize = fi.Length;
			FileDate = fi.LastWriteTimeUtc;
		}

		public ImageFileInfo(byte[] imgBuffer, DateTime lastModified)
		{
			if (imgBuffer == null) throw new ArgumentNullException(nameof(imgBuffer));

			using (var ctx = new WicProcessingContext(new ProcessImageSettings()))
			using (var dec = new WicDecoder(imgBuffer, ctx))
				loadInfo(dec, ctx);

			FileSize = imgBuffer.Length;
			FileDate = lastModified;
		}

		public ImageFileInfo(Stream istm, DateTime lastModified)
		{
			if (istm == null) throw new ArgumentNullException(nameof(istm));
			if (!istm.CanSeek || !istm.CanRead) throw new ArgumentException("Input Stream must allow Seek and Read", nameof(istm));
			if (istm.Length <= 0 || istm.Position >= istm.Length) throw new ArgumentException("Input Stream is empty or positioned at its end", nameof(istm));

			using (var ctx = new WicProcessingContext(new ProcessImageSettings()))
			using (var dec = new WicDecoder(istm, ctx))
				loadInfo(dec, ctx);

			FileSize = istm.Length;
			FileDate = lastModified;
		}

		private void loadInfo(WicDecoder dec, WicProcessingContext ctx)
		{
			ContainerType = FileFormat.Unknown;
			if (ctx.ContainerFormat == Consts.GUID_ContainerFormatJpeg)
				ContainerType = FileFormat.Jpeg;
			else if (ctx.ContainerFormat == Consts.GUID_ContainerFormatPng)
				ContainerType = FileFormat.Png;
			else if (ctx.ContainerFormat == Consts.GUID_ContainerFormatGif)
				ContainerType = FileFormat.Gif;
			else if (ctx.ContainerFormat == Consts.GUID_ContainerFormatBmp)
				ContainerType = FileFormat.Bmp;
			else if (ctx.ContainerFormat == Consts.GUID_ContainerFormatTiff)
				ContainerType = FileFormat.Tiff;

			Frames = new FrameInfo[ctx.ContainerFrameCount];
			for (int i = 0; i < ctx.ContainerFrameCount; i++)
			{
				ctx.Settings.FrameIndex = i;
				using (var frm = new WicFrameReader(dec, ctx))
				using (var met = new WicMetadataReader(frm, basicOnly: true))
				{
					Frames[i] = new FrameInfo((int)ctx.Width, (int)ctx.Height, ctx.IsRotated90, ctx.HasAlpha);
				}
			}
		}
	}
}