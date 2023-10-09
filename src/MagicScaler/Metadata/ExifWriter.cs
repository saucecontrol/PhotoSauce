// Copyright © Clinton Ingram and Contributors.  Licensed under the MIT License.

using System;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace PhotoSauce.MagicScaler;

[StructLayout(LayoutKind.Auto)]
internal unsafe ref struct ExifWriter
{
	private const int nextIfdLength = sizeof(uint);
	private const uint nextIfd = default;

	private readonly int dataOffset;
	private BufferPool.LocalBuffer<byte> buffer;
	private SpanBufferWriter tagWriter;
	private SpanBufferWriter dataWriter;
	private int length;

	private readonly int dataPos => dataOffset + dataWriter.Position;
	public readonly int Length => length;

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
			if (sizeof(T) == sizeof(byte))
				tagWriter.Write(default(Triple));
			else if (sizeof(T) == sizeof(ushort))
				tagWriter.Write(default(ushort));
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

	public readonly ReadOnlySpan<byte> Span => buffer.Span[..length];

	public void Dispose()
	{
		buffer.Dispose();
		buffer = default;

		tagWriter = dataWriter = default;
		length = default;
	}
}
