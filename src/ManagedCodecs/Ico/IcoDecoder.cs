// Copyright © Clinton Ingram and Contributors
// SPDX-License-Identifier: MIT

using System;
using System.IO;
using System.Buffers.Binary;
using System.Diagnostics.CodeAnalysis;

using PhotoSauce.MagicScaler;

namespace PhotoSauce.ManagedCodecs.Ico;

internal readonly record struct IcoDirEntry(int Width, int Height, uint Offset);

internal sealed class IcoContainer : IImageContainer
{
	private const int headerSize = 6;
	private const int entrySize = 16;

	private readonly Stream stream;
	private readonly long start;
	private readonly IcoDirEntry[] entries;
	private bool disposed;

	private IcoContainer(Stream stm, long pos, IcoDirEntry[] ents) => (stream, start, entries) = (stm, pos, ents);

	public string MimeType => IcoFactory.MimeType;

	public int FrameCount => entries.Length;

	public IImageFrame GetFrame(int index)
	{
		if (disposed)
			ThrowHelper.ThrowObjectDisposed(nameof(IcoContainer));

		if ((uint)index >= (uint)entries.Length)
			throw new ArgumentOutOfRangeException(nameof(index), "Invalid frame index.");

		return new IcoFrame(stream, start, entries[index]);
	}

	public static IcoContainer? TryLoad(Stream imgStream, IDecoderOptions? options)
	{
		long start = imgStream.Position;

		var header = (Span<byte>)stackalloc byte[headerSize];
		if (imgStream.TryFillBuffer(header) != header.Length || BinaryPrimitives.ReadUInt16LittleEndian(header) != 0 || BinaryPrimitives.ReadUInt16LittleEndian(header[2..]) is not (1 or 2))
		{
			imgStream.Position = start;
			return null;
		}

		// The 4-byte signature is weak: an uncompressed true-color TGA with no ID field and no color map also starts
		// 00 00 02 00.  Its color map specification is all zero, so the image count (TGA bytes 4-5) reads as 0.
		// Decline such input so other decoders can try it.
		int declared = BinaryPrimitives.ReadUInt16LittleEndian(header[4..]);
		if (declared == 0)
		{
			imgStream.Position = start;
			return null;
		}

		long available = Math.Max(imgStream.Length - imgStream.Position, 0);
		using var dir = BufferPool.RentLocal<byte>((int)Math.Min(declared, available / entrySize) * entrySize);
		var span = dir.Span;

		// A truncated directory keeps only the entries that were read in full.
		int count = imgStream.TryFillBuffer(span) / entrySize;
		if (count == 0)
			throw new InvalidDataException("The ICO directory is truncated.");

		// The first image must start after the directory.
		if (BinaryPrimitives.ReadUInt32LittleEndian(span[12..]) < headerSize + declared * entrySize)
		{
			imgStream.Position = start;
			return null;
		}

		var range = options is IMultiFrameDecoderOptions mul ? mul.FrameRange : Range.All;
		(int first, int length) = range.GetOffsetAndLengthNoThrow(count);

		var entries = new IcoDirEntry[length];
		for (int i = 0; i < entries.Length; i++)
		{
			// Only the size and data offset are used.  The other fields (color count, planes, bit depth, data size)
			// are unreliable in real files; the bitmap header or embedded PNG is the source of truth.
			var entry = span.Slice((first + i) * entrySize, entrySize);
			int width = entry[0] == 0 ? 256 : entry[0];
			int height = entry[1] == 0 ? 256 : entry[1];

			entries[i] = new(width, height, BinaryPrimitives.ReadUInt32LittleEndian(entry[12..]));
		}

		return new IcoContainer(imgStream, start, entries);
	}

	public void Dispose() => disposed = true;
}

internal sealed class IcoFrame : IImageFrame, IMetadataSource
{
	// The core decoder sniff requires at least this many bytes.
	private const int minPngLength = sizeof(ulong) * 2;

	private static ReadOnlySpan<byte> pngSignature => [ 0x89, 0x50, 0x4e, 0x47, 0x0d, 0x0a, 0x1a, 0x0a ];

	private readonly IImageContainer? pngContainer;
	private readonly IImageFrame? pngFrame;
	private readonly BmpPixelSource? bmpSource;
	private bool disposed;

	public IPixelSource PixelSource { get; }

	public IcoFrame(Stream stream, long start, in IcoDirEntry entry)
	{
		long offset = start + entry.Offset;
		long length = stream.Length - offset;

		var sig = (Span<byte>)stackalloc byte[pngSignature.Length];
		int cb = 0;
		if (length > 0)
		{
			stream.Position = offset;
			cb = stream.TryFillBuffer(sig);
		}

		if (cb != 0 && sig[..cb].SequenceEqual(pngSignature[..cb]))
		{
			// A PNG cut off before the decoder can identify it is treated like any other missing frame data.
			if (cb == sig.Length && length >= minPngLength && loadPng(stream, offset, length) is (var cnt, var frm))
			{
				(pngContainer, pngFrame) = (cnt, frm);
				PixelSource = frm.PixelSource;
				return;
			}
		}
		else if (cb >= sizeof(uint))
		{
			uint hdrSize = BinaryPrimitives.ReadUInt32LittleEndian(sig);
			if (!BmpPixelSource.IsValidHeaderSize(hdrSize))
				throw new InvalidDataException($"ICO frame data has an unrecognized header (0x{hdrSize:x8}).");

			PixelSource = bmpSource = BmpPixelSource.Create(stream, offset, entry.Width, entry.Height);
			return;
		}

		// Not enough data remains in the stream to tell what the frame contains.
		PixelSource = bmpSource = BmpPixelSource.CreateEmpty(entry.Width, entry.Height);
	}

	// The size in the directory entry is unreliable, so the PNG may run to the end of the file.  The PNG decoder
	// stops at IEND, so the extra length is harmless.  Returns null for a truncated PNG the codec can't open.
	private static (IImageContainer Container, IImageFrame Frame)? loadPng(Stream stream, long offset, long length)
	{
		var sub = new IcoSubStream(stream, offset, length);
		IImageContainer? cnt = null;
		try
		{
			cnt = CodecManager.GetDecoderForStream(sub);
			return (cnt, cnt.GetFrame(0));
		}
		catch (Exception ex) when (isDataError(ex))
		{
			// A codec may swallow a stream exception and report only that decoding failed.  The stream error is
			// not about the data, so it propagates as it is.  Read it before disposing the container, whose cleanup
			// may read the stream again.
			var fault = sub.Fault;
			cnt?.Dispose();
			fault?.Throw();

			// A PNG that ends before its IEND chunk is missing data, like a truncated bitmap.
			if (isTruncated(stream, offset, length))
				return null;

			// Codecs report a damaged PNG in different ways (the core's "format not supported" when none accepts it,
			// libpng's InvalidOperationException, WIC's COMException), so report one exception type.
			throw new InvalidDataException("ICO frame contains PNG data that could not be decoded.", ex);
		}

		// Failures of the stream, the runtime or the deployment (a missing native library, or a missing type or
		// member from mismatched assemblies) are not about the data.  A codec's own version check is not caught here:
		// libpng reports it as NotSupportedException, which a codec may also use for an image it can't handle.
		static bool isDataError(Exception ex) => ex is not (
			IOException or ObjectDisposedException or OutOfMemoryException or
			TypeLoadException or MissingMemberException or BadImageFormatException or TypeInitializationException
		);
	}

	// Walks the PNG chunk headers.  The PNG is truncated if every chunk header is well-formed and the stream ends
	// before IEND.  A malformed header (a first chunk other than a 13-byte IHDR, a length over the PNG limit, or a
	// type that isn't four letters) means damage, which is reported rather than hidden as missing data.
	private static bool isTruncated(Stream stream, long offset, long length)
	{
		const int chunkHeaderSize = 8, chunkCrcSize = 4, ihdrSize = 13;

		// A stream error here propagates.  Outside Windows, libpng swallows stream exceptions and only reports a
		// failed open, so this read is where a failing stream shows up.
		var chunk = (Span<byte>)stackalloc byte[chunkHeaderSize];
		long pos = pngSignature.Length;
		while (pos + chunkHeaderSize <= length)
		{
			stream.Position = offset + pos;
			if (stream.TryFillBuffer(chunk) != chunk.Length)
				return true;

			uint size = BinaryPrimitives.ReadUInt32BigEndian(chunk);
			var type = chunk[4..];
			if (size > int.MaxValue || !isChunkType(type))
				return false;

			if (pos == pngSignature.Length && (!type.SequenceEqual("IHDR"u8) || size != ihdrSize))
				return false;

			if (type.SequenceEqual("IEND"u8))
				return false;

			pos += chunkHeaderSize + (long)size + chunkCrcSize;
		}

		return true;

		static bool isChunkType(ReadOnlySpan<byte> type)
		{
			foreach (byte b in type)
				if (b is not ((>= (byte)'A' and <= (byte)'Z') or (>= (byte)'a' and <= (byte)'z')))
					return false;

			return true;
		}
	}

	public bool TryGetMetadata<T>([NotNullWhen(true)] out T? metadata) where T : IMetadata
	{
		// An embedded PNG may carry a color profile, but its animation state, if any, does not apply to the icon.
		if (pngFrame is IMetadataSource meta && typeof(T) != typeof(AnimationFrame) && typeof(T) != typeof(AnimationContainer))
			return meta.TryGetMetadata(out metadata);

		metadata = default;
		return false;
	}

	public void Dispose()
	{
		if (disposed)
			return;

		disposed = true;
		pngFrame?.Dispose();
		pngContainer?.Dispose();
		bmpSource?.Dispose();
	}
}
