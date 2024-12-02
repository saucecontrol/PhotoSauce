// Copyright © Clinton Ingram and Contributors
// SPDX-License-Identifier: MIT

// Ported from libjxl headers (encode.h)
// Original source Copyright (c) the JPEG XL Project Authors. All rights reserved.
// See third-party-notices in the repository root for more information.

namespace PhotoSauce.Interop.Libjxl;

internal enum JxlEncoderError
{
    JXL_ENC_ERR_OK = 0,
    JXL_ENC_ERR_GENERIC = 1,
    JXL_ENC_ERR_OOM = 2,
    JXL_ENC_ERR_JBRD = 3,
    JXL_ENC_ERR_BAD_INPUT = 4,
    JXL_ENC_ERR_NOT_SUPPORTED = 0x80,
    JXL_ENC_ERR_API_USAGE = 0x81,
}
