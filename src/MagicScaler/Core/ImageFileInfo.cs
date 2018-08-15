using System;
using System.IO;

namespace PhotoSauce.MagicScaler
{
	public sealed class ImageFileInfo
	{
		public readonly struct FrameInfo
		{
			public int Width { get; }
			public int Height { get; }
			public bool HasAlpha { get; }
			public Orientation ExifOrientation { get; }

			public FrameInfo(int width, int height, bool hasAlpha, Orientation orientation)
			{
				Width = width;
				Height = height;
				HasAlpha = hasAlpha;
				ExifOrientation = orientation;
			}
		}

		public long FileSize { get; private set; }
		public DateTime FileDate { get; private set; }
		public FileFormat ContainerType { get; private set; }
		public FrameInfo[] Frames { get; private set; }

		public ImageFileInfo(int width, int height)
		{
			Frames = new[] { new FrameInfo(width, height, false, Orientation.Normal) };
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

		public ImageFileInfo(ReadOnlySpan<byte> imgBuffer) : this(imgBuffer, DateTime.MinValue) { }

		public ImageFileInfo(ReadOnlySpan<byte> imgBuffer, DateTime lastModified)
		{
			if (imgBuffer == default) throw new ArgumentNullException(nameof(imgBuffer));

			using (var ctx = new WicProcessingContext(new ProcessImageSettings()))
				loadInfo(new WicDecoder(imgBuffer, ctx), ctx);

			FileSize = imgBuffer.Length;
			FileDate = lastModified;
		}

		public ImageFileInfo(Stream imgStream) : this(imgStream, DateTime.MinValue) { }

		public ImageFileInfo(Stream imgStream, DateTime lastModified)
		{
			if (imgStream is null) throw new ArgumentNullException(nameof(imgStream));
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

				int width = (int)(frm.ExifOrientation.RequiresDimensionSwap() ? ctx.Source.Height : ctx.Source.Width);
				int height = (int)(frm.ExifOrientation.RequiresDimensionSwap() ? ctx.Source.Width : ctx.Source.Height);
				Frames[i] = new FrameInfo(width, height, ctx.Source.Format.AlphaRepresentation != PixelAlphaRepresentation.None, frm.ExifOrientation);
			}
		}
	}
}