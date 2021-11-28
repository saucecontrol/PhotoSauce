// Copyright © Clinton Ingram and Contributors.  Licensed under the MIT License.

#pragma warning disable IDE0060

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
		[UnmanagedCallersOnly]
		static
#endif
		public int QueryInterface(IWICBitmapSourceImpl* pinst, Guid* riid, void** ppvObject)
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
		[UnmanagedCallersOnly]
		static
#endif
		public uint AddRef(IWICBitmapSourceImpl* pinst) => (uint)Interlocked.Increment(ref pinst->refCount);

#if NET5_0_OR_GREATER
		[UnmanagedCallersOnly]
		static
#endif
		public uint Release(IWICBitmapSourceImpl* pinst)
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
		[UnmanagedCallersOnly]
		static
#endif
		public int GetSize(IWICBitmapSourceImpl* pinst, uint* puiWidth, uint* puiHeight)
		{
			var ps = Unsafe.As<PixelSource>(pinst->source.Target!);
			*puiWidth = (uint)ps.Width;
			*puiHeight = (uint)ps.Height;

			return S_OK;
		}

#if NET5_0_OR_GREATER
		[UnmanagedCallersOnly]
		static
#endif
		public int GetPixelFormat(IWICBitmapSourceImpl* pinst, Guid* pPixelFormat)
		{
			var ps = Unsafe.As<PixelSource>(pinst->source.Target!);
			*pPixelFormat = ps.Format.FormatGuid;

			return S_OK;
		}

#if NET5_0_OR_GREATER
		[UnmanagedCallersOnly]
		static
#endif
		public int GetResolution(IWICBitmapSourceImpl* pinst, double* pDpiX, double* pDpiY)
		{
			*pDpiX = 96d;
			*pDpiY = 96d;

			return S_OK;
		}

#if NET5_0_OR_GREATER
		[UnmanagedCallersOnly]
		static
#endif
		public int CopyPalette(IWICBitmapSourceImpl* pinst, IWICPalette* pIPalette) => E_NOTIMPL;

#if NET5_0_OR_GREATER
		[UnmanagedCallersOnly]
		static
#endif
		public int CopyPixels(IWICBitmapSourceImpl* pinst, WICRect* prc, uint cbStride, uint cbBufferSize, byte* pbBuffer)
		{
			var ps = Unsafe.As<PixelSource>(pinst->source.Target!);
			var area = prc is not null ? new PixelArea(prc->X, prc->Y, prc->Width, prc->Height) : ps.Area;
			ps.CopyPixels(area, (int)cbStride, (int)cbBufferSize, (IntPtr)pbBuffer);

			return S_OK;
		}

#if NET5_0_OR_GREATER
		private static readonly delegate* unmanaged<IWICBitmapSourceImpl*, Guid*, void**, int> pfnQueryInterface = &QueryInterface;
		private static readonly delegate* unmanaged<IWICBitmapSourceImpl*, uint> pfnAddRef = &AddRef;
		private static readonly delegate* unmanaged<IWICBitmapSourceImpl*, uint> pfnRelease = &Release;
		private static readonly delegate* unmanaged<IWICBitmapSourceImpl*, uint*, uint*, int> pfnGetSize = &GetSize;
		private static readonly delegate* unmanaged<IWICBitmapSourceImpl*, Guid*, int> pfnGetPixelFormat = &GetPixelFormat;
		private static readonly delegate* unmanaged<IWICBitmapSourceImpl*, double*, double*, int> pfnGetResolution = &GetResolution;
		private static readonly delegate* unmanaged<IWICBitmapSourceImpl*, IWICPalette*, int> pfnCopyPalette = &CopyPalette;
		private static readonly delegate* unmanaged<IWICBitmapSourceImpl*, WICRect*, uint, uint, byte*, int> pfnCopyPixels = &CopyPixels;
#else
		[UnmanagedFunctionPointer(CallingConvention.StdCall)] private delegate int QueryInterfaceDelegate(IWICBitmapSourceImpl* pinst, Guid* riid, void** ppvObject);
		[UnmanagedFunctionPointer(CallingConvention.StdCall)] private delegate uint AddRefDelegate(IWICBitmapSourceImpl* pinst);
		[UnmanagedFunctionPointer(CallingConvention.StdCall)] private delegate uint ReleaseDelegate(IWICBitmapSourceImpl* pinst);
		[UnmanagedFunctionPointer(CallingConvention.StdCall)] private delegate int GetSizeDelegate(IWICBitmapSourceImpl* pinst, uint* puiWidth, uint* puiHeight);
		[UnmanagedFunctionPointer(CallingConvention.StdCall)] private delegate int GetPixelFormatDelegate(IWICBitmapSourceImpl* pinst, Guid* pPixelFormat);
		[UnmanagedFunctionPointer(CallingConvention.StdCall)] private delegate int GetResolutionDelegate(IWICBitmapSourceImpl* pinst, double* pDpiX, double* pDpiY);
		[UnmanagedFunctionPointer(CallingConvention.StdCall)] private delegate int CopyPaletteDelegate(IWICBitmapSourceImpl* pinst, IWICPalette* pIPalette);
		[UnmanagedFunctionPointer(CallingConvention.StdCall)] private delegate int CopyPixelsDelegate(IWICBitmapSourceImpl* pinst, WICRect* prc, uint cbStride, uint cbBufferSize, byte* pbBuffer);

		private static readonly QueryInterfaceDelegate delQueryInterface = typeof(IWICBitmapSourceImpl).CreateMethodDelegate<QueryInterfaceDelegate>(nameof(QueryInterface));
		private static readonly AddRefDelegate delAddRef = typeof(IWICBitmapSourceImpl).CreateMethodDelegate<AddRefDelegate>(nameof(AddRef));
		private static readonly ReleaseDelegate delRelease = typeof(IWICBitmapSourceImpl).CreateMethodDelegate<ReleaseDelegate>(nameof(Release));
		private static readonly GetSizeDelegate delGetSize = typeof(IWICBitmapSourceImpl).CreateMethodDelegate<GetSizeDelegate>(nameof(GetSize));
		private static readonly GetPixelFormatDelegate delGetPixelFormat = typeof(IWICBitmapSourceImpl).CreateMethodDelegate<GetPixelFormatDelegate>(nameof(GetPixelFormat));
		private static readonly GetResolutionDelegate delGetResolution = typeof(IWICBitmapSourceImpl).CreateMethodDelegate<GetResolutionDelegate>(nameof(GetResolution));
		private static readonly CopyPaletteDelegate delCopyPalette = typeof(IWICBitmapSourceImpl).CreateMethodDelegate<CopyPaletteDelegate>(nameof(CopyPalette));
		private static readonly CopyPixelsDelegate delCopyPixels = typeof(IWICBitmapSourceImpl).CreateMethodDelegate<CopyPixelsDelegate>(nameof(CopyPixels));

		private static readonly delegate* unmanaged[Stdcall]<IWICBitmapSourceImpl*, Guid*, void**, int> pfnQueryInterface = (delegate* unmanaged[Stdcall]<IWICBitmapSourceImpl*, Guid*, void**, int>)Marshal.GetFunctionPointerForDelegate(delQueryInterface);
		private static readonly delegate* unmanaged[Stdcall]<IWICBitmapSourceImpl*, uint> pfnAddRef = (delegate* unmanaged[Stdcall]<IWICBitmapSourceImpl*, uint>)Marshal.GetFunctionPointerForDelegate(delAddRef);
		private static readonly delegate* unmanaged[Stdcall]<IWICBitmapSourceImpl*, uint> pfnRelease = (delegate* unmanaged[Stdcall]<IWICBitmapSourceImpl*, uint>)Marshal.GetFunctionPointerForDelegate(delRelease);
		private static readonly delegate* unmanaged[Stdcall]<IWICBitmapSourceImpl*, uint*, uint*, int> pfnGetSize = (delegate* unmanaged[Stdcall]<IWICBitmapSourceImpl*, uint*, uint*, int>)Marshal.GetFunctionPointerForDelegate(delGetSize);
		private static readonly delegate* unmanaged[Stdcall]<IWICBitmapSourceImpl*, Guid*, int> pfnGetPixelFormat = (delegate* unmanaged[Stdcall]<IWICBitmapSourceImpl*, Guid*, int>)Marshal.GetFunctionPointerForDelegate(delGetPixelFormat);
		private static readonly delegate* unmanaged[Stdcall]<IWICBitmapSourceImpl*, double*, double*, int> pfnGetResolution = (delegate* unmanaged[Stdcall]<IWICBitmapSourceImpl*, double*, double*, int>)Marshal.GetFunctionPointerForDelegate(delGetResolution);
		private static readonly delegate* unmanaged[Stdcall]<IWICBitmapSourceImpl*, IWICPalette*, int> pfnCopyPalette = (delegate* unmanaged[Stdcall]<IWICBitmapSourceImpl*, IWICPalette*, int>)Marshal.GetFunctionPointerForDelegate(delCopyPalette);
		private static readonly delegate* unmanaged[Stdcall]<IWICBitmapSourceImpl*, WICRect*, uint, uint, byte*, int> pfnCopyPixels = (delegate* unmanaged[Stdcall]<IWICBitmapSourceImpl*, WICRect*, uint, uint, byte*, int>)Marshal.GetFunctionPointerForDelegate(delCopyPixels);
#endif

		private static readonly void** vtblStatic = createVtbl();

		private static void** createVtbl()
		{
#if NET5_0_OR_GREATER
			void** p = (void**)RuntimeHelpers.AllocateTypeAssociatedMemory(typeof(IWICBitmapSourceImpl), sizeof(nuint) * 8);
#else
			void** p = (void**)Marshal.AllocHGlobal(sizeof(nuint) * 8);
#endif

			p[0] = pfnQueryInterface;
			p[1] = pfnAddRef;
			p[2] = pfnRelease;
			p[3] = pfnGetSize;
			p[4] = pfnGetPixelFormat;
			p[5] = pfnGetResolution;
			p[6] = pfnCopyPalette;
			p[7] = pfnCopyPixels;

			return p;
		}
	}
}