using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.ComTypes;

using STATSTG = System.Runtime.InteropServices.ComTypes.STATSTG;

namespace PhotoSauce.MagicScaler.Interop
{
	internal static class StreamAsIStreamExtension
	{
		private class StreamAsIStream : IStream
		{
			private readonly Stream stream;

			internal StreamAsIStream(Stream backingStream) =>
				stream = backingStream ?? throw new ArgumentNullException(nameof(backingStream));

			void IStream.Read(byte[] pv, int cb, IntPtr pcbRead) => Marshal.WriteInt32(pcbRead, stream.Read(pv, 0, cb));

			void IStream.Write(byte[] pv, int cb, IntPtr pcbWritten)
			{
				stream.Write(pv, 0, cb);

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

		public static IStream AsIStream(this Stream s) => new StreamAsIStream(s);
	}
}
