// Copyright © Clinton Ingram and Contributors.  Licensed under the MIT License.

using System;
using System.IO;
using System.Drawing;
using System.Numerics;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.InteropServices;
using System.Runtime.CompilerServices;

using PhotoSauce.MagicScaler;
using PhotoSauce.Interop.Libwebp;
using static PhotoSauce.Interop.Libwebp.Libwebp;
using static PhotoSauce.Interop.Libwebp.Libwebpdemux;

namespace PhotoSauce.NativeCodecs.Libwebp;

internal sealed unsafe class WebPContainer : IImageContainer, IMetadataSource, IIccProfileSource, IExifSource
{
	private const uint iccpTag = 'I' | 'C' << 8 | 'C' << 16 | 'P' << 24;
	private const uint exifTag = 'E' | 'X' << 8 | 'I' << 16 | 'F' << 24;
	private const uint xmpTag  = 'X' | 'M' << 8 | 'P' << 16 | ' ' << 24;

	private readonly WebPFeatureFlags features;
	private readonly uint frames;
	private byte* filebuff;
	private IntPtr handle;

	private WebPContainer(Stream stm, WebPFeatureFlags flags)
	{
		long len = stm.Length - stm.Position;
		filebuff = (byte*)WebPMalloc((nuint)len);
		if (filebuff is null)
			throw new OutOfMemoryException();

		int rem = (int)len;
		var buff = new Span<byte>(filebuff, rem);
		while (rem > 0)
			rem -= stm.Read(buff[^rem..]);

		var data = new WebPData { bytes = filebuff, size = (nuint)len };
		handle = WebPDemux(&data);
		if (handle == default)
			throw new InvalidDataException();

		features = flags;
		frames = WebPDemuxGetI(handle, WebPFormatFeature.WEBP_FF_FRAME_COUNT);
	}

	public string MimeType => ImageMimeTypes.Webp;

	public int FrameCount => (int)frames;

	int IIccProfileSource.ProfileLength => (int)getChunk(iccpTag).size;

	int IExifSource.ExifLength => (int)getChunk(exifTag).size;

	public IImageFrame GetFrame(int index)
	{
		ensureHandle();

		if ((uint)index >= frames)
			throw new IndexOutOfRangeException(nameof(index));

		WebPIterator iter;
		if (!WebPResult.Succeeded(WebPDemuxGetFrame(handle, index + 1, &iter)))
			throw new IndexOutOfRangeException(nameof(index));

		WebPBitstreamFeatures ffeat;
		WebPResult.Check(WebPGetFeatures(iter.fragment.bytes, iter.fragment.size, &ffeat));
		bool isAnimation = features.HasFlag(WebPFeatureFlags.ANIMATION_FLAG);
		bool isLossless = ffeat.format == 2;
		bool hasAlpha = ffeat.has_alpha != 0;
		bool decodePlanar = !hasAlpha && !isAnimation && !isLossless;

		return decodePlanar ? new WebPYuvFrame(this, ffeat, index) : new WebPRgbFrame(this, ffeat, index, isAnimation || hasAlpha);
	}

	private WebPData getChunk(uint fourcc)
	{
		var data = default(WebPData);

		WebPChunkIterator iter;
		if (WebPResult.Succeeded(WebPDemuxGetChunk(handle, (sbyte*)&fourcc, 1, &iter)))
		{
			data = iter.chunk;
			WebPDemuxReleaseChunkIterator(&iter);
		}

		return data;
	}

	void IIccProfileSource.CopyProfile(Span<byte> dest)
	{
		var data = getChunk(iccpTag);
		if (data.size != 0)
			new ReadOnlySpan<byte>(data.bytes, (int)data.size).CopyTo(dest);
	}

	void IExifSource.CopyExif(Span<byte> dest)
	{
		var data = getChunk(exifTag);
		if (data.size != 0)
			new ReadOnlySpan<byte>(data.bytes, (int)data.size).CopyTo(dest);
	}

	public bool TryGetMetadata<T>([NotNullWhen(true)] out T? metadata) where T : IMetadata
	{
		ensureHandle();

		if (typeof(T) == typeof(AnimationContainer) && features.HasFlag(WebPFeatureFlags.ANIMATION_FLAG))
		{
			int w = (int)WebPDemuxGetI(handle, WebPFormatFeature.WEBP_FF_CANVAS_WIDTH);
			int h = (int)WebPDemuxGetI(handle, WebPFormatFeature.WEBP_FF_CANVAS_HEIGHT);
			int lc = (int)WebPDemuxGetI(handle, WebPFormatFeature.WEBP_FF_LOOP_COUNT);
			int bg = (int)WebPDemuxGetI(handle, WebPFormatFeature.WEBP_FF_BACKGROUND_COLOR);
			var anicnt = new AnimationContainer(w, h, lc, bg, true);

			metadata = (T)(object)anicnt;
			return true;
		}

		if (typeof(T) == typeof(OrientationMetadata))
		{
			var orient = Orientation.Normal;
			if (features.HasFlag(WebPFeatureFlags.EXIF_FLAG))
			{
				var exifsrc = (IExifSource)this;
				using var buff = BufferPool.RentLocal<byte>(exifsrc.ExifLength);
				exifsrc.CopyExif(buff.Span);

				var rdr = ExifReader.Create(buff.Span);
				foreach (var tag in rdr)
				{
					if (tag.ID == ExifTags.Tiff.Orientation)
						orient = ((Orientation)rdr.CoerceValue<int>(tag)).Clamp();
				}
			}

			metadata = (T)(object)(new OrientationMetadata(orient));
			return true;
		}

		if (typeof(T) == typeof(IIccProfileSource) && features.HasFlag(WebPFeatureFlags.ICCP_FLAG))
		{
			metadata = (T)(object)this;
			return true;
		}

		if (typeof(T) == typeof(IExifSource) && features.HasFlag(WebPFeatureFlags.EXIF_FLAG))
		{
			metadata = (T)(object)this;
			return true;
		}

		metadata = default;
		return false;
	}

	public static WebPContainer? TryLoad(Stream imgStream, IDecoderOptions? _)
	{
		// 30 bytes are needed to get basic info for webp
		const int bufflen = 32;

		if ((imgStream.Length - imgStream.Position) < bufflen)
			return null;

		int rem = bufflen;
#if NET5_0_OR_GREATER
		var buff = (Span<byte>)stackalloc byte[bufflen];
		while (rem > 0)
			rem -= imgStream.Read(buff[^rem..]);
#else
		using var buff = BufferPool.RentLocalArray<byte>(bufflen);
		while (rem > 0)
			rem -= imgStream.Read(buff.Array, buff.Length - rem, rem);
#endif

		imgStream.Seek(-bufflen, SeekOrigin.Current);

		var state = default(WebPDemuxState);
		var flags = default(WebPFeatureFlags);
		fixed (byte* ptr = buff)
		{
			var data = new WebPData { bytes = ptr, size = bufflen };
			var demuxer = WebPDemuxPartial(&data, &state);

			if (state >= WebPDemuxState.WEBP_DEMUX_PARSED_HEADER)
				flags = (WebPFeatureFlags)WebPDemuxGetI(demuxer, WebPFormatFeature.WEBP_FF_FORMAT_FLAGS);

			WebPDemuxDelete(demuxer);
		}

		if (state >= WebPDemuxState.WEBP_DEMUX_PARSED_HEADER)
			return new WebPContainer(imgStream, flags);

		return null;
	}

	private void ensureHandle()
	{
		if (handle == default)
			throw new ObjectDisposedException(nameof(WebPContainer));
	}

	private void dispose(bool disposing)
	{
		if (filebuff is null)
			return;

		WebPFree(filebuff);
		filebuff = null;

		WebPDemuxDelete(handle);
		handle = default;

		if (disposing)
			GC.SuppressFinalize(this);
	}

	public void Dispose() => dispose(true);

	~WebPContainer() => dispose(false);

	private abstract class WebPFrame : IImageFrame, IMetadataSource
	{
		private readonly WebPContainer container;
		private readonly WebPBitstreamFeatures features;
		private readonly AnimationFrame frmmeta;
		private readonly int frmnum;
		private WebPDecBuffer buffer;

		public abstract IPixelSource PixelSource { get; }

		public WebPFrame(WebPContainer cont, WebPBitstreamFeatures feat, int index)
		{
			container = cont;
			features = feat;
			frmnum = index + 1;

			if (container.features.HasFlag(WebPFeatureFlags.ANIMATION_FLAG))
			{
				WebPIterator iter;
				WebPResult.Check(WebPDemuxGetFrame(cont.handle, frmnum, &iter));

				frmmeta = new AnimationFrame(
					iter.x_offset,
					iter.y_offset,
					new Rational((uint)iter.duration, 1000u),
					iter.dispose_method == WebPMuxAnimDispose.WEBP_MUX_DISPOSE_BACKGROUND ? FrameDisposalMethod.RestoreBackground : FrameDisposalMethod.Preserve,
					iter.has_alpha != 0
					// TODO iter.blend_method
				);

				WebPDemuxReleaseIterator(&iter);
			}
		}

		private void ensureDecoded(WebPPlane plane)
		{
			if (buffer.IsAllocated())
				return;

			container.ensureHandle();

			WebPIterator iter;
			WebPResult.Check(WebPDemuxGetFrame(container.handle, frmnum, &iter));

			WebPDecoderConfig cfg;
			WebPResult.Check(WebPInitDecoderConfig(&cfg));
			cfg.output.colorspace = plane == WebPPlane.Bgra ? WEBP_CSP_MODE.MODE_BGRA : plane == WebPPlane.Bgr ? WEBP_CSP_MODE.MODE_BGR : WEBP_CSP_MODE.MODE_YUV;
			cfg.input = features;

			WebPResult.Check(WebPDecode(iter.fragment.bytes, iter.fragment.size, &cfg));
			WebPDemuxReleaseIterator(&iter);

			buffer = cfg.output;
		}

		public bool TryGetMetadata<T>([NotNullWhen(true)] out T? metadata) where T : IMetadata
		{
			if (typeof(T) == typeof(AnimationFrame) && container.features.HasFlag(WebPFeatureFlags.ANIMATION_FLAG))
			{
				metadata = (T)(object)frmmeta;
				return true;
			}

			return container.TryGetMetadata(out metadata);
		}

		protected virtual void Dispose(bool disposing)
		{
			if (disposing)
				GC.SuppressFinalize(this);

			if (!buffer.IsAllocated())
				return;

			fixed (WebPDecBuffer* pbuff = &buffer)
				WebPFreeDecBuffer(pbuff);
		}

		public void Dispose() => Dispose(true);

		~WebPFrame() => Dispose(false);

		protected sealed class WebPDecBufferPixelSource : IPixelSource
		{
			private readonly WebPFrame frame;
			private readonly WebPPlane plane;

			public Guid Format { get; }
			public int Width { get; }
			public int Height { get; }

			public WebPDecBufferPixelSource(WebPFrame frm, WebPPlane pln)
			{
				frame = frm;
				plane = pln;

				Format = plane switch {
					WebPPlane.Y   => PixelFormats.Planar.Y8bpp,
					WebPPlane.U   => PixelFormats.Planar.Cb8bpp,
					WebPPlane.V   => PixelFormats.Planar.Cr8bpp,
					WebPPlane.Bgr => PixelFormats.Bgr24bpp,
					_             => PixelFormats.Bgra32bpp
				};
				Width = plane is WebPPlane.U or WebPPlane.V ? (frame.features.width + 1) >> 1 : frame.features.width;
				Height = plane is WebPPlane.U or WebPPlane.V ? (frame.features.height + 1) >> 1 : frame.features.height;
			}

			public void CopyPixels(Rectangle sourceArea, int cbStride, Span<byte> buffer)
			{
				var (rx, ry, rw, rh) = (sourceArea.X, sourceArea.Y, sourceArea.Width, sourceArea.Height);
				int bpp = PixelFormat.FromGuid(Format).BytesPerPixel;
				int cb = rw * bpp;

				if (rx < 0 || ry < 0 || rw < 0 || rh < 0 || rx + rw > Width || ry + rh > Height)
					throw new ArgumentOutOfRangeException(nameof(sourceArea), "Requested area does not fall within the image bounds");

				if (cb > cbStride)
					throw new ArgumentOutOfRangeException(nameof(cbStride), "Stride is too small for the requested area");

				if ((rh - 1) * cbStride + cb > buffer.Length)
					throw new ArgumentOutOfRangeException(nameof(buffer), "Buffer is too small for the requested area");

				frame.ensureDecoded(plane);
				ref var decoded = ref frame.buffer;

				ReadOnlySpan<byte> span;
				int stride;
				if (plane is WebPPlane.Bgra)
				{
					var data = decoded.u.RGBA;
					span = new ReadOnlySpan<byte>(data.rgba, checked((int)data.size));
					stride = data.stride;
				}
				else
				{
					var data = decoded.u.YUVA;
					if (plane is WebPPlane.U)
					{
						span = new ReadOnlySpan<byte>(data.u, checked((int)data.u_size));
						stride = data.u_stride;
					}
					else if (plane is WebPPlane.V)
					{
						span = new ReadOnlySpan<byte>(data.v, checked((int)data.v_size));
						stride = data.v_stride;
					}
					else
					{
						span = new ReadOnlySpan<byte>(data.y, checked((int)data.y_size));
						stride = data.y_stride;
					}
				}

				ref byte pixRef = ref Unsafe.Add(ref MemoryMarshal.GetReference(span), ry * stride + rx * bpp);
				for (int y = 0; y < rh; y++)
					Unsafe.CopyBlockUnaligned(ref buffer[y * cbStride], ref Unsafe.Add(ref pixRef, y * stride), (uint)cb);
			}
		}
	}

	private sealed class WebPRgbFrame : WebPFrame
	{
		private readonly WebPPlane plane;
		private IPixelSource pixsrc;

		public override IPixelSource PixelSource => pixsrc ??= new WebPDecBufferPixelSource(this, plane);

		public WebPRgbFrame(WebPContainer cont, WebPBitstreamFeatures feat, int index, bool alpha) : base(cont, feat, index) => plane = alpha ? WebPPlane.Bgra : WebPPlane.Bgr;
	}

	private sealed class WebPYuvFrame : WebPFrame, IYccImageFrame
	{
		// WebP uses a non-standard matrix close to Rec.601 but rounded strangely
		// https://chromium.googlesource.com/webm/libwebp/+/refs/tags/v1.2.2/src/dsp/yuv.h
		private static readonly Matrix4x4 yccMatrix = new() {
			M11 =  0.2992f,
			M21 =  0.5874f,
			M31 =  0.1141f,
			M12 = -0.1688f,
			M22 = -0.3315f,
			M32 =  0.5003f,
			M13 =  0.5003f,
			M23 = -0.4189f,
			M33 = -0.0814f,
			M44 = 1
		};

		private IPixelSource ysrc, usrc, vsrc;

		public ChromaPosition ChromaPosition => ChromaPosition.InterstitialHorizontal | ChromaPosition.CositedVertical;
		public Matrix4x4 RgbYccMatrix => yccMatrix;
		public bool IsFullRange => false;

		public override IPixelSource PixelSource => ysrc ??= new WebPDecBufferPixelSource(this, WebPPlane.Y);
		public IPixelSource PixelSourceCb => usrc ??= new WebPDecBufferPixelSource(this, WebPPlane.U);
		public IPixelSource PixelSourceCr => vsrc ??= new WebPDecBufferPixelSource(this, WebPPlane.V);

		public WebPYuvFrame(WebPContainer cont, WebPBitstreamFeatures feat, int index) : base(cont, feat, index) { }
	}
}
