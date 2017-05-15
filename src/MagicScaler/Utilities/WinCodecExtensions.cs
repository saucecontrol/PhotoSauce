using System;

namespace PhotoSauce.MagicScaler.Interop
{
	internal static class Wic
	{
		public static readonly IWICImagingFactory Factory = new WICImagingFactory2() as IWICImagingFactory;
	}

	internal static class WinCodecExtensions
	{
		public static bool RequiresCache(this WICBitmapTransformOptions opt) => opt != WICBitmapTransformOptions.WICBitmapTransformRotate0 && opt != WICBitmapTransformOptions.WICBitmapTransformFlipHorizontal;

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
	}
}
