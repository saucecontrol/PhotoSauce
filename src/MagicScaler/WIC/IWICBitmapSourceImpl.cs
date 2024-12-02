// Copyright © Clinton Ingram and Contributors
// SPDX-License-Identifier: MIT

#pragma warning disable IDE0060, IDE0251

using System;
using System.Threading;
using System.Runtime.InteropServices;
using System.Runtime.CompilerServices;

using TerraFX.Interop.Windows;
using static TerraFX.Interop.Windows.E;
using static TerraFX.Interop.Windows.S;
using static TerraFX.Interop.Windows.Windows;

using PhotoSauce.MagicScaler;

namespace PhotoSauce.Interop.Wic;

internal unsafe struct IWICBitmapSourceImpl
{
	private readonly void** lpVtbl;
	private readonly GCHandle source;
	private int refCount;

	private IWICBitmapSourceImpl(PixelSource managedSource)
	{
		lpVtbl = vtblStatic;
		source = GCHandle.Alloc(managedSource, GCHandleType.Weak);
	}

	public static IWICBitmapSource* Wrap(PixelSource managedSource)
	{
		var pinst = UnsafeUtil.NativeAlloc<IWICBitmapSourceImpl>();
		*pinst = new(managedSource);

		return (IWICBitmapSource*)pinst;
	}

#if NET5_0_OR_GREATER
	[UnmanagedCallersOnly(CallConvs = [ typeof(CallConvStdcall) ])]
	static
#endif
	private int queryInterface(IWICBitmapSource* pinst, Guid* riid, void** ppvObject)
	{
		var pthis = (IWICBitmapSourceImpl*)pinst;

		var iid = *riid;
		if (iid == __uuidof<IWICBitmapSource>() || iid == __uuidof<IUnknown>())
		{
			Interlocked.Increment(ref pthis->refCount);
			*ppvObject = pthis;
			return S_OK;
		}

		*ppvObject = null;
		return E_NOINTERFACE;
	}

#if NET5_0_OR_GREATER
	[UnmanagedCallersOnly(CallConvs = [ typeof(CallConvStdcall) ])]
	static
#endif
	private uint addRef(IWICBitmapSource* pinst) => (uint)Interlocked.Increment(ref ((IWICBitmapSourceImpl*)pinst)->refCount);

#if NET5_0_OR_GREATER
	[UnmanagedCallersOnly(CallConvs = [ typeof(CallConvStdcall) ])]
	static
#endif
	private uint release(IWICBitmapSource* pinst)
	{
		var pthis = (IWICBitmapSourceImpl*)pinst;

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
	private int getSize(IWICBitmapSource* pinst, uint* puiWidth, uint* puiHeight)
	{
		var ps = Unsafe.As<PixelSource>(((IWICBitmapSourceImpl*)pinst)->source.Target!);
		*puiWidth = (uint)ps.Width;
		*puiHeight = (uint)ps.Height;

		return S_OK;
	}

#if NET5_0_OR_GREATER
	[UnmanagedCallersOnly(CallConvs = [ typeof(CallConvStdcall) ])]
	static
#endif
	private int getPixelFormat(IWICBitmapSource* pinst, Guid* pPixelFormat)
	{
		var ps = Unsafe.As<PixelSource>(((IWICBitmapSourceImpl*)pinst)->source.Target!);
		*pPixelFormat = ps.Format.FormatGuid;

		return S_OK;
	}

#if NET5_0_OR_GREATER
	[UnmanagedCallersOnly(CallConvs = [ typeof(CallConvStdcall) ])]
	static
#endif
	private int getResolution(IWICBitmapSource* pinst, double* pDpiX, double* pDpiY)
	{
		*pDpiX = 96d;
		*pDpiY = 96d;

		return S_OK;
	}

#if NET5_0_OR_GREATER
	[UnmanagedCallersOnly(CallConvs = [ typeof(CallConvStdcall) ])]
	static
#endif
	private int copyPalette(IWICBitmapSource* pinst, IWICPalette* pIPalette)
	{
		var ps = Unsafe.As<PixelSource>(((IWICBitmapSourceImpl*)pinst)->source.Target!);
		if (ps is not IIndexedPixelSource idxs)
			return E_NOTIMPL;

		var pspan = idxs.Palette;
		fixed (uint* pp = pspan)
			pIPalette->InitializeCustom(pp, (uint)pspan.Length);

		return S_OK;
	}

#if NET5_0_OR_GREATER
	[UnmanagedCallersOnly(CallConvs = [ typeof(CallConvStdcall) ])]
	static
#endif
	private int copyPixels(IWICBitmapSource* pinst, WICRect* prc, uint cbStride, uint cbBufferSize, byte* pbBuffer)
	{
		var ps = Unsafe.As<PixelSource>(((IWICBitmapSourceImpl*)pinst)->source.Target!);
		var area = prc is not null ? new PixelArea(prc->X, prc->Y, prc->Width, prc->Height) : ps.Area;
		ps.CopyPixels(area, checked((int)cbStride), checked((int)cbBufferSize), pbBuffer);

		return S_OK;
	}

#if !NET5_0_OR_GREATER
	[UnmanagedFunctionPointer(CallingConvention.StdCall)] private delegate int QueryInterface(IWICBitmapSource* pinst, Guid* riid, void** ppvObject);
	[UnmanagedFunctionPointer(CallingConvention.StdCall)] private delegate uint AddRef(IWICBitmapSource* pinst);
	[UnmanagedFunctionPointer(CallingConvention.StdCall)] private delegate uint Release(IWICBitmapSource* pinst);
	[UnmanagedFunctionPointer(CallingConvention.StdCall)] private delegate int GetSize(IWICBitmapSource* pinst, uint* puiWidth, uint* puiHeight);
	[UnmanagedFunctionPointer(CallingConvention.StdCall)] private delegate int GetPixelFormat(IWICBitmapSource* pinst, Guid* pPixelFormat);
	[UnmanagedFunctionPointer(CallingConvention.StdCall)] private delegate int GetResolution(IWICBitmapSource* pinst, double* pDpiX, double* pDpiY);
	[UnmanagedFunctionPointer(CallingConvention.StdCall)] private delegate int CopyPalette(IWICBitmapSource* pinst, IWICPalette* pIPalette);
	[UnmanagedFunctionPointer(CallingConvention.StdCall)] private delegate int CopyPixels(IWICBitmapSource* pinst, WICRect* prc, uint cbStride, uint cbBufferSize, byte* pbBuffer);

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
		var vtbl = (IWICBitmapSource.Vtbl<IWICBitmapSource>*)UnsafeUtil.AllocateTypeAssociatedMemory(typeof(IWICBitmapSourceImpl), sizeof(IWICBitmapSource.Vtbl<IWICBitmapSource>));
#if NET5_0_OR_GREATER
		vtbl->QueryInterface = &queryInterface;
		vtbl->AddRef = &addRef;
		vtbl->Release = &release;
		vtbl->GetSize = &getSize;
		vtbl->GetPixelFormat = &getPixelFormat;
		vtbl->GetResolution = &getResolution;
		vtbl->CopyPalette = &copyPalette;
		vtbl->CopyPixels = &copyPixels;
#else
		vtbl->QueryInterface = (delegate* unmanaged[Stdcall]<IWICBitmapSource*, Guid*, void**, int>)Marshal.GetFunctionPointerForDelegate(delQueryInterface);
		vtbl->AddRef = (delegate* unmanaged[Stdcall]<IWICBitmapSource*, uint>)Marshal.GetFunctionPointerForDelegate(delAddRef);
		vtbl->Release = (delegate* unmanaged[Stdcall]<IWICBitmapSource*, uint>)Marshal.GetFunctionPointerForDelegate(delRelease);
		vtbl->GetSize = (delegate* unmanaged[Stdcall]<IWICBitmapSource*, uint*, uint*, int>)Marshal.GetFunctionPointerForDelegate(delGetSize);
		vtbl->GetPixelFormat = (delegate* unmanaged[Stdcall]<IWICBitmapSource*, Guid*, int>)Marshal.GetFunctionPointerForDelegate(delGetPixelFormat);
		vtbl->GetResolution = (delegate* unmanaged[Stdcall]<IWICBitmapSource*, double*, double*, int>)Marshal.GetFunctionPointerForDelegate(delGetResolution);
		vtbl->CopyPalette = (delegate* unmanaged[Stdcall]<IWICBitmapSource*, IWICPalette*, int>)Marshal.GetFunctionPointerForDelegate(delCopyPalette);
		vtbl->CopyPixels = (delegate* unmanaged[Stdcall]<IWICBitmapSource*, WICRect*, uint, uint, byte*, int>)Marshal.GetFunctionPointerForDelegate(delCopyPixels);
#endif

		return (void**)vtbl;
	}
}