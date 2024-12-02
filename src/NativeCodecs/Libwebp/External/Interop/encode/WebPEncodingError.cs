// Copyright © Clinton Ingram and Contributors
// SPDX-License-Identifier: MIT

// Ported from libweb headers (encode.h)
// Original source Copyright 2012 Google Inc. All Rights Reserved.
// See third-party-notices in the repository root for more information.

namespace PhotoSauce.Interop.Libwebp;

internal enum WebPEncodingError
{
    VP8_ENC_OK = 0,
    VP8_ENC_ERROR_OUT_OF_MEMORY,
    VP8_ENC_ERROR_BITSTREAM_OUT_OF_MEMORY,
    VP8_ENC_ERROR_NULL_PARAMETER,
    VP8_ENC_ERROR_INVALID_CONFIGURATION,
    VP8_ENC_ERROR_BAD_DIMENSION,
    VP8_ENC_ERROR_PARTITION0_OVERFLOW,
    VP8_ENC_ERROR_PARTITION_OVERFLOW,
    VP8_ENC_ERROR_BAD_WRITE,
    VP8_ENC_ERROR_FILE_TOO_BIG,
    VP8_ENC_ERROR_USER_ABORT,
    VP8_ENC_ERROR_LAST,
}
