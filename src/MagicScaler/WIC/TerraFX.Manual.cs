// Copyright © Clinton Ingram and Contributors.  Licensed under the MIT License.

using System;
using System.Drawing;
using System.Security;
using System.Runtime.InteropServices;
using System.Runtime.CompilerServices;

using static TerraFX.Interop.Windows;

using PhotoSauce.MagicScaler;

namespace TerraFX.Interop
{
	internal partial struct HRESULT
	{
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static void Check(int hr)
		{
			if (FAILED(hr))
				Marshal.ThrowExceptionForHR(hr);
		}
	}

	internal partial struct WICRect
	{
		public WICRect(int x, int y, int width, int height) => (X, Y, Width, Height) = (x, y, width, height);

		public static implicit operator WICRect(in PixelArea a) => new(a.X, a.Y, a.Width, a.Height);
		public static implicit operator WICRect(in Rectangle r) => new(r.X, r.Y, r.Width, r.Height);
	}

	internal unsafe ref partial struct ComPtr<T>
	{
		public ComPtr<U> Cast<U>() where U : unmanaged
		{
			var ret = new ComPtr<U>();
			ret.Attach((U*)ptr_);

			return ret;
		}

		public static ComPtr<T> Wrap(T* ptr)
		{
			var ret = new ComPtr<T>();
			ret.Attach(ptr);

			return ret;
		}

		public static implicit operator ComPtr<T>(T* ptr) => ComPtr<T>.Wrap(ptr);
	}

	internal unsafe partial struct IWICMetadataQueryReader
	{
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public int GetMetadataByName(string wzName, PROPVARIANT* pvarValue)
		{
			fixed (char* pName = wzName)
				return GetMetadataByName((ushort*)pName, pvarValue);
		}
	}

	internal unsafe partial struct IWICMetadataQueryWriter
	{
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public int SetMetadataByName(string wzName, PROPVARIANT* pvarValue)
		{
			fixed (char* pName = wzName)
				return SetMetadataByName((ushort*)pName, pvarValue);
		}
	}

	[SuppressUnmanagedCodeSecurity]
	internal static partial class Windows
	{
		// Microsoft Camera Codec Pack
		public static readonly Guid GUID_ContainerFormatRaw2 = new(0xc1fc85cb, 0xd64f, 0x478b, 0xa4, 0xec, 0x69, 0xad, 0xc9, 0xee, 0x13, 0x92);
	}

	internal enum ExifColorSpace : uint
	{
		sRGB = 1,
		AdobeRGB = 2,
		Uncalibrated = 65535
	}
}
