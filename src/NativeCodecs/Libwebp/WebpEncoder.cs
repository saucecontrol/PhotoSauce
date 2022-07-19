// Copyright © Clinton Ingram and Contributors.  Licensed under the MIT License.

using System;
using System.IO;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Runtime.CompilerServices;

using PhotoSauce.MagicScaler;
using PhotoSauce.Interop.Libwebp;
using static PhotoSauce.Interop.Libwebp.Libwebp;
using static PhotoSauce.Interop.Libwebp.Libwebpmux;

namespace PhotoSauce.NativeCodecs.Libwebp;

internal sealed unsafe class WebpEncoder : IImageEncoder
{
	private readonly IWebpEncoderOptions options;
	private readonly Stream stream;

	private IntPtr handle;
	private bool written;

	public static WebpEncoder Create(Stream outStream, IEncoderOptions? webpOptions) => new(outStream, webpOptions);

	private WebpEncoder(Stream outStream, IEncoderOptions? webpOptions)
	{
		stream = outStream;
		options = webpOptions as IWebpEncoderOptions ?? (webpOptions is ILossyEncoderOptions opt ? new WebpLossyEncoderOptions(opt.Quality) : WebpLossyEncoderOptions.Default);

		handle = WebPMuxNew();
	}

	public void WriteFrame(IPixelSource source, IMetadataSource metadata, Rectangle sourceArea)
	{
		if (written)
			throw new InvalidOperationException("An image frame has already been written, and this encoder does not yet support multiple frames.");

		if (sourceArea == default)
			sourceArea = new(0, 0, source.Width, source.Height);

		if (sourceArea.Width > WEBP_MAX_DIMENSION || sourceArea.Height > WEBP_MAX_DIMENSION)
			throw new NotSupportedException($"WebP supports a max of {WEBP_MAX_DIMENSION} pixels in either dimension.");

		var srcfmt = PixelFormat.FromGuid(source.Format);
		var dstfmt =
			srcfmt == PixelFormat.Grey8 || srcfmt == PixelFormat.Bgr24 || srcfmt == PixelFormat.Bgra32 ? PixelFormat.Bgra32 :
			throw new NotSupportedException("Unsupported pixel format.");

		WebPConfig config;
		WebPConfigInit(&config);

		if (options is WebpAdvancedEncoderOptions aopt)
		{
			config = aopt.Config;
		}
		else if (options is WebpLosslessEncoderOptions fopt)
		{
			int level = fopt.Level.Clamp(0, 9);
			WebpResult.Check(WebPConfigLosslessPreset(&config, level));
		}
		else
		{
			int quality = 0;
			if (options is WebpLossyEncoderOptions lopt)
				quality = lopt.Quality.Clamp(0, 100);

			if (quality == default)
				quality = SettingsUtil.GetDefaultQuality(Math.Max(sourceArea.Width, sourceArea.Height));

			WebpResult.Check(WebPConfigPreset(&config, WebPPreset.WEBP_PRESET_DEFAULT, quality));
		}

		if (WebPValidateConfig(&config) == 0)
			throw new InvalidOperationException("Invalid WebP encoder options.");

		writeIccp(metadata);

		WebPPicture picture;
		WebPMemoryWriter writer;
		WebPPictureInit(&picture);
		WebPMemoryWriterInit(&writer);

		try
		{
			picture.width = sourceArea.Width;
			picture.height = sourceArea.Height;
			picture.use_argb = 1;

			if (WebPPictureAlloc(&picture) == 0)
				throw new OutOfMemoryException();

			int stride = picture.argb_stride * dstfmt.BytesPerPixel;
			int len = checked(stride * picture.height);
			if (srcfmt != dstfmt)
			{
				using var tran = new MagicScaler.Transforms.ConversionTransform(source.AsPixelSource(), dstfmt);
				((IPixelSource)tran).CopyPixels(sourceArea, stride, new Span<byte>(picture.argb, len));
			}
			else
			{
				source.CopyPixels(sourceArea, stride, new Span<byte>(picture.argb, len));
			}

			picture.writer = pfnMemoryWrite;
			picture.custom_ptr = &writer;

			if (WebPEncode(&config, &picture) == 0)
				WebpResult.Check(picture.error_code);

			var img = new WebPData { size = writer.size, bytes = writer.mem };
			WebpResult.Check(WebPMuxSetImage(handle, &img, 1));
		}
		finally
		{
			WebPMemoryWriterClear(&writer);
			WebPPictureFree(&picture);
		}

		writeExif(metadata);

		written = true;
	}

	public void Commit()
	{
		if (!written)
			throw new InvalidOperationException("An image frame has not been written.");

		WebPData data = default;
		try
		{
			WebpResult.Check(WebPMuxAssemble(handle, &data));
			stream.Write(new Span<byte>(data.bytes, checked((int)data.size)));
		}
		finally
		{
			WebPFree(data.bytes);
		}
	}

	private void writeIccp(IMetadataSource metadata)
	{
		if (written || !metadata.TryGetMetadata<ColorProfileMetadata>(out var prof))
			return;

		var profile = prof.Profile.ProfileBytes;
		fixed (byte* bp = &profile.GetDataRef())
		{
			var data = new WebPData { size = (uint)profile.Length, bytes = bp };
			setChunk(WebpConstants.IccpTag, data);
		}
	}

	private void writeExif(IMetadataSource metadata)
	{
		if (written)
			return;

		var orient = Orientation.Normal;
		if (metadata.TryGetMetadata<OrientationMetadata>(out var ormd))
			orient = ormd.Orientation.Clamp();

		if (!metadata.TryGetMetadata<ResolutionMetadata>(out var remd) || !remd.IsValid)
			remd = ResolutionMetadata.Default;

		if (remd.Units != ResolutionUnit.Inch)
			remd = remd.ToDpi();

		int tags = orient == Orientation.Normal ? 3 : 4;
		using var exif = ExifWriter.Create(tags, sizeof(Rational) * 2);

		if (orient != Orientation.Normal)
			exif.Write(ExifTags.Tiff.Orientation, ExifType.Short, (short)orient);
		exif.Write(ExifTags.Tiff.ResolutionX, ExifType.Rational, remd.ResolutionX);
		exif.Write(ExifTags.Tiff.ResolutionY, ExifType.Rational, remd.ResolutionY);
		exif.Write(ExifTags.Tiff.ResolutionUnit, ExifType.Short, (short)2);
		exif.Finish();

		var exifspan = exif.Span;
		fixed (byte* bp = exifspan)
		{
			var data = new WebPData { size = (uint)exifspan.Length, bytes = bp };
			setChunk(WebpConstants.ExifTag, data);
		}
	}

	private void setChunk(uint fourcc, WebPData data)
	{
		WebpResult.Check(WebPMuxSetChunk(handle, (sbyte*)&fourcc, &data, 1));
	}

	private void dispose(bool disposing)
	{
		if (handle == default)
			return;

		WebPMuxDelete(handle);
		handle = default;

		if (disposing)
			GC.SuppressFinalize(this);
	}

	public void Dispose() => dispose(true);

	~WebpEncoder() => dispose(false);

#if !NET5_0_OR_GREATER
	[UnmanagedFunctionPointer(CallingConvention.Cdecl)] private delegate int MemoryWriter(byte* data, nuint data_size, WebPPicture* picture);
	private static readonly MemoryWriter delMemoryWrite = typeof(WebpEncoder).CreateMethodDelegate<MemoryWriter>(nameof(memoryWriterCallback));
#else
	[UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvCdecl) })]
	static
#endif
	private int memoryWriterCallback(byte* data, nuint data_size, WebPPicture* picture) => WebPMemoryWrite(data, data_size, picture);

	private static delegate* unmanaged[Cdecl]<byte*, nuint, WebPPicture*, int> getMemoryWriterCallback()
	{
#if NET5_0_OR_GREATER
		if (NativeLibrary.TryLoad("webp", out var lib) && NativeLibrary.TryGetExport(lib, nameof(WebPMemoryWrite), out var addr))
			return (delegate* unmanaged[Cdecl]<byte*, nuint, WebPPicture*, int>)addr;

		return &memoryWriterCallback;
#else
		return (delegate* unmanaged[Cdecl]<byte*, nuint, WebPPicture*, int>)Marshal.GetFunctionPointerForDelegate(delMemoryWrite);
#endif
	}

	private static readonly delegate* unmanaged[Cdecl]<byte*, nuint, WebPPicture*, int> pfnMemoryWrite = getMemoryWriterCallback();
}
