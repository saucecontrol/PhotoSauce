// Copyright © Clinton Ingram and Contributors.  Licensed under the MIT License.

// Inspired by https://github.com/dotnet/runtime/blob/release/6.0/src/libraries/System.Private.CoreLib/src/System/IO/Strategies/BufferedFileStreamStrategy.cs
// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See third-party-notices in the repository root for more information.

using System;
using System.IO;
using System.Buffers;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;

namespace PhotoSauce.MagicScaler;

internal sealed class PoolBufferedStream : Stream
{
	const int bufflen = 1 << 14;

	private readonly Stream backingStream;
	private readonly bool ownStream;

	private byte[]? buffer;

	private long stmlen, stmpos;
	private int writepos, readpos, readlen;

	[DoesNotReturn]
	private static byte[] throwObjectDisposed() => throw new ObjectDisposedException(nameof(PoolBufferedStream));

	public static Stream? WrapIfFile(Stream stm) => stm is FileStream fs ? new PoolBufferedStream(fs) : null;

	public PoolBufferedStream(Stream stm, bool own = false)
	{
		if (!stm.CanSeek) throw new NotSupportedException("Stream must support Seek.");

		backingStream = stm;
		ownStream = own;

		stmlen = stm.Length;
		stmpos = stm.Position;

		buffer = ArrayPool<byte>.Shared.Rent(bufflen);
	}

	public override bool CanRead => backingStream.CanRead;
	public override bool CanWrite => backingStream.CanWrite;
	public override bool CanSeek => true;

	public override long Length => Math.Max(stmlen, stmpos + (uint)writepos);

	public override long Position
	{
		get => stmpos - (uint)(readlen - readpos) + (uint)writepos;
		set => Seek(value, SeekOrigin.Begin);
	}

	private void flushRead()
	{
		if (readpos != readlen)
			stmpos = backingStream.Seek(readpos - readlen, SeekOrigin.Current);

		readpos = readlen = 0;
	}

	private void flushWrite()
	{
		backingStream.Write(buffer!, 0, writepos);
		stmlen = Length;
		stmpos = Position;
		writepos = 0;
	}

	public override void SetLength(long length)
	{
		flushWrite();

		backingStream.SetLength(length);
		stmlen = length;
		stmpos = Math.Min(stmpos, length);
	}

	public override long Seek(long offset, SeekOrigin origin)
	{
		if (buffer is null) throwObjectDisposed();

		if (writepos != 0)
		{
			flushWrite();
			return stmpos = backingStream.Seek(offset, origin);
		}

		long newpos = origin switch {
			SeekOrigin.Current => Position + offset,
			SeekOrigin.End     => Length + offset,
			_                  => offset
		};

		readpos = (int)(newpos - (stmpos - (uint)readlen));
		if ((uint)readpos > (uint)readlen)
		{
			readpos = readlen = 0;
			return stmpos = backingStream.Seek(newpos, SeekOrigin.Begin);
		}

		return newpos;
	}

	public override void Flush()
	{
		if (writepos != 0)
			flushWrite();
		else if (readpos < readlen)
			flushRead();

		backingStream.Flush();
	}

	public override int Read(byte[] buffer, int offset, int count) => readSpan(buffer.AsSpan(offset, count), new ArraySegment<byte>(buffer, offset, count));

#if BUILTIN_SPAN
	override
#endif
	public int Read(Span<byte> buffer) => readSpan(buffer, default);

	private int readSpan(Span<byte> dest, ArraySegment<byte> array)
	{
		byte[] lbuf = buffer ?? throwObjectDisposed();

		int buffrem = readlen - readpos;
		if (buffrem == 0)
		{
			if (writepos != 0)
				flushWrite();

			if (dest.Length >= bufflen)
			{
				int read = array.Array is not null
					? backingStream.Read(array.Array, array.Offset, array.Count)
					: backingStream.Read(dest);

				readpos = readlen = 0;
				stmpos += (uint)read;
				return read;
			}

			buffrem = backingStream.Read(lbuf, 0, bufflen);

			readpos = 0;
			readlen = buffrem;
			stmpos += (uint)buffrem;
		}

		if (buffrem > dest.Length)
			buffrem = dest.Length;

		if (buffrem != 0)
		{
			lbuf.AsSpan(readpos, buffrem).CopyTo(dest);
			readpos += buffrem;
		}

		return buffrem;
	}

	public override int ReadByte()
	{
		if (readpos != readlen)
			return Unsafe.Add(ref buffer!.GetDataRef(), readpos++);

		return readByteSlow();

		[MethodImpl(MethodImplOptions.NoInlining)]
		int readByteSlow()
		{
			byte[] lbuf = buffer ?? throwObjectDisposed();

			if (writepos != 0)
				flushWrite();

			readlen = backingStream.Read(lbuf, 0, bufflen);
			if (readlen == 0)
			{
				readpos = 0;
				return -1;
			}

			stmpos += (uint)readlen;
			readpos = 1;
			return lbuf.GetDataRef();
		}
	}

	public override void Write(byte[] buffer, int offset, int count) => writeSpan(buffer.AsSpan(offset, count), new ArraySegment<byte>(buffer, offset, count));

#if BUILTIN_SPAN
	override
#endif
	public void Write(ReadOnlySpan<byte> buffer) => writeSpan(buffer, default);

	private void writeSpan(ReadOnlySpan<byte> source, ArraySegment<byte> array)
	{
		byte[] lbuf = buffer ?? throwObjectDisposed();

		if (writepos == 0)
		{
			flushRead();
		}
		else
		{
			int buffrem = bufflen - writepos;
			if (buffrem != 0)
			{
				if (source.Length <= buffrem)
					goto WriteToBuffer;

				source[..buffrem].CopyTo(lbuf.AsSpan(writepos));
				writepos += buffrem;

				source = source[buffrem..];
				if (array.Array is not null)
					array = array.Slice(buffrem);
			}

			flushWrite();
		}

		if (source.Length >= bufflen)
		{
			if (array.Array is not null)
				backingStream.Write(array.Array, array.Offset, array.Count);
			else
				backingStream.Write(source);

			stmpos += (uint)source.Length;
			if (stmlen < stmpos)
				stmlen = stmpos;

			return;
		}

	WriteToBuffer:
		if (!source.IsEmpty)
		{
			source.CopyTo(lbuf.AsSpan(writepos));
			writepos += source.Length;
		}
	}

	public override void WriteByte(byte value)
	{
		if (writepos != 0 && writepos < bufflen - 1)
		{
			Unsafe.Add(ref buffer!.GetDataRef(), writepos++) = value;
			return;
		}

		writeByteSlow(value);

		[MethodImpl(MethodImplOptions.NoInlining)]
		void writeByteSlow(byte value)
		{
			byte[] lbuf = buffer ?? throwObjectDisposed();

			if (writepos != 0)
				flushWrite();
			else
				flushRead();

			writepos = 1;
			lbuf.GetDataRef() = value;
		}
	}

	protected override void Dispose(bool disposing)
	{
		if (buffer is null)
			return;

		Flush();

		ArrayPool<byte>.Shared.TryReturn(buffer);
		buffer = default;

		writepos = readpos = readlen = 0;

		if (ownStream)
			backingStream.Dispose();

		base.Dispose(disposing);
	}
}
