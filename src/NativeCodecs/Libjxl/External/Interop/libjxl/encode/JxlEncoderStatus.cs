// Copyright © Clinton Ingram and Contributors. Licensed under the MIT License (MIT).

// Ported from libjxl headers (encode.h)
// Original source Copyright (c) the JPEG XL Project Authors. All rights reserved.
// See third-party-notices in the repository root for more information.

namespace PhotoSauce.Interop.Libjxl;

internal enum JxlEncoderStatus
{
    JXL_ENC_SUCCESS = 0,
    JXL_ENC_ERROR = 1,
    JXL_ENC_NEED_MORE_OUTPUT = 2,
    JXL_ENC_NOT_SUPPORTED = 3,
}
