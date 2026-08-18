// Copyright © Clinton Ingram and Contributors
// SPDX-License-Identifier: MIT

#include "psraw.h"

#include <cstring>
#include <new>

#include <libraw/libraw.h>

struct psraw_decoder {
    libraw_processed_image_t* image;
};

int PsRawVersion(void) {
    return libraw_versionNumber();
}

const char* PsRawStrError(int error_code) {
    return libraw_strerror(error_code);
}

psraw_decoder* PsRawCreate(const void* data, size_t length, const psraw_options* options, psraw_image_info* info, int* error_code) {
    if (error_code)
        *error_code = LIBRAW_UNSPECIFIED_ERROR;

    if (!data || !length || !options || !info)
        return nullptr;

    try {
        LibRaw raw;
        raw.imgdata.params.use_camera_wb = options->use_camera_white_balance != 0;
        raw.imgdata.params.no_auto_bright = options->auto_brightness == 0;
        raw.imgdata.params.half_size = options->half_size != 0;
        raw.imgdata.params.output_color = 1;
        raw.imgdata.params.output_bps = 8;

        int result = raw.open_buffer(data, length);
        if (result != LIBRAW_SUCCESS) {
            if (error_code)
                *error_code = result;
            return nullptr;
        }

        result = raw.unpack();
        if (result == LIBRAW_SUCCESS)
            result = raw.dcraw_process();

        if (result != LIBRAW_SUCCESS) {
            if (error_code)
                *error_code = result;
            return nullptr;
        }

        libraw_processed_image_t* image = raw.dcraw_make_mem_image(&result);
        if (!image || result != LIBRAW_SUCCESS) {
            if (image)
                LibRaw::dcraw_clear_mem(image);
            if (error_code)
                *error_code = result;
            return nullptr;
        }

        const size_t expected_size = (size_t)image->width * image->height * image->colors;
        if (image->type != LIBRAW_IMAGE_BITMAP || image->bits != 8 ||
            (image->colors != 1 && image->colors != 3) || image->data_size != expected_size) {
            LibRaw::dcraw_clear_mem(image);
            if (error_code)
                *error_code = LIBRAW_NOT_IMPLEMENTED;
            return nullptr;
        }

        psraw_decoder* decoder = new (std::nothrow) psraw_decoder { image };
        if (!decoder) {
            LibRaw::dcraw_clear_mem(image);
            if (error_code)
                *error_code = LIBRAW_UNSUFFICIENT_MEMORY;
            return nullptr;
        }

        info->width = image->width;
        info->height = image->height;
        info->channels = image->colors;
        if (error_code)
            *error_code = LIBRAW_SUCCESS;
        return decoder;
    }
    catch (const std::bad_alloc&) {
        if (error_code)
            *error_code = LIBRAW_UNSUFFICIENT_MEMORY;
        return nullptr;
    }
    catch (...) {
        return nullptr;
    }
}

int PsRawCopyPixels(const psraw_decoder* decoder, int x, int y, int width, int height, int stride, unsigned char* destination) {
    if (!decoder || !decoder->image || !destination || x < 0 || y < 0 || width < 0 || height < 0)
        return LIBRAW_UNSPECIFIED_ERROR;

    const libraw_processed_image_t* image = decoder->image;
    const int channels = image->colors;
    if (x > image->width - width || y > image->height - height || stride < width * channels)
        return LIBRAW_UNSPECIFIED_ERROR;

    const size_t source_stride = (size_t)image->width * channels;
    const size_t copy_length = (size_t)width * channels;
    const unsigned char* source = image->data + (size_t)y * source_stride + (size_t)x * channels;
    for (int row = 0; row < height; row++) {
        std::memcpy(destination, source, copy_length);
        source += source_stride;
        destination += stride;
    }

    return LIBRAW_SUCCESS;
}

void PsRawDestroy(psraw_decoder* decoder) {
    if (!decoder)
        return;

    LibRaw::dcraw_clear_mem(decoder->image);
    delete decoder;
}
