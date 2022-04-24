// Copyright © Clinton Ingram and Contributors. Licensed under the MIT License (MIT).

// Ported from libweb headers (mux.h)
// Original source Copyright 2012 Google Inc. All Rights Reserved.
// See third-party-notices in the repository root for more information.

namespace PhotoSauce.Interop.Libwebp;

internal enum WebPMuxError
{
    WEBP_MUX_OK = 1,
    WEBP_MUX_NOT_FOUND = 0,
    WEBP_MUX_INVALID_ARGUMENT = -1,
    WEBP_MUX_BAD_DATA = -2,
    WEBP_MUX_MEMORY_ERROR = -3,
    WEBP_MUX_NOT_ENOUGH_DATA = -4,
}
