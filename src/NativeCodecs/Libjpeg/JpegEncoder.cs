// Copyright © Clinton Ingram and Contributors
// SPDX-License-Identifier: MIT

using System;
using System.IO;
using System.Drawing;

using PhotoSauce.MagicScaler;
using PhotoSauce.Interop.Libjpeg;
using static PhotoSauce.Interop.Libjpeg.Libjpeg;

namespace PhotoSauce.NativeCodecs.Libjpeg;

internal sealed unsafe class JpegEncoder : IImageEncoder
{
	private readonly ILossyEncoderOptions options;

	private jpeg_compress_struct* handle;
	private bool written;

	public static JpegEncoder Create(Stream outStream, IEncoderOptions? jpegOptions) => new(outStream, jpegOptions);

	private JpegEncoder(Stream outStream, IEncoderOptions? jpegOptions)
	{
		options = jpegOptions as ILossyEncoderOptions ?? JpegEncoderOptions.Default;

		var stream = StreamWrapper.Wrap(outStream);
		if (stream is null)
			ThrowHelper.ThrowOutOfMemory();

		handle = JpegFactory.CreateEncoder();
		if (handle is null)
		{
			StreamWrapper.Free(stream);
			ThrowHelper.ThrowOutOfMemory();
		}

		var pcd = (ps_client_data*)handle->client_data;
		pcd->stream_handle = (nint)stream;
		pcd->write_callback = JpegCallbacks.Write;
		handle->data_precision = 8;
	}

	public void WriteFrame(IPixelSource source, IMetadataSource metadata, Rectangle sourceArea)
	{
		var area = sourceArea == default ? PixelArea.FromSize(source.Width, source.Height) : ((PixelArea)sourceArea);
		if (source.Width > JPEG_MAX_DIMENSION || area.Height > JPEG_MAX_DIMENSION)
			throw new NotSupportedException($"Image too large.  JPEG supports a max of {JPEG_MAX_DIMENSION} pixels in either dimension.");

		var srcfmt = PixelFormat.FromGuid(source.Format);
		var dstfmt = srcfmt == PixelFormat.Grey8 || srcfmt == PixelFormat.Bgr24 || srcfmt == PixelFormat.Y8
			? srcfmt
			: throw new NotSupportedException("Image format not supported.");

		handle->image_width = (uint)area.Width;
		handle->image_height = (uint)area.Height;
		handle->in_color_space = srcfmt == PixelFormat.Y8 ? J_COLOR_SPACE.JCS_YCbCr : srcfmt == PixelFormat.Grey8 ? J_COLOR_SPACE.JCS_GRAYSCALE : J_COLOR_SPACE.JCS_EXT_BGR;
		handle->input_components = srcfmt == PixelFormat.Y8 ? 3 : srcfmt.ChannelCount;

		checkResult(JpegSetDefaults(handle));

		int quality = options.Quality;
		if (quality == default)
			quality = SettingsUtil.GetDefaultQuality(Math.Max(source.Width, source.Height));

		checkResult(JpegSetQuality(handle, quality));

		var subs = SettingsUtil.GetDefaultSubsampling(quality);
		if (source is PlanarPixelSource psrc)
			subs = psrc.GetSubsampling();
		else if (options is IPlanarEncoderOptions { Subsample: not ChromaSubsampleMode.Default } popt)
			subs = popt.Subsample;

		if (handle->input_components > 1)
		{
			handle->comp_info[0].h_samp_factor = subs.SubsampleRatioX();
			handle->comp_info[0].v_samp_factor = subs.SubsampleRatioY();

			if (srcfmt == PixelFormat.Y8)
				handle->raw_data_in = TRUE;
		}

		if (options is JpegEncoderOptions { SuppressApp0: true } or JpegOptimizedEncoderOptions { SuppressApp0: true })
		{
			handle->write_JFIF_header = FALSE;
		}
		else
		{
			if (!metadata.TryGetMetadata<ResolutionMetadata>(out var remd) || !remd.IsValid)
				remd = ResolutionMetadata.Default;

			var dpi = remd.ToDpi();
			handle->X_density = (ushort)Math.Round((double)dpi.ResolutionX);
			handle->Y_density = (ushort)Math.Round((double)dpi.ResolutionY);
			handle->density_unit = 1;
		}

		if (options is JpegOptimizedEncoderOptions opt)
		{
			handle->optimize_coding = TRUE;

			if (opt.Progressive == JpegProgressiveMode.Semi && handle->jpeg_color_space == J_COLOR_SPACE.JCS_YCbCr)
				JpegFastProgression(handle);
			else if (opt.Progressive != JpegProgressiveMode.None)
				checkResult(JpegSimpleProgression(handle));
		}

		checkResult(JpegStartCompress(handle));

		writeExif(metadata);
		writeIccp(metadata);

		if (source is PlanarPixelSource plsrc)
			writePlanar(plsrc, area);
		else
			writePixels(source, area);

		written = true;
	}

	public void Commit()
	{
		if (!written)
			throw new InvalidOperationException("An image frame has not been written.");

		checkResult(JpegFinishCompress(handle));
	}

	private void writeExif(IMetadataSource metadata)
	{
		var orient = Orientation.Normal;
		if (metadata.TryGetMetadata<OrientationMetadata>(out var ormd))
			orient = ormd.Orientation.Clamp();

		if (orient == Orientation.Normal)
			return;

		using var exif = ExifWriter.Create(1, 0);
		exif.Write(ExifTags.Tiff.Orientation, ExifType.Short, (short)orient);
		exif.Finish();

		var exifspan = exif.Span;
		var marker = BufferPool.RentLocal<byte>(ExifIdentifier.Length + exifspan.Length);

		var markerspan = marker.Span;
		ExifIdentifier.CopyTo(markerspan);
		exifspan.CopyTo(markerspan[ExifIdentifier.Length..]);

		fixed (byte* bp = markerspan)
			checkResult(JpegWriteMarker(handle, JPEG_APP1, bp, (uint)markerspan.Length));
	}

	private void writeIccp(IMetadataSource metadata)
	{
		if (!metadata.TryGetMetadata<ColorProfileMetadata>(out var prof))
			return;

		byte[] embed = prof.Embed;
		fixed (byte* bp = &embed.GetDataRef())
			checkResult(JpegWriteIccProfile(handle, bp, (uint)embed.Length));
	}

	private void writePlanar(PlanarPixelSource src, PixelArea area)
	{
		const int mcu = DCTSIZE;

		var subs = src.GetSubsampling();
		int mcuYW = mcu * subs.SubsampleRatioX();
		int mcuYH = mcu * subs.SubsampleRatioY();

		var areaC = area.ScaleTo(subs.SubsampleRatioX(), subs.SubsampleRatioY(), area.Width, area.Height);
		int padY = MathUtil.PowerOfTwoCeiling(area.Width, mcuYW) - area.Width;
		int padC = MathUtil.PowerOfTwoCeiling(areaC.Width, mcu) - areaC.Width;
		int strideY = MathUtil.PowerOfTwoCeiling(area.Width, HWIntrinsics.VectorCount<byte>());
		int strideC = MathUtil.PowerOfTwoCeiling(areaC.Width, HWIntrinsics.VectorCount<byte>());

		using var buffY = BufferPool.RentLocalAligned<byte>(strideY * mcuYH);
		using var buffCb = BufferPool.RentLocalAligned<byte>(strideC * mcu);
		using var buffCr = BufferPool.RentLocalAligned<byte>(strideC * mcu);

		var spanY = buffY.Span;
		var spanCb = buffCb.Span;
		var spanCr = buffCr.Span;

		fixed (byte* py = spanY, pb = spanCb, pr = spanCr)
		{
			byte** pyr = stackalloc byte*[mcu * 2], pbr = stackalloc byte*[mcu], prr = stackalloc byte*[mcu];
			byte*** planes = stackalloc[] { pyr, pbr, prr };

			for (int i = 0; i < mcuYH; i++)
				pyr[i] = py + strideY * i;

			for (int i = 0; i < mcu; i++)
			{
				pbr[i] = pb + strideC * i;
				prr[i] = pr + strideC * i;
			}

			for (int row = 0; row < area.Height; row += mcuYH)
			{
				int rowc = row / subs.SubsampleRatioY();
				var sliceY = area.SliceMax(row, mcuYH);
				var sliceC = areaC.SliceMax(rowc, mcu);

				src.SourceY.CopyPixels(sliceY, strideY, spanY.Length, py);
				src.SourceCb.CopyPixels(sliceC, strideC, spanCb.Length, pb);
				src.SourceCr.CopyPixels(sliceC, strideC, spanCr.Length, pr);

				for (int i = sliceY.Height; i < mcuYH; i++)
					pyr[i] = pyr[i - 1];

				for (int i = sliceC.Height; i < mcu; i++)
				{
					pbr[i] = pbr[i - 1];
					prr[i] = prr[i - 1];
				}

				if (padY > 0)
				{
					uint syw = (uint)sliceY.Width;
					for (int i = 0; i < sliceY.Height; i++)
						new Span<byte>(pyr[i] + syw, padY).Fill(pyr[i][syw - 1]);
				}

				if (padC > 0)
				{
					uint scw = (uint)sliceC.Width;
					for (int i = 0; i < sliceC.Height; i++)
					{
						new Span<byte>(pbr[i] + scw, padC).Fill(pbr[i][scw - 1]);
						new Span<byte>(prr[i] + scw, padC).Fill(prr[i][scw - 1]);
					}
				}

				uint written;
				checkResult(JpegWriteRawData(handle, planes, (uint)mcuYH, &written));
			}
		}
	}

	private void writePixels(IPixelSource src, PixelArea area)
	{
		var srcfmt = PixelFormat.FromGuid(src.Format);
		int stride = MathUtil.PowerOfTwoCeiling(area.Width * srcfmt.BytesPerPixel, sizeof(uint));

		using var buff = BufferPool.RentLocalAligned<byte>(stride);
		var span = buff.Span;

		fixed (byte* pline = buff)
		{
			for (int y = 0; y < area.Height; y++)
			{
				src.CopyPixels(area.Slice(y, 1), stride, span);

				uint written;
				checkResult(JpegWriteScanlines(handle, &pline, 1, &written));
			}
		}
	}

	private void checkResult(int res)
	{
		((ps_client_data*)handle->client_data)->Stream->ThrowIfExceptional();

		if (res == FALSE)
			throw new InvalidOperationException($"{nameof(Libjpeg)} encoder failed. {new string(JpegGetLastError((jpeg_common_struct*)handle))}");
	}

	private void dispose(bool disposing)
	{
		if (handle is null)
			return;

		var pcd = (ps_client_data*)handle->client_data;
		StreamWrapper.Free(pcd->Stream);

		JpegDestroy((jpeg_common_struct*)handle);
		handle = null;

		if (disposing)
			GC.SuppressFinalize(this);
	}

	public void Dispose() => dispose(true);

	~JpegEncoder()
	{
		ThrowHelper.ThrowIfFinalizerExceptionsEnabled(nameof(JpegEncoder));

		dispose(false);
	}
}
