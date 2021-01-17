// Copyright © Clinton Ingram and Contributors.  Licensed under the MIT License.

#pragma warning disable IDE0060

using System;
using System.IO;
using System.Diagnostics;
using System.Runtime.InteropServices;

#if BUILTIN_CSHARP9
using System.Runtime.CompilerServices;
#elif !BUILTIN_SPAN
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
		private readonly IntPtr source;
		private readonly uint offset;

		public IStreamImpl(GCHandle managedSource, uint offs = 0)
		{
			Debug.Assert(managedSource.Target is Stream);

			lpVtbl = vtblStatic;
			source = GCHandle.ToIntPtr(managedSource);
			offset = offs;
		}

#if BUILTIN_CSHARP9
		[UnmanagedCallersOnly]
		static
#endif
		public int QueryInterface(IStreamImpl* pinst, Guid* riid, void** ppvObject)
		{
			var iid = *riid;
			if (iid == __uuidof<IStream>() || iid == __uuidof<ISequentialStream>() || iid == __uuidof<IUnknown>())
			{
				*ppvObject = pinst;
				return S_OK;
			}

			return E_NOINTERFACE;
		}

#if BUILTIN_CSHARP9
		[UnmanagedCallersOnly]
		static
#endif
		public uint AddRef(IStreamImpl* pinst) => 1;

#if BUILTIN_CSHARP9
		[UnmanagedCallersOnly]
		static
#endif
		public uint Release(IStreamImpl* pinst) => 1;

#if BUILTIN_CSHARP9
		[UnmanagedCallersOnly]
		static
#endif
		public int Read(IStreamImpl* pinst, void* pv, uint cb, uint* pcbRead)
		{
			var stm = (Stream)GCHandle.FromIntPtr(pinst->source).Target!;
			*pcbRead = (uint)stm.Read(new Span<byte>(pv, (int)cb));

			return S_OK;
		}

#if BUILTIN_CSHARP9
		[UnmanagedCallersOnly]
		static
#endif
		public int Write(IStreamImpl* pinst, void* pv, uint cb, uint* pcbWritten)
		{
			var stm = (Stream)GCHandle.FromIntPtr(pinst->source).Target!;
			stm.Write(new ReadOnlySpan<byte>(pv, (int)cb));

			if (pcbWritten is not null)
				*pcbWritten = cb;

			return S_OK;
		}

#if BUILTIN_CSHARP9
		[UnmanagedCallersOnly]
		static
#endif
		public int Seek(IStreamImpl* pinst, LARGE_INTEGER dlibMove, uint dwOrigin, ULARGE_INTEGER* plibNewPosition)
		{
			long npos = dlibMove.QuadPart + (dwOrigin == (uint)SeekOrigin.Begin ? pinst->offset : 0);
			var stm = (Stream)GCHandle.FromIntPtr(pinst->source).Target!;

			long cpos = stm.Position;
			if ((dwOrigin != (uint)SeekOrigin.Current || npos != 0) && (dwOrigin != (uint)SeekOrigin.Begin || npos != cpos))
				cpos = stm.Seek(npos, (SeekOrigin)dwOrigin);

			if (plibNewPosition is not null)
				plibNewPosition->QuadPart = (ulong)(cpos - pinst->offset);

			return S_OK;
		}

#if BUILTIN_CSHARP9
		[UnmanagedCallersOnly]
		static
#endif
		public int SetSize(IStreamImpl* pinst, ULARGE_INTEGER libNewSize)
		{
			var stm = (Stream)GCHandle.FromIntPtr(pinst->source).Target!;
			stm.SetLength((long)libNewSize.QuadPart + pinst->offset);

			return S_OK;
		}

#if BUILTIN_CSHARP9
		[UnmanagedCallersOnly]
		static
#endif
		public int CopyTo(IStreamImpl* pinst, IStream* pstm, ULARGE_INTEGER cb, ULARGE_INTEGER* pcbRead, ULARGE_INTEGER* pcbWritten) => E_NOTIMPL;

#if BUILTIN_CSHARP9
		[UnmanagedCallersOnly]
		static
#endif
		public int Commit(IStreamImpl* pinst, uint grfCommitFlags)
		{
			var stm = (Stream)GCHandle.FromIntPtr(pinst->source).Target!;
			stm.Flush();

			return S_OK;
		}

#if BUILTIN_CSHARP9
		[UnmanagedCallersOnly]
		static
#endif
		public int Revert(IStreamImpl* pinst) => E_NOTIMPL;

#if BUILTIN_CSHARP9
		[UnmanagedCallersOnly]
		static
#endif
		public int LockRegion(IStreamImpl* pinst, ULARGE_INTEGER libOffset, ULARGE_INTEGER cb, uint dwLockType) => E_NOTIMPL;

#if BUILTIN_CSHARP9
		[UnmanagedCallersOnly]
		static
#endif
		public int UnlockRegion(IStreamImpl* pinst, ULARGE_INTEGER libOffset, ULARGE_INTEGER cb, uint dwLockType) => E_NOTIMPL;

#if BUILTIN_CSHARP9
		[UnmanagedCallersOnly]
		static
#endif
		public int Stat(IStreamImpl* pinst, STATSTG* pstatstg, uint grfStatFlag)
		{
			var stm = (Stream)GCHandle.FromIntPtr(pinst->source).Target!;
			*pstatstg = new STATSTG { cbSize = new ULARGE_INTEGER { QuadPart = (ulong)(stm.Length - pinst->offset) }, type = (uint)STGTY.STGTY_STREAM };

			return S_OK;
		}

#if BUILTIN_CSHARP9
		[UnmanagedCallersOnly]
		static
#endif
		public int Clone(IStreamImpl* pinst, IStream** ppstm) => E_NOTIMPL;

#if BUILTIN_CSHARP9
		public static readonly delegate* unmanaged<IStreamImpl*, Guid*, void**, int> pfnQueryInterface = &QueryInterface;
		public static readonly delegate* unmanaged<IStreamImpl*, uint> pfnAddRef = &AddRef;
		public static readonly delegate* unmanaged<IStreamImpl*, uint> pfnRelease = &Release;
		public static readonly delegate* unmanaged<IStreamImpl*, void*, uint, uint*, int> pfnRead = &Read;
		public static readonly delegate* unmanaged<IStreamImpl*, void*, uint, uint*, int> pfnWrite = &Write;
		public static readonly delegate* unmanaged<IStreamImpl*, LARGE_INTEGER, uint, ULARGE_INTEGER*, int> pfnSeek = &Seek;
		public static readonly delegate* unmanaged<IStreamImpl*, ULARGE_INTEGER, int> pfnSetSize = &SetSize;
		public static readonly delegate* unmanaged<IStreamImpl*, IStream*, ULARGE_INTEGER, ULARGE_INTEGER*, ULARGE_INTEGER*, int> pfnCopyTo = &CopyTo;
		public static readonly delegate* unmanaged<IStreamImpl*, uint, int> pfnCommit = &Commit;
		public static readonly delegate* unmanaged<IStreamImpl*, int> pfnRevert = &Revert;
		public static readonly delegate* unmanaged<IStreamImpl*, ULARGE_INTEGER, ULARGE_INTEGER, uint, int> pfnLockRegion = &LockRegion;
		public static readonly delegate* unmanaged<IStreamImpl*, ULARGE_INTEGER, ULARGE_INTEGER, uint, int> pfnUnlockRegion = &UnlockRegion;
		public static readonly delegate* unmanaged<IStreamImpl*, STATSTG*, uint, int> pfnStat = &Stat;
		public static readonly delegate* unmanaged<IStreamImpl*, IStream**, int> pfnClone = &Clone;
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

		public static readonly QueryInterfaceDelegate delQueryInterface = (QueryInterfaceDelegate)typeof(IStreamImpl).GetMethod(nameof(QueryInterface))!.CreateDelegate(typeof(QueryInterfaceDelegate), null);
		public static readonly AddRefDelegate delAddRef = (AddRefDelegate)typeof(IStreamImpl).GetMethod(nameof(AddRef))!.CreateDelegate(typeof(AddRefDelegate), null);
		public static readonly ReleaseDelegate delRelease = (ReleaseDelegate)typeof(IStreamImpl).GetMethod(nameof(Release))!.CreateDelegate(typeof(ReleaseDelegate), null);
		public static readonly ReadDelegate delRead = (ReadDelegate)typeof(IStreamImpl).GetMethod(nameof(Read))!.CreateDelegate(typeof(ReadDelegate), null);
		public static readonly WriteDelegate delWrite = (WriteDelegate)typeof(IStreamImpl).GetMethod(nameof(Write))!.CreateDelegate(typeof(WriteDelegate), null);
		public static readonly SeekDelegate delSeek = (SeekDelegate)typeof(IStreamImpl).GetMethod(nameof(Seek))!.CreateDelegate(typeof(SeekDelegate), null);
		public static readonly SetSizeDelegate delSetSize = (SetSizeDelegate)typeof(IStreamImpl).GetMethod(nameof(SetSize))!.CreateDelegate(typeof(SetSizeDelegate), null);
		public static readonly CopyToDelegate delCopyTo = (CopyToDelegate)typeof(IStreamImpl).GetMethod(nameof(CopyTo))!.CreateDelegate(typeof(CopyToDelegate), null);
		public static readonly CommitDelegate delCommit = (CommitDelegate)typeof(IStreamImpl).GetMethod(nameof(Commit))!.CreateDelegate(typeof(CommitDelegate), null);
		public static readonly RevertDelegate delRevert = (RevertDelegate)typeof(IStreamImpl).GetMethod(nameof(Revert))!.CreateDelegate(typeof(RevertDelegate), null);
		public static readonly LockRegionDelegate delLockRegion = (LockRegionDelegate)typeof(IStreamImpl).GetMethod(nameof(LockRegion))!.CreateDelegate(typeof(LockRegionDelegate), null);
		public static readonly UnlockRegionDelegate delUnlockRegion = (UnlockRegionDelegate)typeof(IStreamImpl).GetMethod(nameof(UnlockRegion))!.CreateDelegate(typeof(UnlockRegionDelegate), null);
		public static readonly StatDelegate delStat = (StatDelegate)typeof(IStreamImpl).GetMethod(nameof(Stat))!.CreateDelegate(typeof(StatDelegate), null);
		public static readonly CloneDelegate delClone = (CloneDelegate)typeof(IStreamImpl).GetMethod(nameof(Clone))!.CreateDelegate(typeof(CloneDelegate), null);

		public static readonly delegate* unmanaged[Stdcall]<IStreamImpl*, Guid*, void**, int> pfnQueryInterface = (delegate* unmanaged[Stdcall]<IStreamImpl*, Guid*, void**, int>)Marshal.GetFunctionPointerForDelegate(delQueryInterface);
		public static readonly delegate* unmanaged[Stdcall]<IStreamImpl*, uint> pfnAddRef = (delegate* unmanaged[Stdcall]<IStreamImpl*, uint>)Marshal.GetFunctionPointerForDelegate(delAddRef);
		public static readonly delegate* unmanaged[Stdcall]<IStreamImpl*, uint> pfnRelease = (delegate* unmanaged[Stdcall]<IStreamImpl*, uint>)Marshal.GetFunctionPointerForDelegate(delRelease);
		public static readonly delegate* unmanaged[Stdcall]<IStreamImpl*, void*, uint, uint*, int> pfnRead = (delegate* unmanaged[Stdcall]<IStreamImpl*, void*, uint, uint*, int>)Marshal.GetFunctionPointerForDelegate(delRead);
		public static readonly delegate* unmanaged[Stdcall]<IStreamImpl*, void*, uint, uint*, int> pfnWrite = (delegate* unmanaged[Stdcall]<IStreamImpl*, void*, uint, uint*, int>)Marshal.GetFunctionPointerForDelegate(delWrite);
		public static readonly delegate* unmanaged[Stdcall]<IStreamImpl*, LARGE_INTEGER, uint, ULARGE_INTEGER*, int> pfnSeek = (delegate* unmanaged[Stdcall]<IStreamImpl*, LARGE_INTEGER, uint, ULARGE_INTEGER*, int>)Marshal.GetFunctionPointerForDelegate(delSeek);
		public static readonly delegate* unmanaged[Stdcall]<IStreamImpl*, ULARGE_INTEGER, int> pfnSetSize = (delegate* unmanaged[Stdcall]<IStreamImpl*, ULARGE_INTEGER, int>)Marshal.GetFunctionPointerForDelegate(delSetSize);
		public static readonly delegate* unmanaged[Stdcall]<IStreamImpl*, IStream*, ULARGE_INTEGER, ULARGE_INTEGER*, ULARGE_INTEGER*, int> pfnCopyTo = (delegate* unmanaged[Stdcall]<IStreamImpl*, IStream*, ULARGE_INTEGER, ULARGE_INTEGER*, ULARGE_INTEGER*, int>)Marshal.GetFunctionPointerForDelegate(delCopyTo);
		public static readonly delegate* unmanaged[Stdcall]<IStreamImpl*, uint, int> pfnCommit = (delegate* unmanaged[Stdcall]<IStreamImpl*, uint, int>)Marshal.GetFunctionPointerForDelegate(delCommit);
		public static readonly delegate* unmanaged[Stdcall]<IStreamImpl*, int> pfnRevert = (delegate* unmanaged[Stdcall]<IStreamImpl*, int>)Marshal.GetFunctionPointerForDelegate(delRevert);
		public static readonly delegate* unmanaged[Stdcall]<IStreamImpl*, ULARGE_INTEGER, ULARGE_INTEGER, uint, int> pfnLockRegion = (delegate* unmanaged[Stdcall]<IStreamImpl*, ULARGE_INTEGER, ULARGE_INTEGER, uint, int>)Marshal.GetFunctionPointerForDelegate(delLockRegion);
		public static readonly delegate* unmanaged[Stdcall]<IStreamImpl*, ULARGE_INTEGER, ULARGE_INTEGER, uint, int> pfnUnlockRegion = (delegate* unmanaged[Stdcall]<IStreamImpl*, ULARGE_INTEGER, ULARGE_INTEGER, uint, int>)Marshal.GetFunctionPointerForDelegate(delUnlockRegion);
		public static readonly delegate* unmanaged[Stdcall]<IStreamImpl*, STATSTG*, uint, int> pfnStat = (delegate* unmanaged[Stdcall]<IStreamImpl*, STATSTG*, uint, int>)Marshal.GetFunctionPointerForDelegate(delStat);
		public static readonly delegate* unmanaged[Stdcall]<IStreamImpl*, IStream**, int> pfnClone = (delegate* unmanaged[Stdcall]<IStreamImpl*, IStream**, int>)Marshal.GetFunctionPointerForDelegate(delClone);
#endif

		public static void** vtblStatic = createVtbl();

		private static void** createVtbl()
		{
#if BUILTIN_CSHARP9
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