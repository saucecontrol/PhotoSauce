// Copyright © Clinton Ingram and Contributors
// SPDX-License-Identifier: MIT

// Ported from libheif headers (heif.h)
// Original source Copyright (c) struktur AG, Dirk Farin
// See third-party-notices in the repository root for more information.

using System.Runtime.InteropServices;
using static PhotoSauce.Interop.Libheif.heif_chroma;
using static PhotoSauce.Interop.Libheif.heif_colorspace;

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
    public static extern void heif_string_release([NativeTypeName("const char *")] sbyte* param0);

    [DllImport("heif", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    public static extern heif_error heif_init(heif_init_params* param0);

    [DllImport("heif", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    public static extern void heif_deinit();

    [DllImport("heif", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    public static extern heif_colorspace heif_image_get_colorspace([NativeTypeName("const heif_image *")] void* param0);

    [DllImport("heif", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    public static extern heif_chroma heif_image_get_chroma_format([NativeTypeName("const heif_image *")] void* param0);

    [DllImport("heif", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    public static extern int heif_image_get_width([NativeTypeName("const heif_image *")] void* img, heif_channel channel);

    [DllImport("heif", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    public static extern int heif_image_get_height([NativeTypeName("const heif_image *")] void* img, heif_channel channel);

    [DllImport("heif", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    public static extern int heif_image_get_primary_width([NativeTypeName("const heif_image *")] void* img);

    [DllImport("heif", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    public static extern int heif_image_get_primary_height([NativeTypeName("const heif_image *")] void* img);

    [DllImport("heif", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    public static extern heif_error heif_image_crop([NativeTypeName("heif_image*")] void* img, int left, int right, int top, int bottom);

    [DllImport("heif", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    public static extern heif_error heif_image_extract_area([NativeTypeName("const heif_image *")] void* param0, [NativeTypeName("uint32_t")] uint x0, [NativeTypeName("uint32_t")] uint y0, [NativeTypeName("uint32_t")] uint w, [NativeTypeName("uint32_t")] uint h, [NativeTypeName("const heif_security_limits *")] heif_security_limits* limits, [NativeTypeName("heif_image **")] void** out_image);

    [DllImport("heif", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    public static extern int heif_image_get_bits_per_pixel([NativeTypeName("const heif_image *")] void* param0, heif_channel channel);

    [DllImport("heif", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    public static extern int heif_image_get_bits_per_pixel_range([NativeTypeName("const heif_image *")] void* param0, heif_channel channel);

    [DllImport("heif", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    public static extern int heif_image_has_channel([NativeTypeName("const heif_image *")] void* param0, heif_channel channel);

    [DllImport("heif", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    [return: NativeTypeName("const uint8_t *")]
    public static extern byte* heif_image_get_plane_readonly([NativeTypeName("const heif_image *")] void* param0, heif_channel channel, int* out_stride);

    [DllImport("heif", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    [return: NativeTypeName("uint8_t *")]
    public static extern byte* heif_image_get_plane([NativeTypeName("heif_image*")] void* param0, heif_channel channel, int* out_stride);

    [DllImport("heif", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    [return: NativeTypeName("const uint8_t *")]
    public static extern byte* heif_image_get_plane_readonly2([NativeTypeName("const heif_image *")] void* param0, heif_channel channel, [NativeTypeName("size_t *")] nuint* out_stride);

    [DllImport("heif", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    [return: NativeTypeName("uint8_t *")]
    public static extern byte* heif_image_get_plane2([NativeTypeName("heif_image*")] void* param0, heif_channel channel, [NativeTypeName("size_t *")] nuint* out_stride);

    [DllImport("heif", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    public static extern heif_error heif_image_scale_image([NativeTypeName("const heif_image *")] void* input, [NativeTypeName("heif_image **")] void** output, int width, int height, [NativeTypeName("const heif_scaling_options *")] void* options);

    [DllImport("heif", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    public static extern heif_error heif_image_extend_to_size_fill_with_zero([NativeTypeName("heif_image*")] void* image, [NativeTypeName("uint32_t")] uint width, [NativeTypeName("uint32_t")] uint height);

    [DllImport("heif", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    public static extern int heif_image_get_decoding_warnings([NativeTypeName("heif_image*")] void* image, int first_warning_idx, heif_error* out_warnings, int max_output_buffer_entries);

    [DllImport("heif", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    public static extern void heif_image_add_decoding_warning([NativeTypeName("heif_image*")] void* image, heif_error err);

    [DllImport("heif", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    public static extern void heif_image_release([NativeTypeName("const heif_image *")] void* param0);

    [DllImport("heif", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    public static extern void heif_image_get_pixel_aspect_ratio([NativeTypeName("const heif_image *")] void* param0, [NativeTypeName("uint32_t *")] uint* aspect_h, [NativeTypeName("uint32_t *")] uint* aspect_v);

    [DllImport("heif", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    public static extern void heif_image_set_pixel_aspect_ratio([NativeTypeName("heif_image*")] void* param0, [NativeTypeName("uint32_t")] uint aspect_h, [NativeTypeName("uint32_t")] uint aspect_v);

    [DllImport("heif", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    public static extern void heif_image_handle_set_pixel_aspect_ratio([NativeTypeName("heif_image_handle*")] void* param0, [NativeTypeName("uint32_t")] uint aspect_h, [NativeTypeName("uint32_t")] uint aspect_v);

    [DllImport("heif", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    public static extern heif_error heif_image_create(int width, int height, heif_colorspace colorspace, heif_chroma chroma, [NativeTypeName("heif_image **")] void** out_image);

    [DllImport("heif", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    public static extern heif_error heif_image_add_plane([NativeTypeName("heif_image*")] void* image, heif_channel channel, int width, int height, int bit_depth);

    [DllImport("heif", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    public static extern heif_error heif_image_add_plane_safe([NativeTypeName("heif_image*")] void* image, heif_channel channel, int width, int height, int bit_depth, [NativeTypeName("const heif_security_limits *")] heif_security_limits* limits);

    [DllImport("heif", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    public static extern void heif_image_set_premultiplied_alpha([NativeTypeName("heif_image*")] void* image, int is_premultiplied_alpha);

    [DllImport("heif", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    public static extern int heif_image_is_premultiplied_alpha([NativeTypeName("heif_image*")] void* image);

    [DllImport("heif", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    public static extern heif_error heif_image_extend_padding_to_size([NativeTypeName("heif_image*")] void* image, int min_physical_width, int min_physical_height);

    [DllImport("heif", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    public static extern void heif_color_conversion_options_set_defaults(heif_color_conversion_options* param0);

    [DllImport("heif", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    public static extern heif_color_conversion_options_ext* heif_color_conversion_options_ext_alloc();

    [DllImport("heif", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    public static extern void heif_color_conversion_options_ext_copy(heif_color_conversion_options_ext* dst, [NativeTypeName("const heif_color_conversion_options_ext *")] heif_color_conversion_options_ext* src);

    [DllImport("heif", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    public static extern void heif_color_conversion_options_ext_free(heif_color_conversion_options_ext* param0);

    [DllImport("heif", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    public static extern heif_color_profile_type heif_image_handle_get_color_profile_type([NativeTypeName("const heif_image_handle *")] void* handle);

    [DllImport("heif", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    [return: NativeTypeName("size_t")]
    public static extern nuint heif_image_handle_get_raw_color_profile_size([NativeTypeName("const heif_image_handle *")] void* handle);

    [DllImport("heif", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    [return: NativeTypeName("struct heif_error")]
    public static extern heif_error heif_image_handle_get_raw_color_profile([NativeTypeName("const heif_image_handle *")] void* handle, void* out_data);

    [DllImport("heif", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    public static extern heif_error heif_nclx_color_profile_set_color_primaries(heif_color_profile_nclx* nclx, [NativeTypeName("uint16_t")] ushort cp);

    [DllImport("heif", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    public static extern heif_error heif_nclx_color_profile_set_transfer_characteristics(heif_color_profile_nclx* nclx, [NativeTypeName("uint16_t")] ushort transfer_characteristics);

    [DllImport("heif", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    public static extern heif_error heif_nclx_color_profile_set_matrix_coefficients(heif_color_profile_nclx* nclx, [NativeTypeName("uint16_t")] ushort matrix_coefficients);

    [DllImport("heif", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    public static extern heif_error heif_image_handle_get_nclx_color_profile([NativeTypeName("const heif_image_handle *")] void* handle, heif_color_profile_nclx** out_data);

    [DllImport("heif", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    public static extern heif_color_profile_nclx* heif_nclx_color_profile_alloc();

    [DllImport("heif", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    public static extern void heif_nclx_color_profile_free(heif_color_profile_nclx* nclx_profile);

    [DllImport("heif", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    public static extern heif_color_profile_type heif_image_get_color_profile_type([NativeTypeName("const heif_image *")] void* image);

    [DllImport("heif", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    [return: NativeTypeName("size_t")]
    public static extern nuint heif_image_get_raw_color_profile_size([NativeTypeName("const heif_image *")] void* image);

    [DllImport("heif", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    public static extern heif_error heif_image_get_raw_color_profile([NativeTypeName("const heif_image *")] void* image, void* out_data);

    [DllImport("heif", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    public static extern heif_error heif_image_get_nclx_color_profile([NativeTypeName("const heif_image *")] void* image, heif_color_profile_nclx** out_data);

    [DllImport("heif", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    public static extern heif_error heif_image_set_raw_color_profile([NativeTypeName("heif_image*")] void* image, [NativeTypeName("const char *")] sbyte* profile_type_fourcc_string, [NativeTypeName("const void *")] void* profile_data, [NativeTypeName("const size_t")] nuint profile_size);

    [DllImport("heif", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    public static extern heif_error heif_image_set_nclx_color_profile([NativeTypeName("heif_image*")] void* image, [NativeTypeName("const heif_color_profile_nclx *")] heif_color_profile_nclx* color_profile);

    [DllImport("heif", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    public static extern int heif_image_has_content_light_level([NativeTypeName("const heif_image *")] void* param0);

    [DllImport("heif", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    public static extern int heif_image_handle_has_content_light_level([NativeTypeName("const heif_image_handle *")] void* param0);

    [DllImport("heif", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    public static extern void heif_image_get_content_light_level([NativeTypeName("const heif_image *")] void* param0, heif_content_light_level* @out);

    [DllImport("heif", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    public static extern int heif_image_handle_get_content_light_level([NativeTypeName("const heif_image_handle *")] void* param0, heif_content_light_level* @out);

    [DllImport("heif", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    public static extern void heif_image_set_content_light_level([NativeTypeName("const heif_image *")] void* param0, [NativeTypeName("const heif_content_light_level *")] heif_content_light_level* @in);

    [DllImport("heif", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    public static extern void heif_image_handle_set_content_light_level([NativeTypeName("const heif_image_handle *")] void* param0, [NativeTypeName("const heif_content_light_level *")] heif_content_light_level* @in);

    [DllImport("heif", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    public static extern int heif_image_has_mastering_display_colour_volume([NativeTypeName("const heif_image *")] void* param0);

    [DllImport("heif", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    public static extern int heif_image_handle_has_mastering_display_colour_volume([NativeTypeName("const heif_image_handle *")] void* param0);

    [DllImport("heif", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    public static extern void heif_image_get_mastering_display_colour_volume([NativeTypeName("const heif_image *")] void* param0, heif_mastering_display_colour_volume* @out);

    [DllImport("heif", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    public static extern int heif_image_handle_get_mastering_display_colour_volume([NativeTypeName("const heif_image_handle *")] void* param0, heif_mastering_display_colour_volume* @out);

    [DllImport("heif", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    public static extern void heif_image_set_mastering_display_colour_volume([NativeTypeName("const heif_image *")] void* param0, [NativeTypeName("const heif_mastering_display_colour_volume *")] heif_mastering_display_colour_volume* @in);

    [DllImport("heif", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    public static extern void heif_image_handle_set_mastering_display_colour_volume([NativeTypeName("const heif_image_handle *")] void* param0, [NativeTypeName("const heif_mastering_display_colour_volume *")] heif_mastering_display_colour_volume* @in);

    [DllImport("heif", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    public static extern int heif_image_has_ambient_viewing_environment([NativeTypeName("const heif_image *")] void* param0);

    [DllImport("heif", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    public static extern int heif_image_handle_has_ambient_viewing_environment([NativeTypeName("const heif_image_handle *")] void* param0);

    [DllImport("heif", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    public static extern int heif_image_get_ambient_viewing_environment([NativeTypeName("const heif_image *")] void* param0, heif_ambient_viewing_environment* @out);

    [DllImport("heif", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    public static extern int heif_image_handle_get_ambient_viewing_environment([NativeTypeName("const heif_image_handle *")] void* param0, heif_ambient_viewing_environment* @out);

    [DllImport("heif", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    public static extern void heif_image_set_ambient_viewing_environment([NativeTypeName("const heif_image *")] void* param0, [NativeTypeName("const heif_ambient_viewing_environment *")] heif_ambient_viewing_environment* @in);

    [DllImport("heif", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    public static extern void heif_image_handle_set_ambient_viewing_environment([NativeTypeName("const heif_image_handle *")] void* param0, [NativeTypeName("const heif_ambient_viewing_environment *")] heif_ambient_viewing_environment* @in);

    [DllImport("heif", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    public static extern int heif_image_has_nominal_diffuse_white_luminance([NativeTypeName("const heif_image *")] void* param0);

    [DllImport("heif", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    [return: NativeTypeName("uint32_t")]
    public static extern uint heif_image_get_nominal_diffuse_white_luminance([NativeTypeName("const heif_image *")] void* param0);

    [DllImport("heif", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    public static extern void heif_image_set_nominal_diffuse_white_luminance([NativeTypeName("const heif_image *")] void* param0, [NativeTypeName("uint32_t")] uint luminance);

    [DllImport("heif", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    public static extern int heif_image_handle_has_nominal_diffuse_white_luminance([NativeTypeName("const heif_image_handle *")] void* param0);

    [DllImport("heif", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    [return: NativeTypeName("uint32_t")]
    public static extern uint heif_image_handle_get_nominal_diffuse_white_luminance([NativeTypeName("const heif_image_handle *")] void* param0);

    [DllImport("heif", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    public static extern void heif_image_handle_set_nominal_diffuse_white_luminance([NativeTypeName("const heif_image_handle *")] void* param0, [NativeTypeName("uint32_t")] uint luminance);

    [DllImport("heif", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    public static extern heif_error heif_mastering_display_colour_volume_decode([NativeTypeName("const heif_mastering_display_colour_volume *")] heif_mastering_display_colour_volume* @in, heif_decoded_mastering_display_colour_volume* @out);

    [DllImport("heif", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    [return: NativeTypeName("heif_brand2")]
    public static extern uint heif_read_main_brand([NativeTypeName("const uint8_t *")] byte* data, int len);

    [DllImport("heif", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    [return: NativeTypeName("heif_brand2")]
    public static extern uint heif_read_minor_version_brand([NativeTypeName("const uint8_t *")] byte* data, int len);

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
    [return: NativeTypeName("enum heif_filetype_result")]
    public static extern heif_filetype_result heif_check_filetype([NativeTypeName("const uint8_t *")] byte* data, int len);

    [DllImport("heif", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    public static extern heif_error heif_has_compatible_filetype([NativeTypeName("const uint8_t *")] byte* data, int len);

    [DllImport("heif", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    public static extern int heif_check_jpeg_filetype([NativeTypeName("const uint8_t *")] byte* data, int len);

    [DllImport("heif", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    public static extern int heif_metadata_compression_method_supported([NativeTypeName("enum heif_metadata_compression")] heif_metadata_compression method);

    [DllImport("heif", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    public static extern int heif_image_handle_get_number_of_metadata_blocks([NativeTypeName("const heif_image_handle *")] void* handle, [NativeTypeName("const char *")] sbyte* type_filter);

    [DllImport("heif", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    public static extern int heif_image_handle_get_list_of_metadata_block_IDs([NativeTypeName("const heif_image_handle *")] void* handle, [NativeTypeName("const char *")] sbyte* type_filter, [NativeTypeName("heif_item_id *")] uint* ids, int count);

    [DllImport("heif", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    [return: NativeTypeName("const char *")]
    public static extern sbyte* heif_image_handle_get_metadata_type([NativeTypeName("const heif_image_handle *")] void* handle, [NativeTypeName("heif_item_id")] uint metadata_id);

    [DllImport("heif", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    [return: NativeTypeName("const char *")]
    public static extern sbyte* heif_image_handle_get_metadata_content_type([NativeTypeName("const heif_image_handle *")] void* handle, [NativeTypeName("heif_item_id")] uint metadata_id);

    [DllImport("heif", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    [return: NativeTypeName("size_t")]
    public static extern nuint heif_image_handle_get_metadata_size([NativeTypeName("const heif_image_handle *")] void* handle, [NativeTypeName("heif_item_id")] uint metadata_id);

    [DllImport("heif", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    public static extern heif_error heif_image_handle_get_metadata([NativeTypeName("const heif_image_handle *")] void* handle, [NativeTypeName("heif_item_id")] uint metadata_id, void* out_data);

    [DllImport("heif", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    [return: NativeTypeName("const char *")]
    public static extern sbyte* heif_image_handle_get_metadata_item_uri_type([NativeTypeName("const heif_image_handle *")] void* handle, [NativeTypeName("heif_item_id")] uint metadata_id);

    [DllImport("heif", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    public static extern heif_error heif_context_add_exif_metadata([NativeTypeName("heif_context*")] void* param0, [NativeTypeName("const heif_image_handle *")] void* image_handle, [NativeTypeName("const void *")] void* data, int size);

    [DllImport("heif", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    public static extern heif_error heif_context_add_XMP_metadata([NativeTypeName("heif_context*")] void* param0, [NativeTypeName("const heif_image_handle *")] void* image_handle, [NativeTypeName("const void *")] void* data, int size);

    [DllImport("heif", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    public static extern heif_error heif_context_add_XMP_metadata2([NativeTypeName("heif_context*")] void* param0, [NativeTypeName("const heif_image_handle *")] void* image_handle, [NativeTypeName("const void *")] void* data, int size, [NativeTypeName("enum heif_metadata_compression")] heif_metadata_compression compression);

    [DllImport("heif", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    public static extern heif_error heif_context_add_generic_metadata([NativeTypeName("heif_context*")] void* ctx, [NativeTypeName("const heif_image_handle *")] void* image_handle, [NativeTypeName("const void *")] void* data, int size, [NativeTypeName("const char *")] sbyte* item_type, [NativeTypeName("const char *")] sbyte* content_type);

    [DllImport("heif", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    public static extern heif_error heif_context_add_generic_uri_metadata([NativeTypeName("heif_context*")] void* ctx, [NativeTypeName("const heif_image_handle *")] void* image_handle, [NativeTypeName("const void *")] void* data, int size, [NativeTypeName("const char *")] sbyte* item_uri_type, [NativeTypeName("heif_item_id *")] uint* out_item_id);

    [DllImport("heif", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    public static extern int heif_image_handle_has_depth_image([NativeTypeName("const heif_image_handle *")] void* param0);

    [DllImport("heif", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    public static extern int heif_image_handle_get_number_of_depth_images([NativeTypeName("const heif_image_handle *")] void* handle);

    [DllImport("heif", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    public static extern int heif_image_handle_get_list_of_depth_image_IDs([NativeTypeName("const heif_image_handle *")] void* handle, [NativeTypeName("heif_item_id *")] uint* ids, int count);

    [DllImport("heif", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    public static extern heif_error heif_image_handle_get_depth_image_handle([NativeTypeName("const heif_image_handle *")] void* handle, [NativeTypeName("heif_item_id")] uint depth_image_id, [NativeTypeName("heif_image_handle **")] void** out_depth_handle);

    [DllImport("heif", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    public static extern void heif_depth_representation_info_free([NativeTypeName("const heif_depth_representation_info *")] heif_depth_representation_info* info);

    [DllImport("heif", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    public static extern int heif_image_handle_get_depth_image_representation_info([NativeTypeName("const heif_image_handle *")] void* handle, [NativeTypeName("heif_item_id")] uint depth_image_id, [NativeTypeName("const heif_depth_representation_info **")] heif_depth_representation_info** @out);

    [DllImport("heif", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    public static extern int heif_image_handle_get_number_of_thumbnails([NativeTypeName("const heif_image_handle *")] void* handle);

    [DllImport("heif", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    public static extern int heif_image_handle_get_list_of_thumbnail_IDs([NativeTypeName("const heif_image_handle *")] void* handle, [NativeTypeName("heif_item_id *")] uint* ids, int count);

    [DllImport("heif", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    public static extern heif_error heif_image_handle_get_thumbnail([NativeTypeName("const heif_image_handle *")] void* main_image_handle, [NativeTypeName("heif_item_id")] uint thumbnail_id, [NativeTypeName("heif_image_handle **")] void** out_thumbnail_handle);

    [DllImport("heif", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    public static extern heif_error heif_context_encode_thumbnail([NativeTypeName("heif_context*")] void* param0, [NativeTypeName("const heif_image *")] void* image, [NativeTypeName("const heif_image_handle *")] void* master_image_handle, [NativeTypeName("heif_encoder*")] void* encoder, [NativeTypeName("const heif_encoding_options *")] heif_encoding_options* options, int bbox_size, [NativeTypeName("heif_image_handle **")] void** out_thumb_image_handle);

    [DllImport("heif", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    public static extern heif_error heif_context_assign_thumbnail([NativeTypeName("heif_context *")] void* param0, [NativeTypeName("const heif_image_handle *")] void* master_image, [NativeTypeName("const heif_image_handle *")] void* thumbnail_image);

    [DllImport("heif", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    public static extern int heif_image_handle_get_number_of_auxiliary_images([NativeTypeName("const heif_image_handle *")] void* handle, int aux_filter);

    [DllImport("heif", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    public static extern int heif_image_handle_get_list_of_auxiliary_image_IDs([NativeTypeName("const heif_image_handle *")] void* handle, int aux_filter, [NativeTypeName("heif_item_id *")] uint* ids, int count);

    [DllImport("heif", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    public static extern heif_error heif_image_handle_get_auxiliary_type([NativeTypeName("const heif_image_handle *")] void* handle, [NativeTypeName("const char **")] sbyte** out_type);

    [DllImport("heif", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    public static extern void heif_image_handle_release_auxiliary_type([NativeTypeName("const heif_image_handle *")] void* handle, [NativeTypeName("const char **")] sbyte** out_type);

    [DllImport("heif", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    public static extern heif_error heif_image_handle_get_auxiliary_image_handle([NativeTypeName("const heif_image_handle *")] void* main_image_handle, [NativeTypeName("heif_item_id")] uint auxiliary_id, [NativeTypeName("heif_image_handle **")] void** out_auxiliary_handle);

    [DllImport("heif", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    public static extern heif_entity_group* heif_context_get_entity_groups([NativeTypeName("const heif_context *")] void* param0, [NativeTypeName("uint32_t")] uint type_filter, [NativeTypeName("heif_item_id")] uint item_filter, int* out_num_groups);

    [DllImport("heif", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    public static extern void heif_entity_groups_release(heif_entity_group* param0, int num_groups);

    [DllImport("heif", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    [return: NativeTypeName("const heif_security_limits *")]
    public static extern heif_security_limits* heif_get_global_security_limits();

    [DllImport("heif", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    [return: NativeTypeName("const heif_security_limits *")]
    public static extern heif_security_limits* heif_get_disabled_security_limits();

    [DllImport("heif", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    public static extern heif_security_limits* heif_context_get_security_limits([NativeTypeName("const heif_context *")] void* param0);

    [DllImport("heif", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    public static extern heif_error heif_context_set_security_limits([NativeTypeName("heif_context*")] void* param0, [NativeTypeName("const heif_security_limits *")] heif_security_limits* param1);

    [DllImport("heif", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    public static extern void heif_context_set_maximum_image_size_limit([NativeTypeName("heif_context*")] void* ctx, int maximum_width);

    [DllImport("heif", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    [return: NativeTypeName("heif_context*")]
    public static extern void* heif_context_alloc();

    [DllImport("heif", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    public static extern void heif_context_free([NativeTypeName("heif_context*")] void* param0);

    [DllImport("heif", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    public static extern heif_error heif_context_read_from_file([NativeTypeName("heif_context*")] void* param0, [NativeTypeName("const char *")] sbyte* filename, [NativeTypeName("const heif_reading_options *")] void* param2);

    [DllImport("heif", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    public static extern heif_error heif_context_read_from_memory_without_copy([NativeTypeName("heif_context*")] void* param0, [NativeTypeName("const void *")] void* mem, [NativeTypeName("size_t")] nuint size, [NativeTypeName("const heif_reading_options *")] void* param3);

    [DllImport("heif", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    public static extern heif_error heif_context_read_from_reader([NativeTypeName("heif_context*")] void* param0, [NativeTypeName("const heif_reader *")] heif_reader* reader, void* userdata, [NativeTypeName("const heif_reading_options *")] void* param3);

    [DllImport("heif", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    public static extern int heif_context_get_number_of_top_level_images([NativeTypeName("heif_context*")] void* ctx);

    [DllImport("heif", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    public static extern int heif_context_is_top_level_image_ID([NativeTypeName("heif_context*")] void* ctx, [NativeTypeName("heif_item_id")] uint id);

    [DllImport("heif", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    public static extern int heif_context_get_list_of_top_level_image_IDs([NativeTypeName("heif_context*")] void* ctx, [NativeTypeName("heif_item_id *")] uint* ID_array, int count);

    [DllImport("heif", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    public static extern heif_error heif_context_get_primary_image_ID([NativeTypeName("heif_context*")] void* ctx, [NativeTypeName("heif_item_id *")] uint* id);

    [DllImport("heif", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    public static extern heif_error heif_context_get_primary_image_handle([NativeTypeName("heif_context*")] void* ctx, [NativeTypeName("heif_image_handle **")] void** param1);

    [DllImport("heif", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    public static extern heif_error heif_context_get_image_handle([NativeTypeName("heif_context*")] void* ctx, [NativeTypeName("heif_item_id")] uint id, [NativeTypeName("heif_image_handle **")] void** param2);

    [DllImport("heif", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    public static extern void heif_context_debug_dump_boxes_to_file([NativeTypeName("heif_context*")] void* ctx, int fd);

    [DllImport("heif", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    public static extern void heif_context_set_write_mini_format([NativeTypeName("heif_context*")] void* param0, int enable);

    [DllImport("heif", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    public static extern heif_error heif_context_write_to_file([NativeTypeName("heif_context*")] void* param0, [NativeTypeName("const char *")] sbyte* filename);

    [DllImport("heif", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    public static extern heif_error heif_context_write([NativeTypeName("heif_context*")] void* param0, heif_writer* writer, void* userdata);

    [DllImport("heif", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    public static extern int heif_have_encoder_for_format(heif_compression_format format);

    [DllImport("heif", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    public static extern int heif_get_encoder_descriptors(heif_compression_format format_filter, [NativeTypeName("const char *")] sbyte* name_filter, [NativeTypeName("const heif_encoder_descriptor **")] void** out_encoders, int count);

    [DllImport("heif", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    [return: NativeTypeName("const char *")]
    public static extern sbyte* heif_encoder_descriptor_get_name([NativeTypeName("const heif_encoder_descriptor *")] void* param0);

    [DllImport("heif", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    [return: NativeTypeName("const char *")]
    public static extern sbyte* heif_encoder_descriptor_get_id_name([NativeTypeName("const heif_encoder_descriptor *")] void* param0);

    [DllImport("heif", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    public static extern heif_compression_format heif_encoder_descriptor_get_compression_format([NativeTypeName("const heif_encoder_descriptor *")] void* param0);

    [DllImport("heif", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    public static extern int heif_encoder_descriptor_supports_lossy_compression([NativeTypeName("const heif_encoder_descriptor *")] void* param0);

    [DllImport("heif", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    public static extern int heif_encoder_descriptor_supports_lossless_compression([NativeTypeName("const heif_encoder_descriptor *")] void* param0);

    [DllImport("heif", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    public static extern heif_error heif_context_get_encoder([NativeTypeName("heif_context*")] void* context, [NativeTypeName("const heif_encoder_descriptor *")] void* param1, [NativeTypeName("heif_encoder **")] void** out_encoder);

    [DllImport("heif", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    public static extern heif_error heif_context_get_encoder_for_format([NativeTypeName("heif_context*")] void* context, heif_compression_format format, [NativeTypeName("heif_encoder **")] void** param2);

    [DllImport("heif", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    public static extern void heif_encoder_release([NativeTypeName("heif_encoder*")] void* param0);

    [DllImport("heif", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    [return: NativeTypeName("const char *")]
    public static extern sbyte* heif_encoder_get_name([NativeTypeName("const heif_encoder *")] void* param0);

    [DllImport("heif", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    public static extern heif_error heif_encoder_set_lossy_quality([NativeTypeName("heif_encoder*")] void* param0, int quality);

    [DllImport("heif", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    public static extern heif_error heif_encoder_set_lossless([NativeTypeName("heif_encoder*")] void* param0, int enable);

    [DllImport("heif", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    public static extern heif_error heif_encoder_set_logging_level([NativeTypeName("heif_encoder*")] void* param0, int level);

    [DllImport("heif", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    [return: NativeTypeName("const heif_encoder_parameter *const *")]
    public static extern void** heif_encoder_list_parameters([NativeTypeName("heif_encoder*")] void* param0);

    [DllImport("heif", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    [return: NativeTypeName("const char *")]
    public static extern sbyte* heif_encoder_parameter_get_name([NativeTypeName("const heif_encoder_parameter *")] void* param0);

    [DllImport("heif", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    [return: NativeTypeName("enum heif_encoder_parameter_type")]
    public static extern heif_encoder_parameter_type heif_encoder_parameter_get_type([NativeTypeName("const heif_encoder_parameter *")] void* param0);

    [DllImport("heif", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    public static extern heif_error heif_encoder_parameter_get_valid_integer_values([NativeTypeName("const heif_encoder_parameter *")] void* param0, int* have_minimum, int* have_maximum, int* minimum, int* maximum, int* num_valid_values, [NativeTypeName("const int **")] int** out_integer_array);

    [DllImport("heif", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    public static extern heif_error heif_encoder_parameter_get_valid_string_values([NativeTypeName("const heif_encoder_parameter *")] void* param0, [NativeTypeName("const char *const **")] sbyte*** out_stringarray);

    [DllImport("heif", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    public static extern heif_error heif_encoder_set_parameter_integer([NativeTypeName("heif_encoder*")] void* param0, [NativeTypeName("const char *")] sbyte* parameter_name, int value);

    [DllImport("heif", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    public static extern heif_error heif_encoder_get_parameter_integer([NativeTypeName("heif_encoder*")] void* param0, [NativeTypeName("const char *")] sbyte* parameter_name, int* value);

    [DllImport("heif", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    public static extern heif_error heif_encoder_set_parameter_boolean([NativeTypeName("heif_encoder*")] void* param0, [NativeTypeName("const char *")] sbyte* parameter_name, int value);

    [DllImport("heif", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    public static extern heif_error heif_encoder_get_parameter_boolean([NativeTypeName("heif_encoder*")] void* param0, [NativeTypeName("const char *")] sbyte* parameter_name, int* value);

    [DllImport("heif", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    public static extern heif_error heif_encoder_set_parameter_string([NativeTypeName("heif_encoder*")] void* param0, [NativeTypeName("const char *")] sbyte* parameter_name, [NativeTypeName("const char *")] sbyte* value);

    [DllImport("heif", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    public static extern heif_error heif_encoder_get_parameter_string([NativeTypeName("heif_encoder*")] void* param0, [NativeTypeName("const char *")] sbyte* parameter_name, [NativeTypeName("char *")] sbyte* value, int value_size);

    [DllImport("heif", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    public static extern heif_error heif_encoder_parameter_string_valid_values([NativeTypeName("heif_encoder*")] void* param0, [NativeTypeName("const char *")] sbyte* parameter_name, [NativeTypeName("const char *const **")] sbyte*** out_stringarray);

    [DllImport("heif", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    public static extern heif_error heif_encoder_parameter_integer_valid_values([NativeTypeName("heif_encoder*")] void* param0, [NativeTypeName("const char *")] sbyte* parameter_name, int* have_minimum, int* have_maximum, int* minimum, int* maximum, int* num_valid_values, [NativeTypeName("const int **")] int** out_integer_array);

    [DllImport("heif", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    public static extern heif_error heif_encoder_set_parameter([NativeTypeName("heif_encoder*")] void* param0, [NativeTypeName("const char *")] sbyte* parameter_name, [NativeTypeName("const char *")] sbyte* value);

    [DllImport("heif", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    public static extern heif_error heif_encoder_get_parameter([NativeTypeName("heif_encoder*")] void* param0, [NativeTypeName("const char *")] sbyte* parameter_name, [NativeTypeName("char *")] sbyte* value_ptr, int value_size);

    [DllImport("heif", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    public static extern int heif_encoder_has_default([NativeTypeName("heif_encoder*")] void* param0, [NativeTypeName("const char *")] sbyte* parameter_name);

    [DllImport("heif", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    public static extern heif_orientation heif_orientation_concat(heif_orientation first, heif_orientation second);

    [DllImport("heif", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    public static extern heif_encoding_options* heif_encoding_options_alloc();

    [DllImport("heif", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    public static extern void heif_encoding_options_copy(heif_encoding_options* dst, [NativeTypeName("const heif_encoding_options *")] heif_encoding_options* src);

    [DllImport("heif", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    public static extern void heif_encoding_options_free(heif_encoding_options* param0);

    [DllImport("heif", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    public static extern heif_error heif_context_encode_image([NativeTypeName("heif_context*")] void* param0, [NativeTypeName("const heif_image *")] void* image, [NativeTypeName("heif_encoder*")] void* encoder, [NativeTypeName("const heif_encoding_options *")] heif_encoding_options* options, [NativeTypeName("heif_image_handle **")] void** out_image_handle);

    [DllImport("heif", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    public static extern heif_error heif_context_add_overlay_image([NativeTypeName("heif_context*")] void* ctx, [NativeTypeName("uint32_t")] uint image_width, [NativeTypeName("uint32_t")] uint image_height, [NativeTypeName("uint16_t")] ushort nImages, [NativeTypeName("const heif_item_id *")] uint* image_ids, [NativeTypeName("int32_t *")] int* offsets, [NativeTypeName("const uint16_t[4]")] ushort* background_rgba, [NativeTypeName("heif_image_handle **")] void** out_iovl_image_handle);

    [DllImport("heif", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    public static extern heif_error heif_context_set_primary_image([NativeTypeName("heif_context*")] void* param0, [NativeTypeName("heif_image_handle*")] void* image_handle);

    [DllImport("heif", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    public static extern void heif_context_set_major_brand([NativeTypeName("heif_context*")] void* ctx, [NativeTypeName("heif_brand2")] uint major_brand);

    [DllImport("heif", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    public static extern void heif_context_add_compatible_brand([NativeTypeName("heif_context*")] void* ctx, [NativeTypeName("heif_brand2")] uint compatible_brand);

    [DllImport("heif", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    public static extern void heif_context_set_unif([NativeTypeName("heif_context*")] void* ctx, int flag);

    [DllImport("heif", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    public static extern void heif_context_set_max_decoding_threads([NativeTypeName("heif_context*")] void* ctx, int max_threads);

    [DllImport("heif", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    public static extern int heif_context_get_max_decoding_threads([NativeTypeName("const heif_context *")] void* ctx);

    [DllImport("heif", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    public static extern int heif_have_decoder_for_format(heif_compression_format format);

    [DllImport("heif", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    public static extern heif_decoding_options* heif_decoding_options_alloc();

    [DllImport("heif", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    public static extern void heif_decoding_options_copy(heif_decoding_options* dst, [NativeTypeName("const heif_decoding_options *")] heif_decoding_options* src);

    [DllImport("heif", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    public static extern void heif_decoding_options_free(heif_decoding_options* param0);

    [DllImport("heif", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    public static extern int heif_get_decoder_descriptors(heif_compression_format format_filter, [NativeTypeName("const heif_decoder_descriptor **")] void** out_decoders, int count);

    [DllImport("heif", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    [return: NativeTypeName("const char *")]
    public static extern sbyte* heif_decoder_descriptor_get_name([NativeTypeName("const heif_decoder_descriptor *")] void* param0);

    [DllImport("heif", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    [return: NativeTypeName("const char *")]
    public static extern sbyte* heif_decoder_descriptor_get_id_name([NativeTypeName("const heif_decoder_descriptor *")] void* param0);

    [DllImport("heif", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    public static extern heif_error heif_decode_image([NativeTypeName("const heif_image_handle *")] void* in_handle, [NativeTypeName("heif_image **")] void** out_img, heif_colorspace colorspace, heif_chroma chroma, [NativeTypeName("const heif_decoding_options *")] heif_decoding_options* options);

    [DllImport("heif", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    public static extern void heif_image_handle_release([NativeTypeName("const heif_image_handle *")] void* param0);

    [DllImport("heif", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    public static extern int heif_image_handle_is_primary_image([NativeTypeName("const heif_image_handle *")] void* handle);

    [DllImport("heif", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    [return: NativeTypeName("heif_item_id")]
    public static extern uint heif_image_handle_get_item_id([NativeTypeName("const heif_image_handle *")] void* handle);

    [DllImport("heif", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    public static extern int heif_image_handle_get_width([NativeTypeName("const heif_image_handle *")] void* handle);

    [DllImport("heif", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    public static extern int heif_image_handle_get_height([NativeTypeName("const heif_image_handle *")] void* handle);

    [DllImport("heif", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    public static extern int heif_image_handle_has_alpha_channel([NativeTypeName("const heif_image_handle *")] void* param0);

    [DllImport("heif", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    public static extern int heif_image_handle_is_premultiplied_alpha([NativeTypeName("const heif_image_handle *")] void* param0);

    [DllImport("heif", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    public static extern int heif_image_handle_get_luma_bits_per_pixel([NativeTypeName("const heif_image_handle *")] void* param0);

    [DllImport("heif", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    public static extern int heif_image_handle_get_chroma_bits_per_pixel([NativeTypeName("const heif_image_handle *")] void* param0);

    [DllImport("heif", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    public static extern heif_error heif_image_handle_get_preferred_decoding_colorspace([NativeTypeName("const heif_image_handle *")] void* image_handle, heif_colorspace* out_colorspace, heif_chroma* out_chroma);

    [DllImport("heif", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    public static extern int heif_image_handle_get_ispe_width([NativeTypeName("const heif_image_handle *")] void* handle);

    [DllImport("heif", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    public static extern int heif_image_handle_get_ispe_height([NativeTypeName("const heif_image_handle *")] void* handle);

    [DllImport("heif", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    public static extern int heif_image_handle_get_pixel_aspect_ratio([NativeTypeName("const heif_image_handle *")] void* param0, [NativeTypeName("uint32_t *")] uint* aspect_h, [NativeTypeName("uint32_t *")] uint* aspect_v);

    [DllImport("heif", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    [return: NativeTypeName("heif_context*")]
    public static extern void* heif_image_handle_get_context([NativeTypeName("const heif_image_handle *")] void* handle);

    [DllImport("heif", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    [return: NativeTypeName("const char *")]
    public static extern sbyte* heif_image_handle_get_gimi_content_id([NativeTypeName("const heif_image_handle *")] void* handle);

    [DllImport("heif", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    public static extern void heif_image_handle_set_gimi_content_id([NativeTypeName("heif_image_handle*")] void* handle, [NativeTypeName("const char *")] sbyte* content_id);

    [DllImport("heif", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    [return: NativeTypeName("uint32_t")]
    public static extern uint heif_image_handle_get_number_of_cmpd_components([NativeTypeName("const heif_image_handle *")] void* param0);

    [DllImport("heif", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    [return: NativeTypeName("uint16_t")]
    public static extern ushort heif_image_handle_get_cmpd_component_type([NativeTypeName("const heif_image_handle *")] void* param0, [NativeTypeName("uint32_t")] uint component_idx);

    [DllImport("heif", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    [return: NativeTypeName("const char *")]
    public static extern sbyte* heif_image_handle_get_cmpd_component_type_uri([NativeTypeName("const heif_image_handle *")] void* param0, [NativeTypeName("uint32_t")] uint component_idx);

    [DllImport("heif", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    public static extern int heif_image_handle_has_gimi_component_content_ids([NativeTypeName("const heif_image_handle *")] void* param0);

    [DllImport("heif", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    [return: NativeTypeName("const char *")]
    public static extern sbyte* heif_image_handle_get_gimi_component_content_id([NativeTypeName("const heif_image_handle *")] void* param0, [NativeTypeName("uint32_t")] uint component_idx);

    [DllImport("heif", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    public static extern void heif_image_handle_set_gimi_component_content_id([NativeTypeName("heif_image_handle*")] void* param0, [NativeTypeName("uint32_t")] uint component_idx, [NativeTypeName("const char *")] sbyte* content_id);

    [DllImport("heif", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    public static extern heif_error heif_image_handle_get_image_tiling([NativeTypeName("const heif_image_handle *")] void* handle, int process_image_transformations, [NativeTypeName("struct heif_image_tiling *")] heif_image_tiling* out_tiling);

    [DllImport("heif", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    public static extern heif_error heif_image_handle_get_grid_image_tile_id([NativeTypeName("const heif_image_handle *")] void* handle, int process_image_transformations, [NativeTypeName("uint32_t")] uint tile_x, [NativeTypeName("uint32_t")] uint tile_y, [NativeTypeName("heif_item_id *")] uint* out_tile_item_id);

    [DllImport("heif", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    public static extern heif_error heif_image_handle_decode_image_tile([NativeTypeName("const heif_image_handle *")] void* in_handle, [NativeTypeName("heif_image **")] void** out_img, heif_colorspace colorspace, heif_chroma chroma, [NativeTypeName("const heif_decoding_options *")] heif_decoding_options* options, [NativeTypeName("uint32_t")] uint tile_x, [NativeTypeName("uint32_t")] uint tile_y);

    [DllImport("heif", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    public static extern heif_error heif_context_encode_grid([NativeTypeName("heif_context*")] void* ctx, [NativeTypeName("heif_image **")] void** tiles, [NativeTypeName("uint16_t")] ushort rows, [NativeTypeName("uint16_t")] ushort columns, [NativeTypeName("heif_encoder*")] void* encoder, [NativeTypeName("const heif_encoding_options *")] heif_encoding_options* input_options, [NativeTypeName("heif_image_handle **")] void** out_image_handle);

    [DllImport("heif", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    public static extern heif_error heif_context_add_grid_image([NativeTypeName("heif_context*")] void* ctx, [NativeTypeName("uint32_t")] uint image_width, [NativeTypeName("uint32_t")] uint image_height, [NativeTypeName("uint32_t")] uint tile_columns, [NativeTypeName("uint32_t")] uint tile_rows, [NativeTypeName("const heif_encoding_options *")] heif_encoding_options* encoding_options, [NativeTypeName("heif_image_handle **")] void** out_grid_image_handle);

    [DllImport("heif", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    public static extern heif_error heif_context_add_image_tile([NativeTypeName("heif_context*")] void* ctx, [NativeTypeName("heif_image_handle*")] void* tiled_image, [NativeTypeName("uint32_t")] uint tile_x, [NativeTypeName("uint32_t")] uint tile_y, [NativeTypeName("const heif_image *")] void* image, [NativeTypeName("heif_encoder*")] void* encoder);

    [DllImport("heif", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    [return: NativeTypeName("uint32_t")]
    public static extern uint heif_image_get_number_of_used_components([NativeTypeName("const heif_image *")] void* param0);

    [DllImport("heif", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    public static extern void heif_image_get_used_component_ids([NativeTypeName("const heif_image *")] void* param0, [NativeTypeName("uint32_t *")] uint* out_component_ids);

    [DllImport("heif", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    public static extern heif_channel heif_image_get_component_channel([NativeTypeName("const heif_image *")] void* param0, [NativeTypeName("uint32_t")] uint component_id);

    [DllImport("heif", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    [return: NativeTypeName("uint32_t")]
    public static extern uint heif_image_get_component_width([NativeTypeName("const heif_image *")] void* param0, [NativeTypeName("uint32_t")] uint component_id);

    [DllImport("heif", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    [return: NativeTypeName("uint32_t")]
    public static extern uint heif_image_get_component_height([NativeTypeName("const heif_image *")] void* param0, [NativeTypeName("uint32_t")] uint component_id);

    [DllImport("heif", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    public static extern int heif_image_get_component_bits_per_pixel([NativeTypeName("const heif_image *")] void* param0, [NativeTypeName("uint32_t")] uint component_id);

    [DllImport("heif", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    [return: NativeTypeName("uint16_t")]
    public static extern ushort heif_image_get_component_type([NativeTypeName("const heif_image *")] void* param0, [NativeTypeName("uint32_t")] uint component_id);

    [DllImport("heif", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    public static extern heif_component_datatype heif_image_get_component_datatype([NativeTypeName("const heif_image *")] void* param0, [NativeTypeName("uint32_t")] uint component_id);

    [DllImport("heif", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    [return: NativeTypeName("uint32_t")]
    public static extern uint heif_image_handle_get_number_of_components([NativeTypeName("const heif_image_handle *")] void* param0);

    [DllImport("heif", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    public static extern void heif_image_handle_get_used_component_ids([NativeTypeName("const heif_image_handle *")] void* param0, [NativeTypeName("uint32_t *")] uint* out_component_ids);

    [DllImport("heif", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    [return: NativeTypeName("uint16_t")]
    public static extern ushort heif_image_handle_get_component_type([NativeTypeName("const heif_image_handle *")] void* param0, [NativeTypeName("uint32_t")] uint component_id);

    [DllImport("heif", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    public static extern int heif_image_handle_get_component_bits_per_pixel([NativeTypeName("const heif_image_handle *")] void* param0, [NativeTypeName("uint32_t")] uint component_id);

    [DllImport("heif", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    public static extern heif_component_datatype heif_image_handle_get_component_datatype([NativeTypeName("const heif_image_handle *")] void* param0, [NativeTypeName("uint32_t")] uint component_id);

    [DllImport("heif", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    public static extern heif_error heif_image_add_component([NativeTypeName("heif_image*")] void* image, int width, int height, [NativeTypeName("uint16_t")] ushort component_type, heif_component_datatype datatype, int bit_depth, [NativeTypeName("uint32_t *")] uint* out_component_id);

    [DllImport("heif", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    [return: NativeTypeName("const uint8_t *")]
    public static extern byte* heif_image_get_component_readonly([NativeTypeName("const heif_image *")] void* param0, [NativeTypeName("uint32_t")] uint component_id, [NativeTypeName("size_t *")] nuint* out_stride);

    [DllImport("heif", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    [return: NativeTypeName("uint8_t *")]
    public static extern byte* heif_image_get_component([NativeTypeName("heif_image*")] void* param0, [NativeTypeName("uint32_t")] uint component_id, [NativeTypeName("size_t *")] nuint* out_stride);

    [DllImport("heif", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    [return: NativeTypeName("const uint16_t *")]
    public static extern ushort* heif_image_get_component_uint16_readonly([NativeTypeName("const heif_image *")] void* param0, [NativeTypeName("uint32_t")] uint component_id, [NativeTypeName("size_t *")] nuint* out_row_elements);

    [DllImport("heif", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    [return: NativeTypeName("uint16_t *")]
    public static extern ushort* heif_image_get_component_uint16([NativeTypeName("heif_image*")] void* param0, [NativeTypeName("uint32_t")] uint component_id, [NativeTypeName("size_t *")] nuint* out_row_elements);

    [DllImport("heif", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    [return: NativeTypeName("const uint32_t *")]
    public static extern uint* heif_image_get_component_uint32_readonly([NativeTypeName("const heif_image *")] void* param0, [NativeTypeName("uint32_t")] uint component_id, [NativeTypeName("size_t *")] nuint* out_row_elements);

    [DllImport("heif", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    [return: NativeTypeName("uint32_t *")]
    public static extern uint* heif_image_get_component_uint32([NativeTypeName("heif_image*")] void* param0, [NativeTypeName("uint32_t")] uint component_id, [NativeTypeName("size_t *")] nuint* out_row_elements);

    [DllImport("heif", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    [return: NativeTypeName("const uint64_t *")]
    public static extern ulong* heif_image_get_component_uint64_readonly([NativeTypeName("const heif_image *")] void* param0, [NativeTypeName("uint32_t")] uint component_id, [NativeTypeName("size_t *")] nuint* out_row_elements);

    [DllImport("heif", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    [return: NativeTypeName("uint64_t *")]
    public static extern ulong* heif_image_get_component_uint64([NativeTypeName("heif_image*")] void* param0, [NativeTypeName("uint32_t")] uint component_id, [NativeTypeName("size_t *")] nuint* out_row_elements);

    [DllImport("heif", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    [return: NativeTypeName("const int8_t *")]
    public static extern sbyte* heif_image_get_component_int8_readonly([NativeTypeName("const heif_image *")] void* param0, [NativeTypeName("uint32_t")] uint component_id, [NativeTypeName("size_t *")] nuint* out_row_elements);

    [DllImport("heif", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    [return: NativeTypeName("int8_t *")]
    public static extern sbyte* heif_image_get_component_int8([NativeTypeName("heif_image*")] void* param0, [NativeTypeName("uint32_t")] uint component_id, [NativeTypeName("size_t *")] nuint* out_row_elements);

    [DllImport("heif", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    [return: NativeTypeName("const int16_t *")]
    public static extern short* heif_image_get_component_int16_readonly([NativeTypeName("const heif_image *")] void* param0, [NativeTypeName("uint32_t")] uint component_id, [NativeTypeName("size_t *")] nuint* out_row_elements);

    [DllImport("heif", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    [return: NativeTypeName("int16_t *")]
    public static extern short* heif_image_get_component_int16([NativeTypeName("heif_image*")] void* param0, [NativeTypeName("uint32_t")] uint component_id, [NativeTypeName("size_t *")] nuint* out_row_elements);

    [DllImport("heif", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    [return: NativeTypeName("const int32_t *")]
    public static extern int* heif_image_get_component_int32_readonly([NativeTypeName("const heif_image *")] void* param0, [NativeTypeName("uint32_t")] uint component_id, [NativeTypeName("size_t *")] nuint* out_row_elements);

    [DllImport("heif", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    [return: NativeTypeName("int32_t *")]
    public static extern int* heif_image_get_component_int32([NativeTypeName("heif_image*")] void* param0, [NativeTypeName("uint32_t")] uint component_id, [NativeTypeName("size_t *")] nuint* out_row_elements);

    [DllImport("heif", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    [return: NativeTypeName("const int64_t *")]
    public static extern long* heif_image_get_component_int64_readonly([NativeTypeName("const heif_image *")] void* param0, [NativeTypeName("uint32_t")] uint component_id, [NativeTypeName("size_t *")] nuint* out_row_elements);

    [DllImport("heif", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    [return: NativeTypeName("int64_t *")]
    public static extern long* heif_image_get_component_int64([NativeTypeName("heif_image*")] void* param0, [NativeTypeName("uint32_t")] uint component_id, [NativeTypeName("size_t *")] nuint* out_row_elements);

    [DllImport("heif", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    [return: NativeTypeName("const float *")]
    public static extern float* heif_image_get_component_float32_readonly([NativeTypeName("const heif_image *")] void* param0, [NativeTypeName("uint32_t")] uint component_id, [NativeTypeName("size_t *")] nuint* out_row_elements);

    [DllImport("heif", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    public static extern float* heif_image_get_component_float32([NativeTypeName("heif_image*")] void* param0, [NativeTypeName("uint32_t")] uint component_id, [NativeTypeName("size_t *")] nuint* out_row_elements);

    [DllImport("heif", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    [return: NativeTypeName("const double *")]
    public static extern double* heif_image_get_component_float64_readonly([NativeTypeName("const heif_image *")] void* param0, [NativeTypeName("uint32_t")] uint component_id, [NativeTypeName("size_t *")] nuint* out_row_elements);

    [DllImport("heif", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    public static extern double* heif_image_get_component_float64([NativeTypeName("heif_image*")] void* param0, [NativeTypeName("uint32_t")] uint component_id, [NativeTypeName("size_t *")] nuint* out_row_elements);

    [DllImport("heif", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    [return: NativeTypeName("const heif_complex32 *")]
    public static extern heif_complex32* heif_image_get_component_complex32_readonly([NativeTypeName("const heif_image *")] void* param0, [NativeTypeName("uint32_t")] uint component_id, [NativeTypeName("size_t *")] nuint* out_row_elements);

    [DllImport("heif", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    public static extern heif_complex32* heif_image_get_component_complex32([NativeTypeName("heif_image*")] void* param0, [NativeTypeName("uint32_t")] uint component_id, [NativeTypeName("size_t *")] nuint* out_row_elements);

    [DllImport("heif", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    [return: NativeTypeName("const heif_complex64 *")]
    public static extern heif_complex64* heif_image_get_component_complex64_readonly([NativeTypeName("const heif_image *")] void* param0, [NativeTypeName("uint32_t")] uint component_id, [NativeTypeName("size_t *")] nuint* out_row_elements);

    [DllImport("heif", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    public static extern heif_complex64* heif_image_get_component_complex64([NativeTypeName("heif_image*")] void* param0, [NativeTypeName("uint32_t")] uint component_id, [NativeTypeName("size_t *")] nuint* out_row_elements);

    [DllImport("heif", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    public static extern heif_error heif_image_set_gimi_component_content_id([NativeTypeName("heif_image*")] void* param0, [NativeTypeName("uint32_t")] uint component_id, [NativeTypeName("const char *")] sbyte* content_id);

    [DllImport("heif", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    public static extern heif_omaf_image_projection heif_image_handle_get_omaf_image_projection([NativeTypeName("const heif_image_handle *")] void* handle);

    [DllImport("heif", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    public static extern void heif_image_handle_set_omaf_image_projection([NativeTypeName("heif_image_handle*")] void* handle, heif_omaf_image_projection image_projection);

    [DllImport("heif", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    public static extern heif_omaf_image_projection heif_image_get_omaf_image_projection([NativeTypeName("const heif_image *")] void* image);

    [DllImport("heif", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    public static extern void heif_image_set_omaf_image_projection([NativeTypeName("heif_image*")] void* image, heif_omaf_image_projection image_projection);

    [NativeTypeName("#define LIBHEIF_NUMERIC_VERSION ((1<<24) | (23<<16) | (7<<8) | 0)")]
    public const int LIBHEIF_NUMERIC_VERSION = ((1 << 24) | (23 << 16) | (7 << 8) | 0);

    [NativeTypeName("#define LIBHEIF_VERSION \"1.23.7\"")]
    public const string LIBHEIF_VERSION = "1.23.7";

    [NativeTypeName("#define heif_chroma_monochrome heif_chroma_planar")]
    public const heif_chroma heif_chroma_monochrome = heif_chroma_planar;

    [NativeTypeName("#define heif_colorspace_nonvisual heif_colorspace_custom")]
    public const heif_colorspace heif_colorspace_nonvisual = heif_colorspace_custom;

    [NativeTypeName("#define heif_brand2_heic heif_fourcc('h','e','i','c')")]
    public const uint heif_brand2_heic = ((uint)(('h' << 24) | ('e' << 16) | ('i' << 8) | 'c'));

    [NativeTypeName("#define heif_brand2_heix heif_fourcc('h','e','i','x')")]
    public const uint heif_brand2_heix = ((uint)(('h' << 24) | ('e' << 16) | ('i' << 8) | 'x'));

    [NativeTypeName("#define heif_brand2_hevc heif_fourcc('h','e','v','c')")]
    public const uint heif_brand2_hevc = ((uint)(('h' << 24) | ('e' << 16) | ('v' << 8) | 'c'));

    [NativeTypeName("#define heif_brand2_hevx heif_fourcc('h','e','v','x')")]
    public const uint heif_brand2_hevx = ((uint)(('h' << 24) | ('e' << 16) | ('v' << 8) | 'x'));

    [NativeTypeName("#define heif_brand2_heim heif_fourcc('h','e','i','m')")]
    public const uint heif_brand2_heim = ((uint)(('h' << 24) | ('e' << 16) | ('i' << 8) | 'm'));

    [NativeTypeName("#define heif_brand2_heis heif_fourcc('h','e','i','s')")]
    public const uint heif_brand2_heis = ((uint)(('h' << 24) | ('e' << 16) | ('i' << 8) | 's'));

    [NativeTypeName("#define heif_brand2_hevm heif_fourcc('h','e','v','m')")]
    public const uint heif_brand2_hevm = ((uint)(('h' << 24) | ('e' << 16) | ('v' << 8) | 'm'));

    [NativeTypeName("#define heif_brand2_hevs heif_fourcc('h','e','v','s')")]
    public const uint heif_brand2_hevs = ((uint)(('h' << 24) | ('e' << 16) | ('v' << 8) | 's'));

    [NativeTypeName("#define heif_brand2_avif heif_fourcc('a','v','i','f')")]
    public const uint heif_brand2_avif = ((uint)(('a' << 24) | ('v' << 16) | ('i' << 8) | 'f'));

    [NativeTypeName("#define heif_brand2_avis heif_fourcc('a','v','i','s')")]
    public const uint heif_brand2_avis = ((uint)(('a' << 24) | ('v' << 16) | ('i' << 8) | 's'));

    [NativeTypeName("#define heif_brand2_mif1 heif_fourcc('m','i','f','1')")]
    public const uint heif_brand2_mif1 = ((uint)(('m' << 24) | ('i' << 16) | ('f' << 8) | '1'));

    [NativeTypeName("#define heif_brand2_mif2 heif_fourcc('m','i','f','2')")]
    public const uint heif_brand2_mif2 = ((uint)(('m' << 24) | ('i' << 16) | ('f' << 8) | '2'));

    [NativeTypeName("#define heif_brand2_mif3 heif_fourcc('m','i','f','3')")]
    public const uint heif_brand2_mif3 = ((uint)(('m' << 24) | ('i' << 16) | ('f' << 8) | '3'));

    [NativeTypeName("#define heif_brand2_msf1 heif_fourcc('m','s','f','1')")]
    public const uint heif_brand2_msf1 = ((uint)(('m' << 24) | ('s' << 16) | ('f' << 8) | '1'));

    [NativeTypeName("#define heif_brand2_vvic heif_fourcc('v','v','i','c')")]
    public const uint heif_brand2_vvic = ((uint)(('v' << 24) | ('v' << 16) | ('i' << 8) | 'c'));

    [NativeTypeName("#define heif_brand2_vvis heif_fourcc('v','v','i','s')")]
    public const uint heif_brand2_vvis = ((uint)(('v' << 24) | ('v' << 16) | ('i' << 8) | 's'));

    [NativeTypeName("#define heif_brand2_evbi heif_fourcc('e','v','b','i')")]
    public const uint heif_brand2_evbi = ((uint)(('e' << 24) | ('v' << 16) | ('b' << 8) | 'i'));

    [NativeTypeName("#define heif_brand2_evmi heif_fourcc('e','v','m','i')")]
    public const uint heif_brand2_evmi = ((uint)(('e' << 24) | ('v' << 16) | ('m' << 8) | 'i'));

    [NativeTypeName("#define heif_brand2_evbs heif_fourcc('e','v','b','s')")]
    public const uint heif_brand2_evbs = ((uint)(('e' << 24) | ('v' << 16) | ('b' << 8) | 's'));

    [NativeTypeName("#define heif_brand2_evms heif_fourcc('e','v','m','s')")]
    public const uint heif_brand2_evms = ((uint)(('e' << 24) | ('v' << 16) | ('m' << 8) | 's'));

    [NativeTypeName("#define heif_brand2_jpeg heif_fourcc('j','p','e','g')")]
    public const uint heif_brand2_jpeg = ((uint)(('j' << 24) | ('p' << 16) | ('e' << 8) | 'g'));

    [NativeTypeName("#define heif_brand2_jpgs heif_fourcc('j','p','g','s')")]
    public const uint heif_brand2_jpgs = ((uint)(('j' << 24) | ('p' << 16) | ('g' << 8) | 's'));

    [NativeTypeName("#define heif_brand2_j2ki heif_fourcc('j','2','k','i')")]
    public const uint heif_brand2_j2ki = ((uint)(('j' << 24) | ('2' << 16) | ('k' << 8) | 'i'));

    [NativeTypeName("#define heif_brand2_j2is heif_fourcc('j','2','i','s')")]
    public const uint heif_brand2_j2is = ((uint)(('j' << 24) | ('2' << 16) | ('i' << 8) | 's'));

    [NativeTypeName("#define heif_brand2_miaf heif_fourcc('m','i','a','f')")]
    public const uint heif_brand2_miaf = ((uint)(('m' << 24) | ('i' << 16) | ('a' << 8) | 'f'));

    [NativeTypeName("#define heif_brand2_1pic heif_fourcc('1','p','i','c')")]
    public const uint heif_brand2_1pic = ((uint)(('1' << 24) | ('p' << 16) | ('i' << 8) | 'c'));

    [NativeTypeName("#define heif_brand2_avci heif_fourcc('a','v','c','i')")]
    public const uint heif_brand2_avci = ((uint)(('a' << 24) | ('v' << 16) | ('c' << 8) | 'i'));

    [NativeTypeName("#define heif_brand2_avcs heif_fourcc('a','v','c','s')")]
    public const uint heif_brand2_avcs = ((uint)(('a' << 24) | ('v' << 16) | ('c' << 8) | 's'));

    [NativeTypeName("#define heif_brand2_unif heif_fourcc('u','n','i','f')")]
    public const uint heif_brand2_unif = ((uint)(('u' << 24) | ('n' << 16) | ('i' << 8) | 'f'));

    [NativeTypeName("#define heif_brand2_iso8 heif_fourcc('i','s','o','8')")]
    public const uint heif_brand2_iso8 = ((uint)(('i' << 24) | ('s' << 16) | ('o' << 8) | '8'));

    [NativeTypeName("#define heif_brand2_isom heif_fourcc('i','s','o','m')")]
    public const uint heif_brand2_isom = ((uint)(('i' << 24) | ('s' << 16) | ('o' << 8) | 'm'));

    [NativeTypeName("#define heif_brand2_mp41 heif_fourcc('m','p','4','1')")]
    public const uint heif_brand2_mp41 = ((uint)(('m' << 24) | ('p' << 16) | ('4' << 8) | '1'));

    [NativeTypeName("#define heif_brand2_mp42 heif_fourcc('m','p','4','2')")]
    public const uint heif_brand2_mp42 = ((uint)(('m' << 24) | ('p' << 16) | ('4' << 8) | '2'));

    [NativeTypeName("#define LIBHEIF_AUX_IMAGE_FILTER_OMIT_ALPHA (1UL<<1)")]
    public const uint LIBHEIF_AUX_IMAGE_FILTER_OMIT_ALPHA = (1U << 1);

    [NativeTypeName("#define LIBHEIF_AUX_IMAGE_FILTER_OMIT_DEPTH (2UL<<1)")]
    public const uint LIBHEIF_AUX_IMAGE_FILTER_OMIT_DEPTH = (2U << 1);

    [NativeTypeName("#define heif_entity_group_altr heif_fourcc('a','l','t','r')")]
    public const uint heif_entity_group_altr = ((uint)(('a' << 24) | ('l' << 16) | ('t' << 8) | 'r'));

    [NativeTypeName("#define heif_entity_group_pymd heif_fourcc('p','y','m','d')")]
    public const uint heif_entity_group_pymd = ((uint)(('p' << 24) | ('y' << 16) | ('m' << 8) | 'd'));

    [NativeTypeName("#define heif_entity_group_eqiv heif_fourcc('e','q','i','v')")]
    public const uint heif_entity_group_eqiv = ((uint)(('e' << 24) | ('q' << 16) | ('i' << 8) | 'v'));

    [NativeTypeName("#define heif_entity_group_brst heif_fourcc('b','r','s','t')")]
    public const uint heif_entity_group_brst = ((uint)(('b' << 24) | ('r' << 16) | ('s' << 8) | 't'));

    [NativeTypeName("#define heif_entity_group_tsyn heif_fourcc('t','s','y','n')")]
    public const uint heif_entity_group_tsyn = ((uint)(('t' << 24) | ('s' << 16) | ('y' << 8) | 'n'));

    [NativeTypeName("#define heif_entity_group_ster heif_fourcc('s','t','e','r')")]
    public const uint heif_entity_group_ster = ((uint)(('s' << 24) | ('t' << 16) | ('e' << 8) | 'r'));

    [NativeTypeName("#define heif_entity_group_stem heif_fourcc('s','t','e','m')")]
    public const uint heif_entity_group_stem = ((uint)(('s' << 24) | ('t' << 16) | ('e' << 8) | 'm'));

    [NativeTypeName("#define heif_entity_group_aebr heif_fourcc('a','e','b','r')")]
    public const uint heif_entity_group_aebr = ((uint)(('a' << 24) | ('e' << 16) | ('b' << 8) | 'r'));

    [NativeTypeName("#define heif_entity_group_wbbr heif_fourcc('w','b','b','r')")]
    public const uint heif_entity_group_wbbr = ((uint)(('w' << 24) | ('b' << 16) | ('b' << 8) | 'r'));

    [NativeTypeName("#define heif_entity_group_fobr heif_fourcc('f','o','b','r')")]
    public const uint heif_entity_group_fobr = ((uint)(('f' << 24) | ('o' << 16) | ('b' << 8) | 'r'));

    [NativeTypeName("#define heif_entity_group_afbr heif_fourcc('a','f','b','r')")]
    public const uint heif_entity_group_afbr = ((uint)(('a' << 24) | ('f' << 16) | ('b' << 8) | 'r'));

    [NativeTypeName("#define heif_entity_group_dobr heif_fourcc('d','o','b','r')")]
    public const uint heif_entity_group_dobr = ((uint)(('d' << 24) | ('o' << 16) | ('b' << 8) | 'r'));

    [NativeTypeName("#define heif_entity_group_albc heif_fourcc('a','l','b','c')")]
    public const uint heif_entity_group_albc = ((uint)(('a' << 24) | ('l' << 16) | ('b' << 8) | 'c'));

    [NativeTypeName("#define heif_entity_group_favc heif_fourcc('f','a','v','c')")]
    public const uint heif_entity_group_favc = ((uint)(('f' << 24) | ('a' << 16) | ('v' << 8) | 'c'));

    [NativeTypeName("#define heif_entity_group_pano heif_fourcc('p','a','n','o')")]
    public const uint heif_entity_group_pano = ((uint)(('p' << 24) | ('a' << 16) | ('n' << 8) | 'o'));

    [NativeTypeName("#define heif_entity_group_slid heif_fourcc('s','l','i','d')")]
    public const uint heif_entity_group_slid = ((uint)(('s' << 24) | ('l' << 16) | ('i' << 8) | 'd'));

    [NativeTypeName("#define heif_entity_group_prgr heif_fourcc('p','r','g','r')")]
    public const uint heif_entity_group_prgr = ((uint)(('p' << 24) | ('r' << 16) | ('g' << 8) | 'r'));
}
