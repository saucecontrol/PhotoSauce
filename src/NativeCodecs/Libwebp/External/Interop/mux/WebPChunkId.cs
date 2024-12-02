// Copyright © Clinton Ingram and Contributors
// SPDX-License-Identifier: MIT

// Ported from libweb headers (mux.h)
// Original source Copyright 2012 Google Inc. All Rights Reserved.
// See third-party-notices in the repository root for more information.

namespace PhotoSauce.Interop.Libwebp;

internal enum WebPChunkId
{
    WEBP_CHUNK_VP8X,
    WEBP_CHUNK_ICCP,
    WEBP_CHUNK_ANIM,
    WEBP_CHUNK_ANMF,
    WEBP_CHUNK_DEPRECATED,
    WEBP_CHUNK_ALPHA,
    WEBP_CHUNK_IMAGE,
    WEBP_CHUNK_EXIF,
    WEBP_CHUNK_XMP,
    WEBP_CHUNK_UNKNOWN,
    WEBP_CHUNK_NIL,
}
