// Copyright © Clinton Ingram and Contributors.  Licensed under the MIT License.

#pragma warning disable IDE0060, CS3016

using System;
using System.Threading;
using System.Runtime.InteropServices;
using System.Runtime.CompilerServices;

using TerraFX.Interop;
using static TerraFX.Interop.Windows;

using PhotoSauce.MagicScaler;

namespace PhotoSauce.Interop.Wic
{
	internal unsafe struct IWICBitmapSourceImpl
	{
		private readonly void** lpVtbl;
		private readonly GCHandle source;
		private int refCount;

		private IWICBitmapSourceImpl(PixelSource managedSource)
		{
			lpVtbl = vtblStatic;
			source = GCHandle.Alloc(managedSource, GCHandleType.Weak);
			refCount = 0;
		}

		public static IWICBitmapSource* Wrap(PixelSource managedSource)
		{
#if NET6_0_OR_GREATER
			var ptr = (IWICBitmapSourceImpl*)NativeMemory.Alloc((nuint)sizeof(IWICBitmapSourceImpl));
#else
			var ptr = (IWICBitmapSourceImpl*)Marshal.AllocHGlobal(sizeof(IWICBitmapSourceImpl));
#endif
			*ptr = new IWICBitmapSourceImpl(managedSource);

			return (IWICBitmapSource*)ptr;
		}

#if NET5_0_OR_GREATER
		[UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvStdcall) })]
		static
#endif
		private int queryInterface(IWICBitmapSourceImpl* pinst, Guid* riid, void** ppvObject)
		{
			var iid = *riid;
			if (iid == __uuidof<IWICBitmapSource>() || iid == __uuidof<IUnknown>())
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
		private uint addRef(IWICBitmapSourceImpl* pinst) => (uint)Interlocked.Increment(ref pinst->refCount);

#if NET5_0_OR_GREATER
		[UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvStdcall) })]
		static
#endif
		private uint release(IWICBitmapSourceImpl* pinst)
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
		private int getSize(IWICBitmapSourceImpl* pinst, uint* puiWidth, uint* puiHeight)
		{
			var ps = Unsafe.As<PixelSource>(pinst->source.Target!);
			*puiWidth = (uint)ps.Width;
			*puiHeight = (uint)ps.Height;

			return S_OK;
		}

#if NET5_0_OR_GREATER
		[UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvStdcall) })]
		static
#endif
		private int getPixelFormat(IWICBitmapSourceImpl* pinst, Guid* pPixelFormat)
		{
			var ps = Unsafe.As<PixelSource>(pinst->source.Target!);
			*pPixelFormat = ps.Format.FormatGuid;

			return S_OK;
		}

#if NET5_0_OR_GREATER
		[UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvStdcall) })]
		static
#endif
		private int getResolution(IWICBitmapSourceImpl* pinst, double* pDpiX, double* pDpiY)
		{
			*pDpiX = 96d;
			*pDpiY = 96d;

			return S_OK;
		}

#if NET5_0_OR_GREATER
		[UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvStdcall) })]
		static
#endif
		private int copyPalette(IWICBitmapSourceImpl* pinst, IWICPalette* pIPalette) => E_NOTIMPL;

#if NET5_0_OR_GREATER
		[UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvStdcall) })]
		static
#endif
		private int copyPixels(IWICBitmapSourceImpl* pinst, WICRect* prc, uint cbStride, uint cbBufferSize, byte* pbBuffer)
		{
			var ps = Unsafe.As<PixelSource>(pinst->source.Target!);
			var area = prc is not null ? new PixelArea(prc->X, prc->Y, prc->Width, prc->Height) : ps.Area;
			ps.CopyPixels(area, (int)cbStride, (int)cbBufferSize, pbBuffer);

			return S_OK;
		}

#if !NET5_0_OR_GREATER
		[UnmanagedFunctionPointer(CallingConvention.StdCall)] private delegate int QueryInterface(IWICBitmapSourceImpl* pinst, Guid* riid, void** ppvObject);
		[UnmanagedFunctionPointer(CallingConvention.StdCall)] private delegate uint AddRef(IWICBitmapSourceImpl* pinst);
		[UnmanagedFunctionPointer(CallingConvention.StdCall)] private delegate uint Release(IWICBitmapSourceImpl* pinst);
		[UnmanagedFunctionPointer(CallingConvention.StdCall)] private delegate int GetSize(IWICBitmapSourceImpl* pinst, uint* puiWidth, uint* puiHeight);
		[UnmanagedFunctionPointer(CallingConvention.StdCall)] private delegate int GetPixelFormat(IWICBitmapSourceImpl* pinst, Guid* pPixelFormat);
		[UnmanagedFunctionPointer(CallingConvention.StdCall)] private delegate int GetResolution(IWICBitmapSourceImpl* pinst, double* pDpiX, double* pDpiY);
		[UnmanagedFunctionPointer(CallingConvention.StdCall)] private delegate int CopyPalette(IWICBitmapSourceImpl* pinst, IWICPalette* pIPalette);
		[UnmanagedFunctionPointer(CallingConvention.StdCall)] private delegate int CopyPixels(IWICBitmapSourceImpl* pinst, WICRect* prc, uint cbStride, uint cbBufferSize, byte* pbBuffer);

		private static readonly QueryInterface delQueryInterface = typeof(IWICBitmapSourceImpl).CreateMethodDelegate<QueryInterface>(nameof(queryInterface));
		private static readonly AddRef delAddRef = typeof(IWICBitmapSourceImpl).CreateMethodDelegate<AddRef>(nameof(addRef));
		private static readonly Release delRelease = typeof(IWICBitmapSourceImpl).CreateMethodDelegate<Release>(nameof(release));
		private static readonly GetSize delGetSize = typeof(IWICBitmapSourceImpl).CreateMethodDelegate<GetSize>(nameof(getSize));
		private static readonly GetPixelFormat delGetPixelFormat = typeof(IWICBitmapSourceImpl).CreateMethodDelegate<GetPixelFormat>(nameof(getPixelFormat));
		private static readonly GetResolution delGetResolution = typeof(IWICBitmapSourceImpl).CreateMethodDelegate<GetResolution>(nameof(getResolution));
		private static readonly CopyPalette delCopyPalette = typeof(IWICBitmapSourceImpl).CreateMethodDelegate<CopyPalette>(nameof(copyPalette));
		private static readonly CopyPixels delCopyPixels = typeof(IWICBitmapSourceImpl).CreateMethodDelegate<CopyPixels>(nameof(copyPixels));
#endif

		private static readonly void** vtblStatic = createVtbl();

		private static void** createVtbl()
		{
#if NET5_0_OR_GREATER
			var vtbl = (Vtbl*)RuntimeHelpers.AllocateTypeAssociatedMemory(typeof(IWICBitmapSourceImpl), sizeof(Vtbl));
			vtbl->QueryInterface = &queryInterface;
			vtbl->AddRef = &addRef;
			vtbl->Release = &release;
			vtbl->GetSize = &getSize;
			vtbl->GetPixelFormat = &getPixelFormat;
			vtbl->GetResolution = &getResolution;
			vtbl->CopyPalette = &copyPalette;
			vtbl->CopyPixels = &copyPixels;
#else
			var vtbl = (Vtbl*)Marshal.AllocHGlobal(sizeof(Vtbl));
			vtbl->QueryInterface = (delegate* unmanaged[Stdcall]<IWICBitmapSourceImpl*, Guid*, void**, int>)Marshal.GetFunctionPointerForDelegate(delQueryInterface);
			vtbl->AddRef = (delegate* unmanaged[Stdcall]<IWICBitmapSourceImpl*, uint>)Marshal.GetFunctionPointerForDelegate(delAddRef);
			vtbl->Release = (delegate* unmanaged[Stdcall]<IWICBitmapSourceImpl*, uint>)Marshal.GetFunctionPointerForDelegate(delRelease);
			vtbl->GetSize = (delegate* unmanaged[Stdcall]<IWICBitmapSourceImpl*, uint*, uint*, int>)Marshal.GetFunctionPointerForDelegate(delGetSize);
			vtbl->GetPixelFormat = (delegate* unmanaged[Stdcall]<IWICBitmapSourceImpl*, Guid*, int>)Marshal.GetFunctionPointerForDelegate(delGetPixelFormat);
			vtbl->GetResolution = (delegate* unmanaged[Stdcall]<IWICBitmapSourceImpl*, double*, double*, int>)Marshal.GetFunctionPointerForDelegate(delGetResolution);
			vtbl->CopyPalette = (delegate* unmanaged[Stdcall]<IWICBitmapSourceImpl*, IWICPalette*, int>)Marshal.GetFunctionPointerForDelegate(delCopyPalette);
			vtbl->CopyPixels = (delegate* unmanaged[Stdcall]<IWICBitmapSourceImpl*, WICRect*, uint, uint, byte*, int>)Marshal.GetFunctionPointerForDelegate(delCopyPixels);
#endif

			return (void**)vtbl;
		}

		public partial struct Vtbl
		{
			[NativeTypeName("HRESULT (const IID &, void **) __attribute__((stdcall))")]
			public delegate* unmanaged[Stdcall]<IWICBitmapSourceImpl*, Guid*, void**, int> QueryInterface;

			[NativeTypeName("ULONG () __attribute__((stdcall))")]
			public delegate* unmanaged[Stdcall]<IWICBitmapSourceImpl*, uint> AddRef;

			[NativeTypeName("ULONG () __attribute__((stdcall))")]
			public delegate* unmanaged[Stdcall]<IWICBitmapSourceImpl*, uint> Release;

			[NativeTypeName("HRESULT (UINT *, UINT *) __attribute__((stdcall))")]
			public delegate* unmanaged[Stdcall]<IWICBitmapSourceImpl*, uint*, uint*, int> GetSize;

			[NativeTypeName("HRESULT (WICPixelFormatGUID *) __attribute__((stdcall))")]
			public delegate* unmanaged[Stdcall]<IWICBitmapSourceImpl*, Guid*, int> GetPixelFormat;

			[NativeTypeName("HRESULT (double *, double *) __attribute__((stdcall))")]
			public delegate* unmanaged[Stdcall]<IWICBitmapSourceImpl*, double*, double*, int> GetResolution;

			[NativeTypeName("HRESULT (IWICPalette *) __attribute__((stdcall))")]
			public delegate* unmanaged[Stdcall]<IWICBitmapSourceImpl*, IWICPalette*, int> CopyPalette;

			[NativeTypeName("HRESULT (const WICRect *, UINT, UINT, BYTE *) __attribute__((stdcall))")]
			public delegate* unmanaged[Stdcall]<IWICBitmapSourceImpl*, WICRect*, uint, uint, byte*, int> CopyPixels;
		}
	}
}