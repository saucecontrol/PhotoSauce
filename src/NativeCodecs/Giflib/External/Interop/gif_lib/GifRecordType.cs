// Copyright © Clinton Ingram and Contributors
// SPDX-License-Identifier: MIT

// Ported from GIFLIB headers (gif_lib.h)
// Original source Copyright (c) 1997  Eric S. Raymond.
// See third-party-notices in the repository root for more information.

namespace PhotoSauce.Interop.Giflib;

internal enum GifRecordType
{
    UNDEFINED_RECORD_TYPE,
    SCREEN_DESC_RECORD_TYPE,
    IMAGE_DESC_RECORD_TYPE,
    EXTENSION_RECORD_TYPE,
    TERMINATE_RECORD_TYPE,
}
