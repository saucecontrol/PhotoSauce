// Copyright © Clinton Ingram and Contributors. Licensed under the MIT License (MIT).

// Ported from libjxl headers
// Original source Copyright (c) the JPEG XL Project Authors. All rights reserved.
// See third-party-notices in the repository root for more information.

namespace PhotoSauce.Interop.Libjxl
{
    internal enum JxlSignature
    {
        JXL_SIG_NOT_ENOUGH_BYTES = 0,
        JXL_SIG_INVALID = 1,
        JXL_SIG_CODESTREAM = 2,
        JXL_SIG_CONTAINER = 3,
    }
}
