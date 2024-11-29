// Copyright © Clinton Ingram and Contributors. Licensed under the MIT License (MIT).

// Ported from libjxl headers (decode.h)
// Original source Copyright (c) the JPEG XL Project Authors. All rights reserved.
// See third-party-notices in the repository root for more information.

namespace PhotoSauce.Interop.Libjxl;

internal enum JxlDecoderStatus
{
    JXL_DEC_SUCCESS = 0,
    JXL_DEC_ERROR = 1,
    JXL_DEC_NEED_MORE_INPUT = 2,
    JXL_DEC_NEED_PREVIEW_OUT_BUFFER = 3,
    JXL_DEC_NEED_IMAGE_OUT_BUFFER = 5,
    JXL_DEC_JPEG_NEED_MORE_OUTPUT = 6,
    JXL_DEC_BOX_NEED_MORE_OUTPUT = 7,
    JXL_DEC_BASIC_INFO = 0x40,
    JXL_DEC_COLOR_ENCODING = 0x100,
    JXL_DEC_PREVIEW_IMAGE = 0x200,
    JXL_DEC_FRAME = 0x400,
    JXL_DEC_FULL_IMAGE = 0x1000,
    JXL_DEC_JPEG_RECONSTRUCTION = 0x2000,
    JXL_DEC_BOX = 0x4000,
    JXL_DEC_FRAME_PROGRESSION = 0x8000,
    JXL_DEC_BOX_COMPLETE = 0x10000,
}
