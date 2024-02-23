// Copyright © Clinton Ingram and Contributors.  Licensed under the MIT License.

using System;
using System.Security;
using System.Runtime.InteropServices;
using System.Runtime.ExceptionServices;
#if NET5_0_OR_GREATER
using System.Runtime.CompilerServices;
#endif

using PhotoSauce.MagicScaler;

namespace PhotoSauce.Interop.Libjpeg;

[SuppressUnmanagedCodeSecurity]
internal static unsafe partial class Libjpeg
{
	public const int DSTATE_READY = 202;
	public const int JPEG_APP1 = JPEG_APP0 + 1;
	public const int JPEG_APP2 = JPEG_APP0 + 2;

	private const int semiProgressiveScanCount = 5;
	private static readonly jpeg_scan_info* semiProgressiveScript = createSemiProgressiveScript();

	public static ReadOnlySpan<byte> ExifIdentifier => "Exif\0\0"u8;
	public static ReadOnlySpan<byte> IccpIdentifier => "ICC_PROFILE\0"u8;

	public static bool IsValidImage(this ref jpeg_decompress_struct handle) =>
		handle.jpeg_color_space is not J_COLOR_SPACE.JCS_UNKNOWN &&
		handle.image_width is > 0 and <= JPEG_MAX_DIMENSION &&
		handle.image_height is > 0 and <= JPEG_MAX_DIMENSION;

	public static bool IsPlanarSupported(this ref jpeg_decompress_struct handle) =>
		handle.jpeg_color_space is J_COLOR_SPACE.JCS_YCbCr &&
		handle.num_components is 3 &&
		handle.comp_info[0].h_samp_factor is 1 or 2 &&
		handle.comp_info[0].v_samp_factor is 1 or 2 &&
		handle.comp_info[1].h_samp_factor is 1 &&
		handle.comp_info[1].v_samp_factor is 1 &&
		handle.comp_info[2].h_samp_factor is 1 &&
		handle.comp_info[2].v_samp_factor is 1;

	public static bool IsExifMarker(this ref jpeg_marker_struct marker) =>
		marker.marker is JPEG_APP1 &&
		marker.data_length >= ExifIdentifier.Length + ExifConstants.MinExifLength &&
		new ReadOnlySpan<byte>(marker.data, ExifIdentifier.Length - 1).SequenceEqual(ExifIdentifier[..^1]);

	public static bool IsIccMarker(this ref jpeg_marker_struct marker) =>
		marker.marker is JPEG_APP2 &&
		marker.data_length >= IccpIdentifier.Length + ColorProfile.MinProfileLength &&
		new ReadOnlySpan<byte>(marker.data, IccpIdentifier.Length).SequenceEqual(IccpIdentifier);

	public static void JpegFastProgression(jpeg_compress_struct* handle)
	{
		handle->num_scans = semiProgressiveScanCount;
		handle->scan_info = semiProgressiveScript;
	}

	private static jpeg_scan_info* createSemiProgressiveScript()
	{
		var si = (jpeg_scan_info*)UnsafeUtil.AllocateTypeAssociatedMemory(typeof(Libjpeg), sizeof(jpeg_scan_info) * semiProgressiveScanCount);

		si[0].comps_in_scan = 3;
		si[0].component_index[0] = 0;
		si[0].component_index[1] = 1;
		si[0].component_index[2] = 2;
		si[0].Ss = 0;
		si[0].Se = 0;
		si[0].Ah = 0;
		si[0].Al = 0;

		si[1].comps_in_scan = 1;
		si[1].component_index[0] = 0;
		si[1].Ss = 1;
		si[1].Se = 9;
		si[1].Ah = 0;
		si[1].Al = 0;

		si[2].comps_in_scan = 1;
		si[2].component_index[0] = 2;
		si[2].Ss = 1;
		si[2].Se = 63;
		si[2].Ah = 0;
		si[2].Al = 0;

		si[3].comps_in_scan = 1;
		si[3].component_index[0] = 1;
		si[3].Ss = 1;
		si[3].Se = 63;
		si[3].Ah = 0;
		si[3].Al = 0;

		si[4].comps_in_scan = 1;
		si[4].component_index[0] = 0;
		si[4].Ss = 10;
		si[4].Se = 63;
		si[4].Ah = 0;
		si[4].Al = 0;

		return si;
	}
}

internal unsafe partial struct ps_client_data
{
	public readonly StreamWrapper* Stream => (StreamWrapper*)stream_handle;
}

#if NET5_0_OR_GREATER
static
#endif
internal unsafe class JpegCallbacks
{
#if !NET5_0_OR_GREATER
	[UnmanagedFunctionPointer(CallingConvention.Cdecl)] private delegate nuint ReadDelegate(nint pinst, byte* buff, nuint cb);
	[UnmanagedFunctionPointer(CallingConvention.Cdecl)] private delegate nuint SeekDelegate(nint pinst, nuint cb);
	[UnmanagedFunctionPointer(CallingConvention.Cdecl)] private delegate nuint WriteDelegate(nint pinst, byte* buff, nuint cb);

	private static readonly ReadDelegate delRead = typeof(JpegCallbacks).CreateMethodDelegate<ReadDelegate>(nameof(read));
	private static readonly SeekDelegate delSeek = typeof(JpegCallbacks).CreateMethodDelegate<SeekDelegate>(nameof(seek));
	private static readonly WriteDelegate delWrite = typeof(JpegCallbacks).CreateMethodDelegate<WriteDelegate>(nameof(write));
#endif

	public static readonly delegate* unmanaged[Cdecl]<nint, byte*, nuint, nuint> Read =
#if NET5_0_OR_GREATER
		&read;
#else
		(delegate* unmanaged[Cdecl]<nint, byte*, nuint, nuint>)Marshal.GetFunctionPointerForDelegate(delRead);
#endif

	public static readonly delegate* unmanaged[Cdecl]<nint, nuint, nuint> Seek =
#if NET5_0_OR_GREATER
		&seek;
#else
		(delegate* unmanaged[Cdecl]<nint, nuint, nuint>)Marshal.GetFunctionPointerForDelegate(delSeek);
#endif

	public static readonly delegate* unmanaged[Cdecl]<nint, byte*, nuint, nuint> Write =
#if NET5_0_OR_GREATER
		&write;
#else
		(delegate* unmanaged[Cdecl]<nint, byte*, nuint, nuint>)Marshal.GetFunctionPointerForDelegate(delWrite);
#endif

#if NET5_0_OR_GREATER
	[UnmanagedCallersOnly(CallConvs = [typeof(CallConvCdecl)])]
	static
#endif
	private nuint read(nint pinst, byte* buff, nuint cb)
	{
		var stm = (StreamWrapper*)pinst;
		try
		{
			return stm->Read(buff, checked((uint)cb));
		}
		catch (Exception ex) when (StreamWrapper.CaptureExceptions)
		{
			stm->SetException(ExceptionDispatchInfo.Capture(ex));
			return unchecked((nuint)~0ul);
		}
	}

#if NET5_0_OR_GREATER
	[UnmanagedCallersOnly(CallConvs = [typeof(CallConvCdecl)])]
	static
#endif
	private nuint seek(nint pinst, nuint cb)
	{
		var stm = (StreamWrapper*)pinst;
		try
		{
			_ = stm->Seek(checked((uint)cb), System.IO.SeekOrigin.Current);
			return cb;
		}
		catch (Exception ex) when (StreamWrapper.CaptureExceptions)
		{
			stm->SetException(ExceptionDispatchInfo.Capture(ex));
			return unchecked((nuint)~0ul);
		}
	}

#if NET5_0_OR_GREATER
	[UnmanagedCallersOnly(CallConvs = [typeof(CallConvCdecl)])]
	static
#endif
	private nuint write(nint pinst, byte* buff, nuint cb)
	{
		var stm = (StreamWrapper*)pinst;
		try
		{
			stm->Write(buff, checked((uint)cb));
			return cb;
		}
		catch (Exception ex) when (StreamWrapper.CaptureExceptions)
		{
			stm->SetException(ExceptionDispatchInfo.Capture(ex));
			return 0;
		}
	}
}
