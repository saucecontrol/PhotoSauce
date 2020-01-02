using System;
using System.Runtime.InteropServices;
using System.Diagnostics.CodeAnalysis;

namespace PhotoSauce.Interop.Wic
{
	internal static class Wic
	{
		public static readonly IWICImagingFactory Factory = new WICImagingFactory2() as IWICImagingFactory ?? throw new PlatformNotSupportedException("Windows Imaging Component (WIC) is not available on this platform.");

		public static class Metadata
		{
			public const string InteropIndexExifPath = "/ifd/exif/interop/{ushort=1}";
			public const string InteropIndexJpegPath = "/app1" + InteropIndexExifPath;
			public const string OrientationWindowsPolicy = "System.Photo.Orientation";
			public const string OrientationExifPath = "/ifd/{ushort=274}";
			public const string OrientationJpegPath = "/app1" + OrientationExifPath;
		}
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

			int hr = ProxyFunctions.GetMetadataByName(meta, name, IntPtr.Zero);
			if (hr >= 0)
			{
				value = new PropVariant();

				var pvMarshal = PropVariant.Marshaler.GetInstance(null);
				var pvNative = pvMarshal.MarshalManagedToNative(value);
				hr = ProxyFunctions.GetMetadataByName(meta, name, pvNative);
				pvMarshal.MarshalNativeToManaged(pvNative);
				pvMarshal.CleanUpNativeData(pvNative);
			}

			return hr >= 0;
		}

		public static bool TrySetMetadataByName(this IWICMetadataQueryWriter meta, string name, PropVariant value)
		{
			var pvMarshal = PropVariant.Marshaler.GetInstance(null);
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

		public static void Write<T>(this IPropertyBag2 bag, string name, T val) where T : unmanaged
		{
			var prop = new PROPBAG2 { pstrName = name };
			var pvar = new UnmanagedPropVariant();

			if (typeof(T) == typeof(bool))
			{
				pvar.vt = VarEnum.VT_BOOL;
				pvar.int16Value = (bool)(object)val ? (short)-1 : (short)0;
			}
			else if (typeof(T) == typeof(byte))
			{
				pvar.vt = VarEnum.VT_UI1;
				pvar.byteValue = (byte)(object)val;
			}
			else if (typeof(T) == typeof(float))
			{
				pvar.vt = VarEnum.VT_R4;
				pvar.floatValue = (float)(object)val;
			}
			else
			{
				throw new ArgumentException("Marshaling not implemented for type: " + typeof(T).Name, nameof(T));
			}

			ProxyFunctions.PropertyBagWrite(bag, 1, prop, pvar);
		}
	}
}
