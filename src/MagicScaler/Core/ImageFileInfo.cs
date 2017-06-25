using System;
using System.IO;

namespace PhotoSauce.MagicScaler
{
	public sealed class ImageFileInfo
	{
		public struct FrameInfo
		{
			public int Width { get; private set; }
			public int Height { get; private set; }
			public bool SwapDimensions { get; private set; }
			public bool HasAlpha { get; private set; }

			public FrameInfo(int width, int height, bool swapDimensions, bool hasAlpha)
			{
				Width = width;
				Height = height;
				SwapDimensions = swapDimensions;
				HasAlpha = hasAlpha;
			}
		}

		public long FileSize { get; private set; }
		public DateTime FileDate { get; private set; }
		public FileFormat ContainerType { get; private set; }
		public FrameInfo[] Frames { get; private set; }

		public ImageFileInfo(int width, int height)
		{
			Frames = new[] { new FrameInfo(width, height, false, false) };
			ContainerType = FileFormat.Unknown;
		}

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

		public ImageFileInfo(ArraySegment<byte> imgBuffer) : this(imgBuffer, DateTime.MinValue) { }

		public ImageFileInfo(ArraySegment<byte> imgBuffer, DateTime lastModified)
		{
			if (imgBuffer == null) throw new ArgumentNullException(nameof(imgBuffer));

			using (var ctx = new WicProcessingContext(new ProcessImageSettings()))
				loadInfo(new WicDecoder(imgBuffer, ctx), ctx);

			FileSize = imgBuffer.Count;
			FileDate = lastModified;
		}

		public ImageFileInfo(Stream imgStream) : this(imgStream, DateTime.MinValue) { }

		public ImageFileInfo(Stream imgStream, DateTime lastModified)
		{
			if (imgStream == null) throw new ArgumentNullException(nameof(imgStream));
			if (!imgStream.CanSeek || !imgStream.CanRead) throw new ArgumentException("Input Stream must allow Seek and Read", nameof(imgStream));
			if (imgStream.Length <= 0 || imgStream.Position >= imgStream.Length) throw new ArgumentException("Input Stream is empty or positioned at its end", nameof(imgStream));

			using (var ctx = new WicProcessingContext(new ProcessImageSettings()))
				loadInfo(new WicDecoder(imgStream, ctx), ctx);

			FileSize = imgStream.Length;
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