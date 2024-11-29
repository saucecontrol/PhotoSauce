// Copyright © Clinton Ingram and Contributors. Licensed under the MIT License (MIT).

// Ported from GIFLIB headers (gif_lib.h)
// Original source Copyright (c) 1997  Eric S. Raymond.
// See third-party-notices in the repository root for more information.

using System;
using System.Runtime.InteropServices;

namespace PhotoSauce.Interop.Giflib;

internal static unsafe partial class Giflib
{
    [DllImport("gif", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    public static extern GifFileType* EGifOpenFileName([NativeTypeName("const char *")] sbyte* GifFileName, [NativeTypeName("const bool")] byte GifTestExistence, int* Error);

    [DllImport("gif", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    public static extern GifFileType* EGifOpenFileHandle([NativeTypeName("const int")] int GifFileHandle, int* Error);

    [DllImport("gif", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    public static extern GifFileType* EGifOpen(void* userPtr, [NativeTypeName("OutputFunc")] delegate* unmanaged[Cdecl]<GifFileType*, byte*, int, int> writeFunc, int* Error);

    [DllImport("gif", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    public static extern int EGifSpew(GifFileType* GifFile);

    [DllImport("gif", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    [return: NativeTypeName("const char *")]
    public static extern sbyte* EGifGetGifVersion(GifFileType* GifFile);

    [DllImport("gif", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    public static extern int EGifCloseFile(GifFileType* GifFile, int* ErrorCode);

    [DllImport("gif", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    public static extern int EGifPutScreenDesc(GifFileType* GifFile, [NativeTypeName("const int")] int GifWidth, [NativeTypeName("const int")] int GifHeight, [NativeTypeName("const int")] int GifColorRes, [NativeTypeName("const int")] int GifBackGround, [NativeTypeName("const ColorMapObject *")] ColorMapObject* GifColorMap);

    [DllImport("gif", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    public static extern int EGifPutImageDesc(GifFileType* GifFile, [NativeTypeName("const int")] int GifLeft, [NativeTypeName("const int")] int GifTop, [NativeTypeName("const int")] int GifWidth, [NativeTypeName("const int")] int GifHeight, [NativeTypeName("const bool")] byte GifInterlace, [NativeTypeName("const ColorMapObject *")] ColorMapObject* GifColorMap);

    [DllImport("gif", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    public static extern void EGifSetGifVersion(GifFileType* GifFile, [NativeTypeName("const bool")] byte gif89);

    [DllImport("gif", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    public static extern int EGifPutLine(GifFileType* GifFile, [NativeTypeName("GifPixelType *")] byte* GifLine, int GifLineLen);

    [DllImport("gif", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    public static extern int EGifPutPixel(GifFileType* GifFile, [NativeTypeName("const GifPixelType")] byte GifPixel);

    [DllImport("gif", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    public static extern int EGifPutComment(GifFileType* GifFile, [NativeTypeName("const char *")] sbyte* GifComment);

    [DllImport("gif", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    public static extern int EGifPutExtensionLeader(GifFileType* GifFile, [NativeTypeName("const int")] int GifExtCode);

    [DllImport("gif", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    public static extern int EGifPutExtensionBlock(GifFileType* GifFile, [NativeTypeName("const int")] int GifExtLen, [NativeTypeName("const void *")] void* GifExtension);

    [DllImport("gif", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    public static extern int EGifPutExtensionTrailer(GifFileType* GifFile);

    [DllImport("gif", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    public static extern int EGifPutExtension(GifFileType* GifFile, [NativeTypeName("const int")] int GifExtCode, [NativeTypeName("const int")] int GifExtLen, [NativeTypeName("const void *")] void* GifExtension);

    [DllImport("gif", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    public static extern int EGifPutCode(GifFileType* GifFile, int GifCodeSize, [NativeTypeName("const GifByteType *")] byte* GifCodeBlock);

    [DllImport("gif", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    public static extern int EGifPutCodeNext(GifFileType* GifFile, [NativeTypeName("const GifByteType *")] byte* GifCodeBlock);

    [DllImport("gif", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    public static extern GifFileType* DGifOpenFileName([NativeTypeName("const char *")] sbyte* GifFileName, int* Error);

    [DllImport("gif", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    public static extern GifFileType* DGifOpenFileHandle(int GifFileHandle, int* Error);

    [DllImport("gif", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    public static extern int DGifSlurp(GifFileType* GifFile);

    [DllImport("gif", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    public static extern GifFileType* DGifOpen(void* userPtr, [NativeTypeName("InputFunc")] delegate* unmanaged[Cdecl]<GifFileType*, byte*, int, int> readFunc, int* Error);

    [DllImport("gif", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    public static extern int DGifCloseFile(GifFileType* GifFile, int* ErrorCode);

    [DllImport("gif", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    public static extern int DGifGetScreenDesc(GifFileType* GifFile);

    [DllImport("gif", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    public static extern int DGifGetRecordType(GifFileType* GifFile, GifRecordType* GifType);

    [DllImport("gif", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    public static extern int DGifGetImageHeader(GifFileType* GifFile);

    [DllImport("gif", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    public static extern int DGifGetImageDesc(GifFileType* GifFile);

    [DllImport("gif", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    public static extern int DGifGetLine(GifFileType* GifFile, [NativeTypeName("GifPixelType *")] byte* GifLine, int GifLineLen);

    [DllImport("gif", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    public static extern int DGifGetPixel(GifFileType* GifFile, [NativeTypeName("GifPixelType")] byte GifPixel);

    [DllImport("gif", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    public static extern int DGifGetExtension(GifFileType* GifFile, int* GifExtCode, [NativeTypeName("GifByteType **")] byte** GifExtension);

    [DllImport("gif", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    public static extern int DGifGetExtensionNext(GifFileType* GifFile, [NativeTypeName("GifByteType **")] byte** GifExtension);

    [DllImport("gif", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    public static extern int DGifGetCode(GifFileType* GifFile, int* GifCodeSize, [NativeTypeName("GifByteType **")] byte** GifCodeBlock);

    [DllImport("gif", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    public static extern int DGifGetCodeNext(GifFileType* GifFile, [NativeTypeName("GifByteType **")] byte** GifCodeBlock);

    [DllImport("gif", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    public static extern int DGifGetLZCodes(GifFileType* GifFile, int* GifCode);

    [DllImport("gif", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    [return: NativeTypeName("const char *")]
    public static extern sbyte* DGifGetGifVersion(GifFileType* GifFile);

    [DllImport("gif", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    [return: NativeTypeName("const char *")]
    public static extern sbyte* GifErrorString(int ErrorCode);

    [DllImport("gif", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    public static extern ColorMapObject* GifMakeMapObject(int ColorCount, [NativeTypeName("const GifColorType *")] GifColorType* ColorMap);

    [DllImport("gif", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    public static extern void GifFreeMapObject(ColorMapObject* Object);

    [DllImport("gif", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    public static extern ColorMapObject* GifUnionColorMap([NativeTypeName("const ColorMapObject *")] ColorMapObject* ColorIn1, [NativeTypeName("const ColorMapObject *")] ColorMapObject* ColorIn2, [NativeTypeName("GifPixelType[]")] byte* ColorTransIn2);

    [DllImport("gif", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    public static extern int GifBitSize(int n);

    [DllImport("gif", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    public static extern void GifApplyTranslation(SavedImage* Image, [NativeTypeName("const GifPixelType[]")] byte* Translation);

    [DllImport("gif", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    public static extern int GifAddExtensionBlock(int* ExtensionBlock_Count, ExtensionBlock** ExtensionBlocks, int Function, [NativeTypeName("unsigned int")] uint Len, [NativeTypeName("unsigned char[]")] byte* ExtData);

    [DllImport("gif", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    public static extern void GifFreeExtensions(int* ExtensionBlock_Count, ExtensionBlock** ExtensionBlocks);

    [DllImport("gif", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    public static extern SavedImage* GifMakeSavedImage(GifFileType* GifFile, [NativeTypeName("const SavedImage *")] SavedImage* CopyFrom);

    [DllImport("gif", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    public static extern void GifFreeSavedImages(GifFileType* GifFile);

    [DllImport("gif", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    public static extern int DGifExtensionToGCB([NativeTypeName("const size_t")] nuint GifExtensionLength, [NativeTypeName("const GifByteType *")] byte* GifExtension, GraphicsControlBlock* GCB);

    [DllImport("gif", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    [return: NativeTypeName("size_t")]
    public static extern nuint EGifGCBToExtension([NativeTypeName("const GraphicsControlBlock *")] GraphicsControlBlock* GCB, [NativeTypeName("GifByteType *")] byte* GifExtension);

    [DllImport("gif", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    public static extern int DGifSavedExtensionToGCB(GifFileType* GifFile, int ImageIndex, GraphicsControlBlock* GCB);

    [DllImport("gif", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    public static extern int EGifGCBToSavedExtension([NativeTypeName("const GraphicsControlBlock *")] GraphicsControlBlock* GCB, GifFileType* GifFile, int ImageIndex);

    [DllImport("gif", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    public static extern void GifDrawText8x8(SavedImage* Image, [NativeTypeName("const int")] int x, [NativeTypeName("const int")] int y, [NativeTypeName("const char *")] sbyte* legend, [NativeTypeName("const int")] int color);

    [DllImport("gif", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    public static extern void GifDrawBox(SavedImage* Image, [NativeTypeName("const int")] int x, [NativeTypeName("const int")] int y, [NativeTypeName("const int")] int w, [NativeTypeName("const int")] int d, [NativeTypeName("const int")] int color);

    [DllImport("gif", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    public static extern void GifDrawRectangle(SavedImage* Image, [NativeTypeName("const int")] int x, [NativeTypeName("const int")] int y, [NativeTypeName("const int")] int w, [NativeTypeName("const int")] int d, [NativeTypeName("const int")] int color);

    [DllImport("gif", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    public static extern void GifDrawBoxedText8x8(SavedImage* Image, [NativeTypeName("const int")] int x, [NativeTypeName("const int")] int y, [NativeTypeName("const char *")] sbyte* legend, [NativeTypeName("const int")] int border, [NativeTypeName("const int")] int bg, [NativeTypeName("const int")] int fg);

    [NativeTypeName("#define _GIF_LIB_H_ 1")]
    public const int _GIF_LIB_H_ = 1;

    [NativeTypeName("#define GIFLIB_MAJOR 5")]
    public const int GIFLIB_MAJOR = 5;

    [NativeTypeName("#define GIFLIB_MINOR 2")]
    public const int GIFLIB_MINOR = 2;

    [NativeTypeName("#define GIFLIB_RELEASE 2")]
    public const int GIFLIB_RELEASE = 2;

    [NativeTypeName("#define GIF_ERROR 0")]
    public const int GIF_ERROR = 0;

    [NativeTypeName("#define GIF_OK 1")]
    public const int GIF_OK = 1;

    [NativeTypeName("#define GIF_STAMP \"GIFVER\"")]
    public static ReadOnlySpan<byte> GIF_STAMP => "GIFVER"u8;

    [NativeTypeName("#define GIF_STAMP_LEN sizeof(GIF_STAMP) - 1")]
    public const ulong GIF_STAMP_LEN = 7 - 1;

    [NativeTypeName("#define GIF_VERSION_POS 3")]
    public const int GIF_VERSION_POS = 3;

    [NativeTypeName("#define GIF87_STAMP \"GIF87a\"")]
    public static ReadOnlySpan<byte> GIF87_STAMP => "GIF87a"u8;

    [NativeTypeName("#define GIF89_STAMP \"GIF89a\"")]
    public static ReadOnlySpan<byte> GIF89_STAMP => "GIF89a"u8;

    [NativeTypeName("#define CONTINUE_EXT_FUNC_CODE 0x00")]
    public const int CONTINUE_EXT_FUNC_CODE = 0x00;

    [NativeTypeName("#define COMMENT_EXT_FUNC_CODE 0xfe")]
    public const int COMMENT_EXT_FUNC_CODE = 0xfe;

    [NativeTypeName("#define GRAPHICS_EXT_FUNC_CODE 0xf9")]
    public const int GRAPHICS_EXT_FUNC_CODE = 0xf9;

    [NativeTypeName("#define PLAINTEXT_EXT_FUNC_CODE 0x01")]
    public const int PLAINTEXT_EXT_FUNC_CODE = 0x01;

    [NativeTypeName("#define APPLICATION_EXT_FUNC_CODE 0xff")]
    public const int APPLICATION_EXT_FUNC_CODE = 0xff;

    [NativeTypeName("#define DISPOSAL_UNSPECIFIED 0")]
    public const int DISPOSAL_UNSPECIFIED = 0;

    [NativeTypeName("#define DISPOSE_DO_NOT 1")]
    public const int DISPOSE_DO_NOT = 1;

    [NativeTypeName("#define DISPOSE_BACKGROUND 2")]
    public const int DISPOSE_BACKGROUND = 2;

    [NativeTypeName("#define DISPOSE_PREVIOUS 3")]
    public const int DISPOSE_PREVIOUS = 3;

    [NativeTypeName("#define NO_TRANSPARENT_COLOR -1")]
    public const int NO_TRANSPARENT_COLOR = -1;

    [NativeTypeName("#define E_GIF_SUCCEEDED 0")]
    public const int E_GIF_SUCCEEDED = 0;

    [NativeTypeName("#define E_GIF_ERR_OPEN_FAILED 1")]
    public const int E_GIF_ERR_OPEN_FAILED = 1;

    [NativeTypeName("#define E_GIF_ERR_WRITE_FAILED 2")]
    public const int E_GIF_ERR_WRITE_FAILED = 2;

    [NativeTypeName("#define E_GIF_ERR_HAS_SCRN_DSCR 3")]
    public const int E_GIF_ERR_HAS_SCRN_DSCR = 3;

    [NativeTypeName("#define E_GIF_ERR_HAS_IMAG_DSCR 4")]
    public const int E_GIF_ERR_HAS_IMAG_DSCR = 4;

    [NativeTypeName("#define E_GIF_ERR_NO_COLOR_MAP 5")]
    public const int E_GIF_ERR_NO_COLOR_MAP = 5;

    [NativeTypeName("#define E_GIF_ERR_DATA_TOO_BIG 6")]
    public const int E_GIF_ERR_DATA_TOO_BIG = 6;

    [NativeTypeName("#define E_GIF_ERR_NOT_ENOUGH_MEM 7")]
    public const int E_GIF_ERR_NOT_ENOUGH_MEM = 7;

    [NativeTypeName("#define E_GIF_ERR_DISK_IS_FULL 8")]
    public const int E_GIF_ERR_DISK_IS_FULL = 8;

    [NativeTypeName("#define E_GIF_ERR_CLOSE_FAILED 9")]
    public const int E_GIF_ERR_CLOSE_FAILED = 9;

    [NativeTypeName("#define E_GIF_ERR_NOT_WRITEABLE 10")]
    public const int E_GIF_ERR_NOT_WRITEABLE = 10;

    [NativeTypeName("#define D_GIF_SUCCEEDED 0")]
    public const int D_GIF_SUCCEEDED = 0;

    [NativeTypeName("#define D_GIF_ERR_OPEN_FAILED 101")]
    public const int D_GIF_ERR_OPEN_FAILED = 101;

    [NativeTypeName("#define D_GIF_ERR_READ_FAILED 102")]
    public const int D_GIF_ERR_READ_FAILED = 102;

    [NativeTypeName("#define D_GIF_ERR_NOT_GIF_FILE 103")]
    public const int D_GIF_ERR_NOT_GIF_FILE = 103;

    [NativeTypeName("#define D_GIF_ERR_NO_SCRN_DSCR 104")]
    public const int D_GIF_ERR_NO_SCRN_DSCR = 104;

    [NativeTypeName("#define D_GIF_ERR_NO_IMAG_DSCR 105")]
    public const int D_GIF_ERR_NO_IMAG_DSCR = 105;

    [NativeTypeName("#define D_GIF_ERR_NO_COLOR_MAP 106")]
    public const int D_GIF_ERR_NO_COLOR_MAP = 106;

    [NativeTypeName("#define D_GIF_ERR_WRONG_RECORD 107")]
    public const int D_GIF_ERR_WRONG_RECORD = 107;

    [NativeTypeName("#define D_GIF_ERR_DATA_TOO_BIG 108")]
    public const int D_GIF_ERR_DATA_TOO_BIG = 108;

    [NativeTypeName("#define D_GIF_ERR_NOT_ENOUGH_MEM 109")]
    public const int D_GIF_ERR_NOT_ENOUGH_MEM = 109;

    [NativeTypeName("#define D_GIF_ERR_CLOSE_FAILED 110")]
    public const int D_GIF_ERR_CLOSE_FAILED = 110;

    [NativeTypeName("#define D_GIF_ERR_NOT_READABLE 111")]
    public const int D_GIF_ERR_NOT_READABLE = 111;

    [NativeTypeName("#define D_GIF_ERR_IMAGE_DEFECT 112")]
    public const int D_GIF_ERR_IMAGE_DEFECT = 112;

    [NativeTypeName("#define D_GIF_ERR_EOF_TOO_SOON 113")]
    public const int D_GIF_ERR_EOF_TOO_SOON = 113;

    [NativeTypeName("#define GIF_FONT_WIDTH 8")]
    public const int GIF_FONT_WIDTH = 8;

    [NativeTypeName("#define GIF_FONT_HEIGHT 8")]
    public const int GIF_FONT_HEIGHT = 8;
}
