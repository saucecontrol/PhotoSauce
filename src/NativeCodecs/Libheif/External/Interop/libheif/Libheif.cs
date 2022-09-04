// Copyright © Clinton Ingram and Contributors. Licensed under the MIT License (MIT).

// Ported from libheif headers (heif.h)
// Original source Copyright (c) struktur AG, Dirk Farin <farin@struktur.de>
// See third-party-notices in the repository root for more information.

using System;
using System.Runtime.InteropServices;
using static PhotoSauce.Interop.Libheif.heif_chroma;

namespace PhotoSauce.Interop.Libheif;

internal static unsafe partial class Libheif
{
    [DllImport("heif", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    [return: NativeTypeName("const char *")]
    public static extern sbyte* heif_get_version();

    [DllImport("heif", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    [return: NativeTypeName("uint32_t")]
    public static extern uint heif_get_version_number();

    [DllImport("heif", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    public static extern int heif_get_version_number_major();

    [DllImport("heif", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    public static extern int heif_get_version_number_minor();

    [DllImport("heif", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    public static extern int heif_get_version_number_maintenance();

    [DllImport("heif", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    [return: NativeTypeName("struct heif_error")]
    public static extern heif_error heif_init([NativeTypeName("struct heif_init_params *")] heif_init_params* param0);

    [DllImport("heif", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    public static extern void heif_deinit();

    [DllImport("heif", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    [return: NativeTypeName("enum heif_filetype_result")]
    public static extern heif_filetype_result heif_check_filetype([NativeTypeName("const uint8_t *")] byte* data, int len);

    [DllImport("heif", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    [return: NativeTypeName("enum heif_brand")]
    public static extern heif_brand heif_main_brand([NativeTypeName("const uint8_t *")] byte* data, int len);

    [DllImport("heif", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    [return: NativeTypeName("heif_brand2")]
    public static extern uint heif_read_main_brand([NativeTypeName("const uint8_t *")] byte* data, int len);

    [DllImport("heif", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    [return: NativeTypeName("heif_brand2")]
    public static extern uint heif_fourcc_to_brand([NativeTypeName("const char *")] sbyte* brand_fourcc);

    [DllImport("heif", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    public static extern void heif_brand_to_fourcc([NativeTypeName("heif_brand2")] uint brand, [NativeTypeName("char *")] sbyte* out_fourcc);

    [DllImport("heif", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    public static extern int heif_has_compatible_brand([NativeTypeName("const uint8_t *")] byte* data, int len, [NativeTypeName("const char *")] sbyte* brand_fourcc);

    [DllImport("heif", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    [return: NativeTypeName("struct heif_error")]
    public static extern heif_error heif_list_compatible_brands([NativeTypeName("const uint8_t *")] byte* data, int len, [NativeTypeName("heif_brand2 **")] uint** out_brands, int* out_size);

    [DllImport("heif", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    public static extern void heif_free_list_of_compatible_brands([NativeTypeName("heif_brand2 *")] uint* brands_list);

    [DllImport("heif", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    [return: NativeTypeName("const char *")]
    public static extern sbyte* heif_get_file_mime_type([NativeTypeName("const uint8_t *")] byte* data, int len);

    [DllImport("heif", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    [return: NativeTypeName("struct heif_context *")]
    public static extern IntPtr heif_context_alloc();

    [DllImport("heif", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    public static extern void heif_context_free([NativeTypeName("struct heif_context *")] IntPtr param0);

    [DllImport("heif", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    [return: NativeTypeName("struct heif_error")]
    public static extern heif_error heif_context_read_from_file([NativeTypeName("struct heif_context *")] IntPtr param0, [NativeTypeName("const char *")] sbyte* filename, [NativeTypeName("const struct heif_reading_options *")] void* param2);

    [DllImport("heif", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    [return: NativeTypeName("struct heif_error")]
    public static extern heif_error heif_context_read_from_memory([NativeTypeName("struct heif_context *")] IntPtr param0, [NativeTypeName("const void *")] void* mem, [NativeTypeName("size_t")] nuint size, [NativeTypeName("const struct heif_reading_options *")] void* param3);

    [DllImport("heif", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    [return: NativeTypeName("struct heif_error")]
    public static extern heif_error heif_context_read_from_memory_without_copy([NativeTypeName("struct heif_context *")] IntPtr param0, [NativeTypeName("const void *")] void* mem, [NativeTypeName("size_t")] nuint size, [NativeTypeName("const struct heif_reading_options *")] void* param3);

    [DllImport("heif", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    [return: NativeTypeName("struct heif_error")]
    public static extern heif_error heif_context_read_from_reader([NativeTypeName("struct heif_context *")] IntPtr param0, [NativeTypeName("const struct heif_reader *")] heif_reader* reader, void* userdata, [NativeTypeName("const struct heif_reading_options *")] void* param3);

    [DllImport("heif", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    public static extern int heif_context_get_number_of_top_level_images([NativeTypeName("struct heif_context *")] IntPtr ctx);

    [DllImport("heif", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    public static extern int heif_context_is_top_level_image_ID([NativeTypeName("struct heif_context *")] IntPtr ctx, [NativeTypeName("heif_item_id")] uint id);

    [DllImport("heif", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    public static extern int heif_context_get_list_of_top_level_image_IDs([NativeTypeName("struct heif_context *")] IntPtr ctx, [NativeTypeName("heif_item_id *")] uint* ID_array, int count);

    [DllImport("heif", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    [return: NativeTypeName("struct heif_error")]
    public static extern heif_error heif_context_get_primary_image_ID([NativeTypeName("struct heif_context *")] IntPtr ctx, [NativeTypeName("heif_item_id *")] uint* id);

    [DllImport("heif", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    [return: NativeTypeName("struct heif_error")]
    public static extern heif_error heif_context_get_primary_image_handle([NativeTypeName("struct heif_context *")] IntPtr ctx, [NativeTypeName("struct heif_image_handle **")] IntPtr* param1);

    [DllImport("heif", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    [return: NativeTypeName("struct heif_error")]
    public static extern heif_error heif_context_get_image_handle([NativeTypeName("struct heif_context *")] IntPtr ctx, [NativeTypeName("heif_item_id")] uint id, [NativeTypeName("struct heif_image_handle **")] IntPtr* param2);

    [DllImport("heif", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    public static extern void heif_context_debug_dump_boxes_to_file([NativeTypeName("struct heif_context *")] IntPtr ctx, int fd);

    [DllImport("heif", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    public static extern void heif_context_set_maximum_image_size_limit([NativeTypeName("struct heif_context *")] IntPtr ctx, int maximum_width);

    [DllImport("heif", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    public static extern void heif_context_set_max_decoding_threads([NativeTypeName("struct heif_context *")] IntPtr ctx, int max_threads);

    [DllImport("heif", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    public static extern void heif_image_handle_release([NativeTypeName("const struct heif_image_handle *")] IntPtr param0);

    [DllImport("heif", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    public static extern int heif_image_handle_is_primary_image([NativeTypeName("const struct heif_image_handle *")] IntPtr handle);

    [DllImport("heif", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    public static extern int heif_image_handle_get_width([NativeTypeName("const struct heif_image_handle *")] IntPtr handle);

    [DllImport("heif", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    public static extern int heif_image_handle_get_height([NativeTypeName("const struct heif_image_handle *")] IntPtr handle);

    [DllImport("heif", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    public static extern int heif_image_handle_has_alpha_channel([NativeTypeName("const struct heif_image_handle *")] IntPtr param0);

    [DllImport("heif", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    public static extern int heif_image_handle_is_premultiplied_alpha([NativeTypeName("const struct heif_image_handle *")] IntPtr param0);

    [DllImport("heif", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    public static extern int heif_image_handle_get_luma_bits_per_pixel([NativeTypeName("const struct heif_image_handle *")] IntPtr param0);

    [DllImport("heif", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    public static extern int heif_image_handle_get_chroma_bits_per_pixel([NativeTypeName("const struct heif_image_handle *")] IntPtr param0);

    [DllImport("heif", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    public static extern int heif_image_handle_get_ispe_width([NativeTypeName("const struct heif_image_handle *")] IntPtr handle);

    [DllImport("heif", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    public static extern int heif_image_handle_get_ispe_height([NativeTypeName("const struct heif_image_handle *")] IntPtr handle);

    [DllImport("heif", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    public static extern int heif_image_handle_has_depth_image([NativeTypeName("const struct heif_image_handle *")] IntPtr param0);

    [DllImport("heif", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    public static extern int heif_image_handle_get_number_of_depth_images([NativeTypeName("const struct heif_image_handle *")] IntPtr handle);

    [DllImport("heif", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    public static extern int heif_image_handle_get_list_of_depth_image_IDs([NativeTypeName("const struct heif_image_handle *")] IntPtr handle, [NativeTypeName("heif_item_id *")] uint* ids, int count);

    [DllImport("heif", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    [return: NativeTypeName("struct heif_error")]
    public static extern heif_error heif_image_handle_get_depth_image_handle([NativeTypeName("const struct heif_image_handle *")] IntPtr handle, [NativeTypeName("heif_item_id")] uint depth_image_id, [NativeTypeName("struct heif_image_handle **")] IntPtr* out_depth_handle);

    [DllImport("heif", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    public static extern void heif_depth_representation_info_free([NativeTypeName("const struct heif_depth_representation_info *")] heif_depth_representation_info* info);

    [DllImport("heif", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    public static extern int heif_image_handle_get_depth_image_representation_info([NativeTypeName("const struct heif_image_handle *")] IntPtr handle, [NativeTypeName("heif_item_id")] uint depth_image_id, [NativeTypeName("const struct heif_depth_representation_info **")] heif_depth_representation_info** @out);

    [DllImport("heif", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    public static extern int heif_image_handle_get_number_of_thumbnails([NativeTypeName("const struct heif_image_handle *")] IntPtr handle);

    [DllImport("heif", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    public static extern int heif_image_handle_get_list_of_thumbnail_IDs([NativeTypeName("const struct heif_image_handle *")] IntPtr handle, [NativeTypeName("heif_item_id *")] uint* ids, int count);

    [DllImport("heif", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    [return: NativeTypeName("struct heif_error")]
    public static extern heif_error heif_image_handle_get_thumbnail([NativeTypeName("const struct heif_image_handle *")] IntPtr main_image_handle, [NativeTypeName("heif_item_id")] uint thumbnail_id, [NativeTypeName("struct heif_image_handle **")] IntPtr* out_thumbnail_handle);

    [DllImport("heif", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    public static extern int heif_image_handle_get_number_of_auxiliary_images([NativeTypeName("const struct heif_image_handle *")] IntPtr handle, int aux_filter);

    [DllImport("heif", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    public static extern int heif_image_handle_get_list_of_auxiliary_image_IDs([NativeTypeName("const struct heif_image_handle *")] IntPtr handle, int aux_filter, [NativeTypeName("heif_item_id *")] uint* ids, int count);

    [DllImport("heif", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    [return: NativeTypeName("struct heif_error")]
    public static extern heif_error heif_image_handle_get_auxiliary_type([NativeTypeName("const struct heif_image_handle *")] IntPtr handle, [NativeTypeName("const char **")] sbyte** out_type);

    [DllImport("heif", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    public static extern void heif_image_handle_free_auxiliary_types([NativeTypeName("const struct heif_image_handle *")] IntPtr handle, [NativeTypeName("const char **")] sbyte** out_type);

    [DllImport("heif", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    [return: NativeTypeName("struct heif_error")]
    public static extern heif_error heif_image_handle_get_auxiliary_image_handle([NativeTypeName("const struct heif_image_handle *")] IntPtr main_image_handle, [NativeTypeName("heif_item_id")] uint auxiliary_id, [NativeTypeName("struct heif_image_handle **")] IntPtr* out_auxiliary_handle);

    [DllImport("heif", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    public static extern int heif_image_handle_get_number_of_metadata_blocks([NativeTypeName("const struct heif_image_handle *")] IntPtr handle, [NativeTypeName("const char *")] sbyte* type_filter);

    [DllImport("heif", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    public static extern int heif_image_handle_get_list_of_metadata_block_IDs([NativeTypeName("const struct heif_image_handle *")] IntPtr handle, [NativeTypeName("const char *")] sbyte* type_filter, [NativeTypeName("heif_item_id *")] uint* ids, int count);

    [DllImport("heif", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    [return: NativeTypeName("const char *")]
    public static extern sbyte* heif_image_handle_get_metadata_type([NativeTypeName("const struct heif_image_handle *")] IntPtr handle, [NativeTypeName("heif_item_id")] uint metadata_id);

    [DllImport("heif", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    [return: NativeTypeName("const char *")]
    public static extern sbyte* heif_image_handle_get_metadata_content_type([NativeTypeName("const struct heif_image_handle *")] IntPtr handle, [NativeTypeName("heif_item_id")] uint metadata_id);

    [DllImport("heif", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    [return: NativeTypeName("size_t")]
    public static extern nuint heif_image_handle_get_metadata_size([NativeTypeName("const struct heif_image_handle *")] IntPtr handle, [NativeTypeName("heif_item_id")] uint metadata_id);

    [DllImport("heif", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    [return: NativeTypeName("struct heif_error")]
    public static extern heif_error heif_image_handle_get_metadata([NativeTypeName("const struct heif_image_handle *")] IntPtr handle, [NativeTypeName("heif_item_id")] uint metadata_id, void* out_data);

    [DllImport("heif", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    [return: NativeTypeName("enum heif_color_profile_type")]
    public static extern heif_color_profile_type heif_image_handle_get_color_profile_type([NativeTypeName("const struct heif_image_handle *")] IntPtr handle);

    [DllImport("heif", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    [return: NativeTypeName("size_t")]
    public static extern nuint heif_image_handle_get_raw_color_profile_size([NativeTypeName("const struct heif_image_handle *")] IntPtr handle);

    [DllImport("heif", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    [return: NativeTypeName("struct heif_error")]
    public static extern heif_error heif_image_handle_get_raw_color_profile([NativeTypeName("const struct heif_image_handle *")] IntPtr handle, void* out_data);

    [DllImport("heif", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    [return: NativeTypeName("struct heif_error")]
    public static extern heif_error heif_nclx_color_profile_set_color_primaries([NativeTypeName("struct heif_color_profile_nclx *")] heif_color_profile_nclx* nclx, [NativeTypeName("uint16_t")] ushort cp);

    [DllImport("heif", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    [return: NativeTypeName("struct heif_error")]
    public static extern heif_error heif_nclx_color_profile_set_transfer_characteristics([NativeTypeName("struct heif_color_profile_nclx *")] heif_color_profile_nclx* nclx, [NativeTypeName("uint16_t")] ushort transfer_characteristics);

    [DllImport("heif", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    [return: NativeTypeName("struct heif_error")]
    public static extern heif_error heif_nclx_color_profile_set_matrix_coefficients([NativeTypeName("struct heif_color_profile_nclx *")] heif_color_profile_nclx* nclx, [NativeTypeName("uint16_t")] ushort matrix_coefficients);

    [DllImport("heif", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    [return: NativeTypeName("struct heif_error")]
    public static extern heif_error heif_image_handle_get_nclx_color_profile([NativeTypeName("const struct heif_image_handle *")] IntPtr handle, [NativeTypeName("struct heif_color_profile_nclx **")] heif_color_profile_nclx** out_data);

    [DllImport("heif", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    [return: NativeTypeName("struct heif_color_profile_nclx *")]
    public static extern heif_color_profile_nclx* heif_nclx_color_profile_alloc();

    [DllImport("heif", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    public static extern void heif_nclx_color_profile_free([NativeTypeName("struct heif_color_profile_nclx *")] heif_color_profile_nclx* nclx_profile);

    [DllImport("heif", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    [return: NativeTypeName("enum heif_color_profile_type")]
    public static extern heif_color_profile_type heif_image_get_color_profile_type([NativeTypeName("const struct heif_image *")] IntPtr image);

    [DllImport("heif", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    [return: NativeTypeName("size_t")]
    public static extern nuint heif_image_get_raw_color_profile_size([NativeTypeName("const struct heif_image *")] IntPtr image);

    [DllImport("heif", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    [return: NativeTypeName("struct heif_error")]
    public static extern heif_error heif_image_get_raw_color_profile([NativeTypeName("const struct heif_image *")] IntPtr image, void* out_data);

    [DllImport("heif", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    [return: NativeTypeName("struct heif_error")]
    public static extern heif_error heif_image_get_nclx_color_profile([NativeTypeName("const struct heif_image *")] IntPtr image, [NativeTypeName("struct heif_color_profile_nclx **")] heif_color_profile_nclx** out_data);

    [DllImport("heif", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    [return: NativeTypeName("struct heif_decoding_options *")]
    public static extern heif_decoding_options* heif_decoding_options_alloc();

    [DllImport("heif", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    public static extern void heif_decoding_options_free([NativeTypeName("struct heif_decoding_options *")] heif_decoding_options* param0);

    [DllImport("heif", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    [return: NativeTypeName("struct heif_error")]
    public static extern heif_error heif_decode_image([NativeTypeName("const struct heif_image_handle *")] IntPtr in_handle, [NativeTypeName("struct heif_image **")] IntPtr* out_img, [NativeTypeName("enum heif_colorspace")] heif_colorspace colorspace, [NativeTypeName("enum heif_chroma")] heif_chroma chroma, [NativeTypeName("const struct heif_decoding_options *")] heif_decoding_options* options);

    [DllImport("heif", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    [return: NativeTypeName("enum heif_colorspace")]
    public static extern heif_colorspace heif_image_get_colorspace([NativeTypeName("const struct heif_image *")] IntPtr param0);

    [DllImport("heif", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    [return: NativeTypeName("enum heif_chroma")]
    public static extern heif_chroma heif_image_get_chroma_format([NativeTypeName("const struct heif_image *")] IntPtr param0);

    [DllImport("heif", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    public static extern int heif_image_get_width([NativeTypeName("const struct heif_image *")] IntPtr param0, [NativeTypeName("enum heif_channel")] heif_channel channel);

    [DllImport("heif", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    public static extern int heif_image_get_height([NativeTypeName("const struct heif_image *")] IntPtr param0, [NativeTypeName("enum heif_channel")] heif_channel channel);

    [DllImport("heif", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    public static extern int heif_image_get_primary_width([NativeTypeName("const struct heif_image *")] IntPtr param0);

    [DllImport("heif", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    public static extern int heif_image_get_primary_height([NativeTypeName("const struct heif_image *")] IntPtr param0);

    [DllImport("heif", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    [return: NativeTypeName("struct heif_error")]
    public static extern heif_error heif_image_crop([NativeTypeName("struct heif_image *")] IntPtr img, int left, int right, int top, int bottom);

    [DllImport("heif", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    public static extern int heif_image_get_bits_per_pixel([NativeTypeName("const struct heif_image *")] IntPtr param0, [NativeTypeName("enum heif_channel")] heif_channel channel);

    [DllImport("heif", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    public static extern int heif_image_get_bits_per_pixel_range([NativeTypeName("const struct heif_image *")] IntPtr param0, [NativeTypeName("enum heif_channel")] heif_channel channel);

    [DllImport("heif", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    public static extern int heif_image_has_channel([NativeTypeName("const struct heif_image *")] IntPtr param0, [NativeTypeName("enum heif_channel")] heif_channel channel);

    [DllImport("heif", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    [return: NativeTypeName("const uint8_t *")]
    public static extern byte* heif_image_get_plane_readonly([NativeTypeName("const struct heif_image *")] IntPtr param0, [NativeTypeName("enum heif_channel")] heif_channel channel, int* out_stride);

    [DllImport("heif", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    [return: NativeTypeName("uint8_t *")]
    public static extern byte* heif_image_get_plane([NativeTypeName("struct heif_image *")] IntPtr param0, [NativeTypeName("enum heif_channel")] heif_channel channel, int* out_stride);

    [DllImport("heif", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    [return: NativeTypeName("struct heif_error")]
    public static extern heif_error heif_image_scale_image([NativeTypeName("const struct heif_image *")] IntPtr input, [NativeTypeName("struct heif_image **")] IntPtr* output, int width, int height, [NativeTypeName("const struct heif_scaling_options *")] void* options);

    [DllImport("heif", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    [return: NativeTypeName("struct heif_error")]
    public static extern heif_error heif_image_set_raw_color_profile([NativeTypeName("struct heif_image *")] IntPtr image, [NativeTypeName("const char *")] sbyte* profile_type_fourcc_string, [NativeTypeName("const void *")] void* profile_data, [NativeTypeName("const size_t")] nuint profile_size);

    [DllImport("heif", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    [return: NativeTypeName("struct heif_error")]
    public static extern heif_error heif_image_set_nclx_color_profile([NativeTypeName("struct heif_image *")] IntPtr image, [NativeTypeName("const struct heif_color_profile_nclx *")] heif_color_profile_nclx* color_profile);

    [DllImport("heif", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    public static extern int heif_image_get_decoding_warnings([NativeTypeName("struct heif_image *")] IntPtr image, int first_warning_idx, [NativeTypeName("struct heif_error *")] heif_error* out_warnings, int max_output_buffer_entries);

    [DllImport("heif", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    public static extern void heif_image_add_decoding_warning([NativeTypeName("struct heif_image *")] IntPtr image, [NativeTypeName("struct heif_error")] heif_error err);

    [DllImport("heif", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    public static extern void heif_image_release([NativeTypeName("const struct heif_image *")] IntPtr param0);

    [DllImport("heif", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    [return: NativeTypeName("struct heif_error")]
    public static extern heif_error heif_context_write_to_file([NativeTypeName("struct heif_context *")] IntPtr param0, [NativeTypeName("const char *")] sbyte* filename);

    [DllImport("heif", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    [return: NativeTypeName("struct heif_error")]
    public static extern heif_error heif_context_write([NativeTypeName("struct heif_context *")] IntPtr param0, [NativeTypeName("struct heif_writer *")] heif_writer* writer, void* userdata);

    [DllImport("heif", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    public static extern int heif_context_get_encoder_descriptors([NativeTypeName("struct heif_context *")] IntPtr param0, [NativeTypeName("enum heif_compression_format")] heif_compression_format format_filter, [NativeTypeName("const char *")] sbyte* name_filter, [NativeTypeName("const struct heif_encoder_descriptor **")] IntPtr* out_encoders, int count);

    [DllImport("heif", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    [return: NativeTypeName("const char *")]
    public static extern sbyte* heif_encoder_descriptor_get_name([NativeTypeName("const struct heif_encoder_descriptor *")] IntPtr param0);

    [DllImport("heif", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    [return: NativeTypeName("const char *")]
    public static extern sbyte* heif_encoder_descriptor_get_id_name([NativeTypeName("const struct heif_encoder_descriptor *")] IntPtr param0);

    [DllImport("heif", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    [return: NativeTypeName("enum heif_compression_format")]
    public static extern heif_compression_format heif_encoder_descriptor_get_compression_format([NativeTypeName("const struct heif_encoder_descriptor *")] IntPtr param0);

    [DllImport("heif", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    public static extern int heif_encoder_descriptor_supports_lossy_compression([NativeTypeName("const struct heif_encoder_descriptor *")] IntPtr param0);

    [DllImport("heif", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    public static extern int heif_encoder_descriptor_supports_lossless_compression([NativeTypeName("const struct heif_encoder_descriptor *")] IntPtr param0);

    [DllImport("heif", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    [return: NativeTypeName("struct heif_error")]
    public static extern heif_error heif_context_get_encoder([NativeTypeName("struct heif_context *")] IntPtr context, [NativeTypeName("const struct heif_encoder_descriptor *")] IntPtr param1, [NativeTypeName("struct heif_encoder **")] IntPtr* out_encoder);

    [DllImport("heif", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    public static extern int heif_have_decoder_for_format([NativeTypeName("enum heif_compression_format")] heif_compression_format format);

    [DllImport("heif", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    public static extern int heif_have_encoder_for_format([NativeTypeName("enum heif_compression_format")] heif_compression_format format);

    [DllImport("heif", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    [return: NativeTypeName("struct heif_error")]
    public static extern heif_error heif_context_get_encoder_for_format([NativeTypeName("struct heif_context *")] IntPtr context, [NativeTypeName("enum heif_compression_format")] heif_compression_format format, [NativeTypeName("struct heif_encoder **")] IntPtr* param2);

    [DllImport("heif", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    public static extern void heif_encoder_release([NativeTypeName("struct heif_encoder *")] IntPtr param0);

    [DllImport("heif", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    [return: NativeTypeName("const char *")]
    public static extern sbyte* heif_encoder_get_name([NativeTypeName("const struct heif_encoder *")] IntPtr param0);

    [DllImport("heif", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    [return: NativeTypeName("struct heif_error")]
    public static extern heif_error heif_encoder_set_lossy_quality([NativeTypeName("struct heif_encoder *")] IntPtr param0, int quality);

    [DllImport("heif", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    [return: NativeTypeName("struct heif_error")]
    public static extern heif_error heif_encoder_set_lossless([NativeTypeName("struct heif_encoder *")] IntPtr param0, int enable);

    [DllImport("heif", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    [return: NativeTypeName("struct heif_error")]
    public static extern heif_error heif_encoder_set_logging_level([NativeTypeName("struct heif_encoder *")] IntPtr param0, int level);

    [DllImport("heif", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    [return: NativeTypeName("const struct heif_encoder_parameter *const *")]
    public static extern IntPtr* heif_encoder_list_parameters([NativeTypeName("struct heif_encoder *")] IntPtr param0);

    [DllImport("heif", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    [return: NativeTypeName("const char *")]
    public static extern sbyte* heif_encoder_parameter_get_name([NativeTypeName("const struct heif_encoder_parameter *")] IntPtr param0);

    [DllImport("heif", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    [return: NativeTypeName("enum heif_encoder_parameter_type")]
    public static extern heif_encoder_parameter_type heif_encoder_parameter_get_type([NativeTypeName("const struct heif_encoder_parameter *")] IntPtr param0);

    [DllImport("heif", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    [return: NativeTypeName("struct heif_error")]
    public static extern heif_error heif_encoder_parameter_get_valid_integer_range([NativeTypeName("const struct heif_encoder_parameter *")] IntPtr param0, int* have_minimum_maximum, int* minimum, int* maximum);

    [DllImport("heif", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    [return: NativeTypeName("struct heif_error")]
    public static extern heif_error heif_encoder_parameter_get_valid_integer_values([NativeTypeName("const struct heif_encoder_parameter *")] IntPtr param0, int* have_minimum, int* have_maximum, int* minimum, int* maximum, int* num_valid_values, [NativeTypeName("const int **")] int** out_integer_array);

    [DllImport("heif", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    [return: NativeTypeName("struct heif_error")]
    public static extern heif_error heif_encoder_parameter_get_valid_string_values([NativeTypeName("const struct heif_encoder_parameter *")] IntPtr param0, [NativeTypeName("const char *const **")] sbyte*** out_stringarray);

    [DllImport("heif", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    [return: NativeTypeName("struct heif_error")]
    public static extern heif_error heif_encoder_set_parameter_integer([NativeTypeName("struct heif_encoder *")] IntPtr param0, [NativeTypeName("const char *")] sbyte* parameter_name, int value);

    [DllImport("heif", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    [return: NativeTypeName("struct heif_error")]
    public static extern heif_error heif_encoder_get_parameter_integer([NativeTypeName("struct heif_encoder *")] IntPtr param0, [NativeTypeName("const char *")] sbyte* parameter_name, int* value);

    [DllImport("heif", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    [return: NativeTypeName("struct heif_error")]
    public static extern heif_error heif_encoder_parameter_integer_valid_range([NativeTypeName("struct heif_encoder *")] IntPtr param0, [NativeTypeName("const char *")] sbyte* parameter_name, int* have_minimum_maximum, int* minimum, int* maximum);

    [DllImport("heif", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    [return: NativeTypeName("struct heif_error")]
    public static extern heif_error heif_encoder_set_parameter_boolean([NativeTypeName("struct heif_encoder *")] IntPtr param0, [NativeTypeName("const char *")] sbyte* parameter_name, int value);

    [DllImport("heif", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    [return: NativeTypeName("struct heif_error")]
    public static extern heif_error heif_encoder_get_parameter_boolean([NativeTypeName("struct heif_encoder *")] IntPtr param0, [NativeTypeName("const char *")] sbyte* parameter_name, int* value);

    [DllImport("heif", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    [return: NativeTypeName("struct heif_error")]
    public static extern heif_error heif_encoder_set_parameter_string([NativeTypeName("struct heif_encoder *")] IntPtr param0, [NativeTypeName("const char *")] sbyte* parameter_name, [NativeTypeName("const char *")] sbyte* value);

    [DllImport("heif", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    [return: NativeTypeName("struct heif_error")]
    public static extern heif_error heif_encoder_get_parameter_string([NativeTypeName("struct heif_encoder *")] IntPtr param0, [NativeTypeName("const char *")] sbyte* parameter_name, [NativeTypeName("char *")] sbyte* value, int value_size);

    [DllImport("heif", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    [return: NativeTypeName("struct heif_error")]
    public static extern heif_error heif_encoder_parameter_string_valid_values([NativeTypeName("struct heif_encoder *")] IntPtr param0, [NativeTypeName("const char *")] sbyte* parameter_name, [NativeTypeName("const char *const **")] sbyte*** out_stringarray);

    [DllImport("heif", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    [return: NativeTypeName("struct heif_error")]
    public static extern heif_error heif_encoder_parameter_integer_valid_values([NativeTypeName("struct heif_encoder *")] IntPtr param0, [NativeTypeName("const char *")] sbyte* parameter_name, int* have_minimum, int* have_maximum, int* minimum, int* maximum, int* num_valid_values, [NativeTypeName("const int **")] int** out_integer_array);

    [DllImport("heif", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    [return: NativeTypeName("struct heif_error")]
    public static extern heif_error heif_encoder_set_parameter([NativeTypeName("struct heif_encoder *")] IntPtr param0, [NativeTypeName("const char *")] sbyte* parameter_name, [NativeTypeName("const char *")] sbyte* value);

    [DllImport("heif", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    [return: NativeTypeName("struct heif_error")]
    public static extern heif_error heif_encoder_get_parameter([NativeTypeName("struct heif_encoder *")] IntPtr param0, [NativeTypeName("const char *")] sbyte* parameter_name, [NativeTypeName("char *")] sbyte* value_ptr, int value_size);

    [DllImport("heif", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    public static extern int heif_encoder_has_default([NativeTypeName("struct heif_encoder *")] IntPtr param0, [NativeTypeName("const char *")] sbyte* parameter_name);

    [DllImport("heif", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    [return: NativeTypeName("struct heif_encoding_options *")]
    public static extern heif_encoding_options* heif_encoding_options_alloc();

    [DllImport("heif", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    public static extern void heif_encoding_options_free([NativeTypeName("struct heif_encoding_options *")] heif_encoding_options* param0);

    [DllImport("heif", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    [return: NativeTypeName("struct heif_error")]
    public static extern heif_error heif_context_encode_image([NativeTypeName("struct heif_context *")] IntPtr param0, [NativeTypeName("const struct heif_image *")] IntPtr image, [NativeTypeName("struct heif_encoder *")] IntPtr encoder, [NativeTypeName("const struct heif_encoding_options *")] heif_encoding_options* options, [NativeTypeName("struct heif_image_handle **")] IntPtr* out_image_handle);

    [DllImport("heif", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    [return: NativeTypeName("struct heif_error")]
    public static extern heif_error heif_context_set_primary_image([NativeTypeName("struct heif_context *")] IntPtr param0, [NativeTypeName("struct heif_image_handle *")] IntPtr image_handle);

    [DllImport("heif", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    [return: NativeTypeName("struct heif_error")]
    public static extern heif_error heif_context_encode_thumbnail([NativeTypeName("struct heif_context *")] IntPtr param0, [NativeTypeName("const struct heif_image *")] IntPtr image, [NativeTypeName("const struct heif_image_handle *")] IntPtr master_image_handle, [NativeTypeName("struct heif_encoder *")] IntPtr encoder, [NativeTypeName("const struct heif_encoding_options *")] heif_encoding_options* options, int bbox_size, [NativeTypeName("struct heif_image_handle **")] IntPtr* out_thumb_image_handle);

    [DllImport("heif", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    [return: NativeTypeName("struct heif_error")]
    public static extern heif_error heif_context_assign_thumbnail([NativeTypeName("struct heif_context *")] IntPtr param0, [NativeTypeName("const struct heif_image_handle *")] IntPtr master_image, [NativeTypeName("const struct heif_image_handle *")] IntPtr thumbnail_image);

    [DllImport("heif", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    [return: NativeTypeName("struct heif_error")]
    public static extern heif_error heif_context_add_exif_metadata([NativeTypeName("struct heif_context *")] IntPtr param0, [NativeTypeName("const struct heif_image_handle *")] IntPtr image_handle, [NativeTypeName("const void *")] void* data, int size);

    [DllImport("heif", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    [return: NativeTypeName("struct heif_error")]
    public static extern heif_error heif_context_add_XMP_metadata([NativeTypeName("struct heif_context *")] IntPtr param0, [NativeTypeName("const struct heif_image_handle *")] IntPtr image_handle, [NativeTypeName("const void *")] void* data, int size);

    [DllImport("heif", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    [return: NativeTypeName("struct heif_error")]
    public static extern heif_error heif_context_add_generic_metadata([NativeTypeName("struct heif_context *")] IntPtr ctx, [NativeTypeName("const struct heif_image_handle *")] IntPtr image_handle, [NativeTypeName("const void *")] void* data, int size, [NativeTypeName("const char *")] sbyte* item_type, [NativeTypeName("const char *")] sbyte* content_type);

    [DllImport("heif", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    [return: NativeTypeName("struct heif_error")]
    public static extern heif_error heif_image_create(int width, int height, [NativeTypeName("enum heif_colorspace")] heif_colorspace colorspace, [NativeTypeName("enum heif_chroma")] heif_chroma chroma, [NativeTypeName("struct heif_image **")] IntPtr* out_image);

    [DllImport("heif", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    [return: NativeTypeName("struct heif_error")]
    public static extern heif_error heif_image_add_plane([NativeTypeName("struct heif_image *")] IntPtr image, [NativeTypeName("enum heif_channel")] heif_channel channel, int width, int height, int bit_depth);

    [DllImport("heif", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    public static extern void heif_image_set_premultiplied_alpha([NativeTypeName("struct heif_image *")] IntPtr image, int is_premultiplied_alpha);

    [DllImport("heif", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    public static extern int heif_image_is_premultiplied_alpha([NativeTypeName("struct heif_image *")] IntPtr image);

    [DllImport("heif", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    [return: NativeTypeName("struct heif_error")]
    public static extern heif_error heif_register_decoder([NativeTypeName("struct heif_context *")] IntPtr heif, [NativeTypeName("const struct heif_decoder_plugin *")] IntPtr param1);

    [DllImport("heif", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    [return: NativeTypeName("struct heif_error")]
    public static extern heif_error heif_register_decoder_plugin([NativeTypeName("const struct heif_decoder_plugin *")] IntPtr param0);

    [DllImport("heif", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    [return: NativeTypeName("struct heif_error")]
    public static extern heif_error heif_register_encoder_plugin([NativeTypeName("const struct heif_encoder_plugin *")] IntPtr param0);

    [DllImport("heif", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    public static extern int heif_encoder_descriptor_supportes_lossy_compression([NativeTypeName("const struct heif_encoder_descriptor *")] IntPtr param0);

    [DllImport("heif", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    public static extern int heif_encoder_descriptor_supportes_lossless_compression([NativeTypeName("const struct heif_encoder_descriptor *")] IntPtr param0);

    [NativeTypeName("#define LIBHEIF_AUX_IMAGE_FILTER_OMIT_ALPHA (1UL<<1)")]
    public const uint LIBHEIF_AUX_IMAGE_FILTER_OMIT_ALPHA = (1U << 1);

    [NativeTypeName("#define LIBHEIF_AUX_IMAGE_FILTER_OMIT_DEPTH (2UL<<1)")]
    public const uint LIBHEIF_AUX_IMAGE_FILTER_OMIT_DEPTH = (2U << 1);

    [NativeTypeName("#define heif_chroma_interleaved_24bit heif_chroma_interleaved_RGB")]
    public const heif_chroma heif_chroma_interleaved_24bit = heif_chroma_interleaved_RGB;

    [NativeTypeName("#define heif_chroma_interleaved_32bit heif_chroma_interleaved_RGBA")]
    public const heif_chroma heif_chroma_interleaved_32bit = heif_chroma_interleaved_RGBA;
}
