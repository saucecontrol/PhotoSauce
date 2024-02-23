// Copyright © Clinton Ingram and Contributors.  Licensed under the MIT License.

using System.Security;
using System.Runtime.InteropServices;
#if NET5_0_OR_GREATER
using System.Runtime.CompilerServices;
#else
using PhotoSauce.MagicScaler;
#endif

namespace PhotoSauce.Interop.Libwebp;

[SuppressUnmanagedCodeSecurity]
internal static partial class Libwebp { }

internal enum WebpPlane
{
	Bgra,
	Bgr,
	Y,
	U,
	V
}

internal static unsafe class WebpExtensions
{
	public static bool IsAllocated(this WebPDecBuffer b) => b.u.RGBA.rgba is not null;
}

#if NET5_0_OR_GREATER
static
#endif
internal unsafe class WebpCallbacks
{
#if !NET5_0_OR_GREATER
	[UnmanagedFunctionPointer(CallingConvention.Cdecl)] private delegate int MemoryWriteDelegate(byte* data, nuint data_size, WebPPicture* picture);
	private static readonly MemoryWriteDelegate delMemoryWrite = typeof(WebpCallbacks).CreateMethodDelegate<MemoryWriteDelegate>(nameof(memoryWrite));
#endif

	public static readonly delegate* unmanaged[Cdecl]<byte*, nuint, WebPPicture*, int> MemoryWrite = getMemoryWriterCallback();

#if NET5_0_OR_GREATER
	[UnmanagedCallersOnly(CallConvs = [ typeof(CallConvCdecl) ])]
	static
#endif
	private int memoryWrite(byte* data, nuint data_size, WebPPicture* picture) => Libwebp.WebPMemoryWrite(data, data_size, picture);

	private static delegate* unmanaged[Cdecl]<byte*, nuint, WebPPicture*, int> getMemoryWriterCallback()
	{
#if NET5_0_OR_GREATER
		if (NativeLibrary.TryLoad("webp", out var lib) && NativeLibrary.TryGetExport(lib, nameof(Libwebp.WebPMemoryWrite), out var addr))
			return (delegate* unmanaged[Cdecl]<byte*, nuint, WebPPicture*, int>)addr;

		return &memoryWrite;
#else
		if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
		{
			[DllImport("kernel32", ExactSpelling = true)]
			static extern nint LoadLibraryA(sbyte* lpLibFileName);

			[DllImport("kernel32", ExactSpelling = true)]
			static extern nint GetProcAddress(nint hModule, sbyte* lpProcName);

			nint hnd, addr;
			if ((hnd = LoadLibraryA((sbyte*)"webp"u8.GetAddressOf())) != default && (addr = GetProcAddress(hnd, (sbyte*)"WebPMemoryWrite"u8.GetAddressOf())) != default)
				return (delegate* unmanaged[Cdecl]<byte*, nuint, WebPPicture*, int>)addr;
		}

		return (delegate* unmanaged[Cdecl]<byte*, nuint, WebPPicture*, int>)Marshal.GetFunctionPointerForDelegate(delMemoryWrite);
#endif
	}
}