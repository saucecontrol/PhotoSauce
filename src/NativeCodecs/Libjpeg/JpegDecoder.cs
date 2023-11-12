// Copyright © Clinton Ingram and Contributors.  Licensed under the MIT License.

using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Runtime.CompilerServices;
using System.Diagnostics.CodeAnalysis;

using PhotoSauce.MagicScaler;
using PhotoSauce.MagicScaler.Converters;
using PhotoSauce.Interop.Libjpeg;
using static PhotoSauce.Interop.Libjpeg.Libjpeg;

namespace PhotoSauce.NativeCodecs.Libjpeg;

internal sealed unsafe class JpegContainer : IImageContainer
{
	private static readonly bool isWindows = RuntimeInformation.IsOSPlatform(OSPlatform.Windows);

	public readonly IPlanarDecoderOptions Options;
	private readonly Stream stream;
	private readonly long streamStart;
	private jpeg_decompress_struct* handle;
	private JpegFrame? frame;

	private JpegContainer(jpeg_decompress_struct* pinst, Stream stm, IDecoderOptions? opt)
	{
		Options = opt as IPlanarDecoderOptions ?? JpegDecoderOptions.Default;
		stream = stm;
		streamStart = stm.Position;
		handle = pinst;
		frame = null;
	}

	public string MimeType => ImageMimeTypes.Jpeg;

	public int FrameCount => 1;

	public IImageFrame GetFrame(int index)
	{
		if (index != 0)
			throw new ArgumentOutOfRangeException(nameof(index));

		return frame ??= new JpegFrame(this);
	}

	public static JpegContainer? TryLoad(Stream imgStream, IDecoderOptions? options)
	{
		long pos = imgStream.Position;
		var handle = JpegFactory.CreateDecoder();
		if (handle is null)
			ThrowHelper.ThrowOutOfMemory();

		var pcd = (ps_client_data*)handle->client_data;
		pcd->stream_handle = GCHandle.ToIntPtr(GCHandle.Alloc(imgStream));
		pcd->read_callback = pfnReadCallback;
		pcd->seek_callback = pfnSeekCallback;

		int read = JpegReadHeader(handle);
		imgStream.Position = pos;

		if (read == TRUE && handle->IsValidImage() && handle->data_precision == 8)
		{
			JpegAbortDecompress(handle);

			return new JpegContainer(handle, imgStream, options);
		}

		GCHandle.FromIntPtr(pcd->stream_handle).Free();
		JpegDestroy((jpeg_common_struct*)handle);

		return null;
	}

	public jpeg_decompress_struct* GetHandle()
	{
		if (handle is null)
			ThrowHelper.ThrowObjectDisposed(nameof(JpegContainer));

		return handle;
	}

	public void CheckResult(int res)
	{
		if (res == FALSE)
			throwJpegError(handle);
	}

	public void RewindStream() => stream.Position = streamStart;

	[DoesNotReturn]
	private static void throwJpegError(jpeg_decompress_struct* handle) =>
		throw new InvalidOperationException($"{nameof(Libjpeg)} decoder failed. {new string(JpegGetLastError((jpeg_common_struct*)handle))}");

	private void dispose(bool disposing)
	{
		if (handle is null)
			return;

		var pcd = (ps_client_data*)handle->client_data;
		GCHandle.FromIntPtr(pcd->stream_handle).Free();

		JpegDestroy((jpeg_common_struct*)handle);
		handle = null;

		if (disposing)
			GC.SuppressFinalize(this);
	}

	public void Dispose() => dispose(true);

	~JpegContainer()
	{
		ThrowHelper.ThrowIfFinalizerExceptionsEnabled(nameof(JpegContainer));

		dispose(false);
	}

#if !NET5_0_OR_GREATER
	[UnmanagedFunctionPointer(CallingConvention.Cdecl)] private delegate nuint ReadCallback(nint pinst, byte* buff, nuint cb);
	private static readonly ReadCallback delReadCallback = typeof(JpegContainer).CreateMethodDelegate<ReadCallback>(nameof(readCallback));
#else
	[UnmanagedCallersOnly(CallConvs = [ typeof(CallConvCdecl) ])]
	static
#endif
	private nuint readCallback(nint pinst, byte* buff, nuint cb)
	{
		try
		{
			var stm = Unsafe.As<Stream>(GCHandle.FromIntPtr(pinst).Target!);
			cb = (uint)stm.Read(new Span<byte>(buff, checked((int)cb)));

			return cb;
		}
		catch when (!isWindows)
		{
			return unchecked((nuint)~0ul);
		}
	}

#if !NET5_0_OR_GREATER
	[UnmanagedFunctionPointer(CallingConvention.Cdecl)] private delegate nuint SeekCallback(nint pinst, nuint cb);
	private static readonly SeekCallback delSeekCallback = typeof(JpegContainer).CreateMethodDelegate<SeekCallback>(nameof(seekCallback));
#else
	[UnmanagedCallersOnly(CallConvs = [ typeof(CallConvCdecl) ])]
	static
#endif
	private nuint seekCallback(nint pinst, nuint cb)
	{
		try
		{
			var stm = Unsafe.As<Stream>(GCHandle.FromIntPtr(pinst).Target!);
			_ = stm.Seek((uint)cb, SeekOrigin.Current);

			return cb;
		}
		catch when (!isWindows)
		{
			return unchecked((nuint)~0ul);
		}
	}

	private static readonly delegate* unmanaged[Cdecl]<nint, byte*, nuint, nuint> pfnReadCallback =
#if NET5_0_OR_GREATER
		&readCallback;
#else
		(delegate* unmanaged[Cdecl]<nint, byte*, nuint, nuint>)Marshal.GetFunctionPointerForDelegate(delReadCallback);
#endif

	private static readonly delegate* unmanaged[Cdecl]<nint, nuint, nuint> pfnSeekCallback =
#if NET5_0_OR_GREATER
		&seekCallback;
#else
		(delegate* unmanaged[Cdecl]<nint, nuint, nuint>)Marshal.GetFunctionPointerForDelegate(delSeekCallback);
#endif
}

internal sealed unsafe class JpegFrame : IImageFrame, IPlanarDecoder, IMetadataSource, ICroppedDecoder, IScaledDecoder, IIccProfileSource, IExifSource
{
	public readonly JpegContainer Container;
	private PixelArea decodeCrop, outCrop;
	private int decodeWidth, decodeHeight;
	private RentedBuffer<byte> exifData, iccpData, lineBuff;

	public IPixelSource PixelSource { get; }
	public ref readonly PixelArea OutCrop => ref outCrop;
	public ref readonly PixelArea ScaledCrop => ref decodeCrop;

	int IIccProfileSource.ProfileLength => iccpData.Length;

	int IExifSource.ExifLength => exifData.Length;

	public JpegFrame(JpegContainer cont)
	{
		var handle = cont.GetHandle();
		cont.CheckResult(JpegSaveMarkers(handle, JPEG_APP1, ushort.MaxValue));
		cont.CheckResult(JpegSaveMarkers(handle, JPEG_APP2, ushort.MaxValue));
		cont.CheckResult(JpegReadHeader(handle));

		Container = cont;
		decodeWidth = (int)handle->image_width;
		decodeHeight = (int)handle->image_height;
		decodeCrop = new(0, 0, decodeWidth, decodeHeight);
		outCrop = decodeCrop;
		PixelSource = new JpegPixelSource(this);
	}

	public bool TryGetYccFrame([NotNullWhen(true)] out IYccImageFrame? frame)
	{
		var handle = Container.GetHandle();
		if (Container.Options.AllowPlanar && handle->IsPlanarSupported())
		{
			handle->raw_data_out = TRUE;
			frame = new JpegPlanarCache(this);
			return true;
		}

		frame = null;
		return false;
	}

	public void SetDecodeCrop(PixelArea crop)
	{
		var handle = Container.GetHandle();
		if (handle->global_state > DSTATE_READY)
			throw new InvalidOperationException("Scale cannot be changed after decode has started.");

		decodeCrop = decodeCrop.Intersect(crop);
		outCrop = decodeCrop;
	}

	public (int width, int height) SetDecodeScale(int ratio)
	{
		var handle = Container.GetHandle();
		if (handle->global_state > DSTATE_READY)
			throw new InvalidOperationException("Scale cannot be changed after decode has started.");

		ratio = ratio.Clamp(1, 8);
		handle->scale_denom = (uint)ratio;
		Container.CheckResult(JpegCalcOutputDimensions(handle));

		decodeWidth = (int)handle->output_width;
		decodeHeight = (int)handle->output_height;
		decodeCrop = decodeCrop.ScaleTo(ratio, ratio, decodeWidth, decodeHeight);
		outCrop = decodeCrop;

		return (decodeCrop.Width, decodeCrop.Height);
	}

	void IIccProfileSource.CopyProfile(Span<byte> dest) => iccpData.Span.CopyTo(dest);

	void IExifSource.CopyExif(Span<byte> dest) => exifData.Span.CopyTo(dest);

	public bool TryGetMetadata<T>([NotNullWhen(true)] out T? metadata) where T : IMetadata
	{
		var handle = Container.GetHandle();

		if (typeof(T) == typeof(ResolutionMetadata))
		{
			if (handle->saw_JFIF_marker == TRUE)
			{
				var unit = handle->density_unit switch {
					1 => ResolutionUnit.Inch,
					2 => ResolutionUnit.Centimeter,
					_ => ResolutionUnit.Virtual
				};

				metadata = (T)(object)(new ResolutionMetadata(new Rational(handle->X_density, 1), new Rational(handle->Y_density, 1), unit));
				return true;
			}
		}

		if (typeof(T) == typeof(IIccProfileSource))
		{
			if (iccpData.Length == 0)
			{
				byte* data;
				uint len;
				Container.CheckResult(JpegReadIccProfile(handle, &data, &len));

				if (data is not null)
				{
					iccpData = BufferPool.Rent<byte>((int)len);
					new Span<byte>(data, (int)len).CopyTo(iccpData.Span);
					JpegFree(data);
				}
			}

			if (iccpData.Length != 0)
			{
				metadata = (T)(object)this;
				return true;
			}
		}

		if (typeof(T) == typeof(IExifSource))
		{
			if (exifData.Length == 0)
			{
				for (var marker = handle->marker_list; marker is not null; marker = marker->next)
				{
					if (marker->IsExifMarker())
					{
						exifData = BufferPool.Rent<byte>((int)marker->data_length - ExifIdentifier.Length);
						new Span<byte>(marker->data, (int)marker->data_length)[ExifIdentifier.Length..].CopyTo(exifData.Span);
					}
				}
			}

			if (exifData.Length != 0)
			{
				metadata = (T)(object)this;
				return true;
			}
		}

		metadata = default;
		return false;
	}

	public void ResetDecoder()
	{
		var handle = Container.GetHandle();
		int planar = handle->raw_data_out;
		uint scale = handle->scale_denom;

		JpegAbortDecompress(handle);
		Container.RewindStream();

		Container.CheckResult(JpegSaveMarkers(handle, JPEG_APP1, 0));
		Container.CheckResult(JpegSaveMarkers(handle, JPEG_APP2, 0));
		Container.CheckResult(JpegReadHeader(handle));

		handle->scale_denom = scale;
		handle->raw_data_out = planar;

		Container.CheckResult(JpegCalcOutputDimensions(handle));
	}

	public void StartDecoder()
	{
		var handle = Container.GetHandle();
		Container.CheckResult(JpegStartDecompress(handle));

		if (decodeCrop.Width < (int)handle->output_width)
		{
			int cx = decodeCrop.X, cw = decodeCrop.Width;
			Container.CheckResult(JpegCropScanline(handle, (uint*)&cx, (uint*)&cw));
			outCrop = decodeCrop.RelativeTo(new(cx, 0, cw, decodeCrop.Height));
		}
	}

	public void Dispose()
	{
		iccpData.Dispose();
		exifData.Dispose();
		lineBuff.Dispose();
	}

	private class JpegPixelSource : PixelSource, IFramePixelSource
	{
		public JpegFrame frame;

		public override PixelFormat Format { get; }
		public override int Width => frame.outCrop.Width;
		public override int Height => frame.outCrop.Height;
		public IImageFrame Frame => frame;

		public JpegPixelSource(JpegFrame frm)
		{
			var handle = frm.Container.GetHandle();

			frame = frm;
			Format = handle->num_components switch {
				1 => PixelFormat.Grey8,
				3 => PixelFormat.Bgr24,
				4 => PixelFormat.Cmyk32,
				_ => throw new NotSupportedException("Pixel format not supported.")
			};

			if (handle->out_color_space is J_COLOR_SPACE.JCS_RGB or J_COLOR_SPACE.JCS_EXT_RGB)
				handle->out_color_space = J_COLOR_SPACE.JCS_EXT_BGR;
		}

		protected override void CopyPixelsInternal(in PixelArea prc, int cbStride, int cbBufferSize, byte* pbBuffer)
		{
			var container = frame.Container;
			var handle = container.GetHandle();
			int prcY = prc.Y + frame.outCrop.Y;

			if (prcY < (int)handle->output_scanline)
				frame.ResetDecoder();

			if (handle->global_state == DSTATE_READY)
				frame.StartDecoder();

			if (prcY != (int)handle->output_scanline)
			{
				uint skipped;
				container.CheckResult(JpegSkipScanlines(handle, (uint)prcY - handle->output_scanline, &skipped));
			}

			var linebuff = Span<byte>.Empty;
			if (prc.Width < handle->output_width)
			{
				if (frame.lineBuff.IsEmpty)
					frame.lineBuff = BufferPool.Rent<byte>(handle->num_components * (int)handle->output_width);

				linebuff = frame.lineBuff.Span;
			}

			fixed (byte* pbuff = linebuff)
			{
				for (int y = 0; y < prc.Height; y++)
				{
					byte* pout = pbBuffer + cbStride * y;
					uint read;
					if (pbuff is null)
					{
						container.CheckResult(JpegReadScanlines(handle, &pout, 1, &read));
					}
					else
					{
						container.CheckResult(JpegReadScanlines(handle, &pbuff, 1, &read));
						Unsafe.CopyBlockUnaligned(pout, pbuff + (frame.outCrop.X + prc.X) * handle->num_components, (uint)(prc.Width * handle->num_components));
					}

					// libjpeg outputs CMYK the way Adobe apps save it, which is inverted.
					if (handle->out_color_space is J_COLOR_SPACE.JCS_CMYK)
						InvertConverter.InvertLine(pout, (nint)((uint)prc.Width * 4));
				}
			}
		}

		public override string ToString() => nameof(JpegPixelSource);
	}
}
