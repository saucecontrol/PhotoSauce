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

internal sealed unsafe class JxlContainer : IImageContainer, IIccProfileSource, IExifSource, IDisposable
{
	private readonly Stream stream;
	private readonly long stmpos;
	private void* decoder;

	private RentedBuffer<byte> iccpData;
	private RentedBuffer<byte> exifData;
	private JxlBasicInfo basinfo;
	private bool pixready;
	private int exiflen;

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

	private JxlContainer(Stream stm, long pos, void* dec)
	{
		(stream, stmpos) = (stm, pos);
		decoder = dec;
	}

	public string MimeType => ImageMimeTypes.Jxl;

	int IImageContainer.FrameCount => 1;

	int IIccProfileSource.ProfileLength => iccpData.Length;

	int IExifSource.ExifLength => exiflen - sizeof(uint);

	private void moveToFrameData(bool readMetadata = false)
	{
		stream.Position = stmpos;
		pixready = false;

		var events = JxlDecoderStatus.JXL_DEC_FULL_IMAGE;
		if (readMetadata)
			events |= JxlDecoderStatus.JXL_DEC_BASIC_INFO | JxlDecoderStatus.JXL_DEC_COLOR_ENCODING | JxlDecoderStatus.JXL_DEC_BOX;

		JxlDecoderRewind(decoder);
		JxlError.Check(JxlDecoderSubscribeEvents(decoder, (int)events));
		JxlError.Check(JxlDecoderSetDecompressBoxes(decoder, JXL_TRUE));

		using var inbuff = BufferPool.RentLocal<byte>(4096);
		using var outbuff = BufferPool.RentLocal<byte>(1024);
		fixed (byte* pibuf = inbuff, pobuff = outbuff)
		{
			var status = default(JxlDecoderStatus);
			while (true)
			{
				status = JxlDecoderProcessInput(decoder);
				if (status == JxlDecoderStatus.JXL_DEC_NEED_MORE_INPUT)
				{
					int keep = (int)JxlDecoderReleaseInput(decoder);
					if (keep == inbuff.Length)
						break;
					else if (keep != 0)
						Buffer.MemoryCopy(pibuf + (inbuff.Length - keep), pibuf, inbuff.Length, keep);

					int read = stream.Read(inbuff.Span[keep..]);
					if (read != 0)
						JxlError.Check(JxlDecoderSetInput(decoder, pibuf, (uint)(keep + read)));
					else
						JxlDecoderCloseInput(decoder);
				}
				else if (status == JxlDecoderStatus.JXL_DEC_BASIC_INFO)
				{
					fixed (JxlBasicInfo* pbi = &basinfo)
						JxlError.Check(JxlDecoderGetBasicInfo(decoder, pbi));
				}
				else if (status == JxlDecoderStatus.JXL_DEC_COLOR_ENCODING)
				{
					nuint icclen;
					JxlError.Check(JxlDecoderGetICCProfileSize(decoder, null, JxlColorProfileTarget.JXL_COLOR_PROFILE_TARGET_DATA, &icclen));

					iccpData = BufferPool.Rent<byte>((int)icclen);
					fixed (byte* picc = iccpData)
						JxlError.Check(JxlDecoderGetColorAsICCProfile(decoder, null, JxlColorProfileTarget.JXL_COLOR_PROFILE_TARGET_DATA, picc, (nuint)iccpData.Length));
				}
				else if (status == JxlDecoderStatus.JXL_DEC_BOX)
				{
					uint box;
					JxlError.Check(JxlDecoderGetBoxType(decoder, (sbyte*)&box, JXL_TRUE));
					if (box == BoxTypeExif)
					{
						ulong size;
						JxlError.Check(JxlDecoderGetBoxSizeRaw(decoder, &size));

						if (exifData.IsEmpty && size < 1 << 20)
						{
							JxlError.Check(JxlDecoderSetBoxBuffer(decoder, pobuff, (uint)outbuff.Length));
							exifData = BufferPool.Rent<byte>(outbuff.Length);
						}
					}
					else if (!exifData.IsEmpty)
					{
						int rem = (int)JxlDecoderReleaseBoxBuffer(decoder);
						int writ = outbuff.Length - rem;

						outbuff.Span[..writ].CopyTo(exifData.Span[exiflen..]);
						exiflen += writ;
					}
				}
				else if (status == JxlDecoderStatus.JXL_DEC_BOX_NEED_MORE_OUTPUT)
				{
					int rem = (int)JxlDecoderReleaseBoxBuffer(decoder);
					int writ = outbuff.Length - rem;

					if (exiflen + writ + outbuff.Length > exifData.Length)
					{
						var tmp = BufferPool.Rent<byte>(exifData.Length * 2);
						exifData.Span.CopyTo(tmp.Span);
						exifData.Dispose();
						exifData = tmp;
					}

					outbuff.Span[..writ].CopyTo(exifData.Span[exiflen..]);
					exiflen += writ;

					JxlError.Check(JxlDecoderSetBoxBuffer(decoder, pobuff, (uint)outbuff.Length));
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

	void IIccProfileSource.CopyProfile(Span<byte> dest) => iccpData.Span.CopyTo(dest);

	void IExifSource.CopyExif(Span<byte> dest) => exifData.Span[sizeof(uint)..exiflen].CopyTo(dest);

	public static JxlContainer? TryLoad(Stream imgStream, IDecoderOptions? jxlOptions)
	{
		var dec = JxlFactory.CreateDecoder();
		JxlError.Check(JxlDecoderSetKeepOrientation(dec, JXL_TRUE));
		JxlError.Check(JxlDecoderSetUnpremultiplyAlpha(dec, JXL_TRUE));
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
				if (keep == buff.Length)
					break;
				else if (keep != 0)
					Buffer.MemoryCopy(pbuf + (buff.Length - keep), pbuf, buff.Length, keep);

				int read = imgStream.Read(buff.Span[keep..]);
				if (read != 0)
					JxlError.Check(JxlDecoderSetInput(dec, pbuf, (uint)(keep + read)));
				else
					JxlDecoderCloseInput(dec);
			}

			if (status == JxlDecoderStatus.JXL_DEC_BASIC_INFO)
			{
				JxlDecoderReleaseInput(dec);
				return new JxlContainer(imgStream, stmpos, dec);
			}

			imgStream.Position = stmpos;
			JxlDecoderDestroy(dec);

			return null;
		}
	}

	private void dispose(bool disposing)
	{
		if (decoder is null)
			return;

		JxlDecoderDestroy(decoder);
		decoder = null;

		if (disposing)
		{
			iccpData.Dispose();
			iccpData = default;

			exifData.Dispose();
			exifData= default;

			GC.SuppressFinalize(this);
		}
	}

	public void Dispose() => dispose(true);

	~JxlContainer()
	{
		ThrowHelper.ThrowIfFinalizerExceptionsEnabled(nameof(JxlContainer));

		dispose(false);
	}

	private sealed class JxlFrame(JxlContainer cont) : IImageFrame, IMetadataSource
	{
		private readonly JxlContainer container = cont;
		private JxlPixelSource? pixsrc;

		public IPixelSource PixelSource
		{
			get
			{
				if (container.decoder == default)
					ThrowHelper.ThrowObjectDisposed(nameof(JxlContainer));

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

			if (typeof(T) == typeof(IExifSource) && container.exiflen > sizeof(uint))
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
		[UnmanagedCallersOnly(CallConvs = [ typeof(CallConvCdecl) ])]
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
			JxlError.Check(JxlDecoderSetImageOutCallback(container.decoder, &pixfmt, pfnImageOutCallback, (void*)GCHandle.ToIntPtr(gchandle)));
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
						if (keep == buff.Length)
							break;
						else if (keep != 0)
							Buffer.MemoryCopy(pbuf + (buff.Length - keep), pbuf, buff.Length, keep);

						int read = stream.Read(buff.Span[keep..]);
						if (read != 0)
							JxlError.Check(JxlDecoderSetInput(dec, pbuf, (uint)(keep + read)));
						else
							JxlDecoderCloseInput(dec);
					}
					else
						break;

					if (abortdecode)
					{
						abortdecode = false;
						setDecoderCallback(true);
						lastseen = -1;

						frameBuff.Dispose();
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
			if (!gchandle.IsAllocated)
				ThrowHelper.ThrowObjectDisposed(nameof(JxlPixelSource));

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
