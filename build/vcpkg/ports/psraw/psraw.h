// Copyright © Clinton Ingram and Contributors
// SPDX-License-Identifier: MIT

#pragma once

#include <stddef.h>

#if defined(__GNUC__) && defined(DLLDEFINE)
#define PSRAW_API __attribute__((__visibility__("default")))
#elif defined(_MSC_VER) && defined(DLLDEFINE)
#define PSRAW_API __declspec(dllexport)
#elif defined(_MSC_VER)
#define PSRAW_API __declspec(dllimport)
#else
#define PSRAW_API
#endif

typedef struct psraw_decoder psraw_decoder;

typedef struct {
    int use_camera_white_balance;
    int auto_brightness;
    int half_size;
} psraw_options;

typedef struct {
    int width;
    int height;
    int channels;
} psraw_image_info;

#ifdef __cplusplus
extern "C" {
#endif

PSRAW_API int PsRawVersion(void);
PSRAW_API const char* PsRawStrError(int error_code);
PSRAW_API psraw_decoder* PsRawCreate(const void* data, size_t length, const psraw_options* options, psraw_image_info* info, int* error_code);
PSRAW_API int PsRawCopyPixels(const psraw_decoder* decoder, int x, int y, int width, int height, int stride, unsigned char* destination);
PSRAW_API void PsRawDestroy(psraw_decoder* decoder);

#ifdef __cplusplus
}
#endif
