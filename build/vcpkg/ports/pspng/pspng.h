// Copyright © Clinton Ingram and Contributors.  Licensed under the MIT License.

#pragma once

#include <stdio.h>
#include <stdint.h>
#include "png.h"

#define TRUE 1
#define FALSE 0

typedef struct {
	intptr_t stream_handle;
	size_t(*write_callback)(intptr_t, png_bytep, size_t);
	size_t(*read_callback)(intptr_t, png_bytep, size_t);
} ps_io_data;

typedef struct {
	png_structp png_ptr;
	png_infop info_ptr;
	ps_io_data* io_ptr;
} ps_png_struct;

#if defined(__GNUC__) && defined(DLLDEFINE)
#define DLLEXPORT __attribute__((__visibility__("default")))
#elif defined(_MSC_VER) && defined(DLLDEFINE)
#define DLLEXPORT __declspec(dllexport)
#elif defined(_MSC_VER)
#define DLLEXPORT __declspec(dllimport)
#else
#define DLLEXPORT
#endif

#ifdef __cplusplus
extern "C" {
#endif

DLLEXPORT png_uint_32 PngVersion();

DLLEXPORT ps_png_struct* PngCreateWrite();
DLLEXPORT ps_png_struct* PngCreateRead();

DLLEXPORT void PngDestroyWrite(ps_png_struct* handle);
DLLEXPORT void PngDestroyRead(ps_png_struct* handle);
DLLEXPORT const char* PngGetLastError(ps_png_struct* handle);

DLLEXPORT int PngSetFilter(ps_png_struct* handle, int filters);
DLLEXPORT int PngSetCompressionLevel(ps_png_struct* handle, int level);

DLLEXPORT int PngWriteSig(ps_png_struct* handle);
DLLEXPORT int PngWriteIhdr(ps_png_struct* handle, png_uint_32 width, png_uint_32 height, int bit_depth, int color_type, int interlace_method);
DLLEXPORT int PngWriteIccp(ps_png_struct* handle, png_const_bytep profile);
DLLEXPORT int PngWriteSrgb(ps_png_struct* handle);
DLLEXPORT int PngWritePlte(ps_png_struct* handle, png_const_colorp palette, int num_pal);
DLLEXPORT int PngWriteTrns(ps_png_struct* handle, png_const_bytep trans, int num_trans);
DLLEXPORT int PngWritePhys(ps_png_struct* handle, png_uint_32 x_pixels_per_meter, png_uint_32 y_pixels_per_meter);
DLLEXPORT int PngWriteExif(ps_png_struct* handle, png_const_bytep exif, int num_exif);
DLLEXPORT int PngWriteRow(ps_png_struct* handle, png_const_bytep row);
DLLEXPORT int PngWriteImage(ps_png_struct* handle, png_bytepp image);
DLLEXPORT int PngWriteIend(ps_png_struct* handle);

#ifdef __cplusplus
}
#endif
