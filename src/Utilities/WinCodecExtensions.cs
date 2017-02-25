using System;

namespace PhotoSauce.MagicScaler.Interop
{
	internal static class WinCodecExtensions
	{
		public static uint GetColorContextCount(this IWICBitmapFrameDecode frame)
		{
			int hr = ProxyFunctions.GetColorContexts(frame, 0, null, out uint ccc);
			return hr >= 0 ? ccc : 0u;
		}

#if NET46
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

		public static bool TryGetMetadataByName(this IWICMetadataQueryReader meta, string name, out PropVariant val)
		{
			val = null;

			int hr = ProxyFunctions.GetMetadataByName(meta, name, IntPtr.Zero);
			if (hr >= 0)
			{
				val = new PropVariant();
				meta.GetMetadataByName(name, val);
			}
			return hr >= 0;
		}

		public static bool TrySetMetadataByName(this IWICMetadataQueryWriter meta, string name, PropVariant value)
		{
			int hr = ProxyFunctions.SetMetadataByName(meta, name, value);
			return hr >= 0;
		}
#endif
	}
}
