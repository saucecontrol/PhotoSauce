using System;
using System.Runtime.InteropServices;

namespace PhotoSauce.MagicScaler.Interop
{
	internal static class WinCodecExtensions
	{
		[DllImport("WindowsCodecs", EntryPoint = "IWICBitmapFrameDecode_GetColorContexts_Proxy")]
		private extern static int GetColorContexts(IWICBitmapFrameDecode THIS_PTR, uint cCount, IntPtr[] ppIColorContexts, out uint pcActualCount);

		[DllImport("WindowsCodecs", EntryPoint = "IWICBitmapFrameDecode_GetMetadataQueryReader_Proxy")]
		private extern static int GetMetadataQueryReader(IWICBitmapFrameDecode THIS_PTR, out IWICMetadataQueryReader ppIMetadataQueryReader);

		[DllImport("WindowsCodecs", EntryPoint = "IWICBitmapFrameEncode_GetMetadataQueryWriter_Proxy")]
		private extern static int GetMetadataQueryWriter(IWICBitmapFrameEncode THIS_PTR, out IWICMetadataQueryWriter ppIMetadataQueryWriter);

		[DllImport("WindowsCodecs", EntryPoint = "IWICMetadataQueryReader_GetMetadataByName_Proxy")]
		private extern static int GetMetadataByName(IWICMetadataQueryReader THIS_PTR, [MarshalAs(UnmanagedType.LPWStr)]string wzName, IntPtr pvarValue);

		[DllImport("WindowsCodecs", EntryPoint = "IWICMetadataQueryWriter_SetMetadataByName_Proxy")]
		private extern static int SetMetadataByName(IWICMetadataQueryWriter THIS_PTR, [MarshalAs(UnmanagedType.LPWStr)]string wzName, [In, MarshalAs(UnmanagedType.CustomMarshaler, MarshalTypeRef = typeof(PropVariant.Marshaler))]PropVariant pvarValue);

		public static uint GetColorContextCount(this IWICBitmapFrameDecode frame)
		{
			uint ccc;
			int hr = GetColorContexts(frame, 0, null, out ccc);
			return hr >= 0 ? ccc : 0u;
		}

		public static IWICMetadataQueryReader GetMetadataQueryReaderNoThrow(this IWICBitmapFrameDecode frame)
		{
			IWICMetadataQueryReader rdr;
			int hr = GetMetadataQueryReader(frame, out rdr);
			return hr >= 0 ? rdr : null;
		}

		public static IWICMetadataQueryWriter GetMetadataQueryWriterNoThrow(this IWICBitmapFrameEncode frame)
		{
			IWICMetadataQueryWriter wri;
			int hr = GetMetadataQueryWriter(frame, out wri);
			return hr >= 0 ? wri : null;
		}

		public static bool HasMetadataName(this IWICMetadataQueryReader meta, string name)
		{
			int hr = GetMetadataByName(meta, name, IntPtr.Zero);
			return hr >= 0;
		}

		public static bool SetMetadataByNameNoThrow(this IWICMetadataQueryWriter meta, string name, PropVariant value)
		{
			int hr = SetMetadataByName(meta, name, value);
			return hr >= 0;
		}
	}
}
