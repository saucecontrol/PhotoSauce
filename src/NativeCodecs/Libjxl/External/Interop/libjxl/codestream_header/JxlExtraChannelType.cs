// Copyright © Clinton Ingram and Contributors
// SPDX-License-Identifier: MIT

// Ported from libjxl headers (codestream_header.h)
// Original source Copyright (c) the JPEG XL Project Authors. All rights reserved.
// See third-party-notices in the repository root for more information.

namespace PhotoSauce.Interop.Libjxl;

internal enum JxlExtraChannelType
{
    JXL_CHANNEL_ALPHA,
    JXL_CHANNEL_DEPTH,
    JXL_CHANNEL_SPOT_COLOR,
    JXL_CHANNEL_SELECTION_MASK,
    JXL_CHANNEL_BLACK,
    JXL_CHANNEL_CFA,
    JXL_CHANNEL_THERMAL,
    JXL_CHANNEL_RESERVED0,
    JXL_CHANNEL_RESERVED1,
    JXL_CHANNEL_RESERVED2,
    JXL_CHANNEL_RESERVED3,
    JXL_CHANNEL_RESERVED4,
    JXL_CHANNEL_RESERVED5,
    JXL_CHANNEL_RESERVED6,
    JXL_CHANNEL_RESERVED7,
    JXL_CHANNEL_UNKNOWN,
    JXL_CHANNEL_OPTIONAL,
}
