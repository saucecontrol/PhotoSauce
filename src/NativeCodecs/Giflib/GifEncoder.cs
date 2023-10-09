// Copyright © Clinton Ingram and Contributors.  Licensed under the MIT License.

using System;
using System.IO;
using System.Drawing;
using System.Buffers.Binary;
using System.Runtime.InteropServices;
using System.Runtime.CompilerServices;

using PhotoSauce.MagicScaler;
using PhotoSauce.MagicScaler.Converters;
using PhotoSauce.Interop.Giflib;
using static PhotoSauce.Interop.Giflib.Giflib;

namespace PhotoSauce.NativeCodecs.Giflib;

internal sealed unsafe class GifEncoder : IAnimatedImageEncoder
{
	private static readonly bool isWindows = RuntimeInformation.IsOSPlatform(OSPlatform.Windows);

	private GifFileType* handle;
	private bool written;
	private AnimationContainer? animation;

	public static GifEncoder Create(Stream outStream, IEncoderOptions? gifOptions) => new(outStream, gifOptions);

	private GifEncoder(Stream outStream, IEncoderOptions? _)
	{
		var gch = GCHandle.Alloc(outStream);

		handle = GifFactory.CreateEncoder(GCHandle.ToIntPtr(gch), pfnWriteCallback);
		if (handle is null)
		{
			gch.Free();
			ThrowHelper.ThrowOutOfMemory();
		}
	}

	public void WriteAnimationMetadata(IMetadataSource metadata)
	{
		if (!metadata.TryGetMetadata<AnimationContainer>(out var anicnt))
			anicnt = default;

		animation = anicnt;
	}

	public void WriteFrame(IPixelSource source, IMetadataSource metadata, Rectangle sourceArea)
	{
		var area = sourceArea == default ? new PixelArea(0, 0, source.Width, source.Height) : ((PixelArea)sourceArea);
		if (source.Width > ushort.MaxValue || area.Height > ushort.MaxValue)
			throw new NotSupportedException($"Image too large.  This encoder supports a max of {ushort.MaxValue}x{ushort.MaxValue} pixels.");

		if (source.Format != PixelFormat.Indexed8.FormatGuid || source is not IIndexedPixelSource idxs)
			throw new NotSupportedException("Image format not supported.");

		var (width, height) = animation.HasValue ? (animation.Value.ScreenWidth, animation.Value.ScreenHeight) : (area.Width, area.Height);

		if (!written)
			writeHeader(width, height, idxs);
		else if (!animation.HasValue)
			throw new InvalidOperationException("An image frame has already been written, and this encoder is not configured for multiple frames.");

		if (!metadata.TryGetMetadata<AnimationFrame>(out var anifrm))
			anifrm = AnimationFrame.Default;

		var pal = idxs.Palette;
		if (animation.HasValue || idxs.HasAlpha())
		{
			int transIdx = -1;
			for (int i = 0; i < pal.Length; i++)
			if (pal[i] < 0xff000000)
			{
				transIdx = i;
				break;
			}

			var duration = anifrm.Duration.NormalizeTo(100);
			var gcb = new GraphicsControlBlock { DelayTime = (int)duration.Numerator, DisposalMode = (int)anifrm.Disposal, TransparentColor = transIdx };

			uint ext;
			_ = EGifGCBToExtension(&gcb, (byte*)&ext);
			checkResult(EGifPutExtension(handle, GRAPHICS_EXT_FUNC_CODE, sizeof(uint), &ext));
		}

		int palbits = 1;
		for (; palbits <= 8; palbits++)
		if ((1 << palbits) >= pal.Length)
			break;

		int palcnt = 1 << palbits;
		using var palbuf = BufferPool.RentLocal<byte>(palcnt * 4, true);
		fixed (byte* pp = palbuf)
		{
			var bpal = MemoryMarshal.AsBytes(pal);
			Unsafe.CopyBlock(ref *pp, ref MemoryMarshal.GetReference(bpal), (uint)bpal.Length);
			Swizzlers<byte>.GetSwapConverter(4, 3).ConvertLine(pp, pp, bpal.Length);

			var cmap = new ColorMapObject { BitsPerPixel = palbits, ColorCount = palcnt, Colors = (GifColorType*)pp };
			checkResult(EGifPutImageDesc(handle, anifrm.OffsetLeft, anifrm.OffsetTop, area.Width, area.Height, 0, &cmap));
		}

		writePixels(source, area);

		written = true;
	}

	public void Commit()
	{
		if (!written)
			throw new InvalidOperationException("An image frame has not been written.");
	}

	private void writeHeader(int width, int height, IIndexedPixelSource idxs)
	{
		if (animation.HasValue || idxs.HasAlpha())
			EGifSetGifVersion(handle, 1);

		if (animation.HasValue && animation.Value.PixelAspectRatio != 1f)
			handle->AspectByte = (byte)((int)(animation.Value.PixelAspectRatio * 64f - 15f)).Clamp(byte.MinValue, byte.MaxValue);

		if (animation.HasValue && animation.Value.BackgroundColor != default)
		{
			uint bg = (uint)animation.Value.BackgroundColor;
			var pal = stackalloc GifColorType[2];
			pal[0] = new GifColorType { Red = (byte)(bg >> 16), Green = (byte)(bg >> 8), Blue = (byte)bg };
			pal[1] = default;

			var cmap = new ColorMapObject { BitsPerPixel = 1, ColorCount = 2, Colors = pal };
			checkResult(EGifPutScreenDesc(handle, width, height, 1, 0, &cmap));
		}
		else
		{
			checkResult(EGifPutScreenDesc(handle, width, height, 1, 0, null));
		}

		if (animation.HasValue && animation.Value.LoopCount != 1)
		{
			byte* ext = stackalloc byte[] { 1, 0, 0 };
			BinaryPrimitives.WriteUInt16LittleEndian(new Span<byte>(ext + 1, 2), (ushort)animation.Value.LoopCount);

			checkResult(EGifPutExtensionLeader(handle, APPLICATION_EXT_FUNC_CODE));
			checkResult(EGifPutExtensionBlock(handle, Netscape2_0.Length, Netscape2_0.GetAddressOf()));
			checkResult(EGifPutExtensionBlock(handle, 3, ext));
			checkResult(EGifPutExtensionTrailer(handle));
		}
	}

	private void writePixels(IPixelSource src, PixelArea area)
	{
		var srcfmt = PixelFormat.FromGuid(src.Format);
		int stride = MathUtil.PowerOfTwoCeiling(area.Width * srcfmt.BytesPerPixel, sizeof(uint));

		using var buff = BufferPool.RentLocalAligned<byte>(stride);
		var span = buff.Span;

		fixed (byte* pbuf = buff)
		{
			for (int y = 0; y < area.Height; y++)
			{
				src.CopyPixels(area.Slice(y, 1), stride, span);
				checkResult(EGifPutLine(handle, pbuf, area.Width));
			}
		}
	}

	private void checkResult(int res)
	{
		if (res == GIF_ERROR)
			throw new InvalidOperationException($"{nameof(Giflib)} encoder failed. {new string(GifErrorString(handle->Error))}");
	}

	private void dispose(bool disposing)
	{
		if (handle is null)
			return;

		var gch = GCHandle.FromIntPtr((IntPtr)handle->UserData);

		// EGifCloseFile will attempt to write GIF trailer byte before cleaning up.
		// We prevent that during finalization by taking the file handle away from it.
		if (!disposing)
			handle->UserData = null;

		int err;
		_ = EGifCloseFile(handle, &err);
		handle = null;

		gch.Free();

		if (disposing)
			GC.SuppressFinalize(this);
	}

	public void Dispose() => dispose(true);

	~GifEncoder()
	{
		ThrowHelper.ThrowIfFinalizerExceptionsEnabled(nameof(GifEncoder));

		dispose(false);
	}

#if !NET5_0_OR_GREATER
	[UnmanagedFunctionPointer(CallingConvention.Cdecl)] private delegate int WriteCallback(GifFileType* pinst, byte* buff, int cb);
	private static readonly WriteCallback delWriteCallback = typeof(GifEncoder).CreateMethodDelegate<WriteCallback>(nameof(writeCallback));
#else
	[UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvCdecl) })]
	static
#endif
	private int writeCallback(GifFileType* pinst, byte* buff, int cb)
	{
		try
		{
			if (pinst->UserData is null)
				return 0;

			var stm = Unsafe.As<Stream>(GCHandle.FromIntPtr((IntPtr)pinst->UserData).Target!);
			stm.Write(new ReadOnlySpan<byte>(buff, cb));

			return cb;
		}
		catch when (!isWindows)
		{
			return 0;
		}
	}

	private static readonly delegate* unmanaged[Cdecl]<GifFileType*, byte*, int, int> pfnWriteCallback =
#if NET5_0_OR_GREATER
		&writeCallback;
#else
		(delegate* unmanaged[Cdecl]<GifFileType*, byte*, int, int>)Marshal.GetFunctionPointerForDelegate(delWriteCallback);
#endif
}
