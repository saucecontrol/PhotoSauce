// Copyright © Clinton Ingram and Contributors.  Licensed under the MIT License.

using System;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Runtime.CompilerServices;

using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Tga;
using SixLabors.ImageSharp.PixelFormats;

using PhotoSauce.MagicScaler;

using GdiRect = System.Drawing.Rectangle;

namespace PhotoSauce.ManagedCodecs.ImageSharp
{
	internal readonly record struct TargaEncoderOptions(TgaCompression Compression) : IEncoderOptions
	{
		public static TargaEncoderOptions Default => default;
	}

	/// <summary>Encoder for TARGA image files.</summary>
	public sealed class TargaEncoder : IImageEncoder
	{
		private readonly TargaEncoderOptions options;
		private readonly Stream stream;
		private bool written;

		/// <summary>Creates a new <see cref="TargaEncoder" /> instance.</summary>
		/// <param name="outStream">The <see cref="Stream" /> to which the image will be written.</param>
		/// <param name="tgaOptions">Options for the image format.</param>
		public static TargaEncoder Create(Stream outStream!!, IEncoderOptions? tgaOptions) => new(outStream, tgaOptions);

		private TargaEncoder(Stream outStream, IEncoderOptions? tgaOptions)
		{
			stream = outStream;
			options = tgaOptions is TargaEncoderOptions opt ? opt : TargaEncoderOptions.Default;
		}

		/// <inheritdoc />
		public void WriteFrame(IPixelSource source, IMetadataSource metadata, GdiRect sourceArea)
		{
			if (written)
				throw new InvalidOperationException("An image frame has already been written, and this codec does not support multiple frames.");

			var srcfmt = source.Format;
			var enc = new TgaEncoder { Compression = options.Compression };

			if (srcfmt == PixelFormats.Grey8bpp)
				encode<L8>(enc, stream, source, sourceArea);
			else if (srcfmt == PixelFormats.Bgr24bpp)
				encode<Bgr24>(enc, stream, source, sourceArea);
			else if (srcfmt == PixelFormats.Bgra32bpp)
				encode<Bgra32>(enc, stream, source, sourceArea);
			else throw new NotSupportedException("Image format not supported.");

			written = true;
		}

		/// <inheritdoc />
		public void Commit()
		{
			if (!written)
				throw new InvalidOperationException("An image frame has not been written.");
		}

		void IDisposable.Dispose() { }

		private static void encode<TPixel>(TgaEncoder enc, Stream stm, IPixelSource src, GdiRect srcArea) where TPixel : unmanaged, IPixel<TPixel>
		{
			if (srcArea == default)
				srcArea = new(0, 0, src.Width, src.Height);

			using var img = new Image<TPixel>(srcArea.Width, srcArea.Height);

			var rect = new GdiRect(srcArea.X, srcArea.Y, srcArea.Width, 1);
			for (int i = 0; i < srcArea.Height; i++)
			{
				var span = MemoryMarshal.AsBytes(img.GetPixelRowSpan(i));
				rect.Y = i;
				src.CopyPixels(rect, span.Length, span);
			}

			enc.BitsPerPixel = (TgaBitsPerPixel)(Unsafe.SizeOf<TPixel>() * 8);
			enc.Encode(img, stm);
		}
	}

	/// <summary>Represents a TARGA image file.</summary>
	public sealed class TargaContainer : IImageContainer
	{
		private readonly Image decodedImage;

		private TargaContainer(Stream imgStream, int bpp) => decodedImage = bpp switch {
			 8 => Image.Load<L8>(imgStream),
			24 => Image.Load<Bgr24>(imgStream),
			 _ => Image.Load<Bgra32>(imgStream)
		};

		/// <inheritdoc />
		public string MimeType => TgaFormat.Instance.DefaultMimeType;

		int IImageContainer.FrameCount => 1;

		IImageFrame IImageContainer.GetFrame(int index) => index == 0 ? new TargaFrame(decodedImage.Frames.RootFrame) : throw new IndexOutOfRangeException("Invalid frame index");

		internal static TargaContainer? TryLoad(Stream imgStream, IDecoderOptions? _)
		{
			long pos = imgStream.Position;
			var info = Image.Identify(imgStream, out var fmt);
			imgStream.Seek(pos, SeekOrigin.Begin);

			return fmt is TgaFormat ? new TargaContainer(imgStream, info.PixelType.BitsPerPixel) : null;
		}

		/// <summary>Loads a TARGA image from an input <see cref="Stream"/>.</summary>
		/// <param name="imgStream">A <see cref="Stream" /> containing the image file.</param>
		/// <returns>A <see cref="TargaContainer"/> encapsulating the image.</returns>
		public static TargaContainer Load(Stream imgStream!!)
		{
			long pos = imgStream.Position;
			var info = Image.Identify(imgStream, out var fmt);
			imgStream.Seek(pos, SeekOrigin.Begin);

			if (fmt is not TgaFormat)
				throw new InvalidDataException("Not a TARGA image.");

			return new TargaContainer(imgStream, info.PixelType.BitsPerPixel);
		}

		/// <inheritdoc />
		public void Dispose() => decodedImage.Dispose();
	}

	/// <summary>Gives access to the pixels in a TARGA file.</summary>
	public sealed class TargaFrame : IImageFrame
	{
		private readonly ImageFrame decodedFrame;

		internal TargaFrame(ImageFrame frame) => decodedFrame = frame;

		/// <inheritdoc />
		public IPixelSource PixelSource => new ImagePixelSource(decodedFrame);

		/// <inheritdoc />
		public void Dispose() => decodedFrame.Dispose();
	}

	internal sealed class ImagePixelSource : IPixelSource
	{
		private readonly ImageFrame decodedFrame;

		public ImagePixelSource(ImageFrame frame) => decodedFrame = frame;

		private int bytesPerPixel => decodedFrame switch {
			ImageFrame<L8>    => 1,
			ImageFrame<Bgr24> => 3,
			_                 => 4
		};

		public Guid Format => decodedFrame switch {
			ImageFrame<L8>    => PixelFormats.Grey8bpp,
			ImageFrame<Bgr24> => PixelFormats.Bgr24bpp,
			_                 => PixelFormats.Bgra32bpp
		};

		public int Width => decodedFrame.Width;

		public int Height => decodedFrame.Height;

		public void CopyPixels(GdiRect sourceArea, int cbStride, Span<byte> buffer)
		{
			var (rx, ry, rw, rh) = (sourceArea.X, sourceArea.Y, sourceArea.Width, sourceArea.Height);
			int cb = rw * bytesPerPixel;

			if (rx < 0 || ry < 0 || rw < 0 || rh < 0 || rx + rw > Width || ry + rh > Height)
				throw new ArgumentOutOfRangeException(nameof(sourceArea), "Requested area does not fall within the image bounds.");

			if (cb > cbStride)
				throw new ArgumentOutOfRangeException(nameof(cbStride), "Stride is too small for the requested area.");

			if ((rh - 1) * cbStride + cb > buffer.Length)
				throw new ArgumentOutOfRangeException(nameof(buffer), "Buffer is too small for the requested area.");

			if (decodedFrame is ImageFrame<L8> img8)
				copyPixels(img8, sourceArea, buffer, cbStride, cb);
			else if (decodedFrame is ImageFrame<Bgr24> img24)
				copyPixels(img24, sourceArea, buffer, cbStride, cb);
			else if (decodedFrame is ImageFrame<Bgra32> img32)
				copyPixels(img32, sourceArea, buffer, cbStride, cb);
			else throw new NotSupportedException("Image format not supported.");
		}

		private static void copyPixels<TPixel>(ImageFrame<TPixel> frame, GdiRect area, Span<byte> dest, int stride, int cb) where TPixel : unmanaged, IPixel<TPixel>
		{
			for (int y = 0; y < area.Height; y++)
			{
				ref byte src = ref Unsafe.As<TPixel, byte>(ref frame.GetPixelRowSpan(area.Y + y)[area.X]);
				Unsafe.CopyBlockUnaligned(ref dest[y * stride], ref src, (uint)cb);
			}
		}
	}

	/// <inheritdoc cref="WindowsCodecExtensions" />
	public static class CodecCollectionExtensions
	{
		/// <inheritdoc cref="WindowsCodecExtensions.UseWicCodecs(CodecCollection, WicCodecPolicy)" />
		public static void UseImageSharpTga(this CodecCollection codecs)
		{
			var targa = TgaFormat.Instance;
			var mimeTypes = new[] { targa.DefaultMimeType }.Concat(targa.MimeTypes).Distinct().ToArray();
			var fileExtensions = targa.FileExtensions.Select(e => e.StartsWith(".") ? e : string.Concat(".", e)).ToArray();

			codecs.Add(new DecoderInfo(
				$"{nameof(SixLabors.ImageSharp)} {targa.Name}",
				mimeTypes,
				fileExtensions,
				new[] { new ContainerPattern(0, new byte[] { 0, 0, 0 }, new byte[] { 0, 0b_1111_1110, 0b_1111_0100 }) },
				null,
				TargaContainer.TryLoad,
				true,
				false,
				false
			));
			codecs.Add(new EncoderInfo(
				$"{nameof(SixLabors.ImageSharp)} {targa.Name}",
				mimeTypes,
				fileExtensions,
				new[] { PixelFormats.Grey8bpp, PixelFormats.Bgr24bpp, PixelFormats.Bgra32bpp },
				TargaEncoderOptions.Default,
				TargaEncoder.Create,
				true,
				false,
				false,
				false
			));
		}
	}
}
