// Copyright © Clinton Ingram and Contributors
// SPDX-License-Identifier: MIT

using System;
using System.Buffers;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Runtime.InteropServices;

using PhotoSauce.MagicScaler;

namespace PhotoSauce.NativeCodecs.JxlRs;

internal sealed unsafe class JxlRsContainer : IImageContainer, IMetadataSource, IIccProfileSource, IExifSource
{
	[UnmanagedFunctionPointer(CallingConvention.Cdecl)]
	private unsafe delegate nuint ReadCallback(nint context, byte* destination, nuint length);

	[UnmanagedFunctionPointer(CallingConvention.Cdecl)]
	private delegate long SeekCallback(nint context, long offset, int origin);

	private static readonly ReadCallback readCallback = readStream;
	private static readonly SeekCallback seekCallback = seekStream;
	private static readonly nint readCallbackPointer = Marshal.GetFunctionPointerForDelegate(readCallback);
	private static readonly nint seekCallbackPointer = Marshal.GetFunctionPointerForDelegate(seekCallback);
	private static readonly nuint callbackError = unchecked((nuint)(nint)(-1));

	private readonly Stream stream;
	private readonly long streamStart;
	private readonly JxlRsImageInfo info;
	private readonly int frameOffset, frameCount;
	private readonly PixelFormat format;
	private readonly JxlExifLocation exif;
	private nint handle;

	private JxlRsContainer(Stream imageStream, long start, nint nativeHandle, in JxlRsImageInfo imageInfo, in JxlExifLocation exifLocation, IDecoderOptions? options)
	{
		stream = imageStream;
		streamStart = start;
		handle = nativeHandle;
		info = imageInfo;
		exif = exifLocation;
		format = info.Channels switch {
			1 => PixelFormat.Grey8,
			3 => PixelFormat.Bgr24,
			4 when info.AlphaAssociated != 0 => PixelFormat.Pbgra32,
			4 => PixelFormat.Bgra32,
			_ => throw new InvalidOperationException($"jxl-rs returned an unsupported channel count: {info.Channels}.")
		};

		var range = options is IMultiFrameDecoderOptions multi ? multi.FrameRange : Range.All;
		(frameOffset, frameCount) = range.GetOffsetAndLengthNoThrow(checked((int)info.FrameCount));
	}

	public string MimeType => ImageMimeTypes.Jxl;

	int IImageContainer.FrameCount => frameCount;

	int IIccProfileSource.ProfileLength => checked((int)info.IccLength);

	int IExifSource.ExifLength => exif.Length;

	IImageFrame IImageContainer.GetFrame(int index)
	{
		ensureHandle();

		index += frameOffset;
		if ((uint)index >= info.FrameCount || index < frameOffset || index >= frameOffset + frameCount)
			throw new ArgumentOutOfRangeException(nameof(index), "Invalid image frame index.");

		JxlRsFrameInfo frameInfo;
		if (!JxlRsNative.jxlrs_get_frame(handle, (uint)index, &frameInfo))
			throw new InvalidOperationException("jxl-rs did not return the requested frame.");

		return new JxlRsFrame(this, (uint)index, frameInfo);
	}

	void IIccProfileSource.CopyProfile(Span<byte> destination)
	{
		ensureHandle();
		if (destination.Length < checked((int)info.IccLength))
			throw new ArgumentException("The destination is too small.", nameof(destination));

		new ReadOnlySpan<byte>(JxlRsNative.jxlrs_get_icc(handle), checked((int)info.IccLength)).CopyTo(destination);
	}

	void IExifSource.CopyExif(Span<byte> destination)
	{
		ensureHandle();

		lock (stream)
			JxlContainerBoxReader.CopyExif(stream, exif, destination);
	}

	public bool TryGetMetadata<T>([NotNullWhen(true)] out T? metadata) where T : IMetadata
	{
		ensureHandle();

		if (typeof(T) == typeof(AnimationContainer) && info.FrameCount > 1)
		{
			metadata = (T)(object)new AnimationContainer((int)info.Width, (int)info.Height, (int)info.FrameCount, (int)info.LoopCount);
			return true;
		}

		if (typeof(T) == typeof(OrientationMetadata))
		{
			metadata = (T)(object)new OrientationMetadata((Orientation)info.Orientation);
			return true;
		}

		if (typeof(T) == typeof(IIccProfileSource) && info.IccLength != 0)
		{
			metadata = (T)(object)this;
			return true;
		}

		if (typeof(T) == typeof(IExifSource) && !exif.IsEmpty)
		{
			metadata = (T)(object)this;
			return true;
		}

		metadata = default;
		return false;
	}

	public static JxlRsContainer? TryLoad(Stream imageStream, IDecoderOptions? options)
	{
		long start = imageStream.Position;
		Span<byte> signature = stackalloc byte[12];
		int signatureLength = 0;
		while (signatureLength < signature.Length)
		{
			int read = imageStream.Read(signature[signatureLength..]);
			if (read == 0)
				break;

			signatureLength += read;
		}
		imageStream.Position = start;

		bool isCodestream = signatureLength >= 2 && signature[0] == 0xff && signature[1] == 0x0a;
		ReadOnlySpan<byte> containerSignature = [ 0x00, 0x00, 0x00, 0x0c, 0x4a, 0x58, 0x4c, 0x20, 0x0d, 0x0a, 0x87, 0x0a ];
		bool isContainer = signatureLength >= containerSignature.Length && signature[..containerSignature.Length].SequenceEqual(containerSignature);
		if (!isCodestream && !isContainer)
			return null;

		JxlExifLocation exif = default;
		if (isContainer)
			JxlContainerBoxReader.TryFindExif(imageStream, start, out exif);

		int maxDegreeOfParallelism = options is JxlRsDecoderOptions jxlOptions ? jxlOptions.MaxDegreeOfParallelism : 1;
		if (maxDegreeOfParallelism < 0)
			throw new ArgumentOutOfRangeException(nameof(JxlRsDecoderOptions.MaxDegreeOfParallelism), "The maximum degree of parallelism must not be negative.");
		uint parallelism = (uint)maxDegreeOfParallelism;

		JxlRsFactory.EnsureDependency();

		JxlRsImageInfo info;
		nint handle = 0;
		try
		{
			if (imageStream is UnmanagedMemoryStream unmanaged)
			{
				unmanaged.Position = start;
				handle = JxlRsNative.jxlrs_inspect_with_options(unmanaged.PositionPointer, checked((nuint)(unmanaged.Length - start)), &info, parallelism);
			}
			else if (imageStream is MemoryStream memory && memory.TryGetBuffer(out var segment))
			{
				int offset = checked(segment.Offset + (int)start);
				int length = checked((int)(memory.Length - start));
				fixed (byte* data = segment.Array)
					handle = JxlRsNative.jxlrs_inspect_with_options(data + offset, (nuint)length, &info, parallelism);
			}
			else
			{
				using var callbacks = new StreamCallbackScope(imageStream);
				var nativeStream = callbacks.Native;
				handle = JxlRsNative.jxlrs_inspect_stream_with_options(&nativeStream, &info, parallelism);
				callbacks.ThrowIfFailed();
			}
		}
		finally
		{
			imageStream.Position = start;
		}

		if (handle == 0)
			throw JxlRsFactory.CreateDecoderException();

		try
		{
			return new JxlRsContainer(imageStream, start, handle, info, exif, options);
		}
		catch
		{
			JxlRsNative.jxlrs_destroy(handle);
			throw;
		}
	}

	private void decodeFrame(uint index, byte* destination, int destinationLength, int stride)
	{
		ensureHandle();

		bool success;
		lock (stream)
		{
			try
			{
				stream.Position = streamStart;
				if (stream is UnmanagedMemoryStream unmanaged)
				{
					success = JxlRsNative.jxlrs_decode_frame(handle, index, unmanaged.PositionPointer, checked((nuint)(unmanaged.Length - streamStart)), destination, (nuint)destinationLength, (nuint)stride);
				}
				else if (stream is MemoryStream memory && memory.TryGetBuffer(out var segment))
				{
					int offset = checked(segment.Offset + (int)streamStart);
					int length = checked((int)(memory.Length - streamStart));
					fixed (byte* data = segment.Array)
						success = JxlRsNative.jxlrs_decode_frame(handle, index, data + offset, (nuint)length, destination, (nuint)destinationLength, (nuint)stride);
				}
				else
				{
					using var callbacks = new StreamCallbackScope(stream);
					var nativeStream = callbacks.Native;
					success = JxlRsNative.jxlrs_decode_frame_stream(handle, index, &nativeStream, destination, (nuint)destinationLength, (nuint)stride);
					callbacks.ThrowIfFailed();
				}
			}
			finally
			{
				stream.Position = streamStart;
			}
		}

		if (!success)
			throw JxlRsFactory.CreateDecoderException();
	}

	private static nuint readStream(nint contextHandle, byte* destination, nuint length)
	{
		var context = (StreamCallbackContext?)GCHandle.FromIntPtr(contextHandle).Target;
		if (context is null || context.Error is not null)
			return callbackError;

		try
		{
			int requested = length > int.MaxValue ? int.MaxValue : (int)length;
#if NETFRAMEWORK
			byte[] buffer = ArrayPool<byte>.Shared.Rent(requested);
			try
			{
				int read = context.Stream.Read(buffer, 0, requested);
				Marshal.Copy(buffer, 0, (nint)destination, read);
				return (nuint)read;
			}
			finally
			{
				ArrayPool<byte>.Shared.Return(buffer);
			}
#else
			return (nuint)context.Stream.Read(new Span<byte>(destination, requested));
#endif
		}
		catch (Exception ex)
		{
			context.Error = ex;
			return callbackError;
		}
	}

	private static long seekStream(nint contextHandle, long offset, int origin)
	{
		var context = (StreamCallbackContext?)GCHandle.FromIntPtr(contextHandle).Target;
		if (context is null || context.Error is not null)
			return long.MinValue;

		try
		{
			long position = origin switch {
				0 => context.Stream.Seek(checked(context.Origin + offset), SeekOrigin.Begin),
				1 => context.Stream.Seek(offset, SeekOrigin.Current),
				2 => context.Stream.Seek(offset, SeekOrigin.End),
				_ => throw new ArgumentOutOfRangeException(nameof(origin))
			};
			return checked(position - context.Origin);
		}
		catch (Exception ex)
		{
			context.Error = ex;
			return long.MinValue;
		}
	}

	private void ensureHandle()
	{
		if (handle == 0)
			ThrowHelper.ThrowObjectDisposed(nameof(JxlRsContainer));
	}

	private void dispose(bool disposing)
	{
		if (handle == 0)
			return;

		JxlRsNative.jxlrs_destroy(handle);
		handle = 0;

		if (disposing)
			GC.SuppressFinalize(this);
	}

	public void Dispose() => dispose(true);

	~JxlRsContainer()
	{
		ThrowHelper.ThrowIfFinalizerExceptionsEnabled(nameof(JxlRsContainer));
		dispose(false);
	}

	private sealed class StreamCallbackContext(Stream stream)
	{
		public Stream Stream { get; } = stream;
		public long Origin { get; } = stream.Position;
		public Exception? Error { get; set; }
	}

	private sealed class StreamCallbackScope : IDisposable
	{
		private readonly StreamCallbackContext context;
		private GCHandle contextHandle;

		public JxlRsStream Native { get; }

		public StreamCallbackScope(Stream stream)
		{
			context = new StreamCallbackContext(stream);
			contextHandle = GCHandle.Alloc(context);
			Native = new JxlRsStream {
				Context = GCHandle.ToIntPtr(contextHandle),
				Read = readCallbackPointer,
				Seek = seekCallbackPointer
			};
		}

		public void ThrowIfFailed()
		{
			if (context.Error is not null)
				throw new IOException("Failed to read the JPEG XL input stream.", context.Error);
		}

		public void Dispose()
		{
			if (contextHandle.IsAllocated)
				contextHandle.Free();
		}
	}

	private sealed class JxlRsFrame : PixelSource, IImageFrame, IMetadataSource
	{
		private readonly JxlRsContainer container;
		private readonly uint frameIndex;
		private readonly JxlRsFrameInfo frame;
		private RentedBuffer<byte> pixelBuffer;
		private bool decodedDirectly;
		private bool disposed;

		public JxlRsFrame(JxlRsContainer container, uint index, in JxlRsFrameInfo frame)
		{
			this.container = container;
			frameIndex = index;
			this.frame = frame;
		}

		public override PixelFormat Format => container.format;
		public override int Width => checked((int)container.info.Width);
		public override int Height => checked((int)container.info.Height);
		public IPixelSource PixelSource => this;

		public bool TryGetMetadata<T>([NotNullWhen(true)] out T? metadata) where T : IMetadata
		{
			if (typeof(T) == typeof(AnimationFrame) && container.info.FrameCount > 1)
			{
				var duration = (frame.DurationMilliseconds / 1000.0).ToRational();
				bool hasAlpha = Format.AlphaRepresentation != PixelAlphaRepresentation.None;
				metadata = (T)(object)new AnimationFrame(0, 0, duration, FrameDisposalMethod.Preserve, AlphaBlendMethod.Source, hasAlpha);
				return true;
			}

			return container.TryGetMetadata(out metadata);
		}

		protected override void CopyPixelsInternal(in PixelArea area, int stride, int bufferSize, byte* destination)
		{
			if (disposed)
				ThrowHelper.ThrowObjectDisposed(nameof(JxlRsFrame));

			int bytesPerPixel = Format.BytesPerPixel;
			int sourceStride = checked(Width * bytesPerPixel);
			bool isFullFrame = area.X == 0 && area.Y == 0 && area.Width == Width && area.Height == Height;
			if (isFullFrame && pixelBuffer.IsEmpty && !decodedDirectly)
			{
				container.decodeFrame(frameIndex, destination, bufferSize, stride);
				decodedDirectly = true;
				return;
			}

			ensurePixelBuffer(sourceStride);
			fixed (byte* sourceStart = pixelBuffer)
			{
				for (int y = 0; y < area.Height; y++)
				{
					byte* source = sourceStart + (area.Y + y) * sourceStride + area.X * bytesPerPixel;
					new ReadOnlySpan<byte>(source, area.Width * bytesPerPixel).CopyTo(new Span<byte>(destination + y * stride, stride));
				}
			}
		}

		private void ensurePixelBuffer(int sourceStride)
		{
			if (!pixelBuffer.IsEmpty)
				return;

			pixelBuffer = BufferPool.Rent<byte>(checked(sourceStride * Height));
			try
			{
				fixed (byte* destination = pixelBuffer)
					container.decodeFrame(frameIndex, destination, pixelBuffer.Length, sourceStride);
			}
			catch
			{
				pixelBuffer.Dispose();
				pixelBuffer = default;
				throw;
			}
		}

		public override string ToString() => nameof(JxlRsFrame);

		protected override void Dispose(bool disposing)
		{
			if (disposed)
				return;

			disposed = true;
			pixelBuffer.Dispose();
			pixelBuffer = default;
			base.Dispose(disposing);
		}

		~JxlRsFrame()
		{
			ThrowHelper.ThrowIfFinalizerExceptionsEnabled(nameof(JxlRsFrame));
			Dispose(false);
		}
	}
}
