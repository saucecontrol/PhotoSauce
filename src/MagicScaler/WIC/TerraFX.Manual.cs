// Copyright © Clinton Ingram and Contributors.  Licensed under the MIT License.

using System;
using System.Runtime.InteropServices;
using System.Runtime.CompilerServices;

using static TerraFX.Interop.Windows;

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

	internal static unsafe partial class Windows
	{
		// Microsoft Camera Codec Pack for Windows 8
		public static readonly Guid GUID_ContainerFormatRaw2 = new(0xc1fc85cb, 0xd64f, 0x478b, 0xa4, 0xec, 0x69, 0xad, 0xc9, 0xee, 0x13, 0x92);
	}

	internal enum ExifColorSpace : uint
	{
		sRGB = 1,
		AdobeRGB = 2,
		Uncalibrated = 65535
	}
}
