// Copyright © Clinton Ingram and Contributors
// SPDX-License-Identifier: MIT

// Ported from libweb headers (demux.h)
// Original source Copyright 2012 Google Inc. All Rights Reserved.
// See third-party-notices in the repository root for more information.

namespace PhotoSauce.Interop.Libwebp;

internal enum WebPDemuxState
{
    WEBP_DEMUX_PARSE_ERROR = -1,
    WEBP_DEMUX_PARSING_HEADER = 0,
    WEBP_DEMUX_PARSED_HEADER = 1,
    WEBP_DEMUX_DONE = 2,
}
