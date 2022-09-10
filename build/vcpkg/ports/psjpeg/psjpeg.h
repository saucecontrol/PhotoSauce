// Copyright © Clinton Ingram and Contributors.  Licensed under the MIT License.

#pragma once

#include <stdint.h>
#include "jpeglib.h"

typedef struct {
	intptr_t stream_handle;
	size_t(*write_callback)(intptr_t pinst, JOCTET* buff, size_t cb);
	size_t(*read_callback)(intptr_t pinst, JOCTET* buff, size_t cb);
	size_t(*seek_callback)(intptr_t pinst, size_t cb);
} ps_client_data;

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

DLLEXPORT int JpegVersion();

DLLEXPORT j_compress_ptr JpegCreateCompress();
DLLEXPORT j_decompress_ptr JpegCreateDecompress();

DLLEXPORT void JpegDestroy(j_common_ptr cinfo);
DLLEXPORT void JpegAbortDecompress(j_decompress_ptr cinfo);

DLLEXPORT void JpegFree(void* mem);
DLLEXPORT const char* JpegGetLastError(j_common_ptr cinfo);

DLLEXPORT int JpegSetDefaults(j_compress_ptr cinfo);
DLLEXPORT int JpegSetQuality(j_compress_ptr cinfo, int quality);
DLLEXPORT int JpegSimpleProgression(j_compress_ptr cinfo);

DLLEXPORT int JpegStartCompress(j_compress_ptr cinfo);
DLLEXPORT int JpegWriteScanlines(j_compress_ptr cinfo, JSAMPARRAY scanlines, JDIMENSION num_lines, JDIMENSION* lines_written);
DLLEXPORT int JpegWriteRawData(j_compress_ptr cinfo, JSAMPIMAGE data, JDIMENSION num_lines, JDIMENSION* lines_written);
DLLEXPORT int JpegFinishCompress(j_compress_ptr cinfo);

DLLEXPORT int JpegWriteMarker(j_compress_ptr cinfo, int marker, const JOCTET* dataptr, unsigned int datalen);
DLLEXPORT int JpegWriteIccProfile(j_compress_ptr cinfo, const JOCTET* icc_data_ptr, unsigned int icc_data_len);

DLLEXPORT int JpegReadHeader(j_decompress_ptr cinfo);
DLLEXPORT int JpegCalcOutputDimensions(j_decompress_ptr cinfo);

DLLEXPORT int JpegStartDecompress(j_decompress_ptr cinfo);
DLLEXPORT int JpegCropScanline(j_decompress_ptr cinfo, JDIMENSION* xoffset, JDIMENSION* width);
DLLEXPORT int JpegReadScanlines(j_decompress_ptr cinfo, JSAMPARRAY scanlines, JDIMENSION max_lines, JDIMENSION* lines_read);
DLLEXPORT int JpegReadRawData(j_decompress_ptr cinfo, JSAMPIMAGE data, JDIMENSION max_lines, JDIMENSION* lines_read);
DLLEXPORT int JpegSkipScanlines(j_decompress_ptr cinfo, JDIMENSION num_lines, JDIMENSION* lines_skipped);
DLLEXPORT int JpegFinishDecompress(j_decompress_ptr cinfo);

DLLEXPORT int JpegSaveMarkers(j_decompress_ptr cinfo, int marker_code, unsigned int length_limit);
DLLEXPORT int JpegReadIccProfile(j_decompress_ptr cinfo, JOCTET** icc_data_ptr, unsigned int* icc_data_len);

#ifdef __cplusplus
}
#endif
