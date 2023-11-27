// Copyright © Clinton Ingram and Contributors.  Licensed under the MIT License.

using System;
using System.IO;
using System.Drawing;
using System.Buffers.Binary;

using PhotoSauce.MagicScaler;
using PhotoSauce.Interop.Libjxl;
using static PhotoSauce.Interop.Libjxl.Libjxl;

namespace PhotoSauce.NativeCodecs.Libjxl;

internal sealed unsafe class JxlEncoder : IImageEncoder
{
	private readonly IJxlEncoderOptions options;
	private readonly Stream stream;

	private void* encoder, encopt;
	private bool written;

	public static JxlEncoder Create(Stream outStream, IEncoderOptions? jxlOptions) => new(outStream, jxlOptions);

	private JxlEncoder(Stream outStream, IEncoderOptions? jxlOptions)
	{
		stream = outStream;
		options = jxlOptions as IJxlEncoderOptions ?? (jxlOptions is ILossyEncoderOptions opt ? JxlLossyEncoderOptions.Default with { Distance = JxlLossyEncoderOptions.DistanceFromQuality(opt.Quality) } : JxlLossyEncoderOptions.Default);

		encoder = JxlFactory.CreateEncoder();
		encopt = JxlEncoderFrameSettingsCreate(encoder, null);
		checkResult(JxlEncoderFrameSettingsSetOption(encopt, JxlEncoderFrameSettingId.JXL_ENC_FRAME_SETTING_EFFORT, (long)options.EncodeSpeed));
		checkResult(JxlEncoderFrameSettingsSetOption(encopt, JxlEncoderFrameSettingId.JXL_ENC_FRAME_SETTING_DECODING_SPEED, (long)options.DecodeSpeed));

		if (options is JxlLossyEncoderOptions lopt)
			checkResult(JxlEncoderSetFrameDistance(encopt, lopt.Distance));
		else
			checkResult(JxlEncoderSetFrameLossless(encopt, JXL_TRUE));
	}

	public void WriteFrame(IPixelSource source, IMetadataSource metadata, Rectangle sourceArea)
	{
		if (written)
			throw new InvalidOperationException("An image frame has already been written, and this encoder does not yet support multiple frames.");

		if (sourceArea == default)
			sourceArea = new(0, 0, source.Width, source.Height);

		var srcfmt = PixelFormat.FromGuid(source.Format);
		if (srcfmt != PixelFormat.Rgba32 && srcfmt != PixelFormat.Rgb24 && srcfmt != PixelFormat.Grey8)
			throw new NotSupportedException("Image format not supported.");

		if (options is JxlLossyEncoderOptions { Distance: 0 })
			checkResult(JxlEncoderSetFrameDistance(encopt, JxlLossyEncoderOptions.DistanceFromQuality(SettingsUtil.GetDefaultQuality(Math.Max(source.Width, source.Height)))));

		var basinf = default(JxlBasicInfo);
		JxlEncoderInitBasicInfo(&basinf);
		basinf.xsize = (uint)sourceArea.Width;
		basinf.ysize = (uint)sourceArea.Height;
		basinf.bits_per_sample = 8;
		basinf.num_extra_channels = srcfmt.AlphaRepresentation == PixelAlphaRepresentation.None ? 0u : 1u;
		basinf.num_color_channels = (uint)srcfmt.ChannelCount - basinf.num_extra_channels;
		basinf.alpha_bits = basinf.num_extra_channels * basinf.bits_per_sample;
		basinf.alpha_premultiplied = srcfmt.AlphaRepresentation == PixelAlphaRepresentation.Associated ? JXL_TRUE : JXL_FALSE;
		basinf.uses_original_profile = JXL_TRUE;

		if (metadata.TryGetMetadata<OrientationMetadata>(out var orient))
			basinf.orientation = (JxlOrientation)orient.Orientation.Clamp();

		checkResult(JxlEncoderSetBasicInfo(encoder, &basinf));

		if (metadata.TryGetMetadata<ColorProfileMetadata>(out var prof))
		{
			var embed = prof.Embed;
			fixed (byte* pp = &embed.GetDataRef())
				checkResult(JxlEncoderSetICCProfile(encoder, pp, (uint)embed.Length));
		}
		else
		{
			var color = default(JxlColorEncoding);
			JxlColorEncodingSetToSRGB(&color, srcfmt.ColorRepresentation == PixelColorRepresentation.Grey ? JXL_TRUE : JXL_FALSE);
			checkResult(JxlEncoderSetColorEncoding(encoder, &color));
		}

		writeExif(metadata);

		var pixfmt = new JxlPixelFormat {
			num_channels = (uint)srcfmt.ChannelCount,
			data_type = JxlDataType.JXL_TYPE_UINT8
		};

		int stride = sourceArea.Width * srcfmt.BytesPerPixel;
		using var pixbuff = BufferPool.RentLocal<byte>(checked(stride * sourceArea.Height));
		source.CopyPixels(sourceArea, stride, pixbuff.Span);

		using var stmbuff = BufferPool.RentLocal<byte>(1 << 14);
		var stmspan = stmbuff.Span;
		fixed (byte* pb = pixbuff, pf = stmspan)
		{
			checkResult(JxlEncoderAddImageFrame(encopt, &pixfmt, pb, (uint)stride * basinf.ysize));
			JxlEncoderCloseInput(encoder);

			var res = default(JxlEncoderStatus);
			do
			{
				byte* ppf = pf;
				nuint cb = (uint)stmspan.Length;

				res = checkResult(JxlEncoderProcessOutput(encoder, &ppf, &cb));
				stream.Write(stmspan[..^((int)cb)]);
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

	private void writeExif(IMetadataSource metadata)
	{
		checkResult(JxlEncoderUseBoxes(encoder));

		var orient = Orientation.Normal;
		if (metadata.TryGetMetadata<OrientationMetadata>(out var ormd))
			orient = ormd.Orientation.Clamp();

		if (!metadata.TryGetMetadata<ResolutionMetadata>(out var remd) || !remd.IsValid)
			remd = ResolutionMetadata.Default;

		if (remd.Units != ResolutionUnit.Inch)
			remd = remd.ToDpi();

		using var exif = ExifWriter.Create(3, sizeof(Rational) * 2);
		exif.Write(ExifTags.Tiff.ResolutionX, ExifType.Rational, remd.ResolutionX);
		exif.Write(ExifTags.Tiff.ResolutionY, ExifType.Rational, remd.ResolutionY);
		exif.Write(ExifTags.Tiff.ResolutionUnit, ExifType.Short, (short)2);
		exif.Finish();

		var exifspan = exif.Span;
		var box = BufferPool.RentLocal<byte>(sizeof(uint) + exifspan.Length);

		var boxspan = box.Span;
		exifspan.CopyTo(boxspan[sizeof(uint)..]);
		BinaryPrimitives.WriteUInt32LittleEndian(boxspan, 0);

		fixed (byte* bp = boxspan)
			checkResult(JxlEncoderAddBox(encoder, (sbyte*)"Exif"u8.GetAddressOf(), bp, (uint)boxspan.Length, JXL_FALSE));
	}

	private JxlEncoderStatus checkResult(JxlEncoderStatus status)
	{
		if (status == JxlEncoderStatus.JXL_ENC_ERROR)
			throw new InvalidOperationException($"{nameof(Libjxl)} encoder failed. {JxlEncoderGetError(encoder)}");

		return status;
	}

	private void dispose(bool disposing)
	{
		if (encoder is null)
			return;

		JxlEncoderDestroy(encoder);
		encoder = encopt = null;

		if (disposing)
			GC.SuppressFinalize(this);
	}

	public void Dispose() => dispose(true);

	~JxlEncoder()
	{
		ThrowHelper.ThrowIfFinalizerExceptionsEnabled(nameof(JxlEncoder));

		dispose(false);
	}
}
