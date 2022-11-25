// Copyright © Clinton Ingram and Contributors.  Licensed under the MIT License.

using System;
using System.IO;
using System.Buffers.Binary;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;

using PhotoSauce.MagicScaler;
using PhotoSauce.Interop.Libheif;
using static PhotoSauce.Interop.Libheif.Libheif;

namespace PhotoSauce.NativeCodecs.Libheif;

internal sealed unsafe class HeifContainer : IImageContainer
{
	private readonly string ftype;
	private readonly Stream stream;
	private void* handle;
	private HeifReader* reader;

	private HeifContainer(string mime, Stream stm, HeifReader* rdr, void* frm)
	{
		ftype = mime;
		stream = stm;
		reader = rdr;
		handle = frm;
	}

	public string MimeType => ftype;

	int IImageContainer.FrameCount => 1;

	IImageFrame IImageContainer.GetFrame(int index)
	{
		if (index != 0)
			throw new ArgumentOutOfRangeException(nameof(index), "Invalid frame index.");

		return new HeifFrame(this);
	}

	public static HeifContainer? TryLoad(Stream imgStream, IDecoderOptions? _)
	{
		long stmpos = imgStream.Position;

		byte* buff = stackalloc byte[12];
		imgStream.FillBuffer(new Span<byte>(buff, 12));
		imgStream.Position = stmpos;

		string mime = HeifFactory.GetMimeType(buff, 12);

		var rdr = HeifReader.Wrap(imgStream);
		var ctx = HeifFactory.CreateContext();

		if (HeifResult.Succeeded(heif_context_read_from_reader(ctx, HeifReader.Impl, rdr, null)))
		{
			void* hfrm;
			if (HeifResult.Succeeded(heif_context_get_primary_image_handle(ctx, &hfrm)))
			{
				heif_context_free(ctx);
				return new HeifContainer(mime, imgStream, rdr, hfrm);
			}
		}

		imgStream.Position = stmpos;
		heif_context_free(ctx);
		HeifReader.Free(rdr);

		return null;
	}

	private void ensureHandle()
	{
		if (handle is null)
			ThrowHelper.ThrowObjectDisposed(nameof(HeifContainer));
	}

	private void dispose(bool disposing)
	{
		if (handle is null)
			return;

		heif_image_handle_release(handle);
		handle = null;

		HeifReader.Free(reader);
		reader = null;

		if (disposing)
			GC.SuppressFinalize(this);
	}

	public void Dispose() => dispose(true);

	~HeifContainer()
	{
		ThrowHelper.ThrowIfFinalizerExceptionsEnabled(nameof(HeifContainer));

		dispose(false);
	}

	private sealed class HeifFrame : IImageFrame, IMetadataSource, IIccProfileSource, IExifSource
	{
		private readonly HeifContainer container;
		private HeifPixelSource? pixsrc;
		private RentedBuffer<byte> exifbuff;

		public HeifFrame(HeifContainer cont) => container = cont;

		public IPixelSource PixelSource => pixsrc ??= new HeifPixelSource(container);

		int IIccProfileSource.ProfileLength => (int)heif_image_handle_get_raw_color_profile_size(container.handle);

		int IExifSource.ExifLength => exifbuff.Length < ExifConstants.MinExifLength ? 0 : exifbuff.Length - sizeof(int) - BinaryPrimitives.ReadInt32BigEndian(exifbuff.Span);

		public bool TryGetMetadata<T>([NotNullWhen(true)] out T? metadata) where T : IMetadata
		{
			container.ensureHandle();

			if (typeof(T) == typeof(OrientationMetadata))
			{
				// libheif can skip orientation correction, but it doesn't provide a way to access the
				// stored orientation, so we allow it to normalize and always report normal orientation.
				metadata = (T)(object)(new OrientationMetadata(Orientation.Normal));
				return true;
			}

			if (typeof(T) == typeof(IIccProfileSource))
			{
				var cpt = heif_image_handle_get_color_profile_type(container.handle);
				if (cpt is heif_color_profile_type.heif_color_profile_type_rICC or heif_color_profile_type.heif_color_profile_type_prof)
				{
					metadata = (T)(object)this;
					return true;
				}
			}

			if (typeof(T) == typeof(IExifSource))
			{
				uint blockid;
				if (heif_image_handle_get_list_of_metadata_block_IDs(container.handle, (sbyte*)"Exif"u8.GetAddressOf(), &blockid, 1) != 0)
				{
					int blocklen = (int)heif_image_handle_get_metadata_size(container.handle, blockid);
					exifbuff = BufferPool.Rent<byte>(blocklen);
					fixed (byte* pbuff = exifbuff)
						HeifResult.Check(heif_image_handle_get_metadata(container.handle, blockid, pbuff));

					metadata = (T)(object)this;
					return true;
				}
			}

			metadata = default;
			return false;
		}

		void IIccProfileSource.CopyProfile(Span<byte> dest)
		{
			container.ensureHandle();
			if (dest.Length < ((IIccProfileSource)this).ProfileLength)
				throw new ArgumentException("Destination too small.", nameof(dest));

			fixed (byte* pcpd = dest)
				HeifResult.Check(heif_image_handle_get_raw_color_profile(container.handle, pcpd));
		}

		void IExifSource.CopyExif(Span<byte> dest)
		{
			container.ensureHandle();
			int exiflen = ((IExifSource)this).ExifLength;

			exifbuff.Span[^exiflen..].CopyTo(dest);
		}

		public void Dispose()
		{
			exifbuff.Dispose();
			exifbuff = default;
		}
	}

	private sealed class HeifPixelSource : PixelSource
	{
		private readonly HeifContainer container;
		private void* handle;
		private byte* pixels;
		private int stride;

		public override PixelFormat Format { get; }

		public override int Width { get; }
		public override int Height { get; }

		public HeifPixelSource(HeifContainer cont)
		{
			cont.ensureHandle();

			Format = PixelFormat.Rgb24;
			Width = heif_image_handle_get_width(cont.handle);
			Height = heif_image_handle_get_height(cont.handle);

			container = cont;
		}

		protected override void CopyPixelsInternal(in PixelArea prc, int cbStride, int cbBufferSize, byte* pbBuffer)
		{
			if (pixels is null)
				decodeImage();

			int bpp = Format.BytesPerPixel;

			for (int i = 0; i < prc.Height; i++)
				Buffer.MemoryCopy(pixels + (prc.Y + i) * stride + prc.X * bpp, pbBuffer + i * cbStride, cbStride, prc.Width * bpp);
		}

		private void decodeImage()
		{
			container.ensureHandle();

			void* img;
			HeifResult.Check(heif_decode_image(container.handle, &img, heif_colorspace.heif_colorspace_RGB, heif_chroma.heif_chroma_interleaved_RGB, null));

			var chan = heif_channel.heif_channel_interleaved;
			Debug.Assert(heif_image_has_channel(img, chan) != 0);
			Debug.Assert(heif_image_get_width(img, chan) == Width);
			Debug.Assert(heif_image_get_height(img, chan) == Height);
			Debug.Assert(heif_image_get_bits_per_pixel(img, chan) == Format.BitsPerPixel);

			int strideout;
			pixels = heif_image_get_plane_readonly(img, chan, &strideout);
			stride = strideout;
			handle = img;
		}

		protected override void Dispose(bool disposing)
		{
			if (handle is null)
				return;

			heif_image_release(handle);
			handle = null;
			pixels = null;

			base.Dispose(disposing);
		}

		public override string ToString() => nameof(HeifPixelSource);
	}
}
