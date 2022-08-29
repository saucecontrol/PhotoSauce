// Copyright © Clinton Ingram and Contributors. Licensed under the MIT License (MIT).

// Ported from libjpeg headers (jpeglib.h)
// This software is based in part on the work of the Independent JPEG Group.
// Original source copyright (C) 1991-1998, Thomas G. Lane. All Rights Reserved
// See third-party-notices in the repository root for more information.

using System;
using System.Runtime.CompilerServices;

namespace PhotoSauce.Interop.Libjpeg;

internal unsafe partial struct jpeg_decompress_struct
{
    [NativeTypeName("struct jpeg_error_mgr *")]
    public jpeg_error_mgr* err;

    [NativeTypeName("struct jpeg_memory_mgr *")]
    public jpeg_memory_mgr* mem;

    [NativeTypeName("struct jpeg_progress_mgr *")]
    public jpeg_progress_mgr* progress;

    public void* client_data;

    [NativeTypeName("boolean")]
    public int is_decompressor;

    public int global_state;

    [NativeTypeName("struct jpeg_source_mgr *")]
    public jpeg_source_mgr* src;

    [NativeTypeName("JDIMENSION")]
    public uint image_width;

    [NativeTypeName("JDIMENSION")]
    public uint image_height;

    public int num_components;

    public J_COLOR_SPACE jpeg_color_space;

    public J_COLOR_SPACE out_color_space;

    [NativeTypeName("unsigned int")]
    public uint scale_num;

    [NativeTypeName("unsigned int")]
    public uint scale_denom;

    public double output_gamma;

    [NativeTypeName("boolean")]
    public int buffered_image;

    [NativeTypeName("boolean")]
    public int raw_data_out;

    public J_DCT_METHOD dct_method;

    [NativeTypeName("boolean")]
    public int do_fancy_upsampling;

    [NativeTypeName("boolean")]
    public int do_block_smoothing;

    [NativeTypeName("boolean")]
    public int quantize_colors;

    public J_DITHER_MODE dither_mode;

    [NativeTypeName("boolean")]
    public int two_pass_quantize;

    public int desired_number_of_colors;

    [NativeTypeName("boolean")]
    public int enable_1pass_quant;

    [NativeTypeName("boolean")]
    public int enable_external_quant;

    [NativeTypeName("boolean")]
    public int enable_2pass_quant;

    [NativeTypeName("JDIMENSION")]
    public uint output_width;

    [NativeTypeName("JDIMENSION")]
    public uint output_height;

    public int out_color_components;

    public int output_components;

    public int rec_outbuf_height;

    public int actual_number_of_colors;

    [NativeTypeName("JSAMPARRAY")]
    public byte** colormap;

    [NativeTypeName("JDIMENSION")]
    public uint output_scanline;

    public int input_scan_number;

    [NativeTypeName("JDIMENSION")]
    public uint input_iMCU_row;

    public int output_scan_number;

    [NativeTypeName("JDIMENSION")]
    public uint output_iMCU_row;

    [NativeTypeName("int (*)[64]")]
    public int* coef_bits;

    [NativeTypeName("JQUANT_TBL *[4]")]
    public _quant_tbl_ptrs_e__FixedBuffer quant_tbl_ptrs;

    [NativeTypeName("JHUFF_TBL *[4]")]
    public _dc_huff_tbl_ptrs_e__FixedBuffer dc_huff_tbl_ptrs;

    [NativeTypeName("JHUFF_TBL *[4]")]
    public _ac_huff_tbl_ptrs_e__FixedBuffer ac_huff_tbl_ptrs;

    public int data_precision;

    public jpeg_component_info* comp_info;

    [NativeTypeName("boolean")]
    public int progressive_mode;

    [NativeTypeName("boolean")]
    public int arith_code;

    [NativeTypeName("UINT8[16]")]
    public fixed byte arith_dc_L[16];

    [NativeTypeName("UINT8[16]")]
    public fixed byte arith_dc_U[16];

    [NativeTypeName("UINT8[16]")]
    public fixed byte arith_ac_K[16];

    [NativeTypeName("unsigned int")]
    public uint restart_interval;

    [NativeTypeName("boolean")]
    public int saw_JFIF_marker;

    [NativeTypeName("UINT8")]
    public byte JFIF_major_version;

    [NativeTypeName("UINT8")]
    public byte JFIF_minor_version;

    [NativeTypeName("UINT8")]
    public byte density_unit;

    [NativeTypeName("UINT16")]
    public ushort X_density;

    [NativeTypeName("UINT16")]
    public ushort Y_density;

    [NativeTypeName("boolean")]
    public int saw_Adobe_marker;

    [NativeTypeName("UINT8")]
    public byte Adobe_transform;

    [NativeTypeName("boolean")]
    public int CCIR601_sampling;

    [NativeTypeName("jpeg_saved_marker_ptr")]
    public jpeg_marker_struct* marker_list;

    public int max_h_samp_factor;

    public int max_v_samp_factor;

    public int min_DCT_scaled_size;

    [NativeTypeName("JDIMENSION")]
    public uint total_iMCU_rows;

    [NativeTypeName("JSAMPLE *")]
    public byte* sample_range_limit;

    public int comps_in_scan;

    [NativeTypeName("jpeg_component_info *[4]")]
    public _cur_comp_info_e__FixedBuffer cur_comp_info;

    [NativeTypeName("JDIMENSION")]
    public uint MCUs_per_row;

    [NativeTypeName("JDIMENSION")]
    public uint MCU_rows_in_scan;

    public int blocks_in_MCU;

    [NativeTypeName("int[10]")]
    public fixed int MCU_membership[10];

    public int Ss;

    public int Se;

    public int Ah;

    public int Al;

    public int unread_marker;

    [NativeTypeName("struct jpeg_decomp_master *")]
    public IntPtr master;

    [NativeTypeName("struct jpeg_d_main_controller *")]
    public IntPtr main;

    [NativeTypeName("struct jpeg_d_coef_controller *")]
    public IntPtr coef;

    [NativeTypeName("struct jpeg_d_post_controller *")]
    public IntPtr post;

    [NativeTypeName("struct jpeg_input_controller *")]
    public IntPtr inputctl;

    [NativeTypeName("struct jpeg_marker_reader *")]
    public IntPtr marker;

    [NativeTypeName("struct jpeg_entropy_decoder *")]
    public IntPtr entropy;

    [NativeTypeName("struct jpeg_inverse_dct *")]
    public IntPtr idct;

    [NativeTypeName("struct jpeg_upsampler *")]
    public IntPtr upsample;

    [NativeTypeName("struct jpeg_color_deconverter *")]
    public IntPtr cconvert;

    [NativeTypeName("struct jpeg_color_quantizer *")]
    public IntPtr cquantize;

    public unsafe partial struct _quant_tbl_ptrs_e__FixedBuffer
    {
        public JQUANT_TBL* e0;
        public JQUANT_TBL* e1;
        public JQUANT_TBL* e2;
        public JQUANT_TBL* e3;

        public ref JQUANT_TBL* this[int index]
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get
            {
                fixed (JQUANT_TBL** pThis = &e0)
                {
                    return ref pThis[index];
                }
            }
        }
    }

    public unsafe partial struct _dc_huff_tbl_ptrs_e__FixedBuffer
    {
        public JHUFF_TBL* e0;
        public JHUFF_TBL* e1;
        public JHUFF_TBL* e2;
        public JHUFF_TBL* e3;

        public ref JHUFF_TBL* this[int index]
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get
            {
                fixed (JHUFF_TBL** pThis = &e0)
                {
                    return ref pThis[index];
                }
            }
        }
    }

    public unsafe partial struct _ac_huff_tbl_ptrs_e__FixedBuffer
    {
        public JHUFF_TBL* e0;
        public JHUFF_TBL* e1;
        public JHUFF_TBL* e2;
        public JHUFF_TBL* e3;

        public ref JHUFF_TBL* this[int index]
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get
            {
                fixed (JHUFF_TBL** pThis = &e0)
                {
                    return ref pThis[index];
                }
            }
        }
    }

    public unsafe partial struct _cur_comp_info_e__FixedBuffer
    {
        public jpeg_component_info* e0;
        public jpeg_component_info* e1;
        public jpeg_component_info* e2;
        public jpeg_component_info* e3;

        public ref jpeg_component_info* this[int index]
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get
            {
                fixed (jpeg_component_info** pThis = &e0)
                {
                    return ref pThis[index];
                }
            }
        }
    }
}
