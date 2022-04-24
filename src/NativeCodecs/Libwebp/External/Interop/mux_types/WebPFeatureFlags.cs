// Copyright © Clinton Ingram and Contributors. Licensed under the MIT License (MIT).

// Ported from libweb headers (mux_types.h)
// Original source Copyright 2012 Google Inc. All Rights Reserved.
// See third-party-notices in the repository root for more information.

using System;

namespace PhotoSauce.Interop.Libwebp;

[Flags]
internal enum WebPFeatureFlags
{
    ANIMATION_FLAG = 0x00000002,
    XMP_FLAG = 0x00000004,
    EXIF_FLAG = 0x00000008,
    ALPHA_FLAG = 0x00000010,
    ICCP_FLAG = 0x00000020,
    ALL_VALID_FLAGS = 0x0000003e,
}
