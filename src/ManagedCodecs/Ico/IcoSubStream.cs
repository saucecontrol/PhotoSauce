// Copyright © Clinton Ingram and Contributors
// SPDX-License-Identifier: MIT

using System;
using System.IO;
using System.Runtime.ExceptionServices;

namespace PhotoSauce.ManagedCodecs.Ico;

// A read-only window over part of a seekable stream.  It keeps its own position and seeks the
// underlying stream before every read, so a lazy decoder for an embedded image can share the
// stream with other frames of the same container.
internal sealed class IcoSubStream : Stream
{
	private readonly Stream stream;
	private readonly long start, length;
	private long position;

	public IcoSubStream(Stream baseStream, long offset, long count) => (stream, start, length) = (baseStream, offset, count);

	// The first exception the underlying stream threw.  Some codecs (libpng outside Windows) swallow stream
	// exceptions and only report that decoding failed, so this is how the original error is recovered.
	public ExceptionDispatchInfo? Fault { get; private set; }

	public override bool CanRead => true;
	public override bool CanSeek => true;
	public override bool CanWrite => false;
	public override long Length => length;

	public override long Position
	{
		get => position;
		set => position = value >= 0 ? value : throw new ArgumentOutOfRangeException(nameof(value));
	}

	public override int Read(byte[] buffer, int offset, int count)
	{
		try
		{
			int cb = prepareRead(count);
			if (cb == 0)
				return 0;

			int rb = stream.Read(buffer, offset, cb);
			position += rb;

			return rb;
		}
		catch (Exception ex)
		{
			Fault ??= ExceptionDispatchInfo.Capture(ex);
			throw;
		}
	}

#if NETSTANDARD2_1_OR_GREATER || NETCOREAPP2_1_OR_GREATER
	public override int Read(Span<byte> buffer)
	{
		try
		{
			int cb = prepareRead(buffer.Length);
			if (cb == 0)
				return 0;

			int rb = stream.Read(buffer[..cb]);
			position += rb;

			return rb;
		}
		catch (Exception ex)
		{
			Fault ??= ExceptionDispatchInfo.Capture(ex);
			throw;
		}
	}
#endif

	public override int ReadByte()
	{
		try
		{
			if (prepareRead(1) == 0)
				return -1;

			int b = stream.ReadByte();
			if (b >= 0)
				position++;

			return b;
		}
		catch (Exception ex)
		{
			Fault ??= ExceptionDispatchInfo.Capture(ex);
			throw;
		}
	}

	public override long Seek(long offset, SeekOrigin origin)
	{
		long pos = origin switch {
			SeekOrigin.Begin   => offset,
			SeekOrigin.Current => position + offset,
			SeekOrigin.End     => length + offset,
			_                  => throw new ArgumentOutOfRangeException(nameof(origin))
		};

		if (pos < 0)
			throw new IOException("An attempt was made to move the position before the beginning of the stream.");

		return position = pos;
	}

	public override void Flush() { }

	public override void SetLength(long value) => throw new NotSupportedException();

	public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();

	private int prepareRead(int count)
	{
		long rem = length - position;
		if (rem <= 0 || count <= 0)
			return 0;

		stream.Position = start + position;

		return (int)Math.Min(count, rem);
	}
}
