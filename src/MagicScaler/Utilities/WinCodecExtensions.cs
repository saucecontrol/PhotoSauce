using System;
using System.Buffers;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Diagnostics.CodeAnalysis;

namespace PhotoSauce.Interop.Wic
{
	internal static class Wic
	{
		private static IWICColorContext getDefaultColorContext(Guid pixelFormat)
		{
			using var pfi = new ComHandle<IWICPixelFormatInfo>(Factory.CreateComponentInfo(pixelFormat));
			return pfi.ComObject.GetColorContext();
		}

		private static IWICColorContext getResourceColorContext(string name)
		{
			string resName = nameof(PhotoSauce) + "." + nameof(MagicScaler) + ".Resources." + name;
			using var stm = typeof(Wic).GetTypeInfo().Assembly.GetManifestResourceStream(resName)!;
			using var buff = MemoryPool<byte>.Shared.Rent((int)stm.Length);

			var cca = buff.GetOwnedArraySegment((int)stm.Length);
			stm.Read(cca.Array!, cca.Offset, cca.Count);

			var cc = Factory.CreateColorContext();
			cc.InitializeFromMemory(cca.Array!, (uint)cca.Count);
			return cc;
		}

		public static readonly IWICImagingFactory Factory = new WICImagingFactory2() as IWICImagingFactory ?? throw new PlatformNotSupportedException("Windows Imaging Component (WIC) is not available on this platform.");

		public static readonly Lazy<IWICColorContext> CmykContext = new Lazy<IWICColorContext>(() => getDefaultColorContext(Consts.GUID_WICPixelFormat32bppCMYK));
		public static readonly Lazy<IWICColorContext> SrgbContext = new Lazy<IWICColorContext>(() => getResourceColorContext("sRGB-v4.icc"));
		public static readonly Lazy<IWICColorContext> GreyContext = new Lazy<IWICColorContext>(() => getResourceColorContext("sGrey-v4.icc"));
		public static readonly Lazy<IWICColorContext> SrgbCompactContext = new Lazy<IWICColorContext>(() => getResourceColorContext("sRGB-v2-micro.icc"));
		public static readonly Lazy<IWICColorContext> GreyCompactContext = new Lazy<IWICColorContext>(() => getResourceColorContext("sGrey-v2-micro.icc"));
	}

	internal static class WinCodecExtensions
	{
		public static bool RequiresCache(this WICBitmapTransformOptions opt) =>
			opt != WICBitmapTransformOptions.WICBitmapTransformRotate0 && opt != WICBitmapTransformOptions.WICBitmapTransformFlipHorizontal;

		public static bool TryGetPreview(this IWICBitmapDecoder dec, [NotNullWhen(true)] out IWICBitmapSource? pvw)
		{
			int hr = ProxyFunctions.GetPreview(dec, out pvw);
			return hr >= 0;
		}

		public static uint GetColorContextCount(this IWICBitmapFrameDecode frame)
		{
			int hr = ProxyFunctions.GetColorContexts(frame, 0, null, out uint ccc);
			return hr >= 0 ? ccc : 0u;
		}

		public static bool TryGetMetadataQueryReader(this IWICBitmapFrameDecode frame, [NotNullWhen(true)] out IWICMetadataQueryReader? rdr)
		{
			int hr = ProxyFunctions.GetMetadataQueryReader(frame, out rdr);
			return hr >= 0;
		}

		public static bool TryGetMetadataQueryWriter(this IWICBitmapFrameEncode frame, [NotNullWhen(true)] out IWICMetadataQueryWriter? wri)
		{
			int hr = ProxyFunctions.GetMetadataQueryWriter(frame, out wri);
			return hr >= 0;
		}

		public static bool TryGetMetadataByName(this IWICMetadataQueryReader meta, string name, [NotNullWhen(true)] out PropVariant? value)
		{
			value = null;

			int hr = ProxyFunctions.GetMetadataByName(meta, name, null);
			if (hr >= 0)
			{
				value = new PropVariant();
				hr = ProxyFunctions.GetMetadataByName(meta, name, value);
			}

			return hr >= 0;
		}

		public static bool TrySetMetadataByName(this IWICMetadataQueryWriter meta, string name, PropVariant value)
		{
			int hr = ProxyFunctions.SetMetadataByName(meta, name, value);
			return hr >= 0;
		}

		public static bool TryInitialize(this IWICColorTransform trans, IWICBitmapSource source, IWICColorContext ctxSrc, IWICColorContext ctxDest, Guid fmtDest)
		{
			int hr = ProxyFunctions.InitializeColorTransform(trans, source, ctxSrc, ctxDest, fmtDest);
			if (hr < 0 && hr != (int)WinCodecError.ERROR_INVALID_PROFILE)
				Marshal.ThrowExceptionForHR(hr);

			return hr >= 0;
		}

		public static bool TrySetColorContexts(this IWICBitmapFrameEncode frame, params IWICColorContext[] contexts)
		{
			int hr = ProxyFunctions.SetColorContexts(frame, (uint)contexts.Length, contexts);
			return hr >= 0;
		}
	}
}
