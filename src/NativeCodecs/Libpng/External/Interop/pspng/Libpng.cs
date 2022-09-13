// Copyright © Clinton Ingram and Contributors. Licensed under the MIT License (MIT).

// Ported from pspng.h
// This software is based in part on the work of the libpng authors.
// See third-party-notices in the repository root for more information.

using System;
using System.Runtime.InteropServices;

namespace PhotoSauce.Interop.Libpng;

internal static unsafe partial class Libpng
{
    [DllImport("pspng", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    [return: NativeTypeName("png_uint_32")]
    public static extern uint PngVersion();

    [DllImport("pspng", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    public static extern ps_png_struct* PngCreateWrite();

    [DllImport("pspng", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    public static extern ps_png_struct* PngCreateRead();

    [DllImport("pspng", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    public static extern int PngResetRead(ps_png_struct* handle);

    [DllImport("pspng", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    public static extern void PngDestroyWrite(ps_png_struct* handle);

    [DllImport("pspng", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    public static extern void PngDestroyRead(ps_png_struct* handle);

    [DllImport("pspng", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    [return: NativeTypeName("const char *")]
    public static extern sbyte* PngGetLastError(ps_png_struct* handle);

    [DllImport("pspng", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    public static extern int PngSetFilter(ps_png_struct* handle, int filters);

    [DllImport("pspng", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    public static extern int PngSetCompressionLevel(ps_png_struct* handle, int level);

    [DllImport("pspng", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    public static extern int PngWriteSig(ps_png_struct* handle);

    [DllImport("pspng", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    public static extern int PngWriteIhdr(ps_png_struct* handle, [NativeTypeName("png_uint_32")] uint width, [NativeTypeName("png_uint_32")] uint height, int bit_depth, int color_type, int interlace_method);

    [DllImport("pspng", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    public static extern int PngWriteIccp(ps_png_struct* handle, [NativeTypeName("png_const_bytep")] byte* profile);

    [DllImport("pspng", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    public static extern int PngWriteSrgb(ps_png_struct* handle);

    [DllImport("pspng", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    public static extern int PngWritePlte(ps_png_struct* handle, [NativeTypeName("png_const_colorp")] png_color_struct* palette, int num_pal);

    [DllImport("pspng", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    public static extern int PngWriteTrns(ps_png_struct* handle, [NativeTypeName("png_const_bytep")] byte* trans, int num_trans);

    [DllImport("pspng", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    public static extern int PngWritePhys(ps_png_struct* handle, [NativeTypeName("png_uint_32")] uint x_pixels_per_meter, [NativeTypeName("png_uint_32")] uint y_pixels_per_meter);

    [DllImport("pspng", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    public static extern int PngWriteExif(ps_png_struct* handle, [NativeTypeName("png_const_bytep")] byte* exif, int num_exif);

    [DllImport("pspng", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    public static extern int PngWriteRow(ps_png_struct* handle, [NativeTypeName("png_const_bytep")] byte* row);

    [DllImport("pspng", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    public static extern int PngWriteImage(ps_png_struct* handle, [NativeTypeName("png_bytepp")] byte** image);

    [DllImport("pspng", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    public static extern int PngWriteIend(ps_png_struct* handle);

    [DllImport("pspng", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    public static extern int PngReadInfo(ps_png_struct* handle);

    [DllImport("pspng", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    public static extern int PngSetExpand(ps_png_struct* handle);

    [DllImport("pspng", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    public static extern int PngSetGrayToRgb(ps_png_struct* handle);

    [DllImport("pspng", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    public static extern int PngSetStrip16(ps_png_struct* handle);

    [DllImport("pspng", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    public static extern int PngSetInterlaceHandling(ps_png_struct* handle);

    [DllImport("pspng", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    public static extern int PngReadUpdateInfo(ps_png_struct* handle);

    [DllImport("pspng", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    public static extern int PngReadFrameHead(ps_png_struct* handle);

    [DllImport("pspng", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    public static extern int PngReadRow(ps_png_struct* handle, [NativeTypeName("png_bytep")] byte* row);

    [DllImport("pspng", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    public static extern int PngReadImage(ps_png_struct* handle, [NativeTypeName("png_bytepp")] byte** image);

    [DllImport("pspng", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    public static extern int PngReadEnd(ps_png_struct* handle, [NativeTypeName("png_infop")] IntPtr end_info);

    [DllImport("pspng", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    public static extern int PngGetValid(ps_png_struct* handle, [NativeTypeName("png_uint_32")] uint flag);

    [DllImport("pspng", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    public static extern int PngGetIhdr(ps_png_struct* handle, [NativeTypeName("png_uint_32 *")] uint* width, [NativeTypeName("png_uint_32 *")] uint* height, int* bit_depth, int* color_type, int* interlace_method);

    [DllImport("pspng", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    public static extern void PngGetIccp(ps_png_struct* handle, [NativeTypeName("png_bytepp")] byte** profile, [NativeTypeName("png_uint_32 *")] uint* proflen);

    [DllImport("pspng", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    public static extern void PngGetChrm(ps_png_struct* handle, [NativeTypeName("png_fixed_point *")] int* white_x, [NativeTypeName("png_fixed_point *")] int* white_y, [NativeTypeName("png_fixed_point *")] int* red_x, [NativeTypeName("png_fixed_point *")] int* red_y, [NativeTypeName("png_fixed_point *")] int* green_x, [NativeTypeName("png_fixed_point *")] int* green_y, [NativeTypeName("png_fixed_point *")] int* blue_x, [NativeTypeName("png_fixed_point *")] int* blue_y);

    [DllImport("pspng", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    public static extern void PngGetGama(ps_png_struct* handle, [NativeTypeName("png_fixed_point *")] int* file_gamma);

    [DllImport("pspng", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    public static extern void PngGetPlte(ps_png_struct* handle, [NativeTypeName("png_colorpp")] png_color_struct** palette, int* num_palette);

    [DllImport("pspng", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    public static extern void PngGetTrns(ps_png_struct* handle, [NativeTypeName("png_bytepp")] byte** trans, int* num_trans);

    [DllImport("pspng", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    public static extern void PngGetPhys(ps_png_struct* handle, [NativeTypeName("png_uint_32 *")] uint* res_x, [NativeTypeName("png_uint_32 *")] uint* res_y, int* unit_type);

    [DllImport("pspng", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    public static extern void PngGetExif(ps_png_struct* handle, [NativeTypeName("png_bytepp")] byte** exif, [NativeTypeName("png_uint_32 *")] uint* num_exif);

    [DllImport("pspng", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    public static extern void PngGetActl(ps_png_struct* handle, [NativeTypeName("png_uint_32 *")] uint* num_frames, [NativeTypeName("png_uint_32 *")] uint* num_plays);

    [DllImport("pspng", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    public static extern void PngGetNextFrameFctl(ps_png_struct* handle, [NativeTypeName("png_uint_32 *")] uint* width, [NativeTypeName("png_uint_32 *")] uint* height, [NativeTypeName("png_uint_32 *")] uint* x_offset, [NativeTypeName("png_uint_32 *")] uint* y_offset, [NativeTypeName("png_uint_16 *")] ushort* delay_num, [NativeTypeName("png_uint_16 *")] ushort* delay_den, [NativeTypeName("png_byte *")] byte* dispose_op, [NativeTypeName("png_byte *")] byte* blend_op);

    [NativeTypeName("#define TRUE 1")]
    public const int TRUE = 1;

    [NativeTypeName("#define FALSE 0")]
    public const int FALSE = 0;

    [NativeTypeName("#define APNG_BLEND_OP_SOURCE 0")]
    public const int APNG_BLEND_OP_SOURCE = 0;

    [NativeTypeName("#define APNG_BLEND_OP_OVER 1")]
    public const int APNG_BLEND_OP_OVER = 1;

    [NativeTypeName("#define APNG_DISPOSE_OP_NONE 0")]
    public const int APNG_DISPOSE_OP_NONE = 0;

    [NativeTypeName("#define APNG_DISPOSE_OP_BACKGROUND 1")]
    public const int APNG_DISPOSE_OP_BACKGROUND = 1;

    [NativeTypeName("#define APNG_DISPOSE_OP_PREVIOUS 2")]
    public const int APNG_DISPOSE_OP_PREVIOUS = 2;
}
