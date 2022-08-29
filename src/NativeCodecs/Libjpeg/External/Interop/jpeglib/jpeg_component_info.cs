// Copyright © Clinton Ingram and Contributors. Licensed under the MIT License (MIT).

// Ported from libjpeg headers (jpeglib.h)
// This software is based in part on the work of the Independent JPEG Group.
// Original source copyright (C) 1991-1998, Thomas G. Lane. All Rights Reserved
// See third-party-notices in the repository root for more information.

namespace PhotoSauce.Interop.Libjpeg;

internal unsafe partial struct jpeg_component_info
{
    public int component_id;

    public int component_index;

    public int h_samp_factor;

    public int v_samp_factor;

    public int quant_tbl_no;

    public int dc_tbl_no;

    public int ac_tbl_no;

    [NativeTypeName("JDIMENSION")]
    public uint width_in_blocks;

    [NativeTypeName("JDIMENSION")]
    public uint height_in_blocks;

    public int DCT_scaled_size;

    [NativeTypeName("JDIMENSION")]
    public uint downsampled_width;

    [NativeTypeName("JDIMENSION")]
    public uint downsampled_height;

    [NativeTypeName("boolean")]
    public int component_needed;

    public int MCU_width;

    public int MCU_height;

    public int MCU_blocks;

    public int MCU_sample_width;

    public int last_col_width;

    public int last_row_height;

    public JQUANT_TBL* quant_table;

    public void* dct_table;
}
