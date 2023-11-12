// Copyright © Clinton Ingram and Contributors.  Licensed under the MIT License.

using System;
using System.IO;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Runtime.CompilerServices;

using PhotoSauce.MagicScaler;
using PhotoSauce.MagicScaler.Transforms;
using PhotoSauce.Interop.Libwebp;
using static PhotoSauce.Interop.Libwebp.Libwebp;
using static PhotoSauce.Interop.Libwebp.Libwebpmux;

namespace PhotoSauce.NativeCodecs.Libwebp;

internal sealed unsafe class WebpEncoder : IAnimatedImageEncoder
{
	private readonly IWebpEncoderOptions options;
	private readonly Stream stream;

	private void* handle;
	private bool written;
	private bool animated;

	public static WebpEncoder Create(Stream outStream, IEncoderOptions? webpOptions) => new(outStream, webpOptions);

	private WebpEncoder(Stream outStream, IEncoderOptions? webpOptions)
	{
		stream = outStream;
		options = webpOptions as IWebpEncoderOptions ?? (webpOptions is ILossyEncoderOptions opt ? new WebpLossyEncoderOptions(opt.Quality) : WebpLossyEncoderOptions.Default);

		handle = WebpFactory.CreateMuxer();
	}

	public void WriteAnimationMetadata(IMetadataSource metadata)
	{
		if (!metadata.TryGetMetadata<AnimationContainer>(out var anicnt))
			anicnt = default;

		animated = true;
		var anip = new WebPMuxAnimParams {
			bgcolor = (uint)anicnt.BackgroundColor,
			loop_count = anicnt.LoopCount
		};
		WebpResult.Check(WebPMuxSetAnimationParams(handle, &anip));
	}

	public void WriteFrame(IPixelSource source, IMetadataSource metadata, Rectangle sourceArea)
	{
		var area = sourceArea == default ? new PixelArea(0, 0, source.Width, source.Height) : ((PixelArea)sourceArea).SnapTo(2, 2, source.Width, source.Height);
		if (area.Width > WEBP_MAX_DIMENSION || area.Height > WEBP_MAX_DIMENSION)
			throw new NotSupportedException($"Image too large.  WebP supports a max of {WEBP_MAX_DIMENSION} pixels in either dimension.");

		var srcfmt = PixelFormat.FromGuid(source.Format);
		var dstfmt = srcfmt == PixelFormat.Grey8 || srcfmt == PixelFormat.Bgra32 || srcfmt == PixelFormat.Y8Video
			? srcfmt
			: throw new NotSupportedException("Image format not supported.");

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
				quality = SettingsUtil.GetDefaultQuality(Math.Max(area.Width, area.Height));

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
			picture.width = area.Width;
			picture.height = area.Height;
			picture.use_argb = 1;

			if (srcfmt == PixelFormat.Grey8)
			{
				bool lossy = config.lossless == 0;
				dstfmt = lossy ? PixelFormat.Y8Video : PixelFormat.Bgra32;
				picture.use_argb = lossy ? 0 : 1;
			}
			else if (srcfmt == PixelFormat.Y8Video)
			{
				if (source is not PlanarPixelSource)
					throw new NotSupportedException($"Planar pixel source is required for {nameof(PixelFormat.Y8Video)} format.");

				picture.use_argb = 0;
			}

			if (WebPPictureAlloc(&picture) == 0)
				ThrowHelper.ThrowOutOfMemory();

			if (picture.use_argb == 1)
			{
				int stride = picture.argb_stride * dstfmt.BytesPerPixel;
				var span = new Span<byte>(picture.argb, checked(stride * area.Height));

				using var tran = srcfmt != dstfmt ? new ConversionTransform(source.AsPixelSource(), dstfmt) : null;
				source = tran ?? source;
				source.CopyPixels(area, stride, span);
			}
			else
			{
				var plsrc = source as PlanarPixelSource;
				var subs = plsrc?.GetSubsampling() ?? ChromaSubsampleMode.Subsample420;
				var srcY = plsrc?.SourceY ?? source;
				var srcU = plsrc?.SourceCb ?? (IPixelSource)new NullChromaPixelSource();
				var srcV = plsrc?.SourceCr ?? srcU;

				var areaUV = area.ScaleTo(subs.SubsampleRatioX(), subs.SubsampleRatioY(), area.Width, area.Height);
				var spanY = new Span<byte>(picture.y, checked(picture.y_stride * area.Height));
				var spanU = new Span<byte>(picture.u, checked(picture.uv_stride * areaUV.Height));
				var spanV = new Span<byte>(picture.v, spanU.Length);

				using var tran = srcfmt != dstfmt ? new ConversionTransform(srcY.AsPixelSource(), dstfmt) : null;
				srcY = tran ?? srcY;

				int incy = 8, incc = incy / subs.SubsampleRatioY();
				int lasty = 0, lastc = 0;
				int blocks = area.Height / incy;
				for (int b = 0; b < blocks; b++, lasty += incy, lastc += incc)
				{
					srcY.CopyPixels(area.Slice(lasty, incy), picture.y_stride, spanY.Slice(lasty * picture.y_stride));
					srcU.CopyPixels(areaUV.Slice(lastc, incc), picture.uv_stride, spanU.Slice(lastc * picture.uv_stride));
					srcV.CopyPixels(areaUV.Slice(lastc, incc), picture.uv_stride, spanV.Slice(lastc * picture.uv_stride));
				}

				if (lasty < area.Height)
				{
					srcY.CopyPixels(area.Slice(lasty), picture.y_stride, spanY.Slice(lasty * picture.y_stride));
					srcU.CopyPixels(areaUV.Slice(lastc), picture.uv_stride, spanU.Slice(lastc * picture.uv_stride));
					srcV.CopyPixels(areaUV.Slice(lastc), picture.uv_stride, spanV.Slice(lastc * picture.uv_stride));
				}
			}

			picture.writer = pfnMemoryWrite;
			picture.custom_ptr = &writer;

			if (WebPEncode(&config, &picture) == 0)
				WebpResult.Check(picture.error_code);

			var img = new WebPData { size = writer.size, bytes = writer.mem };

			if (animated)
			{
				if (!metadata.TryGetMetadata<AnimationFrame>(out var anifrm))
					anifrm = AnimationFrame.Default;

				var anif = new WebPMuxFrameInfo {
					id = WebPChunkId.WEBP_CHUNK_ANMF,
					x_offset = anifrm.OffsetLeft,
					y_offset = anifrm.OffsetTop,
					duration = (int)anifrm.Duration.NormalizeTo(1000).Numerator,
					dispose_method = anifrm.Disposal == FrameDisposalMethod.RestoreBackground ? WebPMuxAnimDispose.WEBP_MUX_DISPOSE_BACKGROUND : WebPMuxAnimDispose.WEBP_MUX_DISPOSE_NONE,
					blend_method = anifrm.Blend == AlphaBlendMethod.Source ? WebPMuxAnimBlend.WEBP_MUX_NO_BLEND : WebPMuxAnimBlend.WEBP_MUX_BLEND,
					bitstream = img
				};
				WebpResult.Check(WebPMuxPushFrame(handle, &anif, 1));
			}
			else
			{
				if (written)
					throw new InvalidOperationException("An image frame has already been written, and this encoder is not configured for multiple frames.");

				WebpResult.Check(WebPMuxSetImage(handle, &img, 1));
			}
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

		var embed = prof.Embed;
		fixed (byte* bp = &embed.GetDataRef())
		{
			var data = new WebPData { size = (uint)embed.Length, bytes = bp };
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
		if (handle is null)
			return;

		WebPMuxDelete(handle);
		handle = null;

		if (disposing)
			GC.SuppressFinalize(this);
	}

	public void Dispose() => dispose(true);

	~WebpEncoder()
	{
		ThrowHelper.ThrowIfFinalizerExceptionsEnabled(nameof(WebpEncoder));

		dispose(false);
	}

#if !NET5_0_OR_GREATER
	[UnmanagedFunctionPointer(CallingConvention.Cdecl)] private delegate int MemoryWriter(byte* data, nuint data_size, WebPPicture* picture);
	private static readonly MemoryWriter delMemoryWrite = typeof(WebpEncoder).CreateMethodDelegate<MemoryWriter>(nameof(memoryWriterCallback));
#else
	[UnmanagedCallersOnly(CallConvs = [ typeof(CallConvCdecl) ])]
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

	private class NullChromaPixelSource : IPixelSource
	{
		public Guid Format => default;
		public int Width => default;
		public int Height => default;

		public void CopyPixels(Rectangle sourceArea, int cbStride, Span<byte> buffer)
		{
			ref byte bstart = ref MemoryMarshal.GetReference(buffer);
			for (int y = 0; y < sourceArea.Height; y++)
				Unsafe.InitBlock(ref Unsafe.Add(ref bstart, y * cbStride), 0x80, (uint)sourceArea.Width);
		}
	}
}
