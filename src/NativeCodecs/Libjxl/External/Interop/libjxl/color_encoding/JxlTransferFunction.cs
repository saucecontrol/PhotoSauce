// Copyright © Clinton Ingram and Contributors. Licensed under the MIT License (MIT).

// Ported from libjxl headers (color_encoding.h)
// Original source Copyright (c) the JPEG XL Project Authors. All rights reserved.
// See third-party-notices in the repository root for more information.

namespace PhotoSauce.Interop.Libjxl;

internal enum JxlTransferFunction
{
    JXL_TRANSFER_FUNCTION_709 = 1,
    JXL_TRANSFER_FUNCTION_UNKNOWN = 2,
    JXL_TRANSFER_FUNCTION_LINEAR = 8,
    JXL_TRANSFER_FUNCTION_SRGB = 13,
    JXL_TRANSFER_FUNCTION_PQ = 16,
    JXL_TRANSFER_FUNCTION_DCI = 17,
    JXL_TRANSFER_FUNCTION_HLG = 18,
    JXL_TRANSFER_FUNCTION_GAMMA = 65535,
}
