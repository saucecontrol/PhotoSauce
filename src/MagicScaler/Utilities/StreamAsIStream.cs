using System;
using System.IO;
using System.Runtime.InteropServices;

#if !BUILTIN_SPAN
using System.Buffers;
#endif

using STATSTG = System.Runtime.InteropServices.ComTypes.STATSTG;

namespace PhotoSauce.Interop.Wic
{
	internal static class StreamAsIStreamExtension
	{
		private sealed class StreamAsIStream : IStream
		{
			private readonly Stream stream;

			internal StreamAsIStream(Stream backingStream) =>
				stream = backingStream ?? throw new ArgumentNullException(nameof(backingStream));

			unsafe void IStream.Read(IntPtr pv, int cb, IntPtr pcbRead) =>
				Marshal.WriteInt32(pcbRead, stream.Read(new Span<byte>(pv.ToPointer(), cb)));

			unsafe void IStream.Write(IntPtr pv, int cb, IntPtr pcbWritten)
			{
				stream.Write(new ReadOnlySpan<byte>(pv.ToPointer(), cb));

				if (pcbWritten != IntPtr.Zero)
					Marshal.WriteInt32(pcbWritten, cb);
			}

			void IStream.Seek(long dlibMove, int dwOrigin, IntPtr plibNewPosition)
			{
				long pos = stream.Seek(dlibMove, (SeekOrigin)dwOrigin);

				if (plibNewPosition != IntPtr.Zero)
					Marshal.WriteInt64(plibNewPosition, pos);
			}

			void IStream.Stat(out STATSTG pstatstg, int grfStatFlag) =>
				pstatstg = new STATSTG { cbSize = stream.Length, type = 2 /*STGTY_STREAM*/ };

			void IStream.SetSize(long libNewSize) => stream.SetLength(libNewSize);

			void IStream.Commit(int grfCommitFlags) => stream.Flush();

			void IStream.CopyTo(IStream pstm, long cb, IntPtr pcbRead, IntPtr pcbWritten) => throw new NotImplementedException();

			void IStream.Clone(out IStream ppstm) => throw new NotImplementedException();

			void IStream.Revert() => throw new NotImplementedException();

			void IStream.LockRegion(long libOffset, long cb, int dwLockType) => throw new NotImplementedException();

			void IStream.UnlockRegion(long libOffset, long cb, int dwLockType) => throw new NotImplementedException();
		}

		public static IStream AsIStream(this Stream stream) => new StreamAsIStream(stream);

#if !BUILTIN_SPAN
		public static int Read(this Stream stream, Span<byte> buffer)
		{
			var buff = ArrayPool<byte>.Shared.Rent(buffer.Length);

			int cb = stream.Read(buff, 0, buffer.Length);
			buff.AsSpan(0, cb).CopyTo(buffer);

			ArrayPool<byte>.Shared.Return(buff);

			return cb;
		}

		public static void Write(this Stream stream, ReadOnlySpan<byte> buffer)
		{
			var buff = ArrayPool<byte>.Shared.Rent(buffer.Length);

			buffer.CopyTo(buff);
			stream.Write(buff, 0, buffer.Length);

			ArrayPool<byte>.Shared.Return(buff);
		}
#endif
	}
}
