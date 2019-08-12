using System;
using System.IO;

namespace PhotoSauce.MagicScaler
{
	/// <summary>Represents basic information about an image container.</summary>
	public sealed class ImageFileInfo
	{
		/// <summary>Represents basic information about an image frame within a container.</summary>
		public readonly struct FrameInfo
		{
			/// <summary>The width of the image frame in pixels.</summary>
			public int Width { get; }
			/// <summary>The height of the image frame in pixels.</summary>
			public int Height { get; }
			/// <summary>True if the image frame contains transparency data, otherwise false.</summary>
			public bool HasAlpha { get; }
			/// <summary>
			/// The stored <a href="https://magnushoff.com/jpeg-orientation.html">Exif orientation</a> for the image frame.
			/// The <see cref="Width" /> and <see cref="Height" /> values reflect the corrected orientation, not the stored orientation.
			/// </summary>
			public Orientation ExifOrientation { get; }

			/// <summary>Constructs a new <see cref="FrameInfo" /> instance with the supplied values.</summary>
			/// <param name="width">The width of the image frame in pixels.</param>
			/// <param name="height">The height of the image frame in pixels.</param>
			/// <param name="hasAlpha">True if the image frame contains transparency data, otherwise false.</param>
			/// <param name="orientation">The Exif orientation associated with the image frame.</param>
			public FrameInfo(int width, int height, bool hasAlpha, Orientation orientation)
			{
				Width = width;
				Height = height;
				HasAlpha = hasAlpha;
				ExifOrientation = orientation;
			}
		}

		/// <summary>The size of the image container in bytes.</summary>
		public long FileSize { get; private set; }
		/// <summary>The last modified date of the image container, if applicable.</summary>
		public DateTime FileDate { get; private set; }
		/// <summary>The format of the image container (e.g. JPEG, PNG).</summary>
		public FileFormat ContainerType { get; private set; }
		/// <summary>One or more <see cref="FrameInfo" /> instances describing each image frame in the container.</summary>
		public FrameInfo[] Frames { get; private set; }

		/// <summary>Constructs a new <see cref="ImageFileInfo" /> instance with a single frame of the specified <paramref name="width" /> and <paramref name="height" />.</summary>
		/// <param name="width">The width of the image frame in pixels.</param>
		/// <param name="height">The height of the image frame in pixels.</param>
		public ImageFileInfo(int width, int height)
		{
			Frames = new[] { new FrameInfo(width, height, false, Orientation.Normal) };
			ContainerType = FileFormat.Unknown;
		}

		/// <summary>Constructs a new <see cref="ImageFileInfo" /> instance by reading the metadata from an image file header.</summary>
		/// <param name="imgPath">The path to the image file.</param>
		public ImageFileInfo(string imgPath)
		{
			var fi = new FileInfo(imgPath);
			if (!fi.Exists)
				throw new FileNotFoundException("File not found", imgPath);

			using (var ctx = new PipelineContext(new ProcessImageSettings()))
				loadInfo(new WicDecoder(imgPath, ctx), ctx);

			FileSize = fi.Length;
			FileDate = fi.LastWriteTimeUtc;
		}

		/// <inheritdoc cref="ImageFileInfo(ReadOnlySpan{byte}, DateTime)" />
		public ImageFileInfo(ReadOnlySpan<byte> imgBuffer) : this(imgBuffer, DateTime.MinValue) { }

		/// <summary>Constructs a new <see cref="ImageFileInfo" /> instance by reading the metadata from an image file contained in a <see cref="ReadOnlySpan{T}" />.</summary>
		/// <param name="imgBuffer">The buffer containing the image data.</param>
		/// <param name="lastModified">The last modified date of the image container.</param>
		public ImageFileInfo(ReadOnlySpan<byte> imgBuffer, DateTime lastModified)
		{
			if (imgBuffer == default) throw new ArgumentNullException(nameof(imgBuffer));

			using (var ctx = new PipelineContext(new ProcessImageSettings()))
				loadInfo(new WicDecoder(imgBuffer, ctx), ctx);

			FileSize = imgBuffer.Length;
			FileDate = lastModified;
		}

		/// <inheritdoc cref="ImageFileInfo(Stream, DateTime)" />
		public ImageFileInfo(Stream imgStream) : this(imgStream, DateTime.MinValue) { }

		/// <summary>Constructs a new <see cref="ImageFileInfo" /> instance by reading the metadata from an image file exposed by a <see cref="Stream" />.</summary>
		/// <param name="imgStream">The stream containing the image data.</param>
		/// <param name="lastModified">The last modified date of the image container.</param>
		public ImageFileInfo(Stream imgStream, DateTime lastModified)
		{
			if (imgStream is null) throw new ArgumentNullException(nameof(imgStream));
			if (!imgStream.CanSeek || !imgStream.CanRead) throw new ArgumentException("Input Stream must allow Seek and Read", nameof(imgStream));
			if (imgStream.Length <= 0 || imgStream.Position >= imgStream.Length) throw new ArgumentException("Input Stream is empty or positioned at its end", nameof(imgStream));

			using (var ctx = new PipelineContext(new ProcessImageSettings()))
				loadInfo(new WicDecoder(imgStream, ctx), ctx);

			FileSize = imgStream.Length;
			FileDate = lastModified;
		}

		private void loadInfo(WicDecoder dec, PipelineContext ctx)
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