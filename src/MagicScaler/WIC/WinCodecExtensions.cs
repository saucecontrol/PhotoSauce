using System;
using System.Runtime.InteropServices;
using System.Diagnostics.CodeAnalysis;

using PhotoSauce.MagicScaler;

namespace PhotoSauce.Interop.Wic
{
	internal static class Wic
	{
		public static readonly IWICImagingFactory Factory = new WICImagingFactory2() as IWICImagingFactory ?? throw new PlatformNotSupportedException("Windows Imaging Component (WIC) is not available on this platform.");

		public static class Metadata
		{
			public const string InteropIndexExif = "/ifd/exif/interop/{ushort=1}";
			public const string InteropIndexJpeg = "/app1" + InteropIndexExif;

			public const string OrientationWindowsPolicy = "System.Photo.Orientation";
			public const string OrientationExif = "/ifd/{ushort=274}";
			public const string OrientationJpeg = "/app1" + OrientationExif;

			public static class Gif
			{
				public const string LogicalScreenWidth = "/logscrdesc/Width";
				public const string LogicalScreenHeight = "/logscrdesc/Height";
				public const string PixelAspectRatio = "/logscrdesc/PixelAspectRatio";
				public const string GlobalPaletteFlag = "/logscrdesc/GlobalColorTableFlag";
				public const string BackgroundColorIndex = "/logscrdesc/BackgroundColorIndex";

				public const string AppExtension = "/appext/application";
				public const string AppExtensionData = "/appext/data";

				public const string FrameLeft = "/imgdesc/Left";
				public const string FrameTop = "/imgdesc/Top";
				public const string FrameWidth = "/imgdesc/Width";
				public const string FrameHeight = "/imgdesc/Height";
				public const string FramePaletteFlag = "/imgdesc/LocalColorTableFlag";

				public const string FrameDelay = "/grctlext/Delay";
				public const string FrameDisposal = "/grctlext/Disposal";
				public const string TransparencyFlag = "/grctlext/TransparencyFlag";
				public const string TransparentColorIndex = "/grctlext/TransparentColorIndex";
			}
		}
	}

	internal static class WinCodecExtensions
	{
		public static bool RequiresCache(this WICBitmapTransformOptions opt) =>
			opt != WICBitmapTransformOptions.WICBitmapTransformRotate0 && opt != WICBitmapTransformOptions.WICBitmapTransformFlipHorizontal;

		public static WICBitmapTransformOptions ToWicTransformOptions(this Orientation o)
		{
			int orientation = (int)o;

			var opt = WICBitmapTransformOptions.WICBitmapTransformRotate0;
			if (orientation == 3 || orientation == 4)
				opt = WICBitmapTransformOptions.WICBitmapTransformRotate180;
			else if (orientation == 6 || orientation == 7)
				opt = WICBitmapTransformOptions.WICBitmapTransformRotate90;
			else if (orientation == 5 || orientation == 8)
				opt = WICBitmapTransformOptions.WICBitmapTransformRotate270;

			if (orientation == 2 || orientation == 4 || orientation == 5 || orientation == 7)
				opt |= WICBitmapTransformOptions.WICBitmapTransformFlipHorizontal;

			return opt;
		}

		public static bool IsSubsampledX(this WICJpegYCrCbSubsamplingOption o) => MiscExtensions.IsSubsampledX((ChromaSubsampleMode)o);

		public static bool IsSubsampledY(this WICJpegYCrCbSubsamplingOption o) => MiscExtensions.IsSubsampledY((ChromaSubsampleMode)o);

		public static WICRect ToWicRect(in this PixelArea a) => new WICRect { X = a.X, Y = a.Y, Width = a.Width, Height = a.Height };

		public static PixelArea ToPixelArea(in this WICRect r) => new PixelArea(r.X, r.Y, r.Width, r.Height);

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

		public static bool TryGetMetadataQueryReader(this IWICBitmapDecoder decoder, [NotNullWhen(true)] out IWICMetadataQueryReader? rdr)
		{
			int hr = ProxyFunctions.GetMetadataQueryReader(decoder, out rdr);
			return hr >= 0;
		}

		public static bool TryGetMetadataQueryReader(this IWICBitmapFrameDecode frame, [NotNullWhen(true)] out IWICMetadataQueryReader? rdr)
		{
			int hr = ProxyFunctions.GetMetadataQueryReader(frame, out rdr);
			return hr >= 0;
		}

		public static bool TryGetMetadataQueryWriter(this IWICBitmapEncoder encoder, [NotNullWhen(true)] out IWICMetadataQueryWriter? wri)
		{
			int hr = ProxyFunctions.GetMetadataQueryWriter(encoder, out wri);
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

		[return: MaybeNull]
		public static T GetValueOrDefault<T>(this IWICMetadataQueryReader meta, string name)
		{
			if (meta.TryGetMetadataByName(name, out var pv) && pv.TryGetValue(out T val))
				return val;

			return default;
		}

		public static void Write<T>(this IPropertyBag2 bag, string name, T val) where T : unmanaged
		{
			var prop = new PROPBAG2 { pstrName = name };
			var pvar = new UnmanagedPropVariant();

			if (typeof(T) == typeof(bool))
			{
				pvar.vt = VarEnum.VT_BOOL;
				pvar.int16Value = (short)((bool)(object)val ? -1 : 0);
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
