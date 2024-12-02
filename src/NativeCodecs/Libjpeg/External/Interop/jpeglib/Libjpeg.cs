// Copyright © Clinton Ingram and Contributors
// SPDX-License-Identifier: MIT

// Ported from libjpeg headers (jpeglib.h)
// This software is based in part on the work of the Independent JPEG Group.
// Original source copyright (C) 1991-1998, Thomas G. Lane. All Rights Reserved
// See third-party-notices in the repository root for more information.

using static PhotoSauce.Interop.Libjpeg.J_DCT_METHOD;

namespace PhotoSauce.Interop.Libjpeg;

internal static partial class Libjpeg
{
    [NativeTypeName("#define DCTSIZE 8")]
    public const int DCTSIZE = 8;

    [NativeTypeName("#define DCTSIZE2 64")]
    public const int DCTSIZE2 = 64;

    [NativeTypeName("#define NUM_QUANT_TBLS 4")]
    public const int NUM_QUANT_TBLS = 4;

    [NativeTypeName("#define NUM_HUFF_TBLS 4")]
    public const int NUM_HUFF_TBLS = 4;

    [NativeTypeName("#define NUM_ARITH_TBLS 16")]
    public const int NUM_ARITH_TBLS = 16;

    [NativeTypeName("#define MAX_COMPS_IN_SCAN 4")]
    public const int MAX_COMPS_IN_SCAN = 4;

    [NativeTypeName("#define MAX_SAMP_FACTOR 4")]
    public const int MAX_SAMP_FACTOR = 4;

    [NativeTypeName("#define C_MAX_BLOCKS_IN_MCU 10")]
    public const int C_MAX_BLOCKS_IN_MCU = 10;

    [NativeTypeName("#define D_MAX_BLOCKS_IN_MCU 10")]
    public const int D_MAX_BLOCKS_IN_MCU = 10;

    [NativeTypeName("#define JCS_EXTENSIONS 1")]
    public const int JCS_EXTENSIONS = 1;

    [NativeTypeName("#define JCS_ALPHA_EXTENSIONS 1")]
    public const int JCS_ALPHA_EXTENSIONS = 1;

    [NativeTypeName("#define JDCT_DEFAULT JDCT_ISLOW")]
    public const J_DCT_METHOD JDCT_DEFAULT = JDCT_ISLOW;

    [NativeTypeName("#define JDCT_FASTEST JDCT_IFAST")]
    public const J_DCT_METHOD JDCT_FASTEST = JDCT_IFAST;

    [NativeTypeName("#define JMSG_LENGTH_MAX 200")]
    public const int JMSG_LENGTH_MAX = 200;

    [NativeTypeName("#define JMSG_STR_PARM_MAX 80")]
    public const int JMSG_STR_PARM_MAX = 80;

    [NativeTypeName("#define JPOOL_PERMANENT 0")]
    public const int JPOOL_PERMANENT = 0;

    [NativeTypeName("#define JPOOL_IMAGE 1")]
    public const int JPOOL_IMAGE = 1;

    [NativeTypeName("#define JPOOL_NUMPOOLS 2")]
    public const int JPOOL_NUMPOOLS = 2;

    [NativeTypeName("#define JPEG_SUSPENDED 0")]
    public const int JPEG_SUSPENDED = 0;

    [NativeTypeName("#define JPEG_HEADER_OK 1")]
    public const int JPEG_HEADER_OK = 1;

    [NativeTypeName("#define JPEG_HEADER_TABLES_ONLY 2")]
    public const int JPEG_HEADER_TABLES_ONLY = 2;

    [NativeTypeName("#define JPEG_REACHED_SOS 1")]
    public const int JPEG_REACHED_SOS = 1;

    [NativeTypeName("#define JPEG_REACHED_EOI 2")]
    public const int JPEG_REACHED_EOI = 2;

    [NativeTypeName("#define JPEG_ROW_COMPLETED 3")]
    public const int JPEG_ROW_COMPLETED = 3;

    [NativeTypeName("#define JPEG_SCAN_COMPLETED 4")]
    public const int JPEG_SCAN_COMPLETED = 4;

    [NativeTypeName("#define JPEG_RST0 0xD0")]
    public const int JPEG_RST0 = 0xD0;

    [NativeTypeName("#define JPEG_EOI 0xD9")]
    public const int JPEG_EOI = 0xD9;

    [NativeTypeName("#define JPEG_APP0 0xE0")]
    public const int JPEG_APP0 = 0xE0;

    [NativeTypeName("#define JPEG_COM 0xFE")]
    public const int JPEG_COM = 0xFE;
}
