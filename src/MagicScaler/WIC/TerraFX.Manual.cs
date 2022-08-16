// Copyright © Clinton Ingram and Contributors.  Licensed under the MIT License.

using System;
using System.Drawing;
using System.Security;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Runtime.CompilerServices;

using PhotoSauce.MagicScaler;

namespace TerraFX.Interop.Windows;

internal readonly partial record struct HRESULT
{
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static void Check(HRESULT hr)
	{
		if (hr.FAILED)
			Marshal.ThrowExceptionForHR(hr);
	}
}

internal partial struct WICRect
{
	public WICRect(int x, int y, int width, int height) => (X, Y, Width, Height) = (x, y, width, height);

	public static implicit operator WICRect(in PixelArea a) => Unsafe.As<PixelArea, WICRect>(ref Unsafe.AsRef(a));
	public static implicit operator WICRect(in Rectangle r) => Unsafe.As<Rectangle, WICRect>(ref Unsafe.AsRef(r));
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
	public static ref readonly Guid GUID_ContainerFormatRaw2
	{
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		get
		{
			ReadOnlySpan<byte> data = new byte[] {
				0xcb, 0x85, 0xfc, 0xc1,
				0x4f, 0xd6,
				0x8b, 0x47,
				0xa4,
				0xec,
				0x69,
				0xad,
				0xc9,
				0xee,
				0x13,
				0x92
			};

			Debug.Assert(data.Length == Unsafe.SizeOf<Guid>());
			return ref Unsafe.As<byte, Guid>(ref MemoryMarshal.GetReference(data));
		}
	}
}

internal enum ExifColorSpace : uint
{
	sRGB = 1,
	AdobeRGB = 2,
	Uncalibrated = 65535
}
