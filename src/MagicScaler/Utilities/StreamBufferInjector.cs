// Copyright © Clinton Ingram and Contributors.  Licensed under the MIT License.

using System;
using System.IO;
using System.Buffers;
using System.Reflection;
using System.Diagnostics;

namespace PhotoSauce.MagicScaler
{
	internal struct StreamBufferInjector : IDisposable
	{
		private struct StreamLayout
		{
			public bool HasKnownLayout;
			public FieldInfo BufferInfo;
			public nint BufferLengthOffset;
			public nint AsyncFlagOffset;
		}

		private static readonly StreamLayout layout = getStreamLayout();

		private readonly Stream stream;
		private byte[]? buffer;

		private static StreamLayout getStreamLayout()
		{
			var type = typeof(FileStream);
			var flags = BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.DeclaredOnly;

			var fieldBuffer = type.GetField("_buffer", flags);
			var fieldBuffLen = type.GetField("_bufferSize", flags) ?? type.GetField("_bufferLength", flags);
			var fieldAsync = type.GetField("_isAsync", flags) ?? type.GetField("_useAsyncIO", flags);

			if (fieldBuffer?.FieldType != typeof(byte[]) || fieldBuffLen?.FieldType != typeof(int) || fieldAsync?.FieldType != typeof(bool))
				return default;

			return new StreamLayout {
				HasKnownLayout = UnsafeUtil.IsFieldDescLayoutKnown,
				BufferInfo = fieldBuffer,
				BufferLengthOffset = fieldBuffLen.GetFieldOffset(),
				AsyncFlagOffset = fieldAsync.GetFieldOffset()
			};
		}

		public StreamBufferInjector(Stream fs)
		{
			stream = fs;
			buffer = null;

			if (fs is not FileStream || !layout.HasKnownLayout)
				return;

			bool isAsync = UnsafeUtil.GetFieldRef<bool>(fs, layout.AsyncFlagOffset);
			int buffLen = UnsafeUtil.GetFieldRef<int>(fs, layout.BufferLengthOffset);

			if (!isAsync && (uint)buffLen <= (1 << 16) && layout.BufferInfo.GetValue(fs) is null)
			{
				buffer = ArrayPool<byte>.Shared.Rent(buffLen);
				layout.BufferInfo.SetValue(fs, buffer);
			}
		}

		public void Dispose()
		{
			Debug.Assert(buffer is null || (byte[]?)layout.BufferInfo.GetValue(stream) == buffer);

			if (buffer is not null)
			{
				stream.Flush();
				layout.BufferInfo.SetValue(stream, null);

				ArrayPool<byte>.Shared.Return(buffer);
				buffer = null;
			}
		}
	}
}
