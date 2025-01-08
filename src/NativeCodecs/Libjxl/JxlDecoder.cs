// Copyright © Clinton Ingram and Contributors
// SPDX-License-Identifier: MIT

using System;
using System.IO;
using System.Buffers.Binary;
using System.Diagnostics.CodeAnalysis;

using PhotoSauce.MagicScaler;
using PhotoSauce.Interop.Libjxl;
using static PhotoSauce.Interop.Libjxl.Libjxl;

namespace PhotoSauce.NativeCodecs.Libjxl;

internal sealed unsafe class JxlContainer : IImageContainer, IMetadataSource, IIccProfileSource, IExifSource
{
	private readonly Stream stream;
	private readonly long stmpos;
	private readonly int frameCount, frameOffset, frameCountRaw;
	private readonly int width, height;
	private readonly Orientation orientation;
	private readonly PixelFormat format;
	private void* decoder;

	private RentedBuffer<byte> iccpData;
	private RentedBuffer<byte> exifData;
	private JxlAnimationHeader? animation;
	private int currentFrame = int.MaxValue;

	private JxlContainer(Stream stm, long pos, void* dec, IDecoderOptions? opt)
	{
		(stream, stmpos) = (stm, pos);
		decoder = dec;

		JxlBasicInfo info;
		JxlError.Check(JxlDecoderGetBasicInfo(dec, &info));

		frameCountRaw = 1;
		width = checked((int)info.xsize);
		height = checked((int)info.ysize);
		orientation = (Orientation)info.orientation;

		format =
			info.alpha_bits != 0 ? PixelFormat.Rgba32 :
			info.num_color_channels == 3 ? PixelFormat.Rgb24 :
			PixelFormat.Grey8;

		nuint icclen;
		JxlError.Check(JxlDecoderGetICCProfileSize(decoder, JxlColorProfileTarget.JXL_COLOR_PROFILE_TARGET_DATA, &icclen));

		iccpData = BufferPool.Rent<byte>((int)icclen);
		fixed (byte* picc = iccpData)
			JxlError.Check(JxlDecoderGetColorAsICCProfile(decoder, JxlColorProfileTarget.JXL_COLOR_PROFILE_TARGET_DATA, picc, (nuint)iccpData.Length));

		var events = default(JxlDecoderStatus);
		if (info.have_container == JXL_TRUE)
			events |= JxlDecoderStatus.JXL_DEC_BOX;

		if (info.have_animation == JXL_TRUE)
		{
			events |= JxlDecoderStatus.JXL_DEC_FRAME;
			animation = info.animation;
			frameCountRaw = 0;
		}

		if (events != default || true)
		{
			stm.Position = pos;
			JxlDecoderRewind(dec);

			JxlError.Check(JxlDecoderSubscribeEvents(decoder, (int)events));
			JxlError.Check(JxlDecoderSetDecompressBoxes(decoder, JXL_TRUE));

			using var inbuff = BufferPool.RentLocal<byte>(1 << 12);
			fixed (byte* pin = inbuff)
			{
				while (true)
				{
					var status = JxlDecoderProcessInput(dec);
					if (status is JxlDecoderStatus.JXL_DEC_NEED_MORE_INPUT)
						status = readLoop(dec, stm, pin, inbuff.Length);

					if (status is JxlDecoderStatus.JXL_DEC_FRAME)
					{
						frameCountRaw++;
						JxlDecoderSkipCurrentFrame(dec);
					}
					else if (status is JxlDecoderStatus.JXL_DEC_BOX)
					{
						uint box;
						JxlError.Check(JxlDecoderGetBoxType(decoder, (sbyte*)&box, JXL_TRUE));
						if (box == BoxTypeExif)
							exifData = readExif(dec, stm, pin, inbuff.Length);
					}
					else
						break;
				}

				JxlDecoderCloseInput(dec);
			}

			var range = opt is IMultiFrameDecoderOptions mul ? mul.FrameRange : Range.All;
			(frameOffset, frameCount) = range.GetOffsetAndLengthNoThrow(frameCountRaw);
		}

		static RentedBuffer<byte> readExif(void* dec, Stream stm, byte* pin, int inlen)
		{
			const int outlen = 1 << 16, maxlen = outlen * 64;

			nuint exiflen = 0, chunklen = 0;
			var stream = default(MemoryStream);

			using var outbuff = BufferPool.RentLocal<byte>(outlen);
			fixed (byte* pout = outbuff)
			{
				while (true)
				{
					JxlError.Check(JxlDecoderSetBoxBuffer(dec, pout, outlen));

					var status = JxlDecoderProcessInput(dec);
					if (status is JxlDecoderStatus.JXL_DEC_NEED_MORE_INPUT)
						status = readLoop(dec, stm, pin, inlen);

					if (status is JxlDecoderStatus.JXL_DEC_BOX_NEED_MORE_OUTPUT)
					{
						chunklen = outlen - JxlDecoderReleaseBoxBuffer(dec);
						exiflen += chunklen;

						stream ??= new MemoryStream(outlen * 2);
						if (stream.Length < maxlen)
							stream.Write(new ReadOnlySpan<byte>(pout, (int)chunklen));
					}
					else
						break;
				}

				chunklen = outlen - JxlDecoderReleaseBoxBuffer(dec);
				exiflen += chunklen;

				if (exiflen < sizeof(uint) + ExifConstants.MinExifLength)
					return default;

				if (stream is not null)
				{
					if (stream.Length < maxlen)
						stream.Write(new ReadOnlySpan<byte>(pout, (int)chunklen));

					stream.Position = 0;

					uint offset;
					stream.Read(new Span<byte>(&offset, sizeof(uint)));
					if (BitConverter.IsLittleEndian)
						offset = BinaryPrimitives.ReverseEndianness(offset);

					stream.Seek(offset, SeekOrigin.Current);
					if (stream.Length <= stream.Position)
						return default;

					var exif = BufferPool.Rent<byte>((int)(stream.Length - stream.Position));
					stream.Read(exif.Span);
					return exif;
				}
				else
				{
					uint offset = checked(BinaryPrimitives.ReadUInt32BigEndian(outbuff.Span) + sizeof(uint));
					if (exiflen <= offset)
						return default;

					var exif = BufferPool.Rent<byte>((int)(exiflen - offset));
					outbuff.Span[(int)offset..(int)exiflen].CopyTo(exif.Span);
					return exif;
				}
			}
		}
	}

	public string MimeType => ImageMimeTypes.Jxl;

	int IImageContainer.FrameCount => frameCount;

	int IIccProfileSource.ProfileLength => iccpData.Length;

	int IExifSource.ExifLength => exifData.Length;

	IImageFrame IImageContainer.GetFrame(int index)
	{
		ensureHandle();

		index += frameOffset;
		if ((uint)index >= (uint)(frameOffset + frameCount))
			throw new ArgumentOutOfRangeException(nameof(index), "Invalid frame index.");

		if (index < currentFrame)
		{
			currentFrame = 0;
			stream.Position = stmpos;
			JxlDecoderRewind(decoder);

			JxlError.Check(JxlDecoderSubscribeEvents(decoder, (int)JxlDecoderStatus.JXL_DEC_FULL_IMAGE));
			JxlError.Check(JxlDecoderSetUnpremultiplyAlpha(decoder, JXL_TRUE));
			JxlError.Check(JxlDecoderSetDesiredIntensityTarget(decoder, 1.0f));
		}

		if (index > currentFrame && JxlDecoderProcessInput(decoder) is JxlDecoderStatus.JXL_DEC_NEED_IMAGE_OUT_BUFFER)
			JxlError.Check(JxlDecoderSkipCurrentFrame(decoder));

		if (index > currentFrame + 1)
			JxlDecoderSkipFrames(decoder, (uint)(index - currentFrame - 1));

		var status = JxlDecoderProcessInput(decoder);
		if (status is JxlDecoderStatus.JXL_DEC_NEED_MORE_INPUT or JxlDecoderStatus.JXL_DEC_ERROR)
		{
			using var inbuff = BufferPool.RentLocal<byte>(1 << 12);
			fixed (byte* pin = inbuff)
			{
				status = readLoop(decoder, stream, pin, inbuff.Length);

				nuint rewind = JxlDecoderReleaseInput(decoder);
				if (rewind != 0)
					stream.Seek(-(long)rewind, SeekOrigin.Current);
			}
		}

		JxlError.Check(status);
		currentFrame = index;

		return new JxlFrame(this);
	}

	void IIccProfileSource.CopyProfile(Span<byte> dest) => iccpData.Span.CopyTo(dest);

	void IExifSource.CopyExif(Span<byte> dest) => exifData.Span.CopyTo(dest);

	public bool TryGetMetadata<T>([NotNullWhen(true)] out T? metadata) where T : IMetadata
	{
		if (typeof(T) == typeof(AnimationContainer) && animation.HasValue)
		{
			var anicnt = new AnimationContainer(width, height, frameCountRaw, (int)animation.Value.num_loops, 0, 1f, true);

			metadata = (T)(object)anicnt;
			return true;
		}

		if (typeof(T) == typeof(OrientationMetadata))
		{
			metadata = (T)(object)(new OrientationMetadata(orientation));
			return true;
		}

		if (typeof(T) == typeof(IIccProfileSource))
		{
			metadata = (T)(object)this;
			return true;
		}

		if (typeof(T) == typeof(IExifSource))
		{
			metadata = (T)(object)this;
			return true;
		}

		metadata = default;
		return false;
	}

	public static JxlContainer? TryLoad(Stream imgStream, IDecoderOptions? options)
	{
		// JxlDecoderSizeHintBasicInfo gives an initial pessimistic estimate of 98 bytes
		const int bufflen = 128;

		void* dec = JxlFactory.CreateDecoder();
		JxlError.Check(JxlDecoderSetKeepOrientation(dec, JXL_TRUE));
		JxlError.Check(JxlDecoderSubscribeEvents(dec, (int)JxlDecoderStatus.JXL_DEC_COLOR_ENCODING));

		long stmpos = imgStream.Position;

		byte* pbuf = stackalloc byte[bufflen];
		var status = readLoop(dec, imgStream, pbuf, bufflen);
		if (status is JxlDecoderStatus.JXL_DEC_COLOR_ENCODING)
		{
			JxlDecoderCloseInput(dec);
			return new JxlContainer(imgStream, stmpos, dec, options);
		}

		imgStream.Position = stmpos;
		JxlDecoderDestroy(dec);

		return null;
	}

	private void ensureHandle()
	{
		if (decoder is null)
			ThrowHelper.ThrowObjectDisposed(nameof(JxlContainer));
	}

	private static JxlDecoderStatus readLoop(void* dec, Stream stm, byte* pbuf, int cbbuf)
	{
		var status = default(JxlDecoderStatus);
		do
		{
			int keep = (int)JxlDecoderReleaseInput(dec);
			if (keep != 0)
				stm.Seek(-keep, SeekOrigin.Current);

			int read = stm.Read(new Span<byte>(pbuf, cbbuf));
			if (read == 0)
				break;

			JxlError.Check(JxlDecoderSetInput(dec, pbuf, (uint)read));
			status = JxlDecoderProcessInput(dec);
		}
		while (status is JxlDecoderStatus.JXL_DEC_NEED_MORE_INPUT);

		return status;
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

	private sealed class JxlFrame : PixelSource, IImageFrame, IMetadataSource
	{
		private readonly JxlContainer container;
		private readonly JxlFrameHeader header;
		private byte* pixbuf;
		private bool disposed;

		public override PixelFormat Format => container.format;
		public override int Width => (int)header.layer_info.xsize;
		public override int Height => (int)header.layer_info.ysize;

		public IPixelSource PixelSource => this;

		public JxlFrame(JxlContainer cont)
		{
			JxlFrameHeader fhdr;
			JxlError.Check(JxlDecoderGetFrameHeader(cont.decoder, &fhdr));

			container = cont;
			header = fhdr;
		}

		public bool TryGetMetadata<T>([NotNullWhen(true)] out T? metadata) where T : IMetadata
		{
			if (typeof(T) == typeof(AnimationFrame) && container.animation.HasValue)
			{
				// TODO Blend and Disposal here are broken when skipping frames.
				// libjxl coalesces animation frames by default, which is good because its animation has features the pipeline doesn't support, but is bad because
				// on replay the decoder doesn't coalesce frames that are skipped. Presently, the pipeline will skip all frames before the first requested because
				// the decoder doesn't expose a way to detemine whether the current frame is used as a keyframe when it is configured to coalesce.
				var acnt = container.animation.Value;
				var duration = new Rational(acnt.tps_denominator * header.duration, acnt.tps_numerator);
				var blend = header.layer_info.blend_info.blendmode == JxlBlendMode.JXL_BLEND_REPLACE ? AlphaBlendMethod.Source : AlphaBlendMethod.BlendOver;
				var disp = header.layer_info.save_as_reference != 0 ? FrameDisposalMethod.Preserve : FrameDisposalMethod.RestoreBackground;
				var afrm = new AnimationFrame(header.layer_info.crop_x0, header.layer_info.crop_y0, duration, disp, blend, blend == AlphaBlendMethod.BlendOver);

				metadata = (T)(object)afrm;
				return true;
			}

			return container.TryGetMetadata(out metadata);
		}

		protected override void CopyPixelsInternal(in PixelArea prc, int cbStride, int cbBufferSize, byte* pbBuffer)
		{
			ensurePixelBuffer();

			int bpp = Format.BytesPerPixel;
			int stride = Width * bpp;
			for (int y = 0; y < prc.Height; y++)
			{
				uint line = (uint)(prc.Y + y);

				var span = new ReadOnlySpan<byte>(pixbuf + line * (nuint)stride, stride).Slice(prc.X * bpp, prc.Width * bpp);
				span.CopyTo(new Span<byte>(pbBuffer + y * cbStride, cbStride));
			}
		}

		private void ensurePixelBuffer()
		{
			if (pixbuf is not null)
				return;

			if (disposed)
				ThrowHelper.ThrowObjectDisposed(nameof(JxlFrame));

			container.ensureHandle();

			nuint size;
			var fmt = new JxlPixelFormat {
				num_channels = (uint)container.format.ChannelCount,
				data_type = JxlDataType.JXL_TYPE_UINT8
			};
			JxlError.Check(JxlDecoderImageOutBufferSize(container.decoder, &fmt, &size));

			pixbuf = (byte*)UnsafeUtil.NativeAlloc(size);
			if (pixbuf is null)
				ThrowHelper.ThrowOutOfMemory();

			JxlError.Check(JxlDecoderSetImageOutBuffer(container.decoder, &fmt, pixbuf, size));

			using var inbuff = BufferPool.RentLocal<byte>(1 << 12);
			fixed (byte* pin = inbuff)
			{
				var status = readLoop(container.decoder, container.stream, pin, inbuff.Length);
				if (status != JxlDecoderStatus.JXL_DEC_FULL_IMAGE && container.currentFrame < container.frameCountRaw - 1)
					throw new InvalidOperationException($"{nameof(Libjxl)} decoder failed.");
			}

			container.currentFrame++;
		}

		public override string ToString() => nameof(JxlFrame);

		protected override void Dispose(bool disposing)
		{
			if (disposed)
				return;

			disposed = true;

			if (pixbuf is not null)
			{
				UnsafeUtil.NativeFree(pixbuf);
				pixbuf = null;
			}

			base.Dispose(disposing);
		}

		~JxlFrame()
		{
			ThrowHelper.ThrowIfFinalizerExceptionsEnabled(nameof(JxlFrame));

			Dispose(false);
		}
	}
}
