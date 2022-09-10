// Copyright © Clinton Ingram and Contributors. Licensed under the MIT License (MIT).

// Ported from pspng.h
// This software is based in part on the work of the libpng authors.
// See third-party-notices in the repository root for more information.

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

    [NativeTypeName("#define TRUE 1")]
    public const int TRUE = 1;

    [NativeTypeName("#define FALSE 0")]
    public const int FALSE = 0;
}
