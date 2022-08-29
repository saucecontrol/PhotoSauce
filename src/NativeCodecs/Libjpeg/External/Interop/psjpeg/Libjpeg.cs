// Copyright © Clinton Ingram and Contributors. Licensed under the MIT License (MIT).

// Ported from psjpeg.h
// This software is based in part on the work of the Independent JPEG Group.
// See third-party-notices in the repository root for more information.

using System.Runtime.InteropServices;

namespace PhotoSauce.Interop.Libjpeg;

internal static unsafe partial class Libjpeg
{
    [DllImport("psjpeg", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    public static extern int JpegVersion();

    [DllImport("psjpeg", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    [return: NativeTypeName("j_compress_ptr")]
    public static extern jpeg_compress_struct* JpegCreateCompress();

    [DllImport("psjpeg", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    [return: NativeTypeName("j_decompress_ptr")]
    public static extern jpeg_decompress_struct* JpegCreateDecompress();

    [DllImport("psjpeg", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    public static extern void JpegDestroy([NativeTypeName("j_common_ptr")] jpeg_common_struct* cinfo);

    [DllImport("psjpeg", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    public static extern void JpegAbortDecompress([NativeTypeName("j_decompress_ptr")] jpeg_decompress_struct* cinfo);

    [DllImport("psjpeg", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    public static extern void JpegFree(void* mem);

    [DllImport("psjpeg", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    [return: NativeTypeName("const char *")]
    public static extern sbyte* JpegGetLastError([NativeTypeName("j_common_ptr")] jpeg_common_struct* cinfo);

    [DllImport("psjpeg", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    public static extern int JpegSetDefaults([NativeTypeName("j_compress_ptr")] jpeg_compress_struct* cinfo);

    [DllImport("psjpeg", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    public static extern int JpegSetQuality([NativeTypeName("j_compress_ptr")] jpeg_compress_struct* cinfo, int quality);

    [DllImport("psjpeg", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    public static extern int JpegSimpleProgression([NativeTypeName("j_compress_ptr")] jpeg_compress_struct* cinfo);

    [DllImport("psjpeg", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    public static extern int JpegStartCompress([NativeTypeName("j_compress_ptr")] jpeg_compress_struct* cinfo);

    [DllImport("psjpeg", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    public static extern int JpegWriteScanlines([NativeTypeName("j_compress_ptr")] jpeg_compress_struct* cinfo, [NativeTypeName("JSAMPARRAY")] byte** scanlines, [NativeTypeName("JDIMENSION")] uint num_lines, [NativeTypeName("JDIMENSION *")] uint* lines_written);

    [DllImport("psjpeg", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    public static extern int JpegWriteRawData([NativeTypeName("j_compress_ptr")] jpeg_compress_struct* cinfo, [NativeTypeName("JSAMPIMAGE")] byte*** data, [NativeTypeName("JDIMENSION")] uint num_lines, [NativeTypeName("JDIMENSION *")] uint* lines_written);

    [DllImport("psjpeg", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    public static extern int JpegFinishCompress([NativeTypeName("j_compress_ptr")] jpeg_compress_struct* cinfo);

    [DllImport("psjpeg", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    public static extern int JpegWriteMarker([NativeTypeName("j_compress_ptr")] jpeg_compress_struct* cinfo, int marker, [NativeTypeName("const JOCTET *")] byte* dataptr, [NativeTypeName("unsigned int")] uint datalen);

    [DllImport("psjpeg", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    public static extern int JpegWriteIccProfile([NativeTypeName("j_compress_ptr")] jpeg_compress_struct* cinfo, [NativeTypeName("const JOCTET *")] byte* icc_data_ptr, [NativeTypeName("unsigned int")] uint icc_data_len);

    [DllImport("psjpeg", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    public static extern int JpegReadHeader([NativeTypeName("j_decompress_ptr")] jpeg_decompress_struct* cinfo);

    [DllImport("psjpeg", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    public static extern int JpegCalcOutputDimensions([NativeTypeName("j_decompress_ptr")] jpeg_decompress_struct* cinfo);

    [DllImport("psjpeg", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    public static extern int JpegStartDecompress([NativeTypeName("j_decompress_ptr")] jpeg_decompress_struct* cinfo);

    [DllImport("psjpeg", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    public static extern int JpegCropScanline([NativeTypeName("j_decompress_ptr")] jpeg_decompress_struct* cinfo, [NativeTypeName("JDIMENSION *")] uint* xoffset, [NativeTypeName("JDIMENSION *")] uint* width);

    [DllImport("psjpeg", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    public static extern int JpegReadScanlines([NativeTypeName("j_decompress_ptr")] jpeg_decompress_struct* cinfo, [NativeTypeName("JSAMPARRAY")] byte** scanlines, [NativeTypeName("JDIMENSION")] uint max_lines, [NativeTypeName("JDIMENSION *")] uint* lines_read);

    [DllImport("psjpeg", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    public static extern int JpegReadRawData([NativeTypeName("j_decompress_ptr")] jpeg_decompress_struct* cinfo, [NativeTypeName("JSAMPIMAGE")] byte*** data, [NativeTypeName("JDIMENSION")] uint max_lines, [NativeTypeName("JDIMENSION *")] uint* lines_read);

    [DllImport("psjpeg", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    public static extern int JpegSkipScanlines([NativeTypeName("j_decompress_ptr")] jpeg_decompress_struct* cinfo, [NativeTypeName("JDIMENSION")] uint num_lines, [NativeTypeName("JDIMENSION *")] uint* lines_skipped);

    [DllImport("psjpeg", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    public static extern int JpegFinishDecompress([NativeTypeName("j_decompress_ptr")] jpeg_decompress_struct* cinfo);

    [DllImport("psjpeg", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    public static extern int JpegSaveMarkers([NativeTypeName("j_decompress_ptr")] jpeg_decompress_struct* cinfo, int marker_code, [NativeTypeName("unsigned int")] uint length_limit);

    [DllImport("psjpeg", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    public static extern int JpegReadIccProfile([NativeTypeName("j_decompress_ptr")] jpeg_decompress_struct* cinfo, [NativeTypeName("JOCTET **")] byte** icc_data_ptr, [NativeTypeName("unsigned int *")] uint* icc_data_len);
}
