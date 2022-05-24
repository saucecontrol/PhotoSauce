// Copyright © Clinton Ingram and Contributors.  Licensed under the MIT License.

using System;
using System.IO;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.InteropServices;
#if NET5_0_OR_GREATER
using System.Runtime.CompilerServices;
#endif

using PhotoSauce.MagicScaler;
using PhotoSauce.Interop.Libjxl;
using static PhotoSauce.Interop.Libjxl.Libjxl;

namespace PhotoSauce.NativeCodecs.Libjxl;

internal sealed unsafe class JxlContainer : IImageContainer, IIccProfileSource, IDisposable
{
	private readonly Stream stream;
	private readonly long stmpos;
	private IntPtr decoder;

	private RentedBuffer<byte> profilebuff;
	private JxlBasicInfo basinfo;
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

	int IIccProfileSource.ProfileLength => profilebuff.Length;

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

					int read = stream.Read(buff.Span[keep..]);
					if (read == keep)
						break;

					JxlError.Check(JxlDecoderSetInput(decoder, pbuf, (uint)(keep + read)));
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

					profilebuff = BufferPool.Rent<byte>((int)icclen);
					fixed (byte* picc = profilebuff)
						JxlError.Check(JxlDecoderGetColorAsICCProfile(decoder, &pixfmt, JxlColorProfileTarget.JXL_COLOR_PROFILE_TARGET_DATA, picc, (nuint)profilebuff.Length));
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
			throw new ArgumentOutOfRangeException(nameof(index), "Invalid frame index.");

		moveToFrameData(true);

		if (!pixready)
			throw new InvalidOperationException($"{nameof(Libjxl)} decode failed.");

		return new JxlFrame(this);
	}

	void IIccProfileSource.CopyProfile(Span<byte> dest) => profilebuff.Span.CopyTo(dest);

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
				if (keep != 0)
					Buffer.MemoryCopy(pbuf + (buff.Length - keep), pbuf, buff.Length, keep);

				int read = imgStream.Read(buff.Span[keep..]);
				if (read == 0)
					break;

				JxlError.Check(JxlDecoderSetInput(dec, pbuf, (uint)(keep + read)));
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

	private void dispose(bool disposing)
	{
		if (decoder == default)
			return;

		profilebuff.Dispose();
		profilebuff = default;

		JxlDecoderDestroy(decoder);
		decoder = default;

		if (disposing)
			GC.SuppressFinalize(this);
	}

	public void Dispose() => dispose(true);

	~JxlContainer() => dispose(false);

	private sealed class JxlFrame : IImageFrame, IMetadataSource
	{
		private readonly JxlContainer container;
		private JxlPixelSource? pixsrc;

		public JxlFrame(JxlContainer cont) => container = cont;

		public IPixelSource PixelSource
		{
			get
			{
				if (container.decoder == default)
					throw new ObjectDisposedException(nameof(JxlContainer));

				return pixsrc ??= new JxlPixelSource(container);
			}
		}

		public bool TryGetMetadata<T>([NotNullWhen(true)] out T? metadata) where T : IMetadata
		{
			if (typeof(T) == typeof(OrientationMetadata))
			{
				metadata = (T)(object)(new OrientationMetadata((Orientation)container.basinfo.orientation));
				return true;
			}

			if (typeof(T) == typeof(IIccProfileSource))
			{
				metadata = (T)(object)container;
				return true;
			}

			metadata = default;
			return false;
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
			var span = src.frameBuff.PrepareLoad(line, 1)[((int)x * bpp)..];
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

						int read = stream.Read(buff.Span[keep..]);
						if (read == 0)
							break;

						JxlError.Check(JxlDecoderSetInput(dec, pbuf, (uint)(keep + read)));
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
			if (!gchandle.IsAllocated)
				return;

			gchandle.Free();
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
