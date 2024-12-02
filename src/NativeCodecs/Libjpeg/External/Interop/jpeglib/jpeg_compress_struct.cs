// Copyright © Clinton Ingram and Contributors
// SPDX-License-Identifier: MIT

// Ported from libjpeg headers (jpeglib.h)
// This software is based in part on the work of the Independent JPEG Group.
// Original source copyright (C) 1991-1998, Thomas G. Lane. All Rights Reserved
// See third-party-notices in the repository root for more information.

using System.Runtime.CompilerServices;

namespace PhotoSauce.Interop.Libjpeg;

internal unsafe partial struct jpeg_compress_struct
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

    [NativeTypeName("struct jpeg_destination_mgr *")]
    public jpeg_destination_mgr* dest;

    [NativeTypeName("JDIMENSION")]
    public uint image_width;

    [NativeTypeName("JDIMENSION")]
    public uint image_height;

    public int input_components;

    public J_COLOR_SPACE in_color_space;

    public double input_gamma;

    public int data_precision;

    public int num_components;

    public J_COLOR_SPACE jpeg_color_space;

    public jpeg_component_info* comp_info;

    [NativeTypeName("JQUANT_TBL *[4]")]
    public _quant_tbl_ptrs_e__FixedBuffer quant_tbl_ptrs;

    [NativeTypeName("JHUFF_TBL *[4]")]
    public _dc_huff_tbl_ptrs_e__FixedBuffer dc_huff_tbl_ptrs;

    [NativeTypeName("JHUFF_TBL *[4]")]
    public _ac_huff_tbl_ptrs_e__FixedBuffer ac_huff_tbl_ptrs;

    [NativeTypeName("UINT8[16]")]
    public fixed byte arith_dc_L[16];

    [NativeTypeName("UINT8[16]")]
    public fixed byte arith_dc_U[16];

    [NativeTypeName("UINT8[16]")]
    public fixed byte arith_ac_K[16];

    public int num_scans;

    [NativeTypeName("const jpeg_scan_info *")]
    public jpeg_scan_info* scan_info;

    [NativeTypeName("boolean")]
    public int raw_data_in;

    [NativeTypeName("boolean")]
    public int arith_code;

    [NativeTypeName("boolean")]
    public int optimize_coding;

    [NativeTypeName("boolean")]
    public int CCIR601_sampling;

    public int smoothing_factor;

    public J_DCT_METHOD dct_method;

    [NativeTypeName("unsigned int")]
    public uint restart_interval;

    public int restart_in_rows;

    [NativeTypeName("boolean")]
    public int write_JFIF_header;

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
    public int write_Adobe_marker;

    [NativeTypeName("JDIMENSION")]
    public uint next_scanline;

    [NativeTypeName("boolean")]
    public int progressive_mode;

    public int max_h_samp_factor;

    public int max_v_samp_factor;

    [NativeTypeName("JDIMENSION")]
    public uint total_iMCU_rows;

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

    [NativeTypeName("struct jpeg_comp_master *")]
    public void* master;

    [NativeTypeName("struct jpeg_c_main_controller *")]
    public void* main;

    [NativeTypeName("struct jpeg_c_prep_controller *")]
    public void* prep;

    [NativeTypeName("struct jpeg_c_coef_controller *")]
    public void* coef;

    [NativeTypeName("struct jpeg_marker_writer *")]
    public void* marker;

    [NativeTypeName("struct jpeg_color_converter *")]
    public void* cconvert;

    [NativeTypeName("struct jpeg_downsampler *")]
    public void* downsample;

    [NativeTypeName("struct jpeg_forward_dct *")]
    public void* fdct;

    [NativeTypeName("struct jpeg_entropy_encoder *")]
    public void* entropy;

    public jpeg_scan_info* script_space;

    public int script_space_size;

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
