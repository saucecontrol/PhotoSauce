// Copyright © Clinton Ingram and Contributors.  Licensed under the MIT License.

using System;
using System.Diagnostics;

namespace PhotoSauce.MagicScaler;

internal unsafe ref struct ExifWriter
{
	const int nextIfdLength = sizeof(uint);
	const uint nextIfd = default;

	BufferPool.LocalBuffer<byte> buffer;
	SpanBufferWriter tagWriter;
	SpanBufferWriter dataWriter;
	readonly int dataOffset;
	int length;

	private int dataPos => dataOffset + dataWriter.Position;
	public int Length => length;

	private ExifWriter(int tagCount, int dataLength)
	{
		int len = ExifConstants.ExifHeadLength + tagCount * ExifConstants.MinTagLength + nextIfdLength + dataLength;
		buffer = BufferPool.RentLocal<byte>(len);

		var writer = buffer.Span.AsWriter(..ExifConstants.ExifHeadLength);
		writer.Write(BitConverter.IsLittleEndian ? ExifConstants.MarkerII : ExifConstants.MarkerMM);
		writer.Write(ExifConstants.TiffHeadLength);
		writer.Write((ushort)tagCount);

		dataOffset = writer.Position + tagCount * ExifConstants.MinTagLength + nextIfdLength;
		tagWriter = buffer.Span.AsWriter(writer.Position..dataOffset);
		dataWriter = buffer.Span.AsWriter(dataOffset..);
	}

	public static ExifWriter Create(int tagCount, int dataLength) => new(tagCount, dataLength);

	public void Write<T>(ushort id, ExifType type, T val) where T : unmanaged
	{
		Debug.Assert(type.GetElementSize() == sizeof(T));

		tagWriter.Write(id);
		tagWriter.Write(type);
		tagWriter.Write(1);

		if (sizeof(T) <= sizeof(uint))
		{
			tagWriter.Write(val);
			return;
		}

		tagWriter.Write(dataPos);
		dataWriter.Write(val);
	}

	public void Write<T>(ushort id, ExifType type, ReadOnlySpan<T> val) where T : unmanaged
	{
		Debug.Assert(type.GetElementSize() == sizeof(T));

		tagWriter.Write(id);
		tagWriter.Write(type);
		tagWriter.Write(val.Length);

		if (sizeof(T) * val.Length <= sizeof(uint))
		{
			tagWriter.Write(val);
			return;
		}

		tagWriter.Write(dataPos);
		dataWriter.Write(val);
	}

	public void Finish()
	{
		tagWriter.Write(nextIfd);
		length = dataPos;
	}

	public void CopyTo(Span<byte> dest) => buffer.Span[..length].CopyTo(dest);

	public void Dispose()
	{
		buffer.Dispose();
		buffer = default;

		tagWriter = dataWriter = default;
		length = default;
	}
}
