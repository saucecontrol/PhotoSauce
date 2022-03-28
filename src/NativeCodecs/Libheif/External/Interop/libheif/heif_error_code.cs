// Copyright © Clinton Ingram and Contributors. Licensed under the MIT License (MIT).

// Ported from libheif headers (heif.h)
// Original source Copyright (c) struktur AG, Dirk Farin <farin@struktur.de>
// See third-party-notices in the repository root for more information.

namespace PhotoSauce.Interop.Libheif;

internal enum heif_error_code
{
    heif_error_Ok = 0,
    heif_error_Input_does_not_exist = 1,
    heif_error_Invalid_input = 2,
    heif_error_Unsupported_filetype = 3,
    heif_error_Unsupported_feature = 4,
    heif_error_Usage_error = 5,
    heif_error_Memory_allocation_error = 6,
    heif_error_Decoder_plugin_error = 7,
    heif_error_Encoder_plugin_error = 8,
    heif_error_Encoding_error = 9,
    heif_error_Color_profile_does_not_exist = 10,
}
