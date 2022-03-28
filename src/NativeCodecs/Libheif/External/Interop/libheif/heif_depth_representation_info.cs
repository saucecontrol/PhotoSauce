// Copyright © Clinton Ingram and Contributors. Licensed under the MIT License (MIT).

// Ported from libheif headers (heif.h)
// Original source Copyright (c) struktur AG, Dirk Farin <farin@struktur.de>
// See third-party-notices in the repository root for more information.

namespace PhotoSauce.Interop.Libheif;

internal unsafe partial struct heif_depth_representation_info
{
    [NativeTypeName("uint8_t")]
    public byte version;

    [NativeTypeName("uint8_t")]
    public byte has_z_near;

    [NativeTypeName("uint8_t")]
    public byte has_z_far;

    [NativeTypeName("uint8_t")]
    public byte has_d_min;

    [NativeTypeName("uint8_t")]
    public byte has_d_max;

    public double z_near;

    public double z_far;

    public double d_min;

    public double d_max;

    [NativeTypeName("enum heif_depth_representation_type")]
    public heif_depth_representation_type depth_representation_type;

    [NativeTypeName("uint32_t")]
    public uint disparity_reference_view;

    [NativeTypeName("uint32_t")]
    public uint depth_nonlinear_representation_model_size;

    [NativeTypeName("uint8_t *")]
    public byte* depth_nonlinear_representation_model;
}
