#include <setjmp.h>
#include <stdlib.h>
#include <string.h>
#include "pngpriv.h"
#include "pspng.h"

#ifdef sigsetjmp
#define SETJMP(e) sigsetjmp((e), 0)
#define LONGJMP siglongjmp
#else
#define SETJMP setjmp
#define LONGJMP longjmp
#endif

#define TRY int _jmp_res; if (!(_jmp_res = SETJMP(((ps_error_data*)png_get_error_ptr(handle->png_ptr))->jmp_buf)))
#define CATCH else
#define TRY_RESULT !_jmp_res ? TRUE : FALSE;

#define MSG_BUF_SIZE 256
#define ZLIB_MEM_LEVEL 9

typedef struct {
	jmp_buf jmp_buf;
	char error_msg[MSG_BUF_SIZE];
} ps_error_data;

static void throwError(png_structp png_ptr, png_const_charp error_message) {
	ps_error_data* err = (ps_error_data*)png_get_error_ptr(png_ptr);
	strncpy(err->error_msg, error_message, MSG_BUF_SIZE - 1);
	err->error_msg[MSG_BUF_SIZE - 1] = '\0';

	LONGJMP(err->jmp_buf, 1);
}

static void writeData(png_structp png_ptr, png_bytep data, size_t length) {
	ps_io_data* client = (ps_io_data*)png_get_io_ptr(png_ptr);

	if ((*client->write_callback)(client->stream_handle, data, length) != length)
		png_error(png_ptr, "Write failed.");
}

static void readData(png_structp png_ptr, png_bytep data, size_t length) {
	ps_io_data* client = (ps_io_data*)png_get_io_ptr(png_ptr);

	if ((*client->read_callback)(client->stream_handle, data, length) != length)
		png_error(png_ptr, "Read failed.");
}

png_uint_32 PngVersion() {
	return png_access_version_number();
}

ps_png_struct* PngCreateWrite() {
	png_structp png_ptr = png_create_write_struct(PNG_LIBPNG_VER_STRING, NULL, NULL, NULL);

	ps_png_struct* handle = (ps_png_struct*)malloc(sizeof(ps_png_struct));
	ps_error_data* err = (ps_error_data*)malloc(sizeof(ps_error_data));
	ps_io_data* io = (ps_io_data*)malloc(sizeof(ps_io_data));

	if (!png_ptr || !handle || !err || !io) {
		free(io);
		free(err);
		free(handle);
		free(png_ptr);
		return NULL;
	}

	handle->png_ptr = png_ptr;
	handle->info_ptr = NULL;
	handle->io_ptr = io;

	png_set_error_fn(png_ptr, memset(err, 0, sizeof(ps_error_data)), throwError, NULL);
	png_set_write_fn(png_ptr, memset(io, 0, sizeof(ps_io_data)), writeData, NULL);

	TRY{
		png_set_option(png_ptr, PNG_SKIP_sRGB_CHECK_PROFILE, PNG_OPTION_ON);
		png_set_compression_mem_level(png_ptr, ZLIB_MEM_LEVEL);
		return handle;
	} CATCH{
		PngDestroyWrite(handle);
		return NULL;
	}
}

ps_png_struct* PngCreateRead() {
	png_structp png_ptr = png_create_read_struct(PNG_LIBPNG_VER_STRING, NULL, NULL, NULL);
	png_infop info_ptr = png_create_info_struct(png_ptr);

	ps_png_struct* handle = (ps_png_struct*)malloc(sizeof(ps_png_struct));
	ps_error_data* err = (ps_error_data*)malloc(sizeof(ps_error_data));
	ps_io_data* io = (ps_io_data*)malloc(sizeof(ps_io_data));

	if (!png_ptr || !info_ptr || !handle || !err || !io) {
		free(io);
		free(err);
		free(handle);
		free(info_ptr);
		free(png_ptr);
		return NULL;
	}

	handle->png_ptr = png_ptr;
	handle->info_ptr = info_ptr;
	handle->io_ptr = io;

	png_set_error_fn(png_ptr, memset(err, 0, sizeof(ps_error_data)), throwError, NULL);
	png_set_read_fn(png_ptr, memset(io, 0, sizeof(ps_io_data)), readData);

	TRY {
		png_set_option(png_ptr, PNG_IGNORE_ADLER32, PNG_OPTION_ON);
		png_set_option(png_ptr, PNG_MAXIMUM_INFLATE_WINDOW, PNG_OPTION_ON);
		png_set_crc_action(png_ptr, PNG_CRC_QUIET_USE, PNG_CRC_QUIET_USE);
		return handle;
	} CATCH{
		PngDestroyRead(handle);
		return NULL;
	}
}

void PngDestroyWrite(ps_png_struct* handle) {
	free(png_get_io_ptr(handle->png_ptr));
	free(png_get_error_ptr(handle->png_ptr));
	png_destroy_write_struct(&handle->png_ptr, NULL);

	memset(handle, 0, sizeof(ps_png_struct));
	free(handle);
}

void PngDestroyRead(ps_png_struct* handle) {
	free(png_get_io_ptr(handle->png_ptr));
	free(png_get_error_ptr(handle->png_ptr));
	png_destroy_read_struct(&handle->png_ptr, &handle->info_ptr, NULL);

	memset(handle, 0, sizeof(ps_png_struct));
	free(handle);
}

const char* PngGetLastError(ps_png_struct* handle) {
	return ((ps_error_data*)png_get_error_ptr(handle->png_ptr))->error_msg;
}

int PngSetFilter(ps_png_struct* handle, int filters) {
	TRY png_set_filter(handle->png_ptr, PNG_FILTER_TYPE_DEFAULT, filters);
	return TRY_RESULT;
}

int PngSetCompressionLevel(ps_png_struct* handle, int level) {
	TRY png_set_compression_level(handle->png_ptr, level);
	return TRY_RESULT;
}

int PngWriteSig(ps_png_struct* handle) {
	TRY png_write_sig(handle->png_ptr);
	return TRY_RESULT;
}

int PngWriteIhdr(ps_png_struct* handle, png_uint_32 width, png_uint_32 height, int bit_depth, int color_type, int interlace_method) {
	TRY {
		png_write_IHDR(handle->png_ptr, width, height, bit_depth, color_type, PNG_COMPRESSION_TYPE_DEFAULT, PNG_FILTER_TYPE_DEFAULT, interlace_method);
		handle->png_ptr->mode |= PNG_WROTE_INFO_BEFORE_PLTE;
	}
	return TRY_RESULT;
}

int PngWriteIccp(ps_png_struct* handle, png_const_bytep profile) {
	TRY png_write_iCCP(handle->png_ptr, "ICC", profile);
	return TRY_RESULT;
}

int PngWriteSrgb(ps_png_struct* handle) {
	TRY png_write_sRGB(handle->png_ptr, PNG_sRGB_INTENT_PERCEPTUAL);
	return TRY_RESULT;
}

int PngWritePlte(ps_png_struct* handle, png_const_colorp palette, int num_pal) {
	TRY png_write_PLTE(handle->png_ptr, palette, (png_uint_32)num_pal);
	return TRY_RESULT;
}

int PngWriteTrns(ps_png_struct* handle, png_const_bytep trans, int num_trans) {
	TRY png_write_tRNS(handle->png_ptr, trans, NULL, num_trans, PNG_COLOR_TYPE_PALETTE);
	return TRY_RESULT;
}

int PngWritePhys(ps_png_struct* handle, png_uint_32 x_pixels_per_meter, png_uint_32 y_pixels_per_meter) {
	TRY png_write_pHYs(handle->png_ptr, x_pixels_per_meter, y_pixels_per_meter, PNG_RESOLUTION_METER);
	return TRY_RESULT;
}

int PngWriteExif(ps_png_struct* handle, png_const_bytep exif, int num_exif) {
	TRY png_write_eXIf(handle->png_ptr, (png_bytep)exif, num_exif);
	return TRY_RESULT;
}

int PngWriteRow(ps_png_struct* handle, png_const_bytep row) {
	TRY png_write_row(handle->png_ptr, row);
	return TRY_RESULT;
}

int PngWriteImage(ps_png_struct* handle, png_bytepp image) {
	TRY png_write_image(handle->png_ptr, image);
	return TRY_RESULT;
}

int PngWriteIend(ps_png_struct* handle) {
	TRY png_write_IEND(handle->png_ptr);
	return TRY_RESULT;
}
