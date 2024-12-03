// Copyright © Clinton Ingram and Contributors
// SPDX-License-Identifier: MIT

using System;
using System.IO;
using System.Drawing;
using System.Buffers.Binary;

using PhotoSauce.MagicScaler;
using PhotoSauce.Interop.Libjxl;
using static PhotoSauce.Interop.Libjxl.Libjxl;

namespace PhotoSauce.NativeCodecs.Libjxl;

internal sealed unsafe class JxlEncoder : IAnimatedImageEncoder
{
	private readonly IJxlEncoderOptions options;
	private readonly Stream stream;

	private void* encoder, encopt;
	private uint frameCount = 1, writeCount = 0;
	private uint? loopCount;

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

	public void WriteAnimationMetadata(IMetadataSource metadata)
	{
		if (!metadata.TryGetMetadata<AnimationContainer>(out var anicnt))
			anicnt = default;

		loopCount = (uint)anicnt.LoopCount;
		frameCount = (uint)anicnt.FrameCount;
	}

	public void WriteFrame(IPixelSource source, IMetadataSource metadata, Rectangle sourceArea)
	{
		var area = sourceArea == default ? PixelArea.FromSize(source.Width, source.Height) : ((PixelArea)sourceArea);

		var srcfmt = PixelFormat.FromGuid(source.Format);
		if (srcfmt != PixelFormat.Rgba32 && srcfmt != PixelFormat.Rgb24 && srcfmt != PixelFormat.Grey8)
			throw new NotSupportedException("Image format not supported.");

		if (writeCount is 0)
		{
			var basinf = default(JxlBasicInfo);
			JxlEncoderInitBasicInfo(&basinf);
			basinf.xsize = (uint)area.Width;
			basinf.ysize = (uint)area.Height;
			basinf.bits_per_sample = 8;
			basinf.num_extra_channels = srcfmt.AlphaRepresentation == PixelAlphaRepresentation.None ? 0u : 1u;
			basinf.num_color_channels = (uint)srcfmt.ChannelCount - basinf.num_extra_channels;
			basinf.alpha_bits = basinf.num_extra_channels * basinf.bits_per_sample;
			basinf.alpha_premultiplied = srcfmt.AlphaRepresentation == PixelAlphaRepresentation.Associated ? JXL_TRUE : JXL_FALSE;
			basinf.uses_original_profile = JXL_TRUE;

			if (metadata.TryGetMetadata<OrientationMetadata>(out var orient))
				basinf.orientation = (JxlOrientation)orient.Orientation.Clamp();

			if (loopCount.HasValue)
			{
				basinf.have_animation = 1;
				basinf.animation.tps_numerator = 100;
				basinf.animation.tps_denominator = 1;
				basinf.animation.num_loops = loopCount.Value;
			}

			checkResult(JxlEncoderSetBasicInfo(encoder, &basinf));
			checkResult(JxlEncoderUseBoxes(encoder));

			writeColorProfile(metadata, srcfmt);
			writeExif(metadata);

			JxlEncoderCloseBoxes(encoder);

			if (options is JxlLossyEncoderOptions { Distance: 0f })
				checkResult(JxlEncoderSetFrameDistance(encopt, JxlLossyEncoderOptions.DistanceFromQuality(SettingsUtil.GetDefaultQuality(Math.Max(source.Width, source.Height)))));
		}

		var pixfmt = new JxlPixelFormat {
			num_channels = (uint)srcfmt.ChannelCount,
			data_type = JxlDataType.JXL_TYPE_UINT8
		};

		if (loopCount.HasValue)
		{
			if (!metadata.TryGetMetadata<AnimationFrame>(out var anifrm))
				anifrm = AnimationFrame.Default;

			JxlFrameHeader fhdr;
			JxlEncoderInitFrameHeader(&fhdr);
			JxlEncoderInitBlendInfo(&fhdr.layer_info.blend_info);

			fhdr.duration = anifrm.Duration.NormalizeTo(100).Numerator;

			ref var layer = ref fhdr.layer_info;
			layer.have_crop = 1;
			layer.crop_x0 = area.X;
			layer.crop_y0 = area.Y;
			layer.xsize = (uint)area.Width;
			layer.ysize = (uint)area.Height;
			layer.save_as_reference = anifrm.Disposal is FrameDisposalMethod.Preserve ? 1u : 0u;
			layer.blend_info.source = 1u;
			layer.blend_info.blendmode = anifrm.Blend is AlphaBlendMethod.Source ? JxlBlendMode.JXL_BLEND_REPLACE : JxlBlendMode.JXL_BLEND_BLEND;

			checkResult(JxlEncoderSetFrameHeader(encopt, &fhdr));
		}

		byte* pbuff = null;
		try
		{
			int stride = area.Width * srcfmt.BytesPerPixel;
			nuint cb = (nuint)stride * (uint)area.Height;
			pbuff = (byte*)UnsafeUtil.NativeAlloc(cb);
			if (pbuff is null)
				ThrowHelper.ThrowOutOfMemory();

			for (int y = 0; y < area.Height; y++)
				source.CopyPixels(area.Slice(y, 1), stride, new Span<byte>(pbuff + y * stride, stride));

			// TODO implement chunked encoding -- this makes a copy of the entire input
			checkResult(JxlEncoderAddImageFrame(encopt, &pixfmt, pbuff, cb));
			if (++writeCount == frameCount)
				JxlEncoderCloseFrames(encoder);
		}
		finally
		{
			if (pbuff is not null)
				UnsafeUtil.NativeFree(pbuff);
		}

		using var stmbuff = BufferPool.RentLocal<byte>(1 << 14);
		var stmspan = stmbuff.Span;
		fixed (byte* pf = stmspan)
		{
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
	}

	public void Commit()
	{
		if (writeCount is 0)
			throw new InvalidOperationException("An image frame has not been written.");
	}

	private void writeColorProfile(IMetadataSource metadata, PixelFormat pixfmt)
	{
		if (metadata.TryGetMetadata<ColorProfileMetadata>(out var prof))
		{
			//TODO use encoded color space for well-known profiles
			byte[] embed = prof.Embed;
			fixed (byte* pp = &embed.GetDataRef())
				checkResult(JxlEncoderSetICCProfile(encoder, pp, (uint)embed.Length));
		}
		else
		{
			var color = default(JxlColorEncoding);
			JxlColorEncodingSetToSRGB(&color, pixfmt.ColorRepresentation == PixelColorRepresentation.Grey ? JXL_TRUE : JXL_FALSE);
			checkResult(JxlEncoderSetColorEncoding(encoder, &color));
		}
	}

	private void writeExif(IMetadataSource metadata)
	{
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
