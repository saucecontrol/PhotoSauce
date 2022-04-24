// Copyright © Clinton Ingram and Contributors. Licensed under the MIT License (MIT).

// Ported from libweb headers (mux.h)
// Original source Copyright 2012 Google Inc. All Rights Reserved.
// See third-party-notices in the repository root for more information.

namespace PhotoSauce.Interop.Libwebp;

internal partial struct WebPMuxAnimParams
{
    [NativeTypeName("uint32_t")]
    public uint bgcolor;

    public int loop_count;
}
