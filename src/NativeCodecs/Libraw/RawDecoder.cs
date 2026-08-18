// Copyright © Clinton Ingram and Contributors
// SPDX-License-Identifier: MIT

using System;
using System.IO;
using System.Runtime.InteropServices;

using PhotoSauce.MagicScaler;
using PhotoSauce.Interop.Libraw;
using static PhotoSauce.Interop.Libraw.Libraw;

namespace PhotoSauce.NativeCodecs.Libraw;

internal sealed unsafe class RawContainer : IImageContainer
{
	private readonly int width, height, channels;
	private void* decoder;

	private RawContainer(void* dec, in psraw_image_info info)
	{
		decoder = dec;
		width = info.width;
		height = info.height;
		channels = info.channels;
	}

	public string MimeType => "image/x-raw";

	public int FrameCount => 1;

	public IImageFrame GetFrame(int index)
	{
		ensureHandle();
		if (index != 0)
			throw new ArgumentOutOfRangeException(nameof(index), "Invalid frame index.");

		return new RawFrame(this);
	}

	public static RawContainer? TryLoad(Stream imgStream, IDecoderOptions? options)
	{
		long position = imgStream.Position;
		long remaining = imgStream.Length - position;
		if (remaining <= 0 || remaining > int.MaxValue)
			return null;

		using var input = BufferPool.RentLocal<byte>((int)remaining);
		imgStream.FillBuffer(input.Span);

		var managedOptions = options is LibrawDecoderOptions rawOptions ? rawOptions : LibrawDecoderOptions.Default;
		var nativeOptions = new psraw_options {
			use_camera_white_balance = managedOptions.UseCameraWhiteBalance ? 1 : 0,
			auto_brightness = managedOptions.AutoBrightness ? 1 : 0,
			half_size = managedOptions.HalfSize ? 1 : 0
		};

		var info = default(psraw_image_info);
		int error;
		void* handle;
		fixed (byte* data = input)
			handle = RawFactory.Create(data, (nuint)input.Length, &nativeOptions, &info, &error);

		if (handle is not null)
			return new RawContainer(handle, info);

		imgStream.Position = position;
		if (error == LIBRAW_FILE_UNSUPPORTED)
			return null;
		if (error == LIBRAW_UNSUFFICIENT_MEMORY)
			ThrowHelper.ThrowOutOfMemory();

		string message = Marshal.PtrToStringAnsi((IntPtr)PsRawStrError(error)) ?? $"LibRaw error {error}.";
		throw new InvalidDataException(message);
	}

	private void ensureHandle()
	{
		if (decoder is null)
			ThrowHelper.ThrowObjectDisposed(nameof(RawContainer));
	}

	private void dispose(bool disposing)
	{
		if (decoder is null)
			return;

		PsRawDestroy(decoder);
		decoder = null;

		if (disposing)
			GC.SuppressFinalize(this);
	}

	public void Dispose() => dispose(true);

	~RawContainer()
	{
		ThrowHelper.ThrowIfFinalizerExceptionsEnabled(nameof(RawContainer));
		dispose(false);
	}

	private sealed class RawFrame(RawContainer container) : PixelSource, IImageFrame
	{
		public override PixelFormat Format => container.channels == 1 ? PixelFormat.Grey8 : PixelFormat.Rgb24;
		public override int Width => container.width;
		public override int Height => container.height;

		public IPixelSource PixelSource => this;

		protected override void CopyPixelsInternal(in PixelArea prc, int cbStride, int cbBufferSize, byte* pbBuffer)
		{
			container.ensureHandle();
			int result = PsRawCopyPixels(container.decoder, prc.X, prc.Y, prc.Width, prc.Height, cbStride, pbBuffer);
			if (result != LIBRAW_SUCCESS)
				throw new InvalidOperationException("LibRaw failed to copy decoded pixels.");
		}

		public override string ToString() => nameof(RawFrame);
	}
}
