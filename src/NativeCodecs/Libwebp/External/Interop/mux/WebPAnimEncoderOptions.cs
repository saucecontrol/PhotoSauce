// Copyright © Clinton Ingram and Contributors. Licensed under the MIT License (MIT).

// Ported from libweb headers (mux.h)
// Original source Copyright 2012 Google Inc. All Rights Reserved.
// See third-party-notices in the repository root for more information.

namespace PhotoSauce.Interop.Libwebp;

internal unsafe partial struct WebPAnimEncoderOptions
{
    public WebPMuxAnimParams anim_params;

    public int minimize_size;

    public int kmin;

    public int kmax;

    public int allow_mixed;

    public int verbose;

    [NativeTypeName("uint32_t[4]")]
    public fixed uint padding[4];
}
