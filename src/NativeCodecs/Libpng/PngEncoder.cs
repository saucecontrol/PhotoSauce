// Copyright © Clinton Ingram and Contributors.  Licensed under the MIT License.

using System;
using System.IO;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Runtime.CompilerServices;

using PhotoSauce.MagicScaler;
using PhotoSauce.MagicScaler.Converters;
using PhotoSauce.MagicScaler.Transforms;
using PhotoSauce.Interop.Libpng;
using static PhotoSauce.Interop.Libpng.Libpng;

namespace PhotoSauce.NativeCodecs.Libpng;

internal sealed unsafe class PngEncoder : IImageEncoder
{
	private static readonly bool isWindows = RuntimeInformation.IsOSPlatform(OSPlatform.Windows);

	private readonly IPngEncoderOptions options;

	private ps_png_struct* handle;
	private bool written;

	public static PngEncoder Create(Stream outStream, IEncoderOptions? pngOptions) => new(outStream, pngOptions);

	private PngEncoder(Stream outStream, IEncoderOptions? pngOptions)
	{
		options = pngOptions as IPngEncoderOptions ?? PngEncoderOptions.Default;

		handle = PngFactory.CreateEncoder();
		if (handle is null)
			ThrowHelper.ThrowOutOfMemory();

		var iod = handle->io_ptr;
		iod->stream_handle = GCHandle.ToIntPtr(GCHandle.Alloc(outStream));
		iod->write_callback = pfnWriteCallback;
	}

	public void WriteFrame(IPixelSource source, IMetadataSource metadata, Rectangle sourceArea)
	{
		var area = sourceArea == default ? new PixelArea(0, 0, source.Width, source.Height) : ((PixelArea)sourceArea);
		if (source.Width > PNG_USER_WIDTH_MAX || area.Height > PNG_USER_HEIGHT_MAX)
			throw new NotSupportedException($"Image too large.  This encoder supports a max of {PNG_USER_WIDTH_MAX}x{PNG_USER_HEIGHT_MAX} pixels.");

		var srcfmt = PixelFormat.FromGuid(source.Format);
		int pngfmt =
			srcfmt == PixelFormat.Grey8 ? PNG_COLOR_TYPE_GRAY :
			srcfmt == PixelFormat.Rgb24 ? PNG_COLOR_TYPE_RGB :
			srcfmt == PixelFormat.Rgba32 ? PNG_COLOR_TYPE_RGBA :
			srcfmt == PixelFormat.Indexed8 ? PNG_COLOR_TYPE_PALETTE :
			throw new NotSupportedException("Image format not supported.");

		int filter = pngfmt == PNG_COLOR_TYPE_PALETTE ? PNG_FILTER_VALUE_NONE : PNG_ALL_FILTERS;
		if (options.Filter is > PngFilter.Unspecified and < PngFilter.Adaptive)
			filter = (int)options.Filter - 1;

		checkResult(PngWriteSig(handle));
		checkResult(PngSetFilter(handle, filter));
		checkResult(PngSetCompressionLevel(handle, 5));
		checkResult(PngWriteIhdr(handle, (uint)area.Width, (uint)area.Height, 8, pngfmt, options.Interlace ? PNG_INTERLACE_ADAM7 : PNG_INTERLACE_NONE));

		if (source is IndexedColorTransform idx)
		{
			var pal = idx.Palette;
			fixed (uint* ppal = pal)
			{
				using (var palbuf = BufferPool.RentLocal<byte>(pal.Length * 4))
				fixed (byte* pp = palbuf)
				{
					Unsafe.CopyBlock(pp, ppal, (uint)palbuf.Length);

					// convert palette BGRA->RGBA then RGBA->RGB
					ChannelChanger<byte>.GetConverter(4, 4).ConvertLine(pp, pp, palbuf.Length);
					ChannelChanger<byte>.GetConverter(4, 3).ConvertLine(pp, pp, palbuf.Length);
					checkResult(PngWritePlte(handle, (png_color_struct*)pp, pal.Length));
				}

				if (idx.HasAlpha)
				{
					using var trnbuf = BufferPool.RentLocal<byte>(pal.Length);
					fixed (byte* pt = trnbuf)
					{
						ChannelChanger<byte>.AlphaExtractor.ConvertLine((byte*)ppal, pt, pal.Length * 4);
						checkResult(PngWriteTrns(handle, pt, pal.Length));
					}
				}
			}
		}

		writeIccp(metadata);
		writeExif(metadata);

		writePixels(source, area);

		written = true;
	}

	public void Commit()
	{
		if (!written)
			throw new InvalidOperationException("An image frame has not been written.");

		checkResult(PngWriteIend(handle));
	}

	private void writeIccp(IMetadataSource metadata)
	{
		if (!metadata.TryGetMetadata<ColorProfileMetadata>(out var prof))
			return;

		if (prof.Profile == ColorProfile.sRGB)
		{
			checkResult(PngWriteSrgb(handle));
			return;
		}

		var profile = prof.Profile.ProfileBytes;
		fixed (byte* bp = &profile.GetDataRef())
			checkResult(PngWriteIccp(handle, bp));
	}

	private void writeExif(IMetadataSource metadata)
	{
		if (!metadata.TryGetMetadata<ResolutionMetadata>(out var remd) || !remd.IsValid)
			remd = ResolutionMetadata.Default;

		remd = remd.ToDpm();
		checkResult(PngWritePhys(handle, (uint)Math.Round((double)remd.ResolutionX), (uint)Math.Round((double)remd.ResolutionY)));

		var orient = Orientation.Normal;
		if (metadata.TryGetMetadata<OrientationMetadata>(out var ormd))
			orient = ormd.Orientation.Clamp();

		if (orient == Orientation.Normal)
			return;

		using var exif = ExifWriter.Create(1, 0);
		exif.Write(ExifTags.Tiff.Orientation, ExifType.Short, (short)orient);
		exif.Finish();

		var exifspan = exif.Span;
		fixed (byte* bp = exifspan)
			checkResult(PngWriteExif(handle, bp, exifspan.Length));
	}

	private void writePixels(IPixelSource src, PixelArea area)
	{
		var srcfmt = PixelFormat.FromGuid(src.Format);
		int stride = MathUtil.PowerOfTwoCeiling(area.Width * srcfmt.BytesPerPixel, sizeof(uint));

		using var buff = BufferPool.RentLocalAligned<byte>(stride * (options.Interlace ? area.Height : 1));
		var span = buff.Span;

		fixed (byte* pbuf = buff)
		{
			if (options.Interlace)
			{
				using var lines = BufferPool.RentLocal<nint>(area.Height);
				var lspan = lines.Span;
				for (int i = 0; i < lspan.Length; i++)
					lspan[i] = (nint)(pbuf + i * stride);

				fixed (nint* plines = lines)
				{
					src.CopyPixels(area, stride, span);
					checkResult(PngWriteImage(handle, (byte**)plines));
				}
			}
			else
			{
				for (int row = 0; row < area.Height; row++)
				{
					src.CopyPixels(area.Slice(row, 1), stride, span);
					checkResult(PngWriteRow(handle, pbuf));
				}
			}
		}
	}

	private void checkResult(int res)
	{
		if (res == FALSE)
			throw new InvalidOperationException($"{nameof(Libpng)} encoder failed. {new string(PngGetLastError(handle))}");
	}

	private void dispose(bool disposing)
	{
		if (handle is null)
			return;

		GCHandle.FromIntPtr(handle->io_ptr->stream_handle).Free();
		PngDestroyWrite(handle);
		handle = null;

		if (disposing)
			GC.SuppressFinalize(this);
	}

	public void Dispose() => dispose(true);

	~PngEncoder()
	{
		ThrowHelper.ThrowIfFinalizerExceptionsEnabled(nameof(PngEncoder));

		dispose(false);
	}

#if !NET5_0_OR_GREATER
	[UnmanagedFunctionPointer(CallingConvention.Cdecl)] private delegate nuint WriteCallback(nint pinst, byte* buff, nuint cb);
	private static readonly WriteCallback delWriteCallback = typeof(PngEncoder).CreateMethodDelegate<WriteCallback>(nameof(writeCallback));
#else
	[UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvCdecl) })]
	static
#endif
	private nuint writeCallback(nint pinst, byte* buff, nuint cb)
	{
		try
		{
			var stm = Unsafe.As<Stream>(GCHandle.FromIntPtr(pinst).Target!);
			stm.Write(new ReadOnlySpan<byte>(buff, checked((int)cb)));

			return cb;
		}
		catch when (!isWindows)
		{
			return 0;
		}
	}

	private static readonly delegate* unmanaged[Cdecl]<nint, byte*, nuint, nuint> pfnWriteCallback =
#if NET5_0_OR_GREATER
		&writeCallback;
#else
		(delegate* unmanaged[Cdecl]<nint, byte*, nuint, nuint>)Marshal.GetFunctionPointerForDelegate(delWriteCallback);
#endif
}
