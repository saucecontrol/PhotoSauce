using System;
using System.IO;

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
				loadInfo(new WicDecoder(imgPath, ctx), ctx);

			FileSize = fi.Length;
			FileDate = fi.LastWriteTimeUtc;
		}

		public ImageFileInfo(ArraySegment<byte> imgBuffer, DateTime lastModified)
		{
			if (imgBuffer == null) throw new ArgumentNullException(nameof(imgBuffer));

			using (var ctx = new WicProcessingContext(new ProcessImageSettings()))
				loadInfo(new WicDecoder(imgBuffer, ctx), ctx);

			FileSize = imgBuffer.Count;
			FileDate = lastModified;
		}

		public ImageFileInfo(Stream istm, DateTime lastModified)
		{
			if (istm == null) throw new ArgumentNullException(nameof(istm));
			if (!istm.CanSeek || !istm.CanRead) throw new ArgumentException("Input Stream must allow Seek and Read", nameof(istm));
			if (istm.Length <= 0 || istm.Position >= istm.Length) throw new ArgumentException("Input Stream is empty or positioned at its end", nameof(istm));

			using (var ctx = new WicProcessingContext(new ProcessImageSettings()))
				loadInfo(new WicDecoder(istm, ctx), ctx);

			FileSize = istm.Length;
			FileDate = lastModified;
		}

		private void loadInfo(WicDecoder dec, WicProcessingContext ctx)
		{
			ContainerType = ctx.Decoder.ContainerFormat;
			Frames = new FrameInfo[ctx.Decoder.FrameCount];
			for (int i = 0; i < ctx.Decoder.FrameCount; i++)
			{
				ctx.Settings.FrameIndex = i;
				var frm = new WicFrameReader(ctx);
				WicTransforms.AddMetadataReader(ctx, basicOnly: true);

				Frames[i] = new FrameInfo((int)ctx.Source.Width, (int)ctx.Source.Height, frm.SwapDimensions, ctx.Source.Format.AlphaRepresentation != PixelAlphaRepresentation.None);
			}
		}
	}
}