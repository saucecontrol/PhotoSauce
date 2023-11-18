// Copyright © Clinton Ingram and Contributors.  Licensed under the MIT License.

using System;
using System.IO;
using System.Buffers.Binary;
using System.Diagnostics.CodeAnalysis;

using PhotoSauce.MagicScaler;
using PhotoSauce.MagicScaler.Converters;
using PhotoSauce.MagicScaler.Transforms;
using PhotoSauce.Interop.Giflib;
using static PhotoSauce.Interop.Giflib.Giflib;

namespace PhotoSauce.NativeCodecs.Giflib;

internal sealed unsafe class GifContainer : IImageContainer, IMetadataSource, IIccProfileSource
{
	private readonly int frameCount, frameOffset;
	private readonly AnimationContainer animation;
	public readonly PixelFormat Format;
	public readonly int ColorCount;

	private GifFileType* handle;
	private GifFrame? frame;
	private RentedBuffer<uint> palette;
	private RentedBuffer<byte> iccpData;

	public bool EOF;

	private GifContainer(GifFileType* pinst, IDecoderOptions? options)
	{
		handle = pinst;

		int loopCount = 1;
		bool alpha = false;

		var rec = default(GifRecordType);
		do
		{
			LoopTop:
			CheckResult(DGifGetRecordType(handle, &rec));
			if (rec == GifRecordType.EXTENSION_RECORD_TYPE)
			{
				int ext;
				byte* data;
				CheckResult(DGifGetExtension(handle, &ext, &data));
				while (data is not null && !EOF)
				{
					if (ext == APPLICATION_EXT_FUNC_CODE)
					{
						var dspan = new ReadOnlySpan<byte>(data + 1, data[0]);
						if (dspan.SequenceEqual(Netscape2_0) || dspan.SequenceEqual(Animexts1_0))
						{
							CheckResult(DGifGetExtensionNext(handle, &data));
							if (data is not null && data[0] >= 3 && data[1] == 1)
								loopCount = BinaryPrimitives.ReadUInt16LittleEndian(new ReadOnlySpan<byte>(data + 2, 2));
						}
						else if (dspan.Length >= 15 && dspan[..11].SequenceEqual(IccExtBlock))
						{
							int proflen = BinaryPrimitives.ReadInt32BigEndian(dspan[11..]);
							if (proflen <= 1 << 20)
							{
								iccpData = BufferPool.Rent<byte>(proflen);
								var wtr = new SpanBufferWriter(iccpData.Span);
								wtr.TryWrite(dspan[11..]);

								while (!EOF)
								{
									CheckResult(DGifGetExtensionNext(handle, &data));
									if (data is null)
										goto LoopTop;

									wtr.TryWrite(new ReadOnlySpan<byte>(data + 1, data[0]));
								}
							}
						}
					}
					else if (handle->ImageCount == 0 && ext == GRAPHICS_EXT_FUNC_CODE)
					{
						var gcb = default(GraphicsControlBlock);
						CheckResult(DGifExtensionToGCB(data[0], data + 1, &gcb));
						alpha = gcb.TransparentColor != NO_TRANSPARENT_COLOR;
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
		while (rec != GifRecordType.TERMINATE_RECORD_TYPE && !EOF);

		uint bgColor = default;
		if (handle->SColorMap is not null)
		{
			var cmap = handle->SColorMap;
			ColorCount = cmap->ColorCount;
			palette = BufferPool.Rent<uint>(256, true);

			var cspan = palette.Span;
			fixed (uint* pp = cspan)
				Swizzlers<byte>.GetSwapConverter(3, 4).ConvertLine((byte*)cmap->Colors, (byte*)pp, ColorCount * 3);

			if (options is IAnimationDecoderOptions { UseBackgroundColor: true } && !alpha && handle->SBackGroundColor < ColorCount)
				bgColor = cspan[handle->SBackGroundColor];
		}

		float pixelAspect = handle->AspectByte == default ? 1f : ((handle->AspectByte + 15) / 64f);
		animation = new(handle->SWidth, handle->SHeight, handle->ImageCount, loopCount, (int)bgColor, pixelAspect, true);

		var range = options is IMultiFrameDecoderOptions mul ? mul.FrameRange : Range.All;
		(frameOffset, frameCount) = range.GetOffsetAndLengthNoThrow(handle->ImageCount);

		Format = PixelFormat.Bgr24;
		if (alpha || handle->ImageCount > 1)
			Format = PixelFormat.Bgra32;
		else if (isGreyscale())
			Format = PixelFormat.Grey8;

		handle = ResetDecoder();
	}

	public string MimeType => ImageMimeTypes.Gif;

	public int FrameCount => frameCount;

	public ReadOnlySpan<uint> Palette => palette.Span;

	int IIccProfileSource.ProfileLength => iccpData.Length;

	public IImageFrame GetFrame(int index)
	{
		index += frameOffset;
		if ((uint)index >= (uint)(frameOffset + frameCount))
			throw new ArgumentOutOfRangeException(nameof(index), "Invalid frame index.");

		int curr = frame?.Index ?? -1;
		if (index < curr)
		{
			handle = ResetDecoder();
			curr = -1;
		}

		if (frame is { IsAtEnd: false })
			advanceFrame();

		var rec = default(GifRecordType);
		var gcb = default(GraphicsControlBlock);
		bool hasGcb = false;
		while (!EOF)
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

				while (data is not null && !EOF)
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

		if (typeof(T) == typeof(IIccProfileSource) && iccpData.Length != 0)
		{
			metadata = (T)(object)this;
			return true;
		}

		metadata = default;
		return false;
	}

	public static GifContainer? TryLoad(Stream imgStream, IDecoderOptions? options)
	{
		var stream = StreamWrapper.Wrap(imgStream);
		if (stream is null)
			ThrowHelper.ThrowOutOfMemory();

		var handle = GifFactory.CreateDecoder(stream, GifCallbacks.Read);
		if (handle is not null)
			return new GifContainer(handle, options);

		stream->Seek(0, SeekOrigin.Begin);
		StreamWrapper.Free(stream);

		return null;
	}

	public GifFileType* GetHandle()
	{
		ensureHandle();

		return handle;
	}

	public void CheckResult(int res)
	{
		if (res == GIF_OK)
			return;

		handle->Stream->ThrowIfExceptional();

		if (handle->ImageCount != 0 && handle->Stream->IsEof())
			EOF = true;
		else
			throwGifError(handle);
	}

	public GifFileType* ResetDecoder(bool keepFrame = false)
	{
		var stm = handle->Stream;
		int err;
		_ = DGifCloseFile(handle, &err);
		EOF = false;

		stm->Seek(0, SeekOrigin.Begin);
		handle = GifFactory.CreateDecoder(stm, GifCallbacks.Read);
		if (!keepFrame)
		{
			frame = null;
			return handle;
		}

		int curr = -1;
		var rec = default(GifRecordType);
		do
		{
			CheckResult(DGifGetRecordType(handle, &rec));
			if (rec == GifRecordType.EXTENSION_RECORD_TYPE)
			{
				int ext;
				byte* data;
				CheckResult(DGifGetExtension(handle, &ext, &data));
				while (data is not null && !EOF)
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
		while (rec != GifRecordType.TERMINATE_RECORD_TYPE && !EOF);

		return handle;
	}

	void IIccProfileSource.CopyProfile(Span<byte> dest) => iccpData.Span.CopyTo(dest);

	private void advanceFrame()
	{
		byte* data;
		do
			CheckResult(DGifGetCodeNext(handle, &data));
		while (data is not null && !EOF);
	}

	private bool isGreyscale()
	{
		var cmap = handle->Image.ColorMap;
		if (cmap is null)
			cmap = handle->SColorMap;

		int cc = cmap->ColorCount;
		for (int i = 0; i < cc; i++)
		{
			var clr = cmap->Colors[i];
			if (clr.Red != clr.Blue | clr.Blue != clr.Green)
				return false;
		}

		return true;
	}

	[DoesNotReturn]
	private static void throwGifError(GifFileType* handle) =>
		throw new InvalidDataException($"{nameof(Giflib)} decoder failed. {new string(GifErrorString(handle->Error))}");

	private void ensureHandle()
	{
		if (handle is null)
			ThrowHelper.ThrowObjectDisposed(nameof(GifContainer));
	}

	private void dispose(bool disposing)
	{
		if (handle is null)
			return;

		StreamWrapper.Free(handle->Stream);

		int err;
		_ = DGifCloseFile(handle, &err);
		handle = null;

		if (disposing)
		{
			palette.Dispose();
			palette = default;

			iccpData.Dispose();
			iccpData = default;

			GC.SuppressFinalize(this);
		}
	}

	public void Dispose() => dispose(true);

	~GifContainer()
	{
		ThrowHelper.ThrowIfFinalizerExceptionsEnabled(nameof(GifContainer));

		dispose(false);
	}
}

internal sealed unsafe class GifFrame : IImageFrame, IMetadataSource
{
	private readonly GifContainer container;
	private readonly AnimationFrame anifrm;
	private readonly int width, height, transIdx;
	private readonly bool interlace;
	public readonly int Index;

	private GifPixelSource? pixelSource;
	private FrameBufferSource? frameBuff;
	private RentedBuffer<byte> lineBuff;
	private RentedBuffer<uint> palette;
	private int lastRow;

	public IPixelSource PixelSource => pixelSource ??= new(this);
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
		if (hasGcb)
		{
			transIdx = gcb.TransparentColor;
			disposal = ((FrameDisposalMethod)gcb.DisposalMode).Clamp();
		}

		anifrm = new AnimationFrame(
			handle->Image.Left,
			handle->Image.Top,
			new((uint)gcb.DelayTime, 100),
			disposal,
			AlphaBlendMethod.Over,
			transIdx != NO_TRANSPARENT_COLOR
		);

		Index = idx;
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
		lineBuff = default;

		palette.Dispose();
		palette = default;
	}

	private sealed class GifPixelSource : PixelSource, IFramePixelSource, IIndexedPixelSource
	{
		private readonly int colorCount;

		public readonly GifContainer container;
		public readonly GifFrame frame;

		public override PixelFormat Format => container.Format;
		public override int Width => frame.width;
		public override int Height => frame.height;
		public IImageFrame Frame => frame;

		public ReadOnlySpan<uint> Palette
		{
			get {
				if (!frame.palette.IsEmpty)
					return frame.palette.Span[..colorCount];

				var handle = container.GetHandle();
				var cmap = handle->Image.ColorMap;
				if (cmap is null && frame.transIdx == NO_TRANSPARENT_COLOR)
					return container.Palette[..colorCount];

				frame.palette = BufferPool.Rent<uint>(256, true);
				var cspan = frame.palette.Span;
				if (cmap is not null)
					fixed (uint* pp = cspan)
						Swizzlers<byte>.GetSwapConverter(3, 4).ConvertLine((byte*)cmap->Colors, (byte*)pp, colorCount * 3);
				else
					container.Palette.CopyTo(cspan);

				if (frame.transIdx != NO_TRANSPARENT_COLOR)
					cspan[frame.transIdx] &= 0xffffffu;

				return cspan[..colorCount];
			}
		}

		public GifPixelSource(GifFrame frm)
		{
			(container, frame) = (frm.container, frm);

			var handle = container.GetHandle();
			var cmap = handle->Image.ColorMap;
			colorCount = Math.Max(cmap is not null ? cmap->ColorCount : container.ColorCount, frame.transIdx + 1);
		}

		protected override void CopyPixelsInternal(in PixelArea prc, int cbStride, int cbBufferSize, byte* pbBuffer)
		{
			var handle = container.GetHandle();
			if (handle->Image.Interlace != 0)
			{
				copyPixelsInterlaced(prc, cbStride, cbBufferSize, pbBuffer);
				return;
			}

			if (container.EOF)
				return;

			if (prc.Y < frame.lastRow)
			{
				handle = container.ResetDecoder(true);
				frame.lastRow = 0;
			}

			var linebuff = Span<byte>.Empty;
			if (prc.Width < Width)
			{
				if (frame.lineBuff.IsEmpty)
					frame.lineBuff = BufferPool.Rent<byte>(Width);

				linebuff = frame.lineBuff.Span;
			}

			fixed (byte* pbuff = linebuff)
			fixed (uint* ppal = Palette)
			{
				for (int y = frame.lastRow; y < prc.Y; y++)
				{
					container.CheckResult(DGifGetLine(handle, pbuff is null ? pbBuffer : pbuff, Width));
					frame.lastRow++;
				}

				int cbout = prc.Width * Format.ChannelCount;
				for (int y = 0; y < prc.Height; y++)
				{
					byte* pout = pbBuffer + cbStride * y;
					if (pbuff is null)
					{
						byte* pin = pout + cbout - prc.Width;
						container.CheckResult(DGifGetLine(handle, pin, Width));
						convertLine(pin, pout, ppal, prc.Width);
					}
					else
					{
						container.CheckResult(DGifGetLine(handle, pbuff, Width));
						convertLine(pbuff + prc.X, pout, ppal, prc.Width);
					}

					frame.lastRow++;
				}
			}
		}

		private void copyPixelsInterlaced(in PixelArea prc, int cbStride, int cbBufferSize, byte* pbBuffer)
		{
			var src = getFrameBuffer();
			int cbout = prc.Width * Format.ChannelCount;

			fixed (uint* ppal = Palette)
			{
				for (int y = 0; y < prc.Height; y++)
				{
					byte* pout = pbBuffer + cbStride * y;
					byte* pin = pout + cbout - prc.Width;
					src.CopyPixels(prc.Slice(y, 1), cbStride, cbBufferSize, pin);
					convertLine(pin, pout, ppal, prc.Width);
				}
			}
		}

		private FrameBufferSource getFrameBuffer()
		{
			if (frame.frameBuff is null)
			{
				var handle = container.GetHandle();

				var fbuf = new FrameBufferSource(Width, Height, PixelFormat.Indexed8);
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

		private void convertLine(byte* istart, byte* ostart, uint* pstart, nint cb)
		{
			switch (Format.ChannelCount)
			{
				case 1:
					PaletteTransform.CopyPixels1Chan(istart, ostart, pstart, cb);
					break;
				case 3:
					PaletteTransform.CopyPixels3Chan(istart, ostart, pstart, cb);
					break;
				case 4:
					PaletteTransform.CopyPixels4Chan(istart, ostart, pstart, cb);
					break;
			}
		}

		public override string ToString() => nameof(GifPixelSource);
	}
}
