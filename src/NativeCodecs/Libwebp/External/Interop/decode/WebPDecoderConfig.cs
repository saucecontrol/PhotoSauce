// Copyright © Clinton Ingram and Contributors. Licensed under the MIT License (MIT).

// Ported from libweb headers (decode.h)
// Original source Copyright 2012 Google Inc. All Rights Reserved.
// See third-party-notices in the repository root for more information.

namespace PhotoSauce.Interop.Libwebp;

internal partial struct WebPDecoderConfig
{
    public WebPBitstreamFeatures input;

    public WebPDecBuffer output;

    public WebPDecoderOptions options;
}
