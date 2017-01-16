using System;
using System.IO;
using System.Diagnostics.Contracts;

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
			Contract.Requires<ArgumentNullException>(imgPath != null, nameof(imgPath));

			var fi = new FileInfo(imgPath);
			if (!fi.Exists)
				throw new ArgumentException("File does not exist");

			using (var ctx = new WicProcessingContext(new ProcessImageSettings()))
			using (var dec = new WicDecoder(imgPath, ctx))
				loadInfo(dec, ctx);

			FileSize = fi.Length;
			FileDate = fi.LastWriteTimeUtc;
		}

		public ImageFileInfo(byte[] imgBuffer, DateTime lastModified)
		{
			Contract.Requires<ArgumentNullException>(imgBuffer != null, nameof(imgBuffer));

			using (var ctx = new WicProcessingContext(new ProcessImageSettings()))
			using (var dec = new WicDecoder(imgBuffer, ctx))
				loadInfo(dec, ctx);

			FileSize = imgBuffer.Length;
			FileDate = lastModified;
		}

		public ImageFileInfo(Stream istm, DateTime lastModified)
		{
			Contract.Requires<ArgumentNullException>(istm != null, nameof(istm));
			Contract.Requires<ArgumentException>(istm.CanSeek && istm.CanRead, "Input Stream must allow Seek and Read");
			Contract.Assert(istm.Length > 0, "Input Stream cannot be empty");
			Contract.Assume(istm.Position < istm.Length, "Input Stream Position is at the end.  Did you forget to Seek?");

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