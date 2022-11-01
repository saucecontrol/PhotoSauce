// Copyright © Clinton Ingram and Contributors.  Licensed under the MIT License.

using System;
using System.IO;
using System.Buffers.Binary;
using System.Runtime.InteropServices;
using System.Runtime.CompilerServices;
using System.Diagnostics.CodeAnalysis;

using PhotoSauce.MagicScaler;
using PhotoSauce.MagicScaler.Converters;
using PhotoSauce.Interop.Giflib;
using static PhotoSauce.Interop.Giflib.Giflib;

namespace PhotoSauce.NativeCodecs.Giflib;

internal sealed unsafe class GifContainer : IImageContainer, IMetadataSource
{
	private static readonly bool isWindows = RuntimeInformation.IsOSPlatform(OSPlatform.Windows);

	private readonly Stream stream;
	private readonly long streamStart;
	private readonly int frameCount, frameOffset;
	private readonly AnimationContainer animation;

	private GifFileType* handle;
	private GifFrame? frame;
	private RentedBuffer<uint> palette;

	public FrameDisposalMethod LastDisposal;

	private GifContainer(GifFileType* pinst, Stream stm, long pos, IDecoderOptions? opt)
	{
		stream = stm;
		streamStart = pos;
		handle = pinst;

		int loopCount = 0;
		var rec = default(GifRecordType);
		do
		{
			CheckResult(DGifGetRecordType(handle, &rec));
			if (rec == GifRecordType.EXTENSION_RECORD_TYPE)
			{
				int ext;
				byte* data;
				CheckResult(DGifGetExtension(handle, &ext, &data));
				while (data is not null)
				{
					if (ext == APPLICATION_EXT_FUNC_CODE)
					{
						var dspan = new Span<byte>(data + 1, data[0]);
						if (dspan.SequenceEqual(Netscape2_0) || dspan.SequenceEqual(Animexts1_0))
						{
							CheckResult(DGifGetExtensionNext(handle, &data));
							if (data is not null && data[0] >= 3 && data[1] == 1)
								loopCount = BinaryPrimitives.ReadUInt16LittleEndian(new Span<byte>(data + 2, 2));
						}
					}

					CheckResult(DGifGetExtensionNext(handle, &data));
				}
			}
			else if (rec == GifRecordType.IMAGE_DESC_RECORD_TYPE)
			{
				CheckResult(DGifGetImageDesc(handle));
				advanceFrame();
			}
		}
		while (rec != GifRecordType.TERMINATE_RECORD_TYPE);

		uint bgColor = 0;
		if (handle->SColorMap is not null)
		{
			var cmap = handle->SColorMap;
			palette = BufferPool.Rent<uint>(cmap->ColorCount);

			var cspan = palette.Span;
			fixed (uint* pp = cspan)
				ChannelChanger<byte>.GetSwapConverter(3, 4).ConvertLine((byte*)cmap->Colors, (byte*)pp, cspan.Length * 3);

			if (handle->SBackGroundColor < cspan.Length)
				bgColor = cspan[handle->SBackGroundColor];
		}

		float pixelAspect = handle->AspectByte == default ? 1f : ((handle->AspectByte + 15) / 64f);
		animation = new(handle->SWidth, handle->SHeight, handle->ImageCount, loopCount, (int)bgColor, pixelAspect, true);

		var range = opt is IMultiFrameDecoderOptions mul ? mul.FrameRange : Range.All;
		(frameOffset, frameCount) = range.GetOffsetAndLength(handle->ImageCount);

		ResetDecoder();
	}

	public string MimeType => ImageMimeTypes.Gif;

	public int FrameCount => frameCount;

	public ReadOnlySpan<uint> Palette => palette.Span;

	public IImageFrame GetFrame(int index)
	{
		index += frameOffset;
		if ((uint)index >= (uint)(frameOffset + frameCount))
			throw new ArgumentOutOfRangeException(nameof(index), "Invalid frame index.");

		int curr = frame?.Index ?? -1;
		if (index < curr)
		{
			ResetDecoder();
			curr = -1;
		}

		if (frame is { IsAtEnd: false })
			advanceFrame();

		var rec = default(GifRecordType);
		var gcb = default(GraphicsControlBlock);
		bool hasGcb = false;
		while (true)
		{
			CheckResult(DGifGetRecordType(handle, &rec));
			if (rec == GifRecordType.EXTENSION_RECORD_TYPE)
			{
				int ext;
				byte* data;
				CheckResult(DGifGetExtension(handle, &ext, &data));
				if (ext == GRAPHICS_EXT_FUNC_CODE)
				{
					CheckResult(DGifExtensionToGCB(data[0], data + 1, &gcb));
					hasGcb = true;
				}

				while (data is not null)
					CheckResult(DGifGetExtensionNext(handle, &data));
			}
			else if (rec == GifRecordType.IMAGE_DESC_RECORD_TYPE)
			{
				if (++curr == index)
					break;

				CheckResult(DGifGetImageDesc(handle));
				advanceFrame();
			}
		}

		return frame = new GifFrame(this, index, hasGcb, gcb);
	}

	public bool TryGetMetadata<T>([NotNullWhen(true)] out T? metadata) where T : IMetadata
	{
		ensureHandle();

		if (typeof(T) == typeof(AnimationContainer))
		{
			metadata = (T)(object)animation;
			return true;
		}

		metadata = default;
		return false;
	}

	public static GifContainer? TryLoad(Stream imgStream, IDecoderOptions? options)
	{
		long pos = imgStream.Position;
		var gch = GCHandle.Alloc(imgStream);

		var handle = GifFactory.CreateDecoder(GCHandle.ToIntPtr(gch), pfnReadCallback);
		if (handle is not null)
			return new GifContainer(handle, imgStream, pos, options);

		gch.Free();
		return null;
	}

	public GifFileType* GetHandle()
	{
		ensureHandle();

		return handle;
	}

	public void CheckResult(int res)
	{
		// TODO handle early EOF as a soft error
		//if (res == GIF_ERROR && handle->Error == D_GIF_ERR_READ_FAILED && handle->ImageCount > 0)
		//	return;

		if (res == GIF_ERROR)
			throwGifError(handle);
	}

	public void ResetDecoder(bool keepFrame = false)
	{
		var gch = handle->UserData;
		int err;
		_ = DGifCloseFile(handle, &err);

		RewindStream();
		handle = GifFactory.CreateDecoder((nint)gch, pfnReadCallback);
		if (!keepFrame)
		{
			frame = null;
			return;
		}

		int curr = 0;
		var rec = default(GifRecordType);
		do
		{
			CheckResult(DGifGetRecordType(handle, &rec));
			if (rec == GifRecordType.EXTENSION_RECORD_TYPE)
			{
				int ext;
				byte* data;
				CheckResult(DGifGetExtension(handle, &ext, &data));
				while (data is not null)
					CheckResult(DGifGetExtensionNext(handle, &data));
			}
			else if (rec == GifRecordType.IMAGE_DESC_RECORD_TYPE)
			{
				CheckResult(DGifGetImageDesc(handle));
				if (++curr == frame!.Index)
					break;

				advanceFrame();
			}
		}
		while (rec != GifRecordType.TERMINATE_RECORD_TYPE);
	}

	public void RewindStream() => stream.Position = streamStart;

	private void advanceFrame()
	{
		byte* data;
		do
			CheckResult(DGifGetCodeNext(handle, &data));
		while (data is not null);
	}

	[DoesNotReturn]
	private static void throwGifError(GifFileType* handle) =>
		throw new InvalidOperationException($"{nameof(Giflib)} decoder failed. {new string(GifErrorString(handle->Error))}");

	private void ensureHandle()
	{
		if (handle is null)
			ThrowHelper.ThrowObjectDisposed(nameof(GifContainer));
	}

	private void dispose(bool disposing)
	{
		if (handle is null)
			return;

		palette.Dispose();
		palette = default;

		GCHandle.FromIntPtr((IntPtr)handle->UserData).Free();

		int err;
		_ = DGifCloseFile(handle, &err);
		handle = null;

		if (disposing)
			GC.SuppressFinalize(this);
	}

	public void Dispose() => dispose(true);

	~GifContainer()
	{
		ThrowHelper.ThrowIfFinalizerExceptionsEnabled(nameof(GifContainer));

		dispose(false);
	}

#if !NET5_0_OR_GREATER
	[UnmanagedFunctionPointer(CallingConvention.Cdecl)] private delegate int ReadCallback(GifFileType pinst, byte* buff, int cb);
	private static readonly ReadCallback delReadCallback = typeof(GifContainer).CreateMethodDelegate<ReadCallback>(nameof(readCallback));
#else
	[UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvCdecl) })]
	static
#endif
	private int readCallback(GifFileType* pinst, byte* buff, int cb)
	{
		try
		{
			var stm = Unsafe.As<Stream>(GCHandle.FromIntPtr((IntPtr)pinst->UserData).Target!);
			cb = stm.TryFillBuffer(new Span<byte>(buff, cb));

			return cb;
		}
		catch when (!isWindows)
		{
			return 0;
		}
	}

	private static readonly delegate* unmanaged[Cdecl]<GifFileType*, byte*, int, int> pfnReadCallback =
#if NET5_0_OR_GREATER
		&readCallback;
#else
		(delegate* unmanaged[Cdecl]<GifFileType*, byte*, int, int>)Marshal.GetFunctionPointerForDelegate(delReadCallback);
#endif
}

internal sealed unsafe class GifFrame : IImageFrame, IMetadataSource
{
	private readonly GifContainer container;
	private readonly AnimationFrame anifrm;
	private readonly int width, height, transIdx;
	private readonly bool interlace;
	public readonly int Index;

	private FrameBufferSource? frameBuff;
	private RentedBuffer<byte> lineBuff;
	private RentedBuffer<uint> palette;
	private int lastRow;

	public IPixelSource PixelSource { get; }
	public bool IsAtEnd => lastRow == height;

	public GifFrame(GifContainer cont, int idx, bool hasGcb, in GraphicsControlBlock gcb)
	{
		var handle = cont.GetHandle();
		cont.CheckResult(DGifGetImageDesc(handle));

		container = cont;
		transIdx = NO_TRANSPARENT_COLOR;
		interlace = handle->Image.Interlace != 0;
		(width, height) = (handle->Image.Width, handle->Image.Height);

		var disposal = FrameDisposalMethod.Preserve;
		var blend = cont.LastDisposal == FrameDisposalMethod.RestoreBackground ? AlphaBlendMethod.Source : AlphaBlendMethod.Over;

		if (hasGcb)
		{
			transIdx = gcb.TransparentColor;
			disposal = ((FrameDisposalMethod)gcb.DisposalMode).Clamp();

			if (idx == 0 && disposal == FrameDisposalMethod.RestorePrevious)
				disposal = FrameDisposalMethod.Preserve;
		}

		cont.LastDisposal = disposal;
		anifrm = new AnimationFrame(
			handle->Image.Left,
			handle->Image.Top,
			new((uint)gcb.DelayTime, 100),
			disposal,
			blend,
			transIdx != NO_TRANSPARENT_COLOR
		);

		Index = idx;
		PixelSource = new GifPixelSource(this);
	}

	public bool TryGetMetadata<T>([NotNullWhen(true)] out T? metadata) where T : IMetadata
	{
		if (typeof(T) == typeof(AnimationFrame))
		{
			metadata = (T)(object)anifrm;
			return true;
		}

		return container.TryGetMetadata(out metadata);
	}

	public void Dispose()
	{
		frameBuff?.Dispose();
		lineBuff.Dispose();
		palette.Dispose();

		lineBuff = default;
		palette = default;
	}

	private sealed class GifPixelSource : PixelSource, IFramePixelSource, IIndexedPixelSource
	{
		public readonly GifContainer container;
		public readonly GifFrame frame;

		public override PixelFormat Format => PixelFormat.Indexed8;
		public override int Width => frame.width;
		public override int Height => frame.height;
		public IImageFrame Frame => frame;

		public ReadOnlySpan<uint> Palette
		{
			get {
				if (!frame.palette.IsEmpty)
					return frame.palette.Span;

				var handle = container.GetHandle();
				var cmap = handle->Image.ColorMap;
				if (cmap is null && frame.transIdx == NO_TRANSPARENT_COLOR)
					return container.Palette;

				int pallen = cmap is not null ? cmap->ColorCount : container.Palette.Length;
				frame.palette = BufferPool.Rent<uint>(pallen);

				var cspan = frame.palette.Span;
				if (cmap is not null)
					fixed (uint* pp = cspan)
						ChannelChanger<byte>.GetSwapConverter(3, 4).ConvertLine((byte*)cmap->Colors, (byte*)pp, cspan.Length * 3);
				else
					container.Palette.CopyTo(cspan);

				int trn = frame.transIdx;
				if (trn >= cspan.Length)
					trn = 0;

				if (trn != NO_TRANSPARENT_COLOR)
					cspan[trn] &= 0xffffffu;

				return cspan;
			}
		}

		public GifPixelSource(GifFrame frm) => (container, frame) = (frm.container, frm);

		private FrameBufferSource getFrameBuffer()
		{
			if (frame.frameBuff is null)
			{
				var handle = container.GetHandle();

				var fbuf = new FrameBufferSource(Width, Height, Format);
				fixed (byte* pbuf = fbuf.Span)
				{
					int* offs = stackalloc int[] { 0, 4, 2, 1 };
					int* incs = stackalloc int[] { 8, 8, 4, 2 };

					for (nint i = 0; i < 4; i++)
					for (int y = offs[i]; y < Height; y += incs[i])
					{
						byte* op = pbuf + y * fbuf.Stride;
						container.CheckResult(DGifGetLine(handle, op, Width));
					}
				}

				frame.frameBuff = fbuf;
				frame.lastRow = Height;
			}

			return frame.frameBuff;
		}

		protected override void CopyPixelsInternal(in PixelArea prc, int cbStride, int cbBufferSize, byte* pbBuffer)
		{
			var handle = container.GetHandle();

			if (handle->Image.Interlace != 0)
			{
				getFrameBuffer().CopyPixels(prc, cbStride, cbBufferSize, pbBuffer);
				return;
			}

			if (prc.Y < frame.lastRow)
			{
				container.ResetDecoder(true);
				frame.lastRow = 0;
			}

			int bpp = Format.BytesPerPixel;

			var linebuff = Span<byte>.Empty;
			if (prc.Width < Width)
			{
				if (frame.lineBuff.IsEmpty)
					frame.lineBuff = BufferPool.Rent<byte>(Width * bpp);

				linebuff = frame.lineBuff.Span;
			}

			fixed (byte* pbuff = linebuff)
			{
				for (int y = frame.lastRow; y < prc.Y; y++)
				{
					container.CheckResult(DGifGetLine(handle, pbuff is null ? pbBuffer : pbuff, Width));
					frame.lastRow++;
				}

				for (int y = 0; y < prc.Height; y++)
				{
					byte* pout = pbBuffer + cbStride * y;
					if (pbuff is null)
					{
						container.CheckResult(DGifGetLine(handle, pout, Width));
					}
					else
					{
						container.CheckResult(DGifGetLine(handle, pbuff, Width));
						Unsafe.CopyBlockUnaligned(pout, pbuff + prc.X * bpp, (uint)(prc.Width * bpp));
					}

					frame.lastRow++;
				}
			}
		}

		public override string ToString() => nameof(GifPixelSource);
	}
}
