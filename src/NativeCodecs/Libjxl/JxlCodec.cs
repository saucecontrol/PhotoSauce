// Copyright © Clinton Ingram and Contributors.  Licensed under the MIT License.

#pragma warning disable CS3016 //https://github.com/dotnet/roslyn/issues/4293

using System;
using System.IO;
using System.Drawing;
using System.Diagnostics;
using System.Runtime.InteropServices;
#if NET5_0_OR_GREATER
using System.Runtime.CompilerServices;
#endif

using PhotoSauce.MagicScaler;
using PhotoSauce.Interop.Libjxl;
using static PhotoSauce.Interop.Libjxl.Libjxl;

namespace PhotoSauce.NativeCodecs.Libjxl
{
	internal sealed unsafe class JxlContainer : IImageContainer, IDisposable
	{
		private readonly Stream stream;
		private readonly long stmpos;
		private IntPtr decoder;

		private JxlBasicInfo basinfo;
		private ColorProfile? profile;
		private bool pixready;

		private JxlPixelFormat pixelfmt
		{
			get
			{
				if (basinfo.num_color_channels == 0)
					throw new InvalidOperationException("Basic info has not been read.");

				return new JxlPixelFormat {
					num_channels = basinfo.alpha_bits == 0 ? basinfo.num_color_channels : 4u,
					data_type = JxlDataType.JXL_TYPE_UINT8
				};
			}
		}

		private JxlContainer(Stream stm, long pos, IntPtr dec) => (stream, stmpos, decoder) = (stm, pos, dec);

		public string MimeType => ImageMimeTypes.Jxl;

		int IImageContainer.FrameCount => 1;

		private void moveToFrameData(bool readMetadata = false)
		{
			stream.Position = stmpos;
			pixready = false;

			var events = JxlDecoderStatus.JXL_DEC_FULL_IMAGE;
			if (readMetadata)
				events |= JxlDecoderStatus.JXL_DEC_BASIC_INFO | JxlDecoderStatus.JXL_DEC_COLOR_ENCODING;

			JxlDecoderRewind(decoder);
			JxlError.Check(JxlDecoderSubscribeEvents(decoder, (int)events));

			using var buff = BufferPool.RentLocal<byte>(4096);
			fixed (byte* pbuf = buff)
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
					}
					else if (status == JxlDecoderStatus.JXL_DEC_COLOR_ENCODING)
					{
						nuint icclen;
						var pixfmt = pixelfmt;
						JxlError.Check(JxlDecoderGetICCProfileSize(decoder, &pixfmt, JxlColorProfileTarget.JXL_COLOR_PROFILE_TARGET_DATA, &icclen));

						using var iccbuf = BufferPool.RentLocal<byte>((int)icclen);
						fixed (byte* picc = iccbuf)
							JxlError.Check(JxlDecoderGetColorAsICCProfile(decoder, &pixfmt, JxlColorProfileTarget.JXL_COLOR_PROFILE_TARGET_DATA, picc, (nuint)iccbuf.Length));

						profile = ColorProfile.Parse(iccbuf.Span);
					}
					else if (status == JxlDecoderStatus.JXL_DEC_NEED_IMAGE_OUT_BUFFER)
					{
						pixready = true;
						break;
					}
					else
						break;
				}

				nuint rewind = JxlDecoderReleaseInput(decoder);
				if (rewind != 0)
					stream.Seek(-(long)rewind, SeekOrigin.Current);
			}
		}

		IImageFrame IImageContainer.GetFrame(int index)
		{
			if (index != 0)
				throw new IndexOutOfRangeException("Frame index does not exist");

			moveToFrameData(true);

			if (!pixready)
				throw new InvalidOperationException($"{nameof(Libjxl)} decode failed.");

			return new JxlFrame(this);
		}

		public static JxlContainer? TryLoad(Stream imgStream, IDecoderOptions? jxlOptions)
		{
			var dec = JxlFactory.CreateDecoder();
			JxlError.Check(JxlDecoderSetKeepOrientation(dec, JXL_TRUE));
			JxlError.Check(JxlDecoderSubscribeEvents(dec, (int)JxlDecoderStatus.JXL_DEC_BASIC_INFO));

			using var buff = BufferPool.RentLocal<byte>((int)JxlDecoderSizeHintBasicInfo(dec));
			fixed (byte* pbuf = buff)
			{
				long stmpos = imgStream.Position;

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

				if (status == JxlDecoderStatus.JXL_DEC_BASIC_INFO)
				{
					JxlDecoderReleaseInput(dec);
					return new JxlContainer(imgStream, stmpos, dec);
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

			GC.SuppressFinalize(this);
		}

		~JxlContainer() => Dispose();

		private sealed class JxlFrame : IImageFrame
		{
			private readonly JxlContainer container;
			private JxlPixelSource pixsrc;

			public JxlFrame(JxlContainer cont) => container = cont;

			double IImageFrame.DpiX => 96;

			double IImageFrame.DpiY => 96;

			Orientation IImageFrame.ExifOrientation => (Orientation)container.basinfo.orientation;

			ReadOnlySpan<byte> IImageFrame.IccProfile => container.profile?.ProfileBytes ?? Array.Empty<byte>();

			public IPixelSource PixelSource
			{
				get
				{
					if (container.decoder == default)
						throw new ObjectDisposedException(nameof(JxlContainer));

					return pixsrc ??= new JxlPixelSource(container);
				}
			}

			public void Dispose() => pixsrc?.Dispose();
		}

		private sealed class JxlPixelSource : PixelSource
		{
			private readonly JxlContainer container;
			private PixelBuffer<BufferType.Caching> frameBuff;
			private GCHandle gchandle;
			private int lastseen = -1;
			private bool fullbuffer, abortdecode;

			public override PixelFormat Format { get; }

			public override int Width => (int)container.basinfo.xsize;
			public override int Height => (int)container.basinfo.ysize;

			public JxlPixelSource(JxlContainer cont)
			{
				Format = 
					cont.basinfo.alpha_bits != 0 ? PixelFormat.Rgba32 :
					cont.basinfo.num_color_channels == 3 ? PixelFormat.Rgb24 :
					PixelFormat.Grey8;

				container = cont;
				frameBuff = new PixelBuffer<BufferType.Caching>(8, MathUtil.PowerOfTwoCeiling((int)cont.basinfo.xsize * Format.BytesPerPixel, IntPtr.Size));

				gchandle = GCHandle.Alloc(this, GCHandleType.Weak);

				setDecoderCallback();
			}

#if NET5_0_OR_GREATER
			[UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvCdecl) })]
			static
#endif
			private void imageOutCallback(void* pinst, nuint x, nuint y, nuint w, void* pb)
			{
				if (pinst is null || GCHandle.FromIntPtr((IntPtr)pinst).Target is not JxlPixelSource src || src.abortdecode)
					return;

				int line = (int)y;
				if (!src.fullbuffer && (line != src.lastseen + 1 || x != 0 || (int)w != src.Width))
				{
					src.abortdecode = true;
					return;
				}

				src.lastseen = line;

				int bpp = src.Format.BytesPerPixel;
				var span = src.frameBuff.PrepareLoad(line, 1).Slice((int)x * bpp);
				new ReadOnlySpan<byte>(pb, (int)w * bpp).CopyTo(span);
			}

			private void setDecoderCallback(bool rewind = false)
			{
				if (rewind)
					container.moveToFrameData();

				Debug.Assert(JxlDecoderProcessInput(container.decoder) == JxlDecoderStatus.JXL_DEC_NEED_IMAGE_OUT_BUFFER);

				var pixfmt = container.pixelfmt;
				JxlError.Check(JxlDecoderSetImageOutCallback(container.decoder, &pixfmt, pfnImageOutCallback, (void*)(IntPtr)gchandle));
			}

			private void loadBuffer(int line)
			{
				var stream = container.stream;
				var dec = container.decoder;

				using var buff = BufferPool.RentLocal<byte>(4096);
				fixed (byte* pbuf = buff)
				{
					var status = default(JxlDecoderStatus);
					while (true)
					{
						status = JxlDecoderProcessInput(dec);
						if (status == JxlDecoderStatus.JXL_DEC_NEED_MORE_INPUT && !frameBuff.ContainsLine(line))
						{
							int keep = (int)JxlDecoderReleaseInput(dec);
							if (keep != 0)
								Buffer.MemoryCopy(pbuf + (buff.Length - keep), pbuf, buff.Length, keep);

							var span = buff.Span.Slice(0, keep + stream.Read(buff.Span.Slice(keep)));
							if (span.Length == keep)
								break;

							JxlError.Check(JxlDecoderSetInput(dec, pbuf, (uint)span.Length));
						}
						else
							break;

						if (abortdecode)
						{
							abortdecode = false;
							setDecoderCallback(true);
							lastseen = -1;

							frameBuff = new PixelBuffer<BufferType.Caching>(Height, MathUtil.PowerOfTwoCeiling(Width * Format.BytesPerPixel, IntPtr.Size));
							fullbuffer = true;
						}
					}

					nuint rewind = JxlDecoderReleaseInput(dec);
					if (rewind != 0)
						stream.Seek(-(long)rewind, SeekOrigin.Current);
				}
			}

			protected override void CopyPixelsInternal(in PixelArea prc, int cbStride, int cbBufferSize, byte* pbBuffer)
			{
				if (gchandle == default)
					throw new ObjectDisposedException(nameof(JxlPixelSource));

				int bpp = Format.BytesPerPixel;
				for (int y = 0; y < prc.Height; y++)
				{
					int line = prc.Y + y;
					if (!frameBuff.ContainsLine(line))
						loadBuffer(line);

					var span = frameBuff.PrepareRead(line, 1).Slice(prc.X * bpp, prc.Width * bpp);
					span.CopyTo(new Span<byte>(pbBuffer + y * cbStride, cbStride));
				}
			}

			protected override void Dispose(bool disposing)
			{
				if (gchandle == default)
					return;

				gchandle.Free();
				gchandle = default;

				frameBuff.Dispose();

				base.Dispose(disposing);
			}

			public override string ToString() => nameof(JxlPixelSource);

#if !NET5_0_OR_GREATER
			[UnmanagedFunctionPointer(CallingConvention.Cdecl)] private delegate void ImageOutCallback(void* pinst, nuint x, nuint y, nuint w, void* pb);
			private static readonly ImageOutCallback delImageOutCallback = typeof(JxlPixelSource).CreateMethodDelegate<ImageOutCallback>(nameof(imageOutCallback));
#endif

			private static readonly delegate* unmanaged[Cdecl]<void*, nuint, nuint, nuint, void*, void> pfnImageOutCallback =
#if NET5_0_OR_GREATER
				&imageOutCallback;
#else
				(delegate* unmanaged[Cdecl]<void*, nuint, nuint, nuint, void*, void>)Marshal.GetFunctionPointerForDelegate(delImageOutCallback);
#endif
		}
	}

	internal sealed unsafe class JxlEncoder : IImageEncoder
	{
		private readonly IJxlEncoderOptions options;
		private readonly Stream stream;

		private IntPtr encoder, encopt;
		private bool written;

		public static JxlEncoder Create(Stream outStream, IEncoderOptions? jxlOptions) => new(outStream, jxlOptions);

		private JxlEncoder(Stream outStream, IEncoderOptions? jxlOptions)
		{
			stream = outStream;
			options = jxlOptions is IJxlEncoderOptions opt ? opt : JxlLossyEncoderOptions.Default;

			encoder = JxlFactory.CreateEncoder();
			encopt = JxlEncoderOptionsCreate(encoder, default);
			JxlError.Check(JxlEncoderOptionsSetEffort(encopt, (int)options.EncodeSpeed));
			JxlError.Check(JxlEncoderOptionsSetDecodingSpeed(encopt, (int)options.DecodeSpeed));

			if (options is JxlLossyEncoderOptions lopt)
			{
				// current version of encoder will AV if this is set over 4 (including the default of 7) for lossy
				if (options.EncodeSpeed > JxlEncodeSpeed.Cheetah)
					JxlError.Check(JxlEncoderOptionsSetEffort(encopt, 4));

				JxlError.Check(JxlEncoderOptionsSetDistance(encopt, lopt.Distance));
			}
			else
			{
				JxlError.Check(JxlEncoderOptionsSetLossless(encopt, JXL_TRUE));
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
				using var tran = new MagicScaler.Transforms.ConversionTransform(source.AsPixelSource(), dstfmt);
				((IPixelSource)tran).CopyPixels(sourceArea, stride, pixbuff.Span);
			}
			else
			{
				source.CopyPixels(sourceArea, stride, pixbuff.Span);
			}

			if (options is JxlLossyEncoderOptions lopt && lopt.Distance == default)
				JxlError.Check(JxlEncoderOptionsSetDistance(encopt, JxlLossyEncoderOptions.DistanceFromQuality(SettingsUtil.GetDefaultQuality(Math.Max(sourceArea.Width, sourceArea.Height)))));

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
				fixed (byte* pp = &prof.ProfileBytes.GetDataRef())
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
				data_type = JxlDataType.JXL_TYPE_UINT8
			};

			using var stmbuff = BufferPool.RentLocal<byte>(1 << 14);
			var stmspan = stmbuff.Span;
			fixed (byte* pb = pixbuff, pf = stmspan)
			{
				JxlError.Check(JxlEncoderAddImageFrame(encopt, &pixfmt, pb, (uint)stride * basinf.ysize));
				JxlEncoderCloseInput(encoder);

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

	internal static unsafe class JxlFactory
	{
		public const string libjxl = nameof(libjxl);
		public const uint libjxlver = 6001;

		private static readonly Lazy<bool> dependencyValid = new(() => {
#if NETFRAMEWORK
			// netfx doesn't have RID-based native dependency resolution, so we include a .props
			// file that copies binaries for all supported architectures to the output folder,
			// then make a perfunctory attempt to load the right one before the first P/Invoke.
			if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
			{
				[DllImport("kernel32", ExactSpelling = true)]
				static extern IntPtr LoadLibraryW(ushort* lpLibFileName);

				string lib = Path.Combine(RuntimeInformation.ProcessArchitecture.ToString(), "jxl");
				fixed (char* plib = lib)
					LoadLibraryW((ushort*)plib);
			}
#endif

			uint ver = JxlDecoderVersion();
			if (ver != libjxlver || (ver = JxlEncoderVersion()) != libjxlver)
				throw new NotSupportedException($"Incorrect {libjxl} version was loaded.  Expected {libjxlver}, found {ver}.");

			return true;
		});

		public static IntPtr CreateDecoder() => dependencyValid.Value ? JxlDecoderCreate(default) : default;

		public static IntPtr CreateEncoder() => dependencyValid.Value ? JxlEncoderCreate(default) : default;
	}

	/// <inheritdoc cref="WindowsCodecExtensions" />
	public static class CodecCollectionExtensions
	{
		/// <inheritdoc cref="WindowsCodecExtensions.UseWicCodecs(CodecCollection, WicCodecPolicy)" />
		public static void UseLibjxl(this CodecCollection codecs)
		{
			var jxlMime = new[] { ImageMimeTypes.Jxl };
			var jxlExtension = new[] { ImageFileExtensions.Jxl };

			codecs.Add(new DecoderInfo(
				JxlFactory.libjxl,
				jxlMime,
				jxlExtension,
				new ContainerPattern[] {
					new(0, new byte[] { 0xff, 0x0a }, new byte[] { 0xff, 0xff }),
					new(0, new byte[] { 0x00, 0x00, 0x00, 0x0c, 0x4a, 0x58, 0x4c, 0x20, 0x0d, 0x0a, 0x87, 0x0a }, new byte[] { 0xff, 0xff, 0xff, 0xff, 0xff, 0xff, 0xff, 0xff, 0xff, 0xff, 0xff, 0xff })
				},
				null,
				JxlContainer.TryLoad,
				true,
				false,
				false
			));
			codecs.Add(new EncoderInfo(
				JxlFactory.libjxl,
				jxlMime,
				jxlExtension,
				new[] { PixelFormat.Grey8.FormatGuid, PixelFormat.Rgb24.FormatGuid, PixelFormat.Rgba32.FormatGuid },
				JxlLossyEncoderOptions.Default,
				JxlEncoder.Create,
				true,
				false,
				false,
				true
			));
		}
	}
}
