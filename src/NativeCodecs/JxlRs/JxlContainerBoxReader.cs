// Copyright © Clinton Ingram and Contributors
// SPDX-License-Identifier: MIT

using System;
#if NET8_0_OR_GREATER
using System.Buffers;
#endif
using System.Buffers.Binary;
using System.IO;
#if NET8_0_OR_GREATER
using System.IO.Compression;
#endif

namespace PhotoSauce.NativeCodecs.JxlRs;

internal readonly struct JxlExifLocation
{
	public JxlExifLocation(long offset, int length)
		: this(offset, length, 0, 0)
	{ }

	public JxlExifLocation(long offset, int length, long compressedLength, int skipLength)
	{
		Offset = offset;
		Length = length;
		CompressedLength = compressedLength;
		SkipLength = skipLength;
	}

	public long Offset { get; }
	public int Length { get; }
	public long CompressedLength { get; }
	public int SkipLength { get; }
	public bool IsCompressed => CompressedLength != 0;
	public bool IsEmpty => Length == 0;
}

internal static class JxlContainerBoxReader
{
	private const uint BoxTypeJxl = 0x4a584c20;
	private const uint BoxTypeExif = 0x45786966;
	private const uint BoxTypeBrob = 0x62726f62;
	private const uint ContainerSignature = 0x0d0a870a;
	private const int BoxHeaderLength = sizeof(uint) * 2;
	private const int ExtendedBoxHeaderLength = BoxHeaderLength + sizeof(ulong);
#if NET8_0_OR_GREATER
	internal const int MaxCompressedExifBoxLength = 8 * 1024 * 1024;
	internal const int MaxDecompressedExifBoxLength = 4 * 1024 * 1024;
	private const int BrotliBufferLength = 4096;
#endif

	public static bool TryFindExif(Stream stream, long start, out JxlExifLocation location)
	{
		if (stream is null)
			throw new ArgumentNullException(nameof(stream));
		if (!stream.CanSeek)
			throw new ArgumentException("The JPEG XL stream must be seekable.", nameof(stream));

		location = default;
		long originalPosition = stream.Position;
		try
		{
			long end = stream.Length;
			if (start < 0 || start > end || end - start < 12)
				return false;

			Span<byte> header = stackalloc byte[ExtendedBoxHeaderLength];
			stream.Position = start;
			if (!tryReadExactly(stream, header[..12]) ||
				BinaryPrimitives.ReadUInt32BigEndian(header) != 12 ||
				BinaryPrimitives.ReadUInt32BigEndian(header[4..]) != BoxTypeJxl ||
				BinaryPrimitives.ReadUInt32BigEndian(header[8..]) != ContainerSignature)
			{
				return false;
			}

			long position = start + 12;
			while (position <= end && end - position >= BoxHeaderLength)
			{
				stream.Position = position;
				if (!tryReadExactly(stream, header[..BoxHeaderLength]))
					return false;

				uint shortLength = BinaryPrimitives.ReadUInt32BigEndian(header);
				uint boxType = BinaryPrimitives.ReadUInt32BigEndian(header[4..]);
				int headerLength = BoxHeaderLength;
				ulong boxLength;
				if (shortLength == 1)
				{
					if (end - position < ExtendedBoxHeaderLength ||
						!tryReadExactly(stream, header[BoxHeaderLength..ExtendedBoxHeaderLength]))
					{
						return false;
					}

					headerLength = ExtendedBoxHeaderLength;
					boxLength = BinaryPrimitives.ReadUInt64BigEndian(header[BoxHeaderLength..]);
				}
				else if (shortLength == 0)
				{
					boxLength = (ulong)(end - position);
				}
				else
				{
					boxLength = shortLength;
				}

				ulong remaining = (ulong)(end - position);
				if (boxLength < (uint)headerLength || boxLength > remaining)
					return false;

				long payloadOffset = position + headerLength;
				ulong payloadLength = boxLength - (uint)headerLength;
				if (boxType == BoxTypeExif)
				{
					if (payloadLength < sizeof(uint))
						return false;

					stream.Position = payloadOffset;
					if (!tryReadExactly(stream, header[..sizeof(uint)]))
						return false;

					ulong tiffOffset = BinaryPrimitives.ReadUInt32BigEndian(header);
					ulong exifOffset = sizeof(uint) + tiffOffset;
					if (exifOffset >= payloadLength)
						return false;

					ulong exifLength = payloadLength - exifOffset;
					if (exifLength > int.MaxValue)
						return false;

					location = new JxlExifLocation(
						checked(payloadOffset + (long)exifOffset),
						(int)exifLength
					);
					return true;
				}

#if NET8_0_OR_GREATER
				if (boxType == BoxTypeBrob && payloadLength > sizeof(uint))
				{
					stream.Position = payloadOffset;
					if (!tryReadExactly(stream, header[..sizeof(uint)]))
						return false;

					uint originalBoxType = BinaryPrimitives.ReadUInt32BigEndian(header);
					if (originalBoxType == BoxTypeExif)
					{
						ulong compressedLength = payloadLength - sizeof(uint);
						if (compressedLength > MaxCompressedExifBoxLength ||
							!tryInspectCompressedExif(stream, payloadOffset + sizeof(uint), (long)compressedLength, out int skipLength, out int exifLength))
						{
							return false;
						}

						location = new JxlExifLocation(
							payloadOffset + sizeof(uint),
							exifLength,
							(long)compressedLength,
							skipLength
						);
						return true;
					}
				}
#endif

				if (shortLength == 0)
					break;

				position = checked(position + (long)boxLength);
			}

			return false;
		}
		finally
		{
			stream.Position = originalPosition;
		}
	}

	public static void CopyExif(Stream stream, in JxlExifLocation location, Span<byte> destination)
	{
		if (stream is null)
			throw new ArgumentNullException(nameof(stream));
		if (destination.Length < location.Length)
			throw new ArgumentException("The destination is too small.", nameof(destination));
		if (location.Offset < 0 || location.IsEmpty)
			throw new ArgumentException("The EXIF location is invalid.", nameof(location));

		destination = destination[..location.Length];
		long originalPosition = stream.Position;
		try
		{
#if NET8_0_OR_GREATER
			if (location.IsCompressed)
			{
				if (!tryCopyCompressedExif(stream, location, destination))
					throw new InvalidDataException("The JPEG XL compressed EXIF box is invalid, truncated, or too large.");

				return;
			}
#else
			if (location.IsCompressed)
				throw new NotSupportedException("Brotli-compressed JPEG XL metadata requires .NET 8 or later.");
#endif

			if (stream is MemoryStream memory && memory.TryGetBuffer(out var segment))
			{
				long end = checked(location.Offset + location.Length);
				if (end > memory.Length)
					throw new EndOfStreamException("The JPEG XL EXIF box is truncated.");

				int offset = checked(segment.Offset + (int)location.Offset);
				new ReadOnlySpan<byte>(segment.Array, offset, location.Length).CopyTo(destination);
				return;
			}

			stream.Position = location.Offset;
			if (!tryReadExactly(stream, destination))
				throw new EndOfStreamException("The JPEG XL EXIF box is truncated.");
		}
		finally
		{
			stream.Position = originalPosition;
		}
	}

#if NET8_0_OR_GREATER
	private static bool tryInspectCompressedExif(Stream stream, long offset, long compressedLength, out int skipLength, out int exifLength)
	{
		skipLength = 0;
		exifLength = 0;
		if (compressedLength <= 0 || compressedLength > MaxCompressedExifBoxLength)
			return false;

		stream.Position = offset;
		Span<byte> inputBuffer = stackalloc byte[BrotliBufferLength];
		Span<byte> outputBuffer = stackalloc byte[BrotliBufferLength];
		Span<byte> prefix = stackalloc byte[sizeof(uint)];
		var input = new BrotliBoxInput(stream, compressedLength, inputBuffer);
		if (!input.Refill())
			return false;

		var decoder = default(BrotliDecoder);
		try
		{
			int decodedLength = 0;
			int prefixLength = 0;
			while (true)
			{
				int remainingLimit = MaxDecompressedExifBoxLength - decodedLength;
				Span<byte> output = outputBuffer[..Math.Max(1, Math.Min(outputBuffer.Length, remainingLimit))];
				OperationStatus status = input.Decompress(ref decoder, output, out int written);
				if (written > remainingLimit)
					return false;

				int prefixBytes = Math.Min(prefix.Length - prefixLength, written);
				if (prefixBytes > 0)
				{
					outputBuffer[..prefixBytes].CopyTo(prefix[prefixLength..]);
					prefixLength += prefixBytes;
				}

				decodedLength += written;
				switch (status)
				{
					case OperationStatus.Done:
						if (input.HasRemainingData || prefixLength != prefix.Length)
							return false;

						uint tiffOffset = BinaryPrimitives.ReadUInt32BigEndian(prefix);
						ulong exifOffset = sizeof(uint) + (ulong)tiffOffset;
						if (exifOffset >= (ulong)decodedLength)
							return false;

						skipLength = (int)exifOffset;
						exifLength = decodedLength - skipLength;
						return true;

					case OperationStatus.DestinationTooSmall:
						break;

					case OperationStatus.NeedMoreData:
						if (!input.Refill())
							return false;
						break;

					default:
						return false;
				}
			}
		}
		finally
		{
			decoder.Dispose();
		}
	}

	private static bool tryCopyCompressedExif(Stream stream, in JxlExifLocation location, Span<byte> destination)
	{
		if (location.CompressedLength <= 0 || location.CompressedLength > MaxCompressedExifBoxLength ||
			location.SkipLength < sizeof(uint) || location.Length > MaxDecompressedExifBoxLength - location.SkipLength)
		{
			return false;
		}

		stream.Position = location.Offset;
		Span<byte> inputBuffer = stackalloc byte[BrotliBufferLength];
		Span<byte> discardBuffer = stackalloc byte[BrotliBufferLength];
		var input = new BrotliBoxInput(stream, location.CompressedLength, inputBuffer);
		if (!input.Refill())
			return false;

		var decoder = default(BrotliDecoder);
		try
		{
			int skipRemaining = location.SkipLength;
			int destinationOffset = 0;
			int decodedLength = 0;
			while (true)
			{
				bool discarding = skipRemaining > 0;
				bool probing = !discarding && destinationOffset == destination.Length;
				OperationStatus status;
				int written;
				if (discarding)
					status = input.Decompress(ref decoder, discardBuffer[..Math.Min(discardBuffer.Length, skipRemaining)], out written);
				else if (!probing)
					status = input.Decompress(ref decoder, destination[destinationOffset..], out written);
				else
					status = input.Decompress(ref decoder, discardBuffer[..1], out written);

				if (written > MaxDecompressedExifBoxLength - decodedLength)
					return false;

				decodedLength += written;
				if (discarding)
					skipRemaining -= written;
				else if (probing)
				{
					if (written != 0)
						return false;
				}
				else
				{
					destinationOffset += written;
				}

				switch (status)
				{
					case OperationStatus.Done:
						return !input.HasRemainingData &&
							skipRemaining == 0 &&
							destinationOffset == destination.Length &&
							decodedLength == location.SkipLength + location.Length;

					case OperationStatus.DestinationTooSmall:
						break;

					case OperationStatus.NeedMoreData:
						if (!input.Refill())
							return false;
						break;

					default:
						return false;
				}
			}
		}
		finally
		{
			decoder.Dispose();
		}
	}

	private ref struct BrotliBoxInput
	{
		private readonly Stream stream;
		private readonly Span<byte> buffer;
		private long remaining;
		private int start;
		private int end;

		public BrotliBoxInput(Stream stream, long length, Span<byte> buffer)
		{
			this.stream = stream;
			this.buffer = buffer;
			remaining = length;
			start = 0;
			end = 0;
		}

		public bool HasRemainingData => remaining != 0 || start != end;

		public bool Refill()
		{
			int retained = end - start;
			if (retained > 0)
				buffer.Slice(start, retained).CopyTo(buffer);

			start = 0;
			end = retained;
			if (remaining == 0 || end == buffer.Length)
				return false;

			int requested = (int)Math.Min(buffer.Length - end, remaining);
			int read = stream.Read(buffer.Slice(end, requested));
			if (read <= 0)
				return false;

			end += read;
			remaining -= read;
			return true;
		}

		public OperationStatus Decompress(ref BrotliDecoder decoder, Span<byte> output, out int written)
		{
			OperationStatus status = decoder.Decompress(buffer.Slice(start, end - start), output, out int consumed, out written);
			start += consumed;
			return status;
		}
	}
#endif

	private static bool tryReadExactly(Stream stream, Span<byte> destination)
	{
#if NETFRAMEWORK
		for (int i = 0; i < destination.Length; i++)
		{
			int value = stream.ReadByte();
			if (value < 0)
				return false;

			destination[i] = (byte)value;
		}
#else
		while (!destination.IsEmpty)
		{
			int read = stream.Read(destination);
			if (read == 0)
				return false;

			destination = destination[read..];
		}
#endif
		return true;
	}
}
