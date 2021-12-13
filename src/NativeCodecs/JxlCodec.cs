// Copyright © Clinton Ingram and Contributors.  Licensed under the MIT License.

using System;
using System.IO;
using System.Drawing;
using System.Runtime.InteropServices;

using PhotoSauce.MagicScaler;
using PhotoSauce.Interop.Libjxl;
using static PhotoSauce.Interop.Libjxl.Libjxl;

namespace PhotoSauce.NativeCodecs
{
	internal sealed unsafe class JxlContainer : IImageContainer, IDisposable
	{
		private readonly Stream stream;
		private IntPtr decoder;

		private JxlBasicInfo basinfo;
		private ColorProfile? profile;
		private IntPtr pixbuf;
		private int pixbuflen;

		private JxlContainer(Stream stm, IntPtr dec) => (stream, decoder) = (stm, dec);

		public FileFormat ContainerFormat => FileFormat.Unknown;

		public bool IsAnimation => false;

		int IImageContainer.FrameCount => 1;

		IImageFrame IImageContainer.GetFrame(int index)
		{
			if (index != 0)
				throw new ArgumentOutOfRangeException(nameof(index), "Invalid frame index.");

			JxlError.Check(JxlDecoderSubscribeEvents(decoder, (int)(JxlDecoderStatus.JXL_DEC_BASIC_INFO | JxlDecoderStatus.JXL_DEC_COLOR_ENCODING | JxlDecoderStatus.JXL_DEC_FULL_IMAGE)));

			var format = new JxlPixelFormat {
				num_channels = 1,
				data_type = JxlDataType.JXL_TYPE_UINT8,
				endianness = JxlEndianness.JXL_LITTLE_ENDIAN
			};

			using var buff = BufferPool.RentLocal<byte>(4096);
			fixed (byte* pbuf = buff.Span)
			{
				var status = default(JxlDecoderStatus);
				while (true)
				{
					status = JxlDecoderProcessInput(decoder);
					if (status == JxlDecoderStatus.JXL_DEC_NEED_MORE_INPUT)
					{
						int keep = (int)JxlDecoderReleaseInput(decoder);
						if (keep != 0)
							Buffer.MemoryCopy(pbuf + (buff.Length - keep), pbuf, buff.Length, keep);

						var span = buff.Span.Slice(0, keep + stream.Read(buff.Span.Slice(keep)));
						if (span.Length == keep)
							break;

						JxlError.Check(JxlDecoderSetInput(decoder, pbuf, (uint)span.Length));
					}
					else if (status == JxlDecoderStatus.JXL_DEC_BASIC_INFO)
					{
						fixed (JxlBasicInfo* pbi = &basinfo)
							JxlError.Check(JxlDecoderGetBasicInfo(decoder, pbi));

						format.num_channels = basinfo.alpha_bits == 0 ? basinfo.num_color_channels : 4u;
					}
					else if (status == JxlDecoderStatus.JXL_DEC_COLOR_ENCODING)
					{
						nuint icclen;
						JxlError.Check(JxlDecoderGetICCProfileSize(decoder, &format, JxlColorProfileTarget.JXL_COLOR_PROFILE_TARGET_DATA, &icclen));

						using var iccbuf = BufferPool.RentLocal<byte>((int)icclen);
						fixed (byte* picc = iccbuf.Span)
							JxlError.Check(JxlDecoderGetColorAsICCProfile(decoder, &format, JxlColorProfileTarget.JXL_COLOR_PROFILE_TARGET_DATA, picc, (nuint)iccbuf.Length));

						profile = ColorProfile.Parse(iccbuf.Span);
					}
					else if (status == JxlDecoderStatus.JXL_DEC_NEED_IMAGE_OUT_BUFFER) {
						nuint pixlen;
						JxlError.Check(JxlDecoderImageOutBufferSize(decoder, &format, &pixlen));

						pixbuflen = (int)pixlen;
						pixbuf = Marshal.AllocHGlobal((nint)pixlen);
						JxlError.Check(JxlDecoderSetImageOutBuffer(decoder, &format, pixbuf.ToPointer(), pixlen));
					}
					else
						break;
				}

				if (pixbuf == default)
					throw new NotSupportedException($"{nameof(Libjxl)} decode failed.");

				JxlDecoderRewind(decoder);
				return new JxlFrame(this);
			}
		}

		public static JxlContainer? TryLoad(Stream imgStream, IDecoderOptions? jxlOptions)
		{
			var dec = JxlDecoderCreate(default);
			JxlError.Check(JxlDecoderSetKeepOrientation(dec, JXL_TRUE));
			JxlError.Check(JxlDecoderSubscribeEvents(dec, (int)JxlDecoderStatus.JXL_DEC_BASIC_INFO));

			long stmpos = imgStream.Position;

			using var buff = BufferPool.RentLocal<byte>((int)JxlDecoderSizeHintBasicInfo(dec));
			fixed (byte* pbuf = buff.Span)
			{
				var status = default(JxlDecoderStatus);
				while (true)
				{
					status = JxlDecoderProcessInput(dec);
					if (status != JxlDecoderStatus.JXL_DEC_NEED_MORE_INPUT)
						break;

					int keep = (int)JxlDecoderReleaseInput(dec);
					if (keep != 0) // TODO grow buffer if keep == bufflen
						Buffer.MemoryCopy(pbuf + (buff.Length - keep), pbuf, buff.Length, keep);

					var span = buff.Span.Slice(0, keep + imgStream.Read(buff.Span.Slice(keep)));
					if (span.Length == keep)
						break;

					JxlError.Check(JxlDecoderSetInput(dec, pbuf, (uint)span.Length));
				}

				imgStream.Position = stmpos;

				if (status == JxlDecoderStatus.JXL_DEC_BASIC_INFO)
				{
					JxlDecoderRewind(dec);
					return new JxlContainer(imgStream, dec);
				}

				JxlDecoderDestroy(dec);
				return null;
			}
		}

		public void Dispose()
		{
			if (decoder == default)
				return;

			JxlDecoderDestroy(decoder);
			decoder = default;

			if (pixbuf != default)
			{
				Marshal.FreeHGlobal(pixbuf);
				pixbuf = default;
				pixbuflen = 0;
			}

			GC.SuppressFinalize(this);
		}

		~JxlContainer() => Dispose();

		private sealed class JxlFrame : IImageFrame
		{
			private readonly JxlContainer container;
			private bool disposed = false;

			public JxlFrame(JxlContainer cont) => container = cont;

			double IImageFrame.DpiX => 96;

			double IImageFrame.DpiY => 96;

			Orientation IImageFrame.ExifOrientation => (Orientation)container.basinfo.orientation;

			ReadOnlySpan<byte> IImageFrame.IccProfile => container.profile?.ProfileBytes ?? Array.Empty<byte>();

			public IPixelSource PixelSource
			{
				get
				{
					if (disposed)
						throw new ObjectDisposedException(nameof(JxlFrame));

					var fmt = 
						container.basinfo.alpha_bits != 0 ? PixelFormat.Rgba32 :
						container.basinfo.num_color_channels == 3 ? PixelFormat.Rgb24 :
						PixelFormat.Grey8;

					return new JxlPixelSource(container, fmt.FormatGuid, (int)container.basinfo.xsize, (int)container.basinfo.ysize, (int)container.basinfo.xsize * fmt.BytesPerPixel);
				}
			}

			public void Dispose() { disposed = true; }
		}

		private sealed class JxlPixelSource : BitmapPixelSource
		{
			private readonly JxlContainer container;

			public JxlPixelSource(JxlContainer cont, Guid fmt, int width, int height, int stride) : base(fmt, width, height, stride) => container = cont;

			protected override ReadOnlySpan<byte> Span => new Span<byte>(container.pixbuf.ToPointer(), container.pixbuflen);
		}
	}

	internal sealed unsafe class JxlEncoder : IImageEncoder
	{
		private readonly JxlEncoderOptions options;
		private readonly Stream stream;

		private IntPtr encoder, encopt;
		private bool written;

		public JxlEncoder(Stream outStream, IEncoderOptions? jxlOptions)
		{
			stream = outStream;
			options = jxlOptions is JxlEncoderOptions opt ? opt : JxlEncoderOptions.Default;

			encoder = JxlEncoderCreate(default);
			encopt = JxlEncoderOptionsCreate(encoder, default);
			if (options.lossless)
			{
				JxlError.Check(JxlEncoderOptionsSetLossless(encopt, JXL_TRUE));
			}
			else
			{
				JxlError.Check(JxlEncoderOptionsSetEffort(encopt, 4)); // current version of encoder will AV if this is set over 4 (including the default of 7)
				JxlError.Check(JxlEncoderOptionsSetDistance(encopt, 1.5f));
				JxlError.Check(JxlEncoderOptionsSetDecodingSpeed(encopt, 1));
			}
		}

		public void WriteFrame(IPixelSource source, IMetadataSource metadata, Rectangle sourceArea)
		{
			if (written)
				throw new InvalidOperationException("An image frame has already been written, and this encoder does not yet support multiple frames.");

			if (sourceArea == default)
				sourceArea = new(0, 0, source.Width, source.Height);

			var srcfmt = PixelFormat.FromGuid(source.Format);
			var dstfmt = 
				srcfmt == PixelFormat.Bgra32 ? PixelFormat.Rgba32 :
				srcfmt == PixelFormat.Bgr24  ? PixelFormat.Rgb24 :
				srcfmt == PixelFormat.Grey8  ? srcfmt :
				throw new NotSupportedException("Image format not supported.");

			int stride = sourceArea.Width * dstfmt.BytesPerPixel;
			using var pixbuff = BufferPool.RentLocal<byte>(stride * sourceArea.Height);

			if (srcfmt != dstfmt)
			{
				using var tran = new MagicScaler.Transforms.ConversionTransform(source.AsPixelSource(), null, null, dstfmt);
				((IPixelSource)tran).CopyPixels(sourceArea, stride, pixbuff.Span);
			}
			else
			{
				source.CopyPixels(sourceArea, stride, pixbuff.Span);
			}

			bool hasProps = metadata.TryGetMetadata<BaseImageProperties>(out var baseprops);

			var basinf = default(JxlBasicInfo);
			JxlEncoderInitBasicInfo(&basinf);
			basinf.xsize = (uint)sourceArea.Width;
			basinf.ysize = (uint)sourceArea.Height;
			basinf.bits_per_sample = 8;
			basinf.num_color_channels = (uint)(dstfmt.ChannelCount - (dstfmt.AlphaRepresentation == PixelAlphaRepresentation.None ? 0 : 1));
			basinf.alpha_bits = dstfmt.AlphaRepresentation == PixelAlphaRepresentation.None ? 0 : basinf.bits_per_sample;
			basinf.alpha_premultiplied = dstfmt.AlphaRepresentation == PixelAlphaRepresentation.Associated ? JXL_TRUE : JXL_FALSE;
			basinf.orientation = hasProps ? (JxlOrientation)baseprops.Orientation.Clamp() : JxlOrientation.JXL_ORIENT_IDENTITY;
			basinf.uses_original_profile = JXL_TRUE;

			JxlError.Check(JxlEncoderSetBasicInfo(encoder, &basinf));

			if (hasProps && baseprops.ColorProfile is ColorProfile prof)
			{
				fixed (byte* pp = &prof.ProfileBytes[0])
					JxlError.Check(JxlEncoderSetICCProfile(encoder, pp, (uint)prof.ProfileBytes.Length));
			}
			else
			{
				var color = default(JxlColorEncoding);
				JxlColorEncodingSetToSRGB(&color, dstfmt.ColorRepresentation == PixelColorRepresentation.Grey ? JXL_TRUE : JXL_FALSE);
				JxlError.Check(JxlEncoderSetColorEncoding(encoder, &color));
			}

			var pixfmt = new JxlPixelFormat {
				num_channels = (uint)dstfmt.ChannelCount,
				data_type = JxlDataType.JXL_TYPE_UINT8,
				endianness = JxlEndianness.JXL_LITTLE_ENDIAN,
			};

			using var stmbuff = BufferPool.RentLocal<byte>(1 << 14);
			var stmspan = stmbuff.Span;
			fixed (byte* pb = pixbuff.Span, pf = stmspan)
			{
				JxlError.Check(JxlEncoderAddImageFrame(encopt, &pixfmt, pb, (uint)stride * basinf.ysize));

				var res = default(JxlEncoderStatus);
				do
				{
					byte* ppf = pf;
					nuint cb = (uint)stmspan.Length;

					res = JxlEncoderProcessOutput(encoder, &ppf, &cb);
					JxlError.Check(res);

					stream.Write(stmspan.Slice(0, stmspan.Length - (int)cb));
				}
				while (res == JxlEncoderStatus.JXL_ENC_NEED_MORE_OUTPUT);
			}

			written = true;
		}

		public void Commit()
		{
			if (!written)
				throw new InvalidOperationException("An image frame has not been written.");
		}

		public void Dispose()
		{
			if (encoder == default)
				return;

			JxlEncoderDestroy(encoder);
			encoder = encopt = default;

			GC.SuppressFinalize(this);
		}

		~JxlEncoder() => Dispose();
	}

	internal readonly record struct JxlEncoderOptions(bool lossless) : IEncoderOptions
	{
		public static JxlEncoderOptions Default => new(false);
	}

	internal static class JxlError
	{
		public static void Check(JxlDecoderStatus status)
		{
			if (status == JxlDecoderStatus.JXL_DEC_ERROR)
				throw new InvalidOperationException($"{nameof(Libjxl)} decoder failed.");
		}

		public static void Check(JxlEncoderStatus status)
		{
			if (status == JxlEncoderStatus.JXL_ENC_ERROR || status == JxlEncoderStatus.JXL_ENC_NOT_SUPPORTED)
				throw new InvalidOperationException($"{nameof(Libjxl)} encoder failed.");
		}
	}

	/// <inheritdoc cref="WindowsCodecExtensions" />
	public static partial class CodecCollectionExtensions
	{
		/// <inheritdoc cref="WindowsCodecExtensions.UseWicCodecs(CodecCollection, WicCodecPolicy)" />
		public static void UseJpegXL(this CodecCollection codecs)
		{
			const string libjxl = nameof(libjxl);
			const uint libjxlver = 6001;

			uint ver = JxlDecoderVersion();
			if (ver != libjxlver || (ver = JxlEncoderVersion()) != libjxlver)
				throw new NotSupportedException($"Incorrect {libjxl} version was loaded.  Expected {libjxlver}, found {ver}.");

			string[] jxlMime = new[] { "image/jxl" };
			string[] jxlExtension = new[] { ".jxl" };

			codecs.Add(new DecoderInfo(
				libjxl,
				jxlMime,
				jxlExtension,
				new[] {
					new ContainerPattern(0, new byte[] { 0xff, 0x0a }, new byte[] { 0xff, 0xff }),
					new ContainerPattern(0, new byte[] { 0x00, 0x00, 0x00, 0x0c, 0x4a, 0x58, 0x4c, 0x20, 0x0d, 0x0a, 0x87, 0x0a }, new byte[] { 0xff, 0xff, 0xff, 0xff, 0xff, 0xff, 0xff, 0xff, 0xff, 0xff, 0xff, 0xff })
				},
				null,
				(s, c) => JxlContainer.TryLoad(s, c),
				true,
				false,
				false
			));
			codecs.Add(new EncoderInfo(
				libjxl,
				jxlMime,
				jxlExtension,
				JxlEncoderOptions.Default,
				(s, c) => new JxlEncoder(s, c),
				true,
				false,
				false,
				true
			));
		}
	}
}
