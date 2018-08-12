using System;
using System.Buffers;
using System.Reflection;
using System.Runtime.InteropServices;

namespace PhotoSauce.MagicScaler.Interop
{
	internal static class Wic
	{
		private static IWICColorContext getDefaultColorContext(Guid pixelFormat)
		{
			var pfi = Factory.CreateComponentInfo(pixelFormat) as IWICPixelFormatInfo;
			var cc = pfi.GetColorContext();
			Marshal.ReleaseComObject(pfi);

			return cc;
		}

		private static IWICColorContext getResourceColorContext(string name)
		{
			string resName = nameof(PhotoSauce) + "." + nameof(MagicScaler) + ".Resources." + name;
			var asm = typeof(IWICColorContext).GetTypeInfo().Assembly;
			using (var stm = asm.GetManifestResourceStream(resName))
			{
				var cc = Factory.CreateColorContext();

				byte[] prof = ArrayPool<byte>.Shared.Rent((int)stm.Length);
				stm.Read(prof, 0, (int)stm.Length);
				cc.InitializeFromMemory(prof, (uint)stm.Length);
				ArrayPool<byte>.Shared.Return(prof);

				return cc;
			}
		}

		public static readonly IWICImagingFactory Factory = new WICImagingFactory2() as IWICImagingFactory;

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

		public static bool TryGetPreview(this IWICBitmapDecoder dec, out IWICBitmapSource pvw)
		{
			int hr = ProxyFunctions.GetPreview(dec, out pvw);
			return hr >= 0;
		}

		public static uint GetColorContextCount(this IWICBitmapFrameDecode frame)
		{
			int hr = ProxyFunctions.GetColorContexts(frame, 0, null, out uint ccc);
			return hr >= 0 ? ccc : 0u;
		}

		public static bool TryGetMetadataQueryReader(this IWICBitmapFrameDecode frame, out IWICMetadataQueryReader rdr)
		{
			int hr = ProxyFunctions.GetMetadataQueryReader(frame, out rdr);
			return hr >= 0;
		}

		public static bool TryGetMetadataQueryWriter(this IWICBitmapFrameEncode frame, out IWICMetadataQueryWriter wri)
		{
			int hr = ProxyFunctions.GetMetadataQueryWriter(frame, out wri);
			return hr >= 0;
		}

		public static bool TryGetMetadataByName(this IWICMetadataQueryReader meta, string name, out PropVariant value)
		{
			value = null;

			int hr = ProxyFunctions.GetMetadataByName(meta, name, IntPtr.Zero);
			if (hr >= 0)
			{
				value = new PropVariant();

				var pvMarshal = new PropVariant.Marshaler();
				var pvNative = pvMarshal.MarshalManagedToNative(value);
				hr = ProxyFunctions.GetMetadataByName(meta, name, pvNative);
				pvMarshal.MarshalNativeToManaged(pvNative);
				pvMarshal.CleanUpNativeData(pvNative);
			}

			return hr >= 0;
		}

		public static bool TrySetMetadataByName(this IWICMetadataQueryWriter meta, string name, PropVariant value)
		{
			var pvMarshal = new PropVariant.Marshaler();
			var pvNative = pvMarshal.MarshalManagedToNative(value);
			int hr = ProxyFunctions.SetMetadataByName(meta, name, pvNative);
			pvMarshal.CleanUpNativeData(pvNative);

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
