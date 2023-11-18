// Copyright © Clinton Ingram and Contributors.  Licensed under the MIT License.

#pragma warning disable IDE0060, IDE0251

using System;
using System.IO;
using System.Threading;
using System.Runtime.InteropServices;
using System.Runtime.CompilerServices;

using PhotoSauce.MagicScaler;

using TerraFX.Interop.Windows;
using static TerraFX.Interop.Windows.E;
using static TerraFX.Interop.Windows.S;
using static TerraFX.Interop.Windows.Windows;
using STATSTG = TerraFX.Interop.Windows.STATSTG;

namespace PhotoSauce.Interop.Wic;

internal unsafe struct IStreamImpl
{
	private readonly void** lpVtbl;
	private readonly GCHandle source;
	private readonly long offset;
	private int refCount;

	private IStreamImpl(Stream managedSource)
	{
		lpVtbl = vtblStatic;
		source = GCHandle.Alloc(managedSource);
		offset = managedSource.Position;
	}

	public static IStream* Wrap(Stream managedSource)
	{
		var pinst = UnsafeUtil.NativeAlloc<IStreamImpl>();
		*pinst = new(managedSource);

		return (IStream*)pinst;
	}

#if NET5_0_OR_GREATER
	[UnmanagedCallersOnly(CallConvs = [ typeof(CallConvStdcall) ])]
	static
#endif
	private int queryInterface(IStream* pinst, Guid* riid, void** ppvObject)
	{
		var pthis = (IStreamImpl*)pinst;

		var iid = *riid;
		if (iid == __uuidof<IStream>() || iid == __uuidof<ISequentialStream>() || iid == __uuidof<IUnknown>())
		{
			Interlocked.Increment(ref pthis->refCount);
			*ppvObject = pthis;
			return S_OK;
		}

		return E_NOINTERFACE;
	}

#if NET5_0_OR_GREATER
	[UnmanagedCallersOnly(CallConvs = [ typeof(CallConvStdcall) ])]
	static
#endif
	private uint addRef(IStream* pinst) => (uint)Interlocked.Increment(ref ((IStreamImpl*)pinst)->refCount);

#if NET5_0_OR_GREATER
	[UnmanagedCallersOnly(CallConvs = [ typeof(CallConvStdcall) ])]
	static
#endif
	private uint release(IStream* pinst)
	{
		var pthis = (IStreamImpl*)pinst;

		uint cnt = (uint)Interlocked.Decrement(ref pthis->refCount);
		if (cnt == 0)
		{
			pthis->source.Free();
			*pthis = default;
			UnsafeUtil.NativeFree(pthis);
		}

		return cnt;
	}

#if NET5_0_OR_GREATER
	[UnmanagedCallersOnly(CallConvs = [ typeof(CallConvStdcall) ])]
	static
#endif
	private int read(IStream* pinst, void* pv, uint cb, uint* pcbRead)
	{
		var stm = Unsafe.As<Stream>(((IStreamImpl*)pinst)->source.Target!);
		uint read = 0;

		if (cb == 1)
		{
			int res = stm.ReadByte();
			if (res >= 0)
			{
				*(byte*)pv = (byte)res;
				read = cb;
			}
		}
		else
		{
			var buff = new Span<byte>(pv, checked((int)cb));
			read = (uint)stm.TryFillBuffer(buff);
		}

		if (pcbRead is not null)
			*pcbRead = read;

		return read == cb ? S_OK : S_FALSE;
	}

#if NET5_0_OR_GREATER
	[UnmanagedCallersOnly(CallConvs = [ typeof(CallConvStdcall) ])]
	static
#endif
	private int write(IStream* pinst, void* pv, uint cb, uint* pcbWritten)
	{
		var stm = Unsafe.As<Stream>(((IStreamImpl*)pinst)->source.Target!);

		if (cb == 1)
			stm.WriteByte(*(byte*)pv);
		else
			stm.Write(new ReadOnlySpan<byte>(pv, checked((int)cb)));

		if (pcbWritten is not null)
			*pcbWritten = cb;

		return S_OK;
	}

#if NET5_0_OR_GREATER
	[UnmanagedCallersOnly(CallConvs = [ typeof(CallConvStdcall) ])]
	static
#endif
	private int seek(IStream* pinst, LARGE_INTEGER dlibMove, uint dwOrigin, ULARGE_INTEGER* plibNewPosition)
	{
		var pthis = (IStreamImpl*)pinst;
		var stm = Unsafe.As<Stream>(pthis->source.Target!);
		long npos = dlibMove.QuadPart;

		if (dwOrigin == (uint)SeekOrigin.Begin)
		{
			// WIC PNG bug per https://twitter.com/rickbrewPDN/status/1123796693777092609 -- fixed in Win10 1909?
			// Negative position might mean an overflowed int was sign entended. As long as stream length would fit in 32 bits, zero extend instead.
			if (npos < 0 && stm.Length > int.MaxValue && stm.Length <= uint.MaxValue)
				npos = (uint)npos;

			npos += pthis->offset;
		}

		long cpos = stm.Position;
		if (!(dwOrigin == (uint)SeekOrigin.Current && npos == 0) && !(dwOrigin == (uint)SeekOrigin.Begin && npos == cpos))
			cpos = stm.Seek(npos, (SeekOrigin)dwOrigin);

		if (plibNewPosition is not null)
			plibNewPosition->QuadPart = (ulong)(cpos - pthis->offset);

		return S_OK;
	}

#if NET5_0_OR_GREATER
	[UnmanagedCallersOnly(CallConvs = [ typeof(CallConvStdcall) ])]
	static
#endif
	private int setSize(IStream* pinst, ULARGE_INTEGER libNewSize)
	{
		var pthis = (IStreamImpl*)pinst;
		var stm = Unsafe.As<Stream>(pthis->source.Target!);
		stm.SetLength((long)libNewSize.QuadPart + pthis->offset);

		return S_OK;
	}

#if NET5_0_OR_GREATER
	[UnmanagedCallersOnly(CallConvs = [ typeof(CallConvStdcall) ])]
	static
#endif
	private int copyTo(IStream* pinst, IStream* pstm, ULARGE_INTEGER cb, ULARGE_INTEGER* pcbRead, ULARGE_INTEGER* pcbWritten) => E_NOTIMPL;

#if NET5_0_OR_GREATER
	[UnmanagedCallersOnly(CallConvs = [ typeof(CallConvStdcall) ])]
	static
#endif
	private int commit(IStream* pinst, uint grfCommitFlags)
	{
		var stm = Unsafe.As<Stream>(((IStreamImpl*)pinst)->source.Target!);
		stm.Flush();

		return S_OK;
	}

#if NET5_0_OR_GREATER
	[UnmanagedCallersOnly(CallConvs = [ typeof(CallConvStdcall) ])]
	static
#endif
	private int revert(IStream* pinst) => E_NOTIMPL;

#if NET5_0_OR_GREATER
	[UnmanagedCallersOnly(CallConvs = [ typeof(CallConvStdcall) ])]
	static
#endif
	private int lockRegion(IStream* pinst, ULARGE_INTEGER libOffset, ULARGE_INTEGER cb, uint dwLockType) => E_NOTIMPL;

#if NET5_0_OR_GREATER
	[UnmanagedCallersOnly(CallConvs = [ typeof(CallConvStdcall) ])]
	static
#endif
	private int unlockRegion(IStream* pinst, ULARGE_INTEGER libOffset, ULARGE_INTEGER cb, uint dwLockType) => E_NOTIMPL;

#if NET5_0_OR_GREATER
	[UnmanagedCallersOnly(CallConvs = [ typeof(CallConvStdcall) ])]
	static
#endif
	private int stat(IStream* pinst, STATSTG* pstatstg, uint grfStatFlag)
	{
		var pthis = (IStreamImpl*)pinst;
		var stm = Unsafe.As<Stream>(pthis->source.Target!);
		*pstatstg = new STATSTG { cbSize = new ULARGE_INTEGER { QuadPart = (ulong)(stm.Length - pthis->offset) }, type = (uint)STGTY.STGTY_STREAM };

		return S_OK;
	}

#if NET5_0_OR_GREATER
	[UnmanagedCallersOnly(CallConvs = [ typeof(CallConvStdcall) ])]
	static
#endif
	private int clone(IStream* pinst, IStream** ppstm) => E_NOTIMPL;

#if !NET5_0_OR_GREATER
	[UnmanagedFunctionPointer(CallingConvention.StdCall)] private delegate int QueryInterface(IStream* pinst, Guid* riid, void** ppvObject);
	[UnmanagedFunctionPointer(CallingConvention.StdCall)] private delegate uint AddRef(IStream* pinst);
	[UnmanagedFunctionPointer(CallingConvention.StdCall)] private delegate uint Release(IStream* pinst);
	[UnmanagedFunctionPointer(CallingConvention.StdCall)] private delegate int Read(IStream* pinst, void* pv, uint cb, uint* pcbRead);
	[UnmanagedFunctionPointer(CallingConvention.StdCall)] private delegate int Write(IStream* pinst, void* pv, uint cb, uint* pcbWritten);
	[UnmanagedFunctionPointer(CallingConvention.StdCall)] private delegate int Seek(IStream* pinst, LARGE_INTEGER dlibMove, uint dwOrigin, ULARGE_INTEGER* plibNewPosition);
	[UnmanagedFunctionPointer(CallingConvention.StdCall)] private delegate int SetSize(IStream* pinst, ULARGE_INTEGER libNewSize);
	[UnmanagedFunctionPointer(CallingConvention.StdCall)] private delegate int CopyTo(IStream* pinst, IStream* pstm, ULARGE_INTEGER cb, ULARGE_INTEGER* pcbRead, ULARGE_INTEGER* pcbWritten);
	[UnmanagedFunctionPointer(CallingConvention.StdCall)] private delegate int Commit(IStream* pinst, uint grfCommitFlags);
	[UnmanagedFunctionPointer(CallingConvention.StdCall)] private delegate int Revert(IStream* pinst);
	[UnmanagedFunctionPointer(CallingConvention.StdCall)] private delegate int LockRegion(IStream* pinst, ULARGE_INTEGER libOffset, ULARGE_INTEGER cb, uint dwLockType);
	[UnmanagedFunctionPointer(CallingConvention.StdCall)] private delegate int UnlockRegion(IStream* pinst, ULARGE_INTEGER libOffset, ULARGE_INTEGER cb, uint dwLockType);
	[UnmanagedFunctionPointer(CallingConvention.StdCall)] private delegate int Stat(IStream* pinst, STATSTG* pstatstg, uint grfStatFlag);
	[UnmanagedFunctionPointer(CallingConvention.StdCall)] private delegate int Clone(IStream* pinst, IStream** ppstm);

	private static readonly QueryInterface delQueryInterface = typeof(IStreamImpl).CreateMethodDelegate<QueryInterface>(nameof(queryInterface));
	private static readonly AddRef delAddRef = typeof(IStreamImpl).CreateMethodDelegate<AddRef>(nameof(addRef));
	private static readonly Release delRelease = typeof(IStreamImpl).CreateMethodDelegate<Release>(nameof(release));
	private static readonly Read delRead = typeof(IStreamImpl).CreateMethodDelegate<Read>(nameof(read));
	private static readonly Write delWrite = typeof(IStreamImpl).CreateMethodDelegate<Write>(nameof(write));
	private static readonly Seek delSeek = typeof(IStreamImpl).CreateMethodDelegate<Seek>(nameof(seek));
	private static readonly SetSize delSetSize = typeof(IStreamImpl).CreateMethodDelegate<SetSize>(nameof(setSize));
	private static readonly CopyTo delCopyTo = typeof(IStreamImpl).CreateMethodDelegate<CopyTo>(nameof(copyTo));
	private static readonly Commit delCommit = typeof(IStreamImpl).CreateMethodDelegate<Commit>(nameof(commit));
	private static readonly Revert delRevert = typeof(IStreamImpl).CreateMethodDelegate<Revert>(nameof(revert));
	private static readonly LockRegion delLockRegion = typeof(IStreamImpl).CreateMethodDelegate<LockRegion>(nameof(lockRegion));
	private static readonly UnlockRegion delUnlockRegion = typeof(IStreamImpl).CreateMethodDelegate<UnlockRegion>(nameof(unlockRegion));
	private static readonly Stat delStat = typeof(IStreamImpl).CreateMethodDelegate<Stat>(nameof(stat));
	private static readonly Clone delClone = typeof(IStreamImpl).CreateMethodDelegate<Clone>(nameof(clone));
#endif

	private static readonly void** vtblStatic = createVtbl();

	private static void** createVtbl()
	{
		var vtbl = (IStream.Vtbl<IStream>*)UnsafeUtil.AllocateTypeAssociatedMemory(typeof(IStreamImpl), sizeof(IStream.Vtbl<IStream>));
#if NET5_0_OR_GREATER
		vtbl->QueryInterface = &queryInterface;
		vtbl->AddRef = &addRef;
		vtbl->Release = &release;
		vtbl->Read = &read;
		vtbl->Write = &write;
		vtbl->Seek = &seek;
		vtbl->SetSize = &setSize;
		vtbl->CopyTo = &copyTo;
		vtbl->Commit = &commit;
		vtbl->Revert = &revert;
		vtbl->LockRegion = &lockRegion;
		vtbl->UnlockRegion = &unlockRegion;
		vtbl->Stat	= &stat;
		vtbl->Clone = &clone;
#else
		vtbl->QueryInterface = (delegate* unmanaged[Stdcall]<IStream*, Guid*, void**, int>)Marshal.GetFunctionPointerForDelegate(delQueryInterface);
		vtbl->AddRef = (delegate* unmanaged[Stdcall]<IStream*, uint>)Marshal.GetFunctionPointerForDelegate(delAddRef);
		vtbl->Release = (delegate* unmanaged[Stdcall]<IStream*, uint>)Marshal.GetFunctionPointerForDelegate(delRelease);
		vtbl->Read = (delegate* unmanaged[Stdcall]<IStream*, void*, uint, uint*, int>)Marshal.GetFunctionPointerForDelegate(delRead);
		vtbl->Write = (delegate* unmanaged[Stdcall]<IStream*, void*, uint, uint*, int>)Marshal.GetFunctionPointerForDelegate(delWrite);
		vtbl->Seek = (delegate* unmanaged[Stdcall]<IStream*, LARGE_INTEGER, uint, ULARGE_INTEGER*, int>)Marshal.GetFunctionPointerForDelegate(delSeek);
		vtbl->SetSize = (delegate* unmanaged[Stdcall]<IStream*, ULARGE_INTEGER, int>)Marshal.GetFunctionPointerForDelegate(delSetSize);
		vtbl->CopyTo = (delegate* unmanaged[Stdcall]<IStream*, IStream*, ULARGE_INTEGER, ULARGE_INTEGER*, ULARGE_INTEGER*, int>)Marshal.GetFunctionPointerForDelegate(delCopyTo);
		vtbl->Commit = (delegate* unmanaged[Stdcall]<IStream*, uint, int>)Marshal.GetFunctionPointerForDelegate(delCommit);
		vtbl->Revert = (delegate* unmanaged[Stdcall]<IStream*, int>)Marshal.GetFunctionPointerForDelegate(delRevert);
		vtbl->LockRegion = (delegate* unmanaged[Stdcall]<IStream*, ULARGE_INTEGER, ULARGE_INTEGER, uint, int>)Marshal.GetFunctionPointerForDelegate(delLockRegion);
		vtbl->UnlockRegion = (delegate* unmanaged[Stdcall]<IStream*, ULARGE_INTEGER, ULARGE_INTEGER, uint, int>)Marshal.GetFunctionPointerForDelegate(delUnlockRegion);
		vtbl->Stat = (delegate* unmanaged[Stdcall]<IStream*, STATSTG*, uint, int>)Marshal.GetFunctionPointerForDelegate(delStat);
		vtbl->Clone = (delegate* unmanaged[Stdcall]<IStream*, IStream**, int>)Marshal.GetFunctionPointerForDelegate(delClone);
#endif

		return (void**)vtbl;
	}
}