// Copyright © Clinton Ingram and Contributors.  Licensed under the MIT License.

using System;
using System.IO;
using System.Buffers.Binary;
using System.Runtime.InteropServices;
using System.Runtime.CompilerServices;
using System.Diagnostics.CodeAnalysis;

using PhotoSauce.MagicScaler;
using PhotoSauce.MagicScaler.Converters;
using PhotoSauce.MagicScaler.Transforms;
using PhotoSauce.Interop.Giflib;
using static PhotoSauce.Interop.Giflib.Giflib;

namespace PhotoSauce.NativeCodecs.Giflib;

internal sealed unsafe class GifContainer : IImageContainer, IMetadataSource, IIccProfileSource
{
	private static readonly bool isWindows = RuntimeInformation.IsOSPlatform(OSPlatform.Windows);

	private readonly Stream stream;
	private readonly long streamStart;
	private readonly int frameCount, frameOffset, colorCount;
	private readonly AnimationContainer animation;
	public readonly PixelFormat Format;

	private GifFileType* handle;
	private GifFrame? frame;
	private RentedBuffer<uint> palette;
	private RentedBuffer<byte> iccpData;

	public FrameDisposalMethod LastDisposal;
	public bool EOF;

	private GifContainer(GifFileType* pinst, Stream stm, long pos, IDecoderOptions? opt)
	{
		stream = stm;
		streamStart = pos;
		handle = pinst;

		int loopCount = 0;
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

		uint bgColor = 0;
		if (handle->SColorMap is not null)
		{
			var cmap = handle->SColorMap;
			colorCount = cmap->ColorCount;
			palette = BufferPool.Rent<uint>(256, true);

			var cspan = palette.Span;
			fixed (uint* pp = cspan)
				ChannelChanger<byte>.GetSwapConverter(3, 4).ConvertLine((byte*)cmap->Colors, (byte*)pp, colorCount * 3);

			if (handle->SBackGroundColor < colorCount)
				bgColor = cspan[handle->SBackGroundColor];
		}

		float pixelAspect = handle->AspectByte == default ? 1f : ((handle->AspectByte + 15) / 64f);
		animation = new(handle->SWidth, handle->SHeight, handle->ImageCount, loopCount, (int)bgColor, pixelAspect, true);

		var range = opt is IMultiFrameDecoderOptions mul ? mul.FrameRange : Range.All;
		(frameOffset, frameCount) = range.GetOffsetAndLength(handle->ImageCount);

		Format = PixelFormat.Bgr24;
		if (alpha || handle->ImageCount > 1)
			Format = PixelFormat.Bgra32;
		else if (isGreyscale())
			Format = PixelFormat.Grey8;

		handle = ResetDecoder();
	}

	public string MimeType => ImageMimeTypes.Gif;

	public int FrameCount => frameCount;

	public ReadOnlySpan<uint> Palette => palette.Span[..colorCount];

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
		if (res == GIF_OK)
			return;

		if (handle->ImageCount != 0 && stream.Position == stream.Length)
			EOF = true;
		else
			throwGifError(handle);
	}

	public GifFileType* ResetDecoder(bool keepFrame = false)
	{
		var gch = handle->UserData;
		int err;
		_ = DGifCloseFile(handle, &err);

		RewindStream();
		handle = GifFactory.CreateDecoder((nint)gch, pfnReadCallback);
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

	public void RewindStream()
	{
		stream.Position = streamStart;
		EOF = false;
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

		palette.Dispose();
		palette = default;

		iccpData.Dispose();
		iccpData = default;

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
	[UnmanagedFunctionPointer(CallingConvention.Cdecl)] private delegate int ReadCallback(GifFileType* pinst, byte* buff, int cb);
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
		lineBuff = default;

		palette.Dispose();
		palette = default;
	}

	private sealed class GifPixelSource : PixelSource, IFramePixelSource, IIndexedPixelSource
	{
		private int colorCount;

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
					return container.Palette;

				colorCount = cmap is not null ? cmap->ColorCount : container.Palette.Length;
				frame.palette = BufferPool.Rent<uint>(256, true);

				var cspan = frame.palette.Span;
				if (cmap is not null)
					fixed (uint* pp = cspan)
						ChannelChanger<byte>.GetSwapConverter(3, 4).ConvertLine((byte*)cmap->Colors, (byte*)pp, colorCount * 3);
				else
					container.Palette.CopyTo(cspan);

				int trn = frame.transIdx;
				if (trn >= colorCount)
					trn = 0;

				if (trn != NO_TRANSPARENT_COLOR)
					cspan[trn] &= 0xffffffu;

				return cspan[..colorCount];
			}
		}

		public GifPixelSource(GifFrame frm) => (container, frame) = (frm.container, frm);

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
