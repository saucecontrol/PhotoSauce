// Copyright © Clinton Ingram and Contributors.  Licensed under the MIT License.

#pragma warning disable IDE0060

using System;
using System.IO;
using System.Threading;
using System.Runtime.InteropServices;
using System.Runtime.CompilerServices;

#if !NET5_0_OR_GREATER
using PhotoSauce.MagicScaler;
#endif

using TerraFX.Interop;
using static TerraFX.Interop.Windows;
using STATSTG = TerraFX.Interop.STATSTG;

namespace PhotoSauce.Interop.Wic
{
	internal unsafe struct IStreamImpl
	{
		private readonly void** lpVtbl;
		private readonly GCHandle source;
		private readonly uint offset;
		private int refCount;

		private IStreamImpl(Stream managedSource, uint offs = 0)
		{
			lpVtbl = vtblStatic;
			source = GCHandle.Alloc(managedSource, GCHandleType.Weak);
			offset = offs;
			refCount = 0;
		}

		public static IStream* Wrap(Stream managedSource, uint offs = 0)
		{
			var ptr = (IStreamImpl*)Marshal.AllocHGlobal(sizeof(IStreamImpl));
			*ptr = new IStreamImpl(managedSource, offs);

			return (IStream*)ptr;
		}

#if NET5_0_OR_GREATER
		[UnmanagedCallersOnly]
		static
#endif
		public int QueryInterface(IStreamImpl* pinst, Guid* riid, void** ppvObject)
		{
			var iid = *riid;
			if (iid == __uuidof<IStream>() || iid == __uuidof<ISequentialStream>() || iid == __uuidof<IUnknown>())
			{
				Interlocked.Increment(ref pinst->refCount);
				*ppvObject = pinst;
				return S_OK;
			}

			return E_NOINTERFACE;
		}

#if NET5_0_OR_GREATER
		[UnmanagedCallersOnly]
		static
#endif
		public uint AddRef(IStreamImpl* pinst) => (uint)Interlocked.Increment(ref pinst->refCount);

#if NET5_0_OR_GREATER
		[UnmanagedCallersOnly]
		static
#endif
		public uint Release(IStreamImpl* pinst)
		{
			uint cnt = (uint)Interlocked.Decrement(ref pinst->refCount);
			if (cnt == 0)
			{
				pinst->source.Free();
				*pinst = default;
				Marshal.FreeHGlobal((IntPtr)pinst);
			}

			return cnt;
		}

#if NET5_0_OR_GREATER
		[UnmanagedCallersOnly]
		static
#endif
		public int Read(IStreamImpl* pinst, void* pv, uint cb, uint* pcbRead)
		{
			var stm = Unsafe.As<Stream>(pinst->source.Target!);
			int read = stm.Read(new Span<byte>(pv, (int)cb));

			if (pcbRead is not null)
				*pcbRead = (uint)read;

			return S_OK;
		}

#if NET5_0_OR_GREATER
		[UnmanagedCallersOnly]
		static
#endif
		public int Write(IStreamImpl* pinst, void* pv, uint cb, uint* pcbWritten)
		{
			var stm = Unsafe.As<Stream>(pinst->source.Target!);
			stm.Write(new ReadOnlySpan<byte>(pv, (int)cb));

			if (pcbWritten is not null)
				*pcbWritten = cb;

			return S_OK;
		}

#if NET5_0_OR_GREATER
		[UnmanagedCallersOnly]
		static
#endif
		public int Seek(IStreamImpl* pinst, LARGE_INTEGER dlibMove, uint dwOrigin, ULARGE_INTEGER* plibNewPosition)
		{
			long npos = dlibMove.QuadPart + (dwOrigin == (uint)SeekOrigin.Begin ? pinst->offset : 0);
			var stm = Unsafe.As<Stream>(pinst->source.Target!);

			long cpos = stm.Position;
			if (!(dwOrigin == (uint)SeekOrigin.Current && npos == 0) && !(dwOrigin == (uint)SeekOrigin.Begin && npos == cpos))
				cpos = stm.Seek(npos, (SeekOrigin)dwOrigin);

			if (plibNewPosition is not null)
				plibNewPosition->QuadPart = (ulong)(cpos - pinst->offset);

			return S_OK;
		}

#if NET5_0_OR_GREATER
		[UnmanagedCallersOnly]
		static
#endif
		public int SetSize(IStreamImpl* pinst, ULARGE_INTEGER libNewSize)
		{
			var stm = Unsafe.As<Stream>(pinst->source.Target!);
			stm.SetLength((long)libNewSize.QuadPart + pinst->offset);

			return S_OK;
		}

#if NET5_0_OR_GREATER
		[UnmanagedCallersOnly]
		static
#endif
		public int CopyTo(IStreamImpl* pinst, IStream* pstm, ULARGE_INTEGER cb, ULARGE_INTEGER* pcbRead, ULARGE_INTEGER* pcbWritten) => E_NOTIMPL;

#if NET5_0_OR_GREATER
		[UnmanagedCallersOnly]
		static
#endif
		public int Commit(IStreamImpl* pinst, uint grfCommitFlags)
		{
			var stm = Unsafe.As<Stream>(pinst->source.Target!);
			stm.Flush();

			return S_OK;
		}

#if NET5_0_OR_GREATER
		[UnmanagedCallersOnly]
		static
#endif
		public int Revert(IStreamImpl* pinst) => E_NOTIMPL;

#if NET5_0_OR_GREATER
		[UnmanagedCallersOnly]
		static
#endif
		public int LockRegion(IStreamImpl* pinst, ULARGE_INTEGER libOffset, ULARGE_INTEGER cb, uint dwLockType) => E_NOTIMPL;

#if NET5_0_OR_GREATER
		[UnmanagedCallersOnly]
		static
#endif
		public int UnlockRegion(IStreamImpl* pinst, ULARGE_INTEGER libOffset, ULARGE_INTEGER cb, uint dwLockType) => E_NOTIMPL;

#if NET5_0_OR_GREATER
		[UnmanagedCallersOnly]
		static
#endif
		public int Stat(IStreamImpl* pinst, STATSTG* pstatstg, uint grfStatFlag)
		{
			var stm = Unsafe.As<Stream>(pinst->source.Target!);
			*pstatstg = new STATSTG { cbSize = new ULARGE_INTEGER { QuadPart = (ulong)(stm.Length - pinst->offset) }, type = (uint)STGTY.STGTY_STREAM };

			return S_OK;
		}

#if NET5_0_OR_GREATER
		[UnmanagedCallersOnly]
		static
#endif
		public int Clone(IStreamImpl* pinst, IStream** ppstm) => E_NOTIMPL;

#if NET5_0_OR_GREATER
		private static readonly delegate* unmanaged<IStreamImpl*, Guid*, void**, int> pfnQueryInterface = &QueryInterface;
		private static readonly delegate* unmanaged<IStreamImpl*, uint> pfnAddRef = &AddRef;
		private static readonly delegate* unmanaged<IStreamImpl*, uint> pfnRelease = &Release;
		private static readonly delegate* unmanaged<IStreamImpl*, void*, uint, uint*, int> pfnRead = &Read;
		private static readonly delegate* unmanaged<IStreamImpl*, void*, uint, uint*, int> pfnWrite = &Write;
		private static readonly delegate* unmanaged<IStreamImpl*, LARGE_INTEGER, uint, ULARGE_INTEGER*, int> pfnSeek = &Seek;
		private static readonly delegate* unmanaged<IStreamImpl*, ULARGE_INTEGER, int> pfnSetSize = &SetSize;
		private static readonly delegate* unmanaged<IStreamImpl*, IStream*, ULARGE_INTEGER, ULARGE_INTEGER*, ULARGE_INTEGER*, int> pfnCopyTo = &CopyTo;
		private static readonly delegate* unmanaged<IStreamImpl*, uint, int> pfnCommit = &Commit;
		private static readonly delegate* unmanaged<IStreamImpl*, int> pfnRevert = &Revert;
		private static readonly delegate* unmanaged<IStreamImpl*, ULARGE_INTEGER, ULARGE_INTEGER, uint, int> pfnLockRegion = &LockRegion;
		private static readonly delegate* unmanaged<IStreamImpl*, ULARGE_INTEGER, ULARGE_INTEGER, uint, int> pfnUnlockRegion = &UnlockRegion;
		private static readonly delegate* unmanaged<IStreamImpl*, STATSTG*, uint, int> pfnStat = &Stat;
		private static readonly delegate* unmanaged<IStreamImpl*, IStream**, int> pfnClone = &Clone;
#else
		[UnmanagedFunctionPointer(CallingConvention.StdCall)] public delegate int QueryInterfaceDelegate(IStreamImpl* pinst, Guid* riid, void** ppvObject);
		[UnmanagedFunctionPointer(CallingConvention.StdCall)] public delegate uint AddRefDelegate(IStreamImpl* pinst);
		[UnmanagedFunctionPointer(CallingConvention.StdCall)] public delegate uint ReleaseDelegate(IStreamImpl* pinst);
		[UnmanagedFunctionPointer(CallingConvention.StdCall)] public delegate int ReadDelegate(IStreamImpl* pinst, void* pv, uint cb, uint* pcbRead);
		[UnmanagedFunctionPointer(CallingConvention.StdCall)] public delegate int WriteDelegate(IStreamImpl* pinst, void* pv, uint cb, uint* pcbWritten);
		[UnmanagedFunctionPointer(CallingConvention.StdCall)] public delegate int SeekDelegate(IStreamImpl* pinst, LARGE_INTEGER dlibMove, uint dwOrigin, ULARGE_INTEGER* plibNewPosition);
		[UnmanagedFunctionPointer(CallingConvention.StdCall)] public delegate int SetSizeDelegate(IStreamImpl* pinst, ULARGE_INTEGER libNewSize);
		[UnmanagedFunctionPointer(CallingConvention.StdCall)] public delegate int CopyToDelegate(IStreamImpl* pinst, IStream* pstm, ULARGE_INTEGER cb, ULARGE_INTEGER* pcbRead, ULARGE_INTEGER* pcbWritten);
		[UnmanagedFunctionPointer(CallingConvention.StdCall)] public delegate int CommitDelegate(IStreamImpl* pinst, uint grfCommitFlags);
		[UnmanagedFunctionPointer(CallingConvention.StdCall)] public delegate int RevertDelegate(IStreamImpl* pinst);
		[UnmanagedFunctionPointer(CallingConvention.StdCall)] public delegate int LockRegionDelegate(IStreamImpl* pinst, ULARGE_INTEGER libOffset, ULARGE_INTEGER cb, uint dwLockType);
		[UnmanagedFunctionPointer(CallingConvention.StdCall)] public delegate int UnlockRegionDelegate(IStreamImpl* pinst, ULARGE_INTEGER libOffset, ULARGE_INTEGER cb, uint dwLockType);
		[UnmanagedFunctionPointer(CallingConvention.StdCall)] public delegate int StatDelegate(IStreamImpl* pinst, STATSTG* pstatstg, uint grfStatFlag);
		[UnmanagedFunctionPointer(CallingConvention.StdCall)] public delegate int CloneDelegate(IStreamImpl* pinst, IStream** ppstm);

		private static readonly QueryInterfaceDelegate delQueryInterface = typeof(IStreamImpl).CreateMethodDelegate<QueryInterfaceDelegate>(nameof(QueryInterface));
		private static readonly AddRefDelegate delAddRef = typeof(IStreamImpl).CreateMethodDelegate<AddRefDelegate>(nameof(AddRef));
		private static readonly ReleaseDelegate delRelease = typeof(IStreamImpl).CreateMethodDelegate<ReleaseDelegate>(nameof(Release));
		private static readonly ReadDelegate delRead = typeof(IStreamImpl).CreateMethodDelegate<ReadDelegate>(nameof(Read));
		private static readonly WriteDelegate delWrite = typeof(IStreamImpl).CreateMethodDelegate<WriteDelegate>(nameof(Write));
		private static readonly SeekDelegate delSeek = typeof(IStreamImpl).CreateMethodDelegate<SeekDelegate>(nameof(Seek));
		private static readonly SetSizeDelegate delSetSize = typeof(IStreamImpl).CreateMethodDelegate<SetSizeDelegate>(nameof(SetSize));
		private static readonly CopyToDelegate delCopyTo = typeof(IStreamImpl).CreateMethodDelegate<CopyToDelegate>(nameof(CopyTo));
		private static readonly CommitDelegate delCommit = typeof(IStreamImpl).CreateMethodDelegate<CommitDelegate>(nameof(Commit));
		private static readonly RevertDelegate delRevert = typeof(IStreamImpl).CreateMethodDelegate<RevertDelegate>(nameof(Revert));
		private static readonly LockRegionDelegate delLockRegion = typeof(IStreamImpl).CreateMethodDelegate<LockRegionDelegate>(nameof(LockRegion));
		private static readonly UnlockRegionDelegate delUnlockRegion = typeof(IStreamImpl).CreateMethodDelegate<UnlockRegionDelegate>(nameof(UnlockRegion));
		private static readonly StatDelegate delStat = typeof(IStreamImpl).CreateMethodDelegate<StatDelegate>(nameof(Stat));
		private static readonly CloneDelegate delClone = typeof(IStreamImpl).CreateMethodDelegate<CloneDelegate>(nameof(Clone));

		private static readonly delegate* unmanaged[Stdcall]<IStreamImpl*, Guid*, void**, int> pfnQueryInterface = (delegate* unmanaged[Stdcall]<IStreamImpl*, Guid*, void**, int>)Marshal.GetFunctionPointerForDelegate(delQueryInterface);
		private static readonly delegate* unmanaged[Stdcall]<IStreamImpl*, uint> pfnAddRef = (delegate* unmanaged[Stdcall]<IStreamImpl*, uint>)Marshal.GetFunctionPointerForDelegate(delAddRef);
		private static readonly delegate* unmanaged[Stdcall]<IStreamImpl*, uint> pfnRelease = (delegate* unmanaged[Stdcall]<IStreamImpl*, uint>)Marshal.GetFunctionPointerForDelegate(delRelease);
		private static readonly delegate* unmanaged[Stdcall]<IStreamImpl*, void*, uint, uint*, int> pfnRead = (delegate* unmanaged[Stdcall]<IStreamImpl*, void*, uint, uint*, int>)Marshal.GetFunctionPointerForDelegate(delRead);
		private static readonly delegate* unmanaged[Stdcall]<IStreamImpl*, void*, uint, uint*, int> pfnWrite = (delegate* unmanaged[Stdcall]<IStreamImpl*, void*, uint, uint*, int>)Marshal.GetFunctionPointerForDelegate(delWrite);
		private static readonly delegate* unmanaged[Stdcall]<IStreamImpl*, LARGE_INTEGER, uint, ULARGE_INTEGER*, int> pfnSeek = (delegate* unmanaged[Stdcall]<IStreamImpl*, LARGE_INTEGER, uint, ULARGE_INTEGER*, int>)Marshal.GetFunctionPointerForDelegate(delSeek);
		private static readonly delegate* unmanaged[Stdcall]<IStreamImpl*, ULARGE_INTEGER, int> pfnSetSize = (delegate* unmanaged[Stdcall]<IStreamImpl*, ULARGE_INTEGER, int>)Marshal.GetFunctionPointerForDelegate(delSetSize);
		private static readonly delegate* unmanaged[Stdcall]<IStreamImpl*, IStream*, ULARGE_INTEGER, ULARGE_INTEGER*, ULARGE_INTEGER*, int> pfnCopyTo = (delegate* unmanaged[Stdcall]<IStreamImpl*, IStream*, ULARGE_INTEGER, ULARGE_INTEGER*, ULARGE_INTEGER*, int>)Marshal.GetFunctionPointerForDelegate(delCopyTo);
		private static readonly delegate* unmanaged[Stdcall]<IStreamImpl*, uint, int> pfnCommit = (delegate* unmanaged[Stdcall]<IStreamImpl*, uint, int>)Marshal.GetFunctionPointerForDelegate(delCommit);
		private static readonly delegate* unmanaged[Stdcall]<IStreamImpl*, int> pfnRevert = (delegate* unmanaged[Stdcall]<IStreamImpl*, int>)Marshal.GetFunctionPointerForDelegate(delRevert);
		private static readonly delegate* unmanaged[Stdcall]<IStreamImpl*, ULARGE_INTEGER, ULARGE_INTEGER, uint, int> pfnLockRegion = (delegate* unmanaged[Stdcall]<IStreamImpl*, ULARGE_INTEGER, ULARGE_INTEGER, uint, int>)Marshal.GetFunctionPointerForDelegate(delLockRegion);
		private static readonly delegate* unmanaged[Stdcall]<IStreamImpl*, ULARGE_INTEGER, ULARGE_INTEGER, uint, int> pfnUnlockRegion = (delegate* unmanaged[Stdcall]<IStreamImpl*, ULARGE_INTEGER, ULARGE_INTEGER, uint, int>)Marshal.GetFunctionPointerForDelegate(delUnlockRegion);
		private static readonly delegate* unmanaged[Stdcall]<IStreamImpl*, STATSTG*, uint, int> pfnStat = (delegate* unmanaged[Stdcall]<IStreamImpl*, STATSTG*, uint, int>)Marshal.GetFunctionPointerForDelegate(delStat);
		private static readonly delegate* unmanaged[Stdcall]<IStreamImpl*, IStream**, int> pfnClone = (delegate* unmanaged[Stdcall]<IStreamImpl*, IStream**, int>)Marshal.GetFunctionPointerForDelegate(delClone);
#endif

		private static readonly void** vtblStatic = createVtbl();

		private static void** createVtbl()
		{
#if NET5_0_OR_GREATER
			void** p = (void**)RuntimeHelpers.AllocateTypeAssociatedMemory(typeof(IStreamImpl), sizeof(nuint) * 14);
#else
			void** p = (void**)Marshal.AllocHGlobal(sizeof(nuint) * 14);
#endif

			p[ 0] = pfnQueryInterface;
			p[ 1] = pfnAddRef;
			p[ 2] = pfnRelease;
			p[ 3] = pfnRead;
			p[ 4] = pfnWrite;
			p[ 5] = pfnSeek;
			p[ 6] = pfnSetSize;
			p[ 7] = pfnCopyTo;
			p[ 8] = pfnCommit;
			p[ 9] = pfnRevert;
			p[10] = pfnLockRegion;
			p[11] = pfnUnlockRegion;
			p[12] = pfnStat;
			p[13] = pfnClone;

			return p;
		}
	}
}