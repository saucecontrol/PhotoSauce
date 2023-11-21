// Copyright © Clinton Ingram and Contributors. Licensed under the MIT License (MIT).

// Ported from libheif headers (heif.h)
// Original source Copyright (c) struktur AG, Dirk Farin
// See third-party-notices in the repository root for more information.

namespace PhotoSauce.Interop.Libheif;

internal partial struct heif_color_conversion_options
{
    [NativeTypeName("uint8_t")]
    public byte version;

    [NativeTypeName("enum heif_chroma_downsampling_algorithm")]
    public heif_chroma_downsampling_algorithm preferred_chroma_downsampling_algorithm;

    [NativeTypeName("enum heif_chroma_upsampling_algorithm")]
    public heif_chroma_upsampling_algorithm preferred_chroma_upsampling_algorithm;

    [NativeTypeName("uint8_t")]
    public byte only_use_preferred_chroma_algorithm;
}
