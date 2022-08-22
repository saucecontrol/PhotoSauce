// Copyright © Clinton Ingram and Contributors.  Licensed under the MIT License.

#include <string.h>
#include <setjmp.h>
#include "jerror.h"
#include "psjpeg.h"

#if defined(__GNUC__) && defined(__x86_64__)
#include <mm_malloc.h>
#elif defined(_MSC_VER)
#include <malloc.h>
#else
#include <stdlib.h>
#define _mm_free(p) free(p)
#define _mm_malloc(a, b) malloc(a)
#endif

#ifdef sigsetjmp
#define SETJMP(e) sigsetjmp((e), 0)
#define LONGJMP siglongjmp
#else
#define SETJMP setjmp
#define LONGJMP longjmp
#endif

#define TRY int _jmp_res; if (!(_jmp_res = SETJMP(((ps_error_mgr*)cinfo->err)->jmp_buf)))
#define CATCH else
#define TRY_RESULT !_jmp_res ? TRUE : FALSE;

// attempt to pad ps_err_mgr so that jmp_buf is 16-byte aligned
#if defined(__GNUC__) && (defined(__x86_64__) || defined(__aarch64__))
#define JMSG_PAD 4
#else
#define JMSG_PAD sizeof(size_t)
#endif

#define SRC_BUF_SIZE 4096
#define DST_BUF_SIZE 4096
#define MSG_BUF_SIZE JMSG_LENGTH_MAX + JMSG_PAD

typedef struct {
	struct jpeg_error_mgr pub;
	char msg[MSG_BUF_SIZE];
	jmp_buf jmp_buf;
} ps_error_mgr;

typedef struct {
	struct jpeg_destination_mgr pub;
	JOCTET* buff;
} ps_dest_mgr;

typedef struct {
	struct jpeg_source_mgr pub;
	JOCTET* buff;
} ps_src_mgr;

static void nullEmit(j_common_ptr cinfo, int msg_level) { }
static void nullOutput(j_common_ptr cinfo) { }
static void nullSource(j_decompress_ptr cinfo) { }

static void throwError(j_common_ptr cinfo) {
  ps_error_mgr* err = (ps_error_mgr*)cinfo->err;
	(*cinfo->err->format_message)(cinfo, err->msg);

  LONGJMP(err->jmp_buf, err->pub.msg_code);
}

static void initDest(j_compress_ptr cinfo) {
	ps_dest_mgr* dest = (ps_dest_mgr*)cinfo->dest;

	dest->buff = (JOCTET*)(*cinfo->mem->alloc_small)((j_common_ptr)cinfo, JPOOL_IMAGE, DST_BUF_SIZE * sizeof(JOCTET));
	dest->pub.next_output_byte = dest->buff;
	dest->pub.free_in_buffer = DST_BUF_SIZE;
}

static boolean writeDest(j_compress_ptr cinfo) {
	ps_client_data* client = (ps_client_data*)cinfo->client_data;
	ps_dest_mgr* dest = (ps_dest_mgr*)cinfo->dest;

	if ((*client->write_file)(client->stream_handle, dest->buff, DST_BUF_SIZE) != DST_BUF_SIZE)
		ERREXIT(cinfo, JERR_FILE_WRITE);

	dest->pub.next_output_byte = dest->buff;
	dest->pub.free_in_buffer = DST_BUF_SIZE;

	return TRUE;
}

static void termDest(j_compress_ptr cinfo) {
	ps_client_data* client = (ps_client_data*)cinfo->client_data;
	ps_dest_mgr* dest = (ps_dest_mgr*)cinfo->dest;
	size_t cb = DST_BUF_SIZE - dest->pub.free_in_buffer;

	if (cb > 0 && (*client->write_file)(client->stream_handle, dest->buff, cb) != cb)
		ERREXIT(cinfo, JERR_FILE_WRITE);
}

static boolean fillSource(j_decompress_ptr cinfo)
{
	ps_client_data* client = (ps_client_data*)cinfo->client_data;
	ps_src_mgr* src = (ps_src_mgr*)cinfo->src;

  size_t cb = (*client->read_file)(client->stream_handle, src->buff, SRC_BUF_SIZE);
	if (cb == ~0)
		ERREXIT(cinfo, JERR_FILE_READ);

  src->pub.next_input_byte = src->buff;
  src->pub.bytes_in_buffer = cb;

  return TRUE;
}

static void skipSource(j_decompress_ptr cinfo, long num_bytes)
{
	ps_client_data* client = (ps_client_data*)cinfo->client_data;
	ps_src_mgr* src = (ps_src_mgr*)cinfo->src;
	size_t cb = (size_t)num_bytes;

	if (cb > src->pub.bytes_in_buffer) {
		cb -= src->pub.bytes_in_buffer;
		cb = (*client->seek_file)(client->stream_handle, cb);
		if (cb == ~0)
			ERREXIT(cinfo, JERR_FILE_READ);

		if (cb == 0) {
			// EOF reached -- fabricate an EOI marker
			src->buff[0] = (JOCTET)0xFF;
			src->buff[1] = (JOCTET)JPEG_EOI;
			cb = 2;
		}

		src->pub.next_input_byte = NULL;
		src->pub.bytes_in_buffer = 0;
	} else {
		src->pub.next_input_byte += cb;
		src->pub.bytes_in_buffer -= cb;
	}
}

static struct jpeg_error_mgr* setErr(ps_error_mgr* err) {
	struct jpeg_error_mgr* jerr = (struct jpeg_error_mgr*)err;

	jpeg_std_error(jerr);
	jerr->error_exit = throwError;
	jerr->output_message = nullOutput;
	jerr->emit_message = nullEmit;
	memset(&err->msg, 0, MSG_BUF_SIZE);

	return jerr;
}

static void setDest(j_compress_ptr cinfo) {
	ps_dest_mgr* dest = (*cinfo->mem->alloc_small)((j_common_ptr)cinfo, JPOOL_PERMANENT, sizeof(ps_dest_mgr));

	dest->buff = NULL;
	dest->pub.init_destination = initDest;
	dest->pub.empty_output_buffer = writeDest;
	dest->pub.term_destination = termDest;

	cinfo->dest = (struct jpeg_destination_mgr*)dest;
}

static void setSource(j_decompress_ptr cinfo) {
	ps_src_mgr* src = (*cinfo->mem->alloc_small)((j_common_ptr)cinfo, JPOOL_PERMANENT, sizeof(ps_src_mgr));

	src->buff = (JOCTET*)(*cinfo->mem->alloc_small)((j_common_ptr)cinfo, JPOOL_PERMANENT, SRC_BUF_SIZE * sizeof(JOCTET));
	src->pub.init_source = nullSource;
	src->pub.fill_input_buffer = fillSource;
	src->pub.skip_input_data = skipSource;
	src->pub.resync_to_restart = jpeg_resync_to_restart;
	src->pub.term_source = nullSource;
	src->pub.next_input_byte = NULL;
	src->pub.bytes_in_buffer = 0;

	cinfo->src = (struct jpeg_source_mgr*)src;
}

j_compress_ptr JpegCreateCompress() {
	j_compress_ptr cinfo = (j_compress_ptr)malloc(sizeof(struct jpeg_compress_struct));
	ps_error_mgr* err = (ps_error_mgr*)_mm_malloc(sizeof(ps_error_mgr), 16);
	if (!cinfo || !err) {
		_mm_free(err);
		free(cinfo);
		return NULL;
	}

	cinfo->err = setErr(err);

	TRY {
		jpeg_create_compress(cinfo);
		setDest(cinfo);
		return cinfo;
	} CATCH {
		JpegDestroy((j_common_ptr)cinfo);
		return NULL;
	}
}

j_decompress_ptr JpegCreateDecompress() {
	j_decompress_ptr cinfo = (j_decompress_ptr)malloc(sizeof(struct jpeg_decompress_struct));
	ps_error_mgr* err = (ps_error_mgr*)_mm_malloc(sizeof(ps_error_mgr), 16);
	if (!cinfo || !err) {
		_mm_free(err);
		free(cinfo);
		return NULL;
	}

	cinfo->err = setErr(err);

	TRY{
		jpeg_create_decompress(cinfo);
		setSource(cinfo);
		return cinfo;
	} CATCH {
		JpegDestroy((j_common_ptr)cinfo);
		return NULL;
	}
}

void JpegDestroy(j_common_ptr cinfo) {
	jpeg_destroy(cinfo);
	_mm_free(cinfo->err);
	free(cinfo);
}

void JpegAbortDecompress(j_decompress_ptr cinfo) {
	jpeg_abort_decompress(cinfo);
}

const char* JpegGetLastError(j_common_ptr cinfo) {
	return ((ps_error_mgr*)cinfo->err)->msg;
}

int JpegSetDefaults(j_compress_ptr cinfo) {
	TRY jpeg_set_defaults(cinfo);
	return TRY_RESULT;
}

int JpegSetQuality(j_compress_ptr cinfo, int quality) {
	TRY jpeg_set_quality(cinfo, quality, TRUE);
	return TRY_RESULT;
}

int JpegSimpleProgression(j_compress_ptr cinfo) {
	TRY jpeg_simple_progression(cinfo);
	return TRY_RESULT;
}

int JpegStartCompress(j_compress_ptr cinfo) {
	TRY jpeg_start_compress(cinfo, TRUE);
	return TRY_RESULT;
}

int JpegWriteScanlines(j_compress_ptr cinfo, JSAMPARRAY scanlines, JDIMENSION num_lines, JDIMENSION* lines_written) {
	TRY
		*lines_written = jpeg_write_scanlines(cinfo, scanlines, num_lines);
	CATCH
		*lines_written = 0;
	return TRY_RESULT;
}

int JpegWriteRawData(j_compress_ptr cinfo, JSAMPIMAGE data, JDIMENSION num_lines, JDIMENSION* lines_written) {
	TRY
		*lines_written = jpeg_write_raw_data(cinfo, data, num_lines);
	CATCH
		*lines_written = 0;
	return TRY_RESULT;
}

int JpegFinishCompress(j_compress_ptr cinfo) {
	TRY jpeg_finish_compress(cinfo);
	return TRY_RESULT;
}

int JpegWriteMarker(j_compress_ptr cinfo, int marker, const JOCTET* dataptr, unsigned int datalen) {
	TRY jpeg_write_marker(cinfo, marker, dataptr, datalen);
	return TRY_RESULT;
}

int JpegWriteIccProfile(j_compress_ptr cinfo, const JOCTET* icc_data_ptr, unsigned int icc_data_len) {
	TRY jpeg_write_icc_profile(cinfo, icc_data_ptr, icc_data_len);
	return TRY_RESULT;
}

int JpegReadHeader(j_decompress_ptr cinfo) {
	TRY jpeg_read_header(cinfo, TRUE);
	return TRY_RESULT;
}

int JpegStartDecompress(j_decompress_ptr cinfo) {
	TRY jpeg_start_decompress(cinfo);
	return TRY_RESULT;
}

int JpegReadScanlines(j_decompress_ptr cinfo, JSAMPARRAY scanlines, JDIMENSION max_lines, JDIMENSION* lines_read) {
	TRY
		*lines_read = jpeg_read_scanlines(cinfo, scanlines, max_lines);
	CATCH
		*lines_read = 0;
	return TRY_RESULT;
}

int JpegReadRawData(j_decompress_ptr cinfo, JSAMPIMAGE data, JDIMENSION max_lines, JDIMENSION* lines_read) {
	TRY
		*lines_read = jpeg_read_raw_data(cinfo, data, max_lines);
	CATCH
		*lines_read = 0;
	return TRY_RESULT;
}

int JpegSkipScanlines(j_decompress_ptr cinfo, JDIMENSION num_lines, JDIMENSION* lines_skipped) {
	TRY
		*lines_skipped = jpeg_skip_scanlines(cinfo, num_lines);
	CATCH
		*lines_skipped = 0;
	return TRY_RESULT;
}

int JpegCropScanline(j_decompress_ptr cinfo, JDIMENSION* xoffset, JDIMENSION* width) {
	TRY jpeg_crop_scanline(cinfo, xoffset, width);
	return TRY_RESULT;
}

int JpegFinishDecompress(j_decompress_ptr cinfo) {
	TRY jpeg_finish_decompress(cinfo);
	return TRY_RESULT;
}

int JpegSaveMarkers(j_decompress_ptr cinfo, int marker_code, unsigned int length_limit) {
	TRY jpeg_save_markers(cinfo, marker_code, length_limit);
	return TRY_RESULT;
}

int JpegReadIccProfile(j_decompress_ptr cinfo, JOCTET** icc_data_ptr, unsigned int* icc_data_len) {
	TRY jpeg_read_icc_profile(cinfo, icc_data_ptr, icc_data_len);
	return TRY_RESULT;
}
