// Copyright © Clinton Ingram and Contributors.  Licensed under the MIT License.

#pragma warning disable IDE0060, CS3016

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
#if NET6_0_OR_GREATER
			var ptr = (IStreamImpl*)NativeMemory.Alloc((nuint)sizeof(IStreamImpl));
#else
			var ptr = (IStreamImpl*)Marshal.AllocHGlobal(sizeof(IStreamImpl));
#endif
			*ptr = new IStreamImpl(managedSource, offs);

			return (IStream*)ptr;
		}

#if NET5_0_OR_GREATER
		[UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvStdcall) })]
		static
#endif
		private int queryInterface(IStreamImpl* pinst, Guid* riid, void** ppvObject)
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
		[UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvStdcall) })]
		static
#endif
		private uint addRef(IStreamImpl* pinst) => (uint)Interlocked.Increment(ref pinst->refCount);

#if NET5_0_OR_GREATER
		[UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvStdcall) })]
		static
#endif
		private uint release(IStreamImpl* pinst)
		{
			uint cnt = (uint)Interlocked.Decrement(ref pinst->refCount);
			if (cnt == 0)
			{
				pinst->source.Free();
				*pinst = default;
#if NET6_0_OR_GREATER
				NativeMemory.Free(pinst);
#else
				Marshal.FreeHGlobal((IntPtr)pinst);
#endif
			}

			return cnt;
		}

#if NET5_0_OR_GREATER
		[UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvStdcall) })]
		static
#endif
		private int read(IStreamImpl* pinst, void* pv, uint cb, uint* pcbRead)
		{
			var stm = Unsafe.As<Stream>(pinst->source.Target!);
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
				var buff = new Span<byte>(pv, (int)cb);

				int rb;
				do
				{
					rb = stm.Read(buff);
					buff = buff.Slice(rb);
				}
				while (rb != 0 && buff.Length != 0);

				read = cb - (uint)buff.Length;
			}

			if (pcbRead is not null)
				*pcbRead = read;

			return read == cb ? S_OK : S_FALSE;
		}

#if NET5_0_OR_GREATER
		[UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvStdcall) })]
		static
#endif
		private int write(IStreamImpl* pinst, void* pv, uint cb, uint* pcbWritten)
		{
			var stm = Unsafe.As<Stream>(pinst->source.Target!);

			if (cb == 1)
				stm.WriteByte(*(byte*)pv);
			else
				stm.Write(new ReadOnlySpan<byte>(pv, (int)cb));

			if (pcbWritten is not null)
				*pcbWritten = cb;

			return S_OK;
		}

#if NET5_0_OR_GREATER
		[UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvStdcall) })]
		static
#endif
		private int seek(IStreamImpl* pinst, LARGE_INTEGER dlibMove, uint dwOrigin, ULARGE_INTEGER* plibNewPosition)
		{
			var stm = Unsafe.As<Stream>(pinst->source.Target!);
			long npos = dlibMove.QuadPart;

			if (dwOrigin == (uint)SeekOrigin.Begin)
			{
				// WIC PNG bug per https://twitter.com/rickbrewPDN/status/1123796693777092609 -- fixed in Win10 1909?
				// Negative position might mean an overflowed int was sign entended. As long as stream length would fit in 32 bits, zero extend instead.
				if (npos < 0 && stm.Length > int.MaxValue && stm.Length <= uint.MaxValue)
					npos = (uint)npos;

				npos += pinst->offset;
			}

			long cpos = stm.Position;
			if (!(dwOrigin == (uint)SeekOrigin.Current && npos == 0) && !(dwOrigin == (uint)SeekOrigin.Begin && npos == cpos))
				cpos = stm.Seek(npos, (SeekOrigin)dwOrigin);

			if (plibNewPosition is not null)
				plibNewPosition->QuadPart = (ulong)cpos - pinst->offset;

			return S_OK;
		}

#if NET5_0_OR_GREATER
		[UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvStdcall) })]
		static
#endif
		private int setSize(IStreamImpl* pinst, ULARGE_INTEGER libNewSize)
		{
			var stm = Unsafe.As<Stream>(pinst->source.Target!);
			stm.SetLength((long)libNewSize.QuadPart + pinst->offset);

			return S_OK;
		}

#if NET5_0_OR_GREATER
		[UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvStdcall) })]
		static
#endif
		private int copyTo(IStreamImpl* pinst, IStream* pstm, ULARGE_INTEGER cb, ULARGE_INTEGER* pcbRead, ULARGE_INTEGER* pcbWritten) => E_NOTIMPL;

#if NET5_0_OR_GREATER
		[UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvStdcall) })]
		static
#endif
		private int commit(IStreamImpl* pinst, uint grfCommitFlags)
		{
			var stm = Unsafe.As<Stream>(pinst->source.Target!);
			stm.Flush();

			return S_OK;
		}

#if NET5_0_OR_GREATER
		[UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvStdcall) })]
		static
#endif
		private int revert(IStreamImpl* pinst) => E_NOTIMPL;

#if NET5_0_OR_GREATER
		[UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvStdcall) })]
		static
#endif
		private int lockRegion(IStreamImpl* pinst, ULARGE_INTEGER libOffset, ULARGE_INTEGER cb, uint dwLockType) => E_NOTIMPL;

#if NET5_0_OR_GREATER
		[UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvStdcall) })]
		static
#endif
		private int unlockRegion(IStreamImpl* pinst, ULARGE_INTEGER libOffset, ULARGE_INTEGER cb, uint dwLockType) => E_NOTIMPL;

#if NET5_0_OR_GREATER
		[UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvStdcall) })]
		static
#endif
		private int stat(IStreamImpl* pinst, STATSTG* pstatstg, uint grfStatFlag)
		{
			var stm = Unsafe.As<Stream>(pinst->source.Target!);
			*pstatstg = new STATSTG { cbSize = new ULARGE_INTEGER { QuadPart = (ulong)(stm.Length - pinst->offset) }, type = (uint)STGTY.STGTY_STREAM };

			return S_OK;
		}

#if NET5_0_OR_GREATER
		[UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvStdcall) })]
		static
#endif
		private int clone(IStreamImpl* pinst, IStream** ppstm) => E_NOTIMPL;

#if !NET5_0_OR_GREATER
		[UnmanagedFunctionPointer(CallingConvention.StdCall)] private delegate int QueryInterface(IStreamImpl* pinst, Guid* riid, void** ppvObject);
		[UnmanagedFunctionPointer(CallingConvention.StdCall)] private delegate uint AddRef(IStreamImpl* pinst);
		[UnmanagedFunctionPointer(CallingConvention.StdCall)] private delegate uint Release(IStreamImpl* pinst);
		[UnmanagedFunctionPointer(CallingConvention.StdCall)] private delegate int Read(IStreamImpl* pinst, void* pv, uint cb, uint* pcbRead);
		[UnmanagedFunctionPointer(CallingConvention.StdCall)] private delegate int Write(IStreamImpl* pinst, void* pv, uint cb, uint* pcbWritten);
		[UnmanagedFunctionPointer(CallingConvention.StdCall)] private delegate int Seek(IStreamImpl* pinst, LARGE_INTEGER dlibMove, uint dwOrigin, ULARGE_INTEGER* plibNewPosition);
		[UnmanagedFunctionPointer(CallingConvention.StdCall)] private delegate int SetSize(IStreamImpl* pinst, ULARGE_INTEGER libNewSize);
		[UnmanagedFunctionPointer(CallingConvention.StdCall)] private delegate int CopyTo(IStreamImpl* pinst, IStream* pstm, ULARGE_INTEGER cb, ULARGE_INTEGER* pcbRead, ULARGE_INTEGER* pcbWritten);
		[UnmanagedFunctionPointer(CallingConvention.StdCall)] private delegate int Commit(IStreamImpl* pinst, uint grfCommitFlags);
		[UnmanagedFunctionPointer(CallingConvention.StdCall)] private delegate int Revert(IStreamImpl* pinst);
		[UnmanagedFunctionPointer(CallingConvention.StdCall)] private delegate int LockRegion(IStreamImpl* pinst, ULARGE_INTEGER libOffset, ULARGE_INTEGER cb, uint dwLockType);
		[UnmanagedFunctionPointer(CallingConvention.StdCall)] private delegate int UnlockRegion(IStreamImpl* pinst, ULARGE_INTEGER libOffset, ULARGE_INTEGER cb, uint dwLockType);
		[UnmanagedFunctionPointer(CallingConvention.StdCall)] private delegate int Stat(IStreamImpl* pinst, STATSTG* pstatstg, uint grfStatFlag);
		[UnmanagedFunctionPointer(CallingConvention.StdCall)] private delegate int Clone(IStreamImpl* pinst, IStream** ppstm);

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
#if NET5_0_OR_GREATER
			var vtbl = (Vtbl*)RuntimeHelpers.AllocateTypeAssociatedMemory(typeof(IStreamImpl), sizeof(Vtbl));
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
			var vtbl = (Vtbl*)Marshal.AllocHGlobal(sizeof(Vtbl));
			vtbl->QueryInterface = (delegate* unmanaged[Stdcall]<IStreamImpl*, Guid*, void**, int>)Marshal.GetFunctionPointerForDelegate(delQueryInterface);
			vtbl->AddRef = (delegate* unmanaged[Stdcall]<IStreamImpl*, uint>)Marshal.GetFunctionPointerForDelegate(delAddRef);
			vtbl->Release = (delegate* unmanaged[Stdcall]<IStreamImpl*, uint>)Marshal.GetFunctionPointerForDelegate(delRelease);
			vtbl->Read = (delegate* unmanaged[Stdcall]<IStreamImpl*, void*, uint, uint*, int>)Marshal.GetFunctionPointerForDelegate(delRead);
			vtbl->Write = (delegate* unmanaged[Stdcall]<IStreamImpl*, void*, uint, uint*, int>)Marshal.GetFunctionPointerForDelegate(delWrite);
			vtbl->Seek = (delegate* unmanaged[Stdcall]<IStreamImpl*, LARGE_INTEGER, uint, ULARGE_INTEGER*, int>)Marshal.GetFunctionPointerForDelegate(delSeek);
			vtbl->SetSize = (delegate* unmanaged[Stdcall]<IStreamImpl*, ULARGE_INTEGER, int>)Marshal.GetFunctionPointerForDelegate(delSetSize);
			vtbl->CopyTo = (delegate* unmanaged[Stdcall]<IStreamImpl*, IStream*, ULARGE_INTEGER, ULARGE_INTEGER*, ULARGE_INTEGER*, int>)Marshal.GetFunctionPointerForDelegate(delCopyTo);
			vtbl->Commit = (delegate* unmanaged[Stdcall]<IStreamImpl*, uint, int>)Marshal.GetFunctionPointerForDelegate(delCommit);
			vtbl->Revert = (delegate* unmanaged[Stdcall]<IStreamImpl*, int>)Marshal.GetFunctionPointerForDelegate(delRevert);
			vtbl->LockRegion = (delegate* unmanaged[Stdcall]<IStreamImpl*, ULARGE_INTEGER, ULARGE_INTEGER, uint, int>)Marshal.GetFunctionPointerForDelegate(delLockRegion);
			vtbl->UnlockRegion = (delegate* unmanaged[Stdcall]<IStreamImpl*, ULARGE_INTEGER, ULARGE_INTEGER, uint, int>)Marshal.GetFunctionPointerForDelegate(delUnlockRegion);
			vtbl->Stat = (delegate* unmanaged[Stdcall]<IStreamImpl*, STATSTG*, uint, int>)Marshal.GetFunctionPointerForDelegate(delStat);
			vtbl->Clone = (delegate* unmanaged[Stdcall]<IStreamImpl*, IStream**, int>)Marshal.GetFunctionPointerForDelegate(delClone);
#endif

			return (void**)vtbl;
		}

		public partial struct Vtbl
		{
			[NativeTypeName("HRESULT (const IID &, void **) __attribute__((stdcall))")]
			public delegate* unmanaged[Stdcall]<IStreamImpl*, Guid*, void**, int> QueryInterface;

			[NativeTypeName("ULONG () __attribute__((stdcall))")]
			public delegate* unmanaged[Stdcall]<IStreamImpl*, uint> AddRef;

			[NativeTypeName("ULONG () __attribute__((stdcall))")]
			public delegate* unmanaged[Stdcall]<IStreamImpl*, uint> Release;

			[NativeTypeName("HRESULT (void *, ULONG, ULONG *) __attribute__((stdcall))")]
			public delegate* unmanaged[Stdcall]<IStreamImpl*, void*, uint, uint*, int> Read;

			[NativeTypeName("HRESULT (const void *, ULONG, ULONG *) __attribute__((stdcall))")]
			public delegate* unmanaged[Stdcall]<IStreamImpl*, void*, uint, uint*, int> Write;

			[NativeTypeName("HRESULT (LARGE_INTEGER, DWORD, ULARGE_INTEGER *) __attribute__((stdcall))")]
			public delegate* unmanaged[Stdcall]<IStreamImpl*, LARGE_INTEGER, uint, ULARGE_INTEGER*, int> Seek;

			[NativeTypeName("HRESULT (ULARGE_INTEGER) __attribute__((stdcall))")]
			public delegate* unmanaged[Stdcall]<IStreamImpl*, ULARGE_INTEGER, int> SetSize;

			[NativeTypeName("HRESULT (IStream *, ULARGE_INTEGER, ULARGE_INTEGER *, ULARGE_INTEGER *) __attribute__((stdcall))")]
			public delegate* unmanaged[Stdcall]<IStreamImpl*, IStream*, ULARGE_INTEGER, ULARGE_INTEGER*, ULARGE_INTEGER*, int> CopyTo;

			[NativeTypeName("HRESULT (DWORD) __attribute__((stdcall))")]
			public delegate* unmanaged[Stdcall]<IStreamImpl*, uint, int> Commit;

			[NativeTypeName("HRESULT () __attribute__((stdcall))")]
			public delegate* unmanaged[Stdcall]<IStreamImpl*, int> Revert;

			[NativeTypeName("HRESULT (ULARGE_INTEGER, ULARGE_INTEGER, DWORD) __attribute__((stdcall))")]
			public delegate* unmanaged[Stdcall]<IStreamImpl*, ULARGE_INTEGER, ULARGE_INTEGER, uint, int> LockRegion;

			[NativeTypeName("HRESULT (ULARGE_INTEGER, ULARGE_INTEGER, DWORD) __attribute__((stdcall))")]
			public delegate* unmanaged[Stdcall]<IStreamImpl*, ULARGE_INTEGER, ULARGE_INTEGER, uint, int> UnlockRegion;

			[NativeTypeName("HRESULT (STATSTG *, DWORD) __attribute__((stdcall))")]
			public delegate* unmanaged[Stdcall]<IStreamImpl*, STATSTG*, uint, int> Stat;

			[NativeTypeName("HRESULT (IStream **) __attribute__((stdcall))")]
			public delegate* unmanaged[Stdcall]<IStreamImpl*, IStream**, int> Clone;
		}
	}
}