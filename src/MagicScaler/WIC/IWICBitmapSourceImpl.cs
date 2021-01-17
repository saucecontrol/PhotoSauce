// Copyright © Clinton Ingram and Contributors.  Licensed under the MIT License.

#pragma warning disable IDE0060

using System;
using System.Diagnostics;
using System.Runtime.InteropServices;

#if BUILTIN_CSHARP9
using System.Runtime.CompilerServices;
#endif

using TerraFX.Interop;
using static TerraFX.Interop.Windows;

using PhotoSauce.MagicScaler;

namespace PhotoSauce.Interop.Wic
{
	internal unsafe struct IWICBitmapSourceImpl
	{
		private readonly void** lpVtbl;
		private readonly IntPtr source;

		public IWICBitmapSourceImpl(GCHandle managedSource)
		{
			Debug.Assert(managedSource.Target is PixelSource);

			lpVtbl = vtblStatic;
			source = GCHandle.ToIntPtr(managedSource);
		}

#if BUILTIN_CSHARP9
		[UnmanagedCallersOnly]
		static
#endif
		public int QueryInterface(IWICBitmapSourceImpl* pinst, Guid* riid, void** ppvObject)
		{
			var iid = *riid;
			if (iid == __uuidof<IWICBitmapSource>() || iid == __uuidof<IUnknown>())
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
		public uint AddRef(IWICBitmapSourceImpl* pinst) => 1;

#if BUILTIN_CSHARP9
		[UnmanagedCallersOnly]
		static
#endif
		public uint Release(IWICBitmapSourceImpl* pinst) => 1;

#if BUILTIN_CSHARP9
		[UnmanagedCallersOnly]
		static
#endif
		public int GetSize(IWICBitmapSourceImpl* pinst, uint* puiWidth, uint* puiHeight)
		{
			var ps = (PixelSource)GCHandle.FromIntPtr(pinst->source).Target!;
			*puiWidth = (uint)ps.Width;
			*puiHeight = (uint)ps.Height;

			return S_OK;
		}

#if BUILTIN_CSHARP9
		[UnmanagedCallersOnly]
		static
#endif
		public int GetPixelFormat(IWICBitmapSourceImpl* pinst, Guid* pPixelFormat)
		{
			var ps = (PixelSource)GCHandle.FromIntPtr(pinst->source).Target!;
			*pPixelFormat = ps.Format.FormatGuid;

			return S_OK;
		}

#if BUILTIN_CSHARP9
		[UnmanagedCallersOnly]
		static
#endif
		public int GetResolution(IWICBitmapSourceImpl* pinst, double* pDpiX, double* pDpiY)
		{
			*pDpiX = 96d;
			*pDpiY = 96d;

			return S_OK;
		}

#if BUILTIN_CSHARP9
		[UnmanagedCallersOnly]
		static
#endif
		public int CopyPalette(IWICBitmapSourceImpl* pinst, IWICPalette* pIPalette) => E_NOTIMPL;

#if BUILTIN_CSHARP9
		[UnmanagedCallersOnly]
		static
#endif
		public int CopyPixels(IWICBitmapSourceImpl* pinst, WICRect* prc, uint cbStride, uint cbBufferSize, byte* pbBuffer)
		{
			var ps = (PixelSource)GCHandle.FromIntPtr(pinst->source).Target!;
			var area = prc is not null ? new PixelArea(prc->X, prc->Y, prc->Width, prc->Height) : ps.Area;
			ps.CopyPixels(area, (int)cbStride, (int)cbBufferSize, (IntPtr)pbBuffer);

			return S_OK;
		}

#if BUILTIN_CSHARP9
		public static readonly delegate* unmanaged<IWICBitmapSourceImpl*, Guid*, void**, int> pfnQueryInterface = &QueryInterface;
		public static readonly delegate* unmanaged<IWICBitmapSourceImpl*, uint> pfnAddRef = &AddRef;
		public static readonly delegate* unmanaged<IWICBitmapSourceImpl*, uint> pfnRelease = &Release;
		public static readonly delegate* unmanaged<IWICBitmapSourceImpl*, uint*, uint*, int> pfnGetSize = &GetSize;
		public static readonly delegate* unmanaged<IWICBitmapSourceImpl*, Guid*, int> pfnGetPixelFormat = &GetPixelFormat;
		public static readonly delegate* unmanaged<IWICBitmapSourceImpl*, double*, double*, int> pfnGetResolution = &GetResolution;
		public static readonly delegate* unmanaged<IWICBitmapSourceImpl*, IWICPalette*, int> pfnCopyPalette = &CopyPalette;
		public static readonly delegate* unmanaged<IWICBitmapSourceImpl*, WICRect*, uint, uint, byte*, int> pfnCopyPixels = &CopyPixels;
#else
		[UnmanagedFunctionPointer(CallingConvention.StdCall)] public delegate int QueryInterfaceDelegate(IWICBitmapSourceImpl* pinst, Guid* riid, void** ppvObject);
		[UnmanagedFunctionPointer(CallingConvention.StdCall)] public delegate uint AddRefDelegate(IWICBitmapSourceImpl* pinst);
		[UnmanagedFunctionPointer(CallingConvention.StdCall)] public delegate uint ReleaseDelegate(IWICBitmapSourceImpl* pinst);
		[UnmanagedFunctionPointer(CallingConvention.StdCall)] public delegate int GetSizeDelegate(IWICBitmapSourceImpl* pinst, uint* puiWidth, uint* puiHeight);
		[UnmanagedFunctionPointer(CallingConvention.StdCall)] public delegate int GetPixelFormatDelegate(IWICBitmapSourceImpl* pinst, Guid* pPixelFormat);
		[UnmanagedFunctionPointer(CallingConvention.StdCall)] public delegate int GetResolutionDelegate(IWICBitmapSourceImpl* pinst, double* pDpiX, double* pDpiY);
		[UnmanagedFunctionPointer(CallingConvention.StdCall)] public delegate int CopyPaletteDelegate(IWICBitmapSourceImpl* pinst, IWICPalette* pIPalette);
		[UnmanagedFunctionPointer(CallingConvention.StdCall)] public delegate int CopyPixelsDelegate(IWICBitmapSourceImpl* pinst, WICRect* prc, uint cbStride, uint cbBufferSize, byte* pbBuffer);

		public static readonly QueryInterfaceDelegate delQueryInterface = (QueryInterfaceDelegate)typeof(IWICBitmapSourceImpl).GetMethod(nameof(QueryInterface))!.CreateDelegate(typeof(QueryInterfaceDelegate), null);
		public static readonly AddRefDelegate delAddRef = (AddRefDelegate)typeof(IWICBitmapSourceImpl).GetMethod(nameof(AddRef))!.CreateDelegate(typeof(AddRefDelegate), null);
		public static readonly ReleaseDelegate delRelease = (ReleaseDelegate)typeof(IWICBitmapSourceImpl).GetMethod(nameof(Release))!.CreateDelegate(typeof(ReleaseDelegate), null);
		public static readonly GetSizeDelegate delGetSize = (GetSizeDelegate)typeof(IWICBitmapSourceImpl).GetMethod(nameof(GetSize))!.CreateDelegate(typeof(GetSizeDelegate), null);
		public static readonly GetPixelFormatDelegate delGetPixelFormat = (GetPixelFormatDelegate)typeof(IWICBitmapSourceImpl).GetMethod(nameof(GetPixelFormat))!.CreateDelegate(typeof(GetPixelFormatDelegate), null);
		public static readonly GetResolutionDelegate delGetResolution = (GetResolutionDelegate)typeof(IWICBitmapSourceImpl).GetMethod(nameof(GetResolution))!.CreateDelegate(typeof(GetResolutionDelegate), null);
		public static readonly CopyPaletteDelegate delCopyPalette = (CopyPaletteDelegate)typeof(IWICBitmapSourceImpl).GetMethod(nameof(CopyPalette))!.CreateDelegate(typeof(CopyPaletteDelegate), null);
		public static readonly CopyPixelsDelegate delCopyPixels = (CopyPixelsDelegate)typeof(IWICBitmapSourceImpl).GetMethod(nameof(CopyPixels))!.CreateDelegate(typeof(CopyPixelsDelegate), null);

		public static readonly delegate* unmanaged[Stdcall]<IWICBitmapSourceImpl*, Guid*, void**, int> pfnQueryInterface = (delegate* unmanaged[Stdcall]<IWICBitmapSourceImpl*, Guid*, void**, int>)Marshal.GetFunctionPointerForDelegate(delQueryInterface);
		public static readonly delegate* unmanaged[Stdcall]<IWICBitmapSourceImpl*, uint> pfnAddRef = (delegate* unmanaged[Stdcall]<IWICBitmapSourceImpl*, uint>)Marshal.GetFunctionPointerForDelegate(delAddRef);
		public static readonly delegate* unmanaged[Stdcall]<IWICBitmapSourceImpl*, uint> pfnRelease = (delegate* unmanaged[Stdcall]<IWICBitmapSourceImpl*, uint>)Marshal.GetFunctionPointerForDelegate(delRelease);
		public static readonly delegate* unmanaged[Stdcall]<IWICBitmapSourceImpl*, uint*, uint*, int> pfnGetSize = (delegate* unmanaged[Stdcall]<IWICBitmapSourceImpl*, uint*, uint*, int>)Marshal.GetFunctionPointerForDelegate(delGetSize);
		public static readonly delegate* unmanaged[Stdcall]<IWICBitmapSourceImpl*, Guid*, int> pfnGetPixelFormat = (delegate* unmanaged[Stdcall]<IWICBitmapSourceImpl*, Guid*, int>)Marshal.GetFunctionPointerForDelegate(delGetPixelFormat);
		public static readonly delegate* unmanaged[Stdcall]<IWICBitmapSourceImpl*, double*, double*, int> pfnGetResolution = (delegate* unmanaged[Stdcall]<IWICBitmapSourceImpl*, double*, double*, int>)Marshal.GetFunctionPointerForDelegate(delGetResolution);
		public static readonly delegate* unmanaged[Stdcall]<IWICBitmapSourceImpl*, IWICPalette*, int> pfnCopyPalette = (delegate* unmanaged[Stdcall]<IWICBitmapSourceImpl*, IWICPalette*, int>)Marshal.GetFunctionPointerForDelegate(delCopyPalette);
		public static readonly delegate* unmanaged[Stdcall]<IWICBitmapSourceImpl*, WICRect*, uint, uint, byte*, int> pfnCopyPixels = (delegate* unmanaged[Stdcall]<IWICBitmapSourceImpl*, WICRect*, uint, uint, byte*, int>)Marshal.GetFunctionPointerForDelegate(delCopyPixels);
#endif

		public static void** vtblStatic = createVtbl();

		private static void** createVtbl()
		{
#if BUILTIN_CSHARP9
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