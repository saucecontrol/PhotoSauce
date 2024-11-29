// Copyright © Clinton Ingram and Contributors. Licensed under the MIT License (MIT).

// Ported from libheif headers (heif_properties.h)
// Original source Copyright (c) struktur AG, Dirk Farin
// See third-party-notices in the repository root for more information.

using System.Runtime.InteropServices;

namespace PhotoSauce.Interop.Libheif;

internal static unsafe partial class Libheif
{
    [DllImport("heif", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    public static extern int heif_item_get_properties_of_type([NativeTypeName("const struct heif_context *")] void* context, [NativeTypeName("heif_item_id")] uint id, [NativeTypeName("enum heif_item_property_type")] heif_item_property_type type, [NativeTypeName("heif_property_id *")] uint* out_list, int count);

    [DllImport("heif", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    public static extern int heif_item_get_transformation_properties([NativeTypeName("const struct heif_context *")] void* context, [NativeTypeName("heif_item_id")] uint id, [NativeTypeName("heif_property_id *")] uint* out_list, int count);

    [DllImport("heif", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    [return: NativeTypeName("enum heif_item_property_type")]
    public static extern heif_item_property_type heif_item_get_property_type([NativeTypeName("const struct heif_context *")] void* context, [NativeTypeName("heif_item_id")] uint id, [NativeTypeName("heif_property_id")] uint property_id);

    [DllImport("heif", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    [return: NativeTypeName("struct heif_error")]
    public static extern heif_error heif_item_get_property_user_description([NativeTypeName("const struct heif_context *")] void* context, [NativeTypeName("heif_item_id")] uint itemId, [NativeTypeName("heif_property_id")] uint propertyId, [NativeTypeName("struct heif_property_user_description **")] heif_property_user_description** @out);

    [DllImport("heif", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    [return: NativeTypeName("struct heif_error")]
    public static extern heif_error heif_item_add_property_user_description([NativeTypeName("const struct heif_context *")] void* context, [NativeTypeName("heif_item_id")] uint itemId, [NativeTypeName("const struct heif_property_user_description *")] heif_property_user_description* description, [NativeTypeName("heif_property_id *")] uint* out_propertyId);

    [DllImport("heif", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    public static extern void heif_property_user_description_release([NativeTypeName("struct heif_property_user_description *")] heif_property_user_description* param0);

    [DllImport("heif", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    [return: NativeTypeName("enum heif_transform_mirror_direction")]
    public static extern heif_transform_mirror_direction heif_item_get_property_transform_mirror([NativeTypeName("const struct heif_context *")] void* context, [NativeTypeName("heif_item_id")] uint itemId, [NativeTypeName("heif_property_id")] uint propertyId);

    [DllImport("heif", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    public static extern int heif_item_get_property_transform_rotation_ccw([NativeTypeName("const struct heif_context *")] void* context, [NativeTypeName("heif_item_id")] uint itemId, [NativeTypeName("heif_property_id")] uint propertyId);

    [DllImport("heif", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    public static extern void heif_item_get_property_transform_crop_borders([NativeTypeName("const struct heif_context *")] void* context, [NativeTypeName("heif_item_id")] uint itemId, [NativeTypeName("heif_property_id")] uint propertyId, int image_width, int image_height, int* left, int* top, int* right, int* bottom);

    [DllImport("heif", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    [return: NativeTypeName("struct heif_error")]
    public static extern heif_error heif_item_add_raw_property([NativeTypeName("const struct heif_context *")] void* context, [NativeTypeName("heif_item_id")] uint itemId, [NativeTypeName("uint32_t")] uint fourcc_type, [NativeTypeName("const uint8_t *")] byte* uuid_type, [NativeTypeName("const uint8_t *")] byte* data, [NativeTypeName("size_t")] nuint size, int is_essential, [NativeTypeName("heif_property_id *")] uint* out_propertyId);

    [DllImport("heif", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    [return: NativeTypeName("struct heif_error")]
    public static extern heif_error heif_item_get_property_raw_size([NativeTypeName("const struct heif_context *")] void* context, [NativeTypeName("heif_item_id")] uint itemId, [NativeTypeName("heif_property_id")] uint propertyId, [NativeTypeName("size_t *")] nuint* out_size);

    [DllImport("heif", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    [return: NativeTypeName("struct heif_error")]
    public static extern heif_error heif_item_get_property_raw_data([NativeTypeName("const struct heif_context *")] void* context, [NativeTypeName("heif_item_id")] uint itemId, [NativeTypeName("heif_property_id")] uint propertyId, [NativeTypeName("uint8_t *")] byte* out_data);

    [DllImport("heif", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    [return: NativeTypeName("struct heif_error")]
    public static extern heif_error heif_item_get_property_uuid_type([NativeTypeName("const struct heif_context *")] void* context, [NativeTypeName("heif_item_id")] uint itemId, [NativeTypeName("heif_property_id")] uint propertyId, [NativeTypeName("uint8_t[16]")] byte* out_extended_type);
}
