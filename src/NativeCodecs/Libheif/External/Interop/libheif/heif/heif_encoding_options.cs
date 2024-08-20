// Copyright © Clinton Ingram and Contributors. Licensed under the MIT License (MIT).

// Ported from libheif headers (heif.h)
// Original source Copyright (c) struktur AG, Dirk Farin
// See third-party-notices in the repository root for more information.

namespace PhotoSauce.Interop.Libheif;

internal unsafe partial struct heif_encoding_options
{
    [NativeTypeName("uint8_t")]
    public byte version;

    [NativeTypeName("uint8_t")]
    public byte save_alpha_channel;

    [NativeTypeName("uint8_t")]
    public byte macOS_compatibility_workaround;

    [NativeTypeName("uint8_t")]
    public byte save_two_colr_boxes_when_ICC_and_nclx_available;

    [NativeTypeName("struct heif_color_profile_nclx *")]
    public heif_color_profile_nclx* output_nclx_profile;

    [NativeTypeName("uint8_t")]
    public byte macOS_compatibility_workaround_no_nclx_profile;

    [NativeTypeName("enum heif_orientation")]
    public heif_orientation image_orientation;

    [NativeTypeName("struct heif_color_conversion_options")]
    public heif_color_conversion_options color_conversion_options;

    [NativeTypeName("uint8_t")]
    public byte prefer_uncC_short_form;
}
