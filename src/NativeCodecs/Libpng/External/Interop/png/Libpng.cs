// Copyright © Clinton Ingram and Contributors. Licensed under the MIT License (MIT).

// Ported from libpng headers (png.h)
// Original source Copyright (c) 1995-2022 The PNG Reference Library Authors.
// See third-party-notices in the repository root for more information.

using System;

namespace PhotoSauce.Interop.Libpng;

internal static unsafe partial class Libpng
{
    [NativeTypeName("#define PNG_LIBPNG_VER_STRING \"1.6.39\"")]
    public static ReadOnlySpan<byte> PNG_LIBPNG_VER_STRING => "1.6.39"u8;

    [NativeTypeName("#define PNG_LIBPNG_VER_SONUM 16")]
    public const int PNG_LIBPNG_VER_SONUM = 16;

    [NativeTypeName("#define PNG_LIBPNG_VER_DLLNUM 16")]
    public const int PNG_LIBPNG_VER_DLLNUM = 16;

    [NativeTypeName("#define PNG_LIBPNG_VER_MAJOR 1")]
    public const int PNG_LIBPNG_VER_MAJOR = 1;

    [NativeTypeName("#define PNG_LIBPNG_VER_MINOR 6")]
    public const int PNG_LIBPNG_VER_MINOR = 6;

    [NativeTypeName("#define PNG_LIBPNG_VER_RELEASE 39")]
    public const int PNG_LIBPNG_VER_RELEASE = 39;

    [NativeTypeName("#define PNG_LIBPNG_VER_BUILD 0")]
    public const int PNG_LIBPNG_VER_BUILD = 0;

    [NativeTypeName("#define PNG_LIBPNG_BUILD_ALPHA 1")]
    public const int PNG_LIBPNG_BUILD_ALPHA = 1;

    [NativeTypeName("#define PNG_LIBPNG_BUILD_BETA 2")]
    public const int PNG_LIBPNG_BUILD_BETA = 2;

    [NativeTypeName("#define PNG_LIBPNG_BUILD_RC 3")]
    public const int PNG_LIBPNG_BUILD_RC = 3;

    [NativeTypeName("#define PNG_LIBPNG_BUILD_STABLE 4")]
    public const int PNG_LIBPNG_BUILD_STABLE = 4;

    [NativeTypeName("#define PNG_LIBPNG_BUILD_RELEASE_STATUS_MASK 7")]
    public const int PNG_LIBPNG_BUILD_RELEASE_STATUS_MASK = 7;

    [NativeTypeName("#define PNG_LIBPNG_BUILD_PATCH 8")]
    public const int PNG_LIBPNG_BUILD_PATCH = 8;

    [NativeTypeName("#define PNG_LIBPNG_BUILD_PRIVATE 16")]
    public const int PNG_LIBPNG_BUILD_PRIVATE = 16;

    [NativeTypeName("#define PNG_LIBPNG_BUILD_SPECIAL 32")]
    public const int PNG_LIBPNG_BUILD_SPECIAL = 32;

    [NativeTypeName("#define PNG_LIBPNG_BUILD_BASE_TYPE PNG_LIBPNG_BUILD_STABLE")]
    public const int PNG_LIBPNG_BUILD_BASE_TYPE = 4;

    [NativeTypeName("#define PNG_LIBPNG_VER 10639")]
    public const int PNG_LIBPNG_VER = 10639;

    [NativeTypeName("#define PNG_LIBPNG_BUILD_TYPE (PNG_LIBPNG_BUILD_BASE_TYPE | PNG_LIBPNG_BUILD_PRIVATE)")]
    public const int PNG_LIBPNG_BUILD_TYPE = (4 | 16);

    [NativeTypeName("#define PNG_DISPOSE_OP_NONE 0x00U")]
    public const uint PNG_DISPOSE_OP_NONE = 0x00U;

    [NativeTypeName("#define PNG_DISPOSE_OP_BACKGROUND 0x01U")]
    public const uint PNG_DISPOSE_OP_BACKGROUND = 0x01U;

    [NativeTypeName("#define PNG_DISPOSE_OP_PREVIOUS 0x02U")]
    public const uint PNG_DISPOSE_OP_PREVIOUS = 0x02U;

    [NativeTypeName("#define PNG_BLEND_OP_SOURCE 0x00U")]
    public const uint PNG_BLEND_OP_SOURCE = 0x00U;

    [NativeTypeName("#define PNG_BLEND_OP_OVER 0x01U")]
    public const uint PNG_BLEND_OP_OVER = 0x01U;

    [NativeTypeName("#define PNG_UINT_31_MAX ((png_uint_32)0x7fffffffL)")]
    public const uint PNG_UINT_31_MAX = ((uint)(0x7fffffff));

    [NativeTypeName("#define PNG_UINT_32_MAX ((png_uint_32)(-1))")]
    public const uint PNG_UINT_32_MAX = unchecked((uint)(-1));

    [NativeTypeName("#define PNG_SIZE_MAX ((size_t)(-1))")]
    public static nuint PNG_SIZE_MAX => unchecked((nuint)(-1));

    [NativeTypeName("#define PNG_FP_1 100000")]
    public const int PNG_FP_1 = 100000;

    [NativeTypeName("#define PNG_FP_HALF 50000")]
    public const int PNG_FP_HALF = 50000;

    [NativeTypeName("#define PNG_FP_MAX ((png_fixed_point)0x7fffffffL)")]
    public const int PNG_FP_MAX = ((int)(0x7fffffff));

    [NativeTypeName("#define PNG_FP_MIN (-PNG_FP_MAX)")]
    public const int PNG_FP_MIN = (-((int)(0x7fffffff)));

    [NativeTypeName("#define PNG_COLOR_MASK_PALETTE 1")]
    public const int PNG_COLOR_MASK_PALETTE = 1;

    [NativeTypeName("#define PNG_COLOR_MASK_COLOR 2")]
    public const int PNG_COLOR_MASK_COLOR = 2;

    [NativeTypeName("#define PNG_COLOR_MASK_ALPHA 4")]
    public const int PNG_COLOR_MASK_ALPHA = 4;

    [NativeTypeName("#define PNG_COLOR_TYPE_GRAY 0")]
    public const int PNG_COLOR_TYPE_GRAY = 0;

    [NativeTypeName("#define PNG_COLOR_TYPE_PALETTE (PNG_COLOR_MASK_COLOR | PNG_COLOR_MASK_PALETTE)")]
    public const int PNG_COLOR_TYPE_PALETTE = (2 | 1);

    [NativeTypeName("#define PNG_COLOR_TYPE_RGB (PNG_COLOR_MASK_COLOR)")]
    public const int PNG_COLOR_TYPE_RGB = (2);

    [NativeTypeName("#define PNG_COLOR_TYPE_RGB_ALPHA (PNG_COLOR_MASK_COLOR | PNG_COLOR_MASK_ALPHA)")]
    public const int PNG_COLOR_TYPE_RGB_ALPHA = (2 | 4);

    [NativeTypeName("#define PNG_COLOR_TYPE_GRAY_ALPHA (PNG_COLOR_MASK_ALPHA)")]
    public const int PNG_COLOR_TYPE_GRAY_ALPHA = (4);

    [NativeTypeName("#define PNG_COLOR_TYPE_RGBA PNG_COLOR_TYPE_RGB_ALPHA")]
    public const int PNG_COLOR_TYPE_RGBA = (2 | 4);

    [NativeTypeName("#define PNG_COLOR_TYPE_GA PNG_COLOR_TYPE_GRAY_ALPHA")]
    public const int PNG_COLOR_TYPE_GA = (4);

    [NativeTypeName("#define PNG_COMPRESSION_TYPE_BASE 0")]
    public const int PNG_COMPRESSION_TYPE_BASE = 0;

    [NativeTypeName("#define PNG_COMPRESSION_TYPE_DEFAULT PNG_COMPRESSION_TYPE_BASE")]
    public const int PNG_COMPRESSION_TYPE_DEFAULT = 0;

    [NativeTypeName("#define PNG_FILTER_TYPE_BASE 0")]
    public const int PNG_FILTER_TYPE_BASE = 0;

    [NativeTypeName("#define PNG_INTRAPIXEL_DIFFERENCING 64")]
    public const int PNG_INTRAPIXEL_DIFFERENCING = 64;

    [NativeTypeName("#define PNG_FILTER_TYPE_DEFAULT PNG_FILTER_TYPE_BASE")]
    public const int PNG_FILTER_TYPE_DEFAULT = 0;

    [NativeTypeName("#define PNG_INTERLACE_NONE 0")]
    public const int PNG_INTERLACE_NONE = 0;

    [NativeTypeName("#define PNG_INTERLACE_ADAM7 1")]
    public const int PNG_INTERLACE_ADAM7 = 1;

    [NativeTypeName("#define PNG_INTERLACE_LAST 2")]
    public const int PNG_INTERLACE_LAST = 2;

    [NativeTypeName("#define PNG_RESOLUTION_UNKNOWN 0")]
    public const int PNG_RESOLUTION_UNKNOWN = 0;

    [NativeTypeName("#define PNG_RESOLUTION_METER 1")]
    public const int PNG_RESOLUTION_METER = 1;

    [NativeTypeName("#define PNG_KEYWORD_MAX_LENGTH 79")]
    public const int PNG_KEYWORD_MAX_LENGTH = 79;

    [NativeTypeName("#define PNG_MAX_PALETTE_LENGTH 256")]
    public const int PNG_MAX_PALETTE_LENGTH = 256;

    [NativeTypeName("#define PNG_INFO_gAMA 0x0001U")]
    public const uint PNG_INFO_gAMA = 0x0001U;

    [NativeTypeName("#define PNG_INFO_sBIT 0x0002U")]
    public const uint PNG_INFO_sBIT = 0x0002U;

    [NativeTypeName("#define PNG_INFO_cHRM 0x0004U")]
    public const uint PNG_INFO_cHRM = 0x0004U;

    [NativeTypeName("#define PNG_INFO_PLTE 0x0008U")]
    public const uint PNG_INFO_PLTE = 0x0008U;

    [NativeTypeName("#define PNG_INFO_tRNS 0x0010U")]
    public const uint PNG_INFO_tRNS = 0x0010U;

    [NativeTypeName("#define PNG_INFO_bKGD 0x0020U")]
    public const uint PNG_INFO_bKGD = 0x0020U;

    [NativeTypeName("#define PNG_INFO_hIST 0x0040U")]
    public const uint PNG_INFO_hIST = 0x0040U;

    [NativeTypeName("#define PNG_INFO_pHYs 0x0080U")]
    public const uint PNG_INFO_pHYs = 0x0080U;

    [NativeTypeName("#define PNG_INFO_oFFs 0x0100U")]
    public const uint PNG_INFO_oFFs = 0x0100U;

    [NativeTypeName("#define PNG_INFO_tIME 0x0200U")]
    public const uint PNG_INFO_tIME = 0x0200U;

    [NativeTypeName("#define PNG_INFO_pCAL 0x0400U")]
    public const uint PNG_INFO_pCAL = 0x0400U;

    [NativeTypeName("#define PNG_INFO_sRGB 0x0800U")]
    public const uint PNG_INFO_sRGB = 0x0800U;

    [NativeTypeName("#define PNG_INFO_iCCP 0x1000U")]
    public const uint PNG_INFO_iCCP = 0x1000U;

    [NativeTypeName("#define PNG_INFO_sPLT 0x2000U")]
    public const uint PNG_INFO_sPLT = 0x2000U;

    [NativeTypeName("#define PNG_INFO_sCAL 0x4000U")]
    public const uint PNG_INFO_sCAL = 0x4000U;

    [NativeTypeName("#define PNG_INFO_IDAT 0x8000U")]
    public const uint PNG_INFO_IDAT = 0x8000U;

    [NativeTypeName("#define PNG_INFO_eXIf 0x10000U")]
    public const uint PNG_INFO_eXIf = 0x10000U;

    [NativeTypeName("#define PNG_INFO_acTL 0x20000U")]
    public const uint PNG_INFO_acTL = 0x20000U;

    [NativeTypeName("#define PNG_INFO_fcTL 0x40000U")]
    public const uint PNG_INFO_fcTL = 0x40000U;

    [NativeTypeName("#define PNG_NO_FILTERS 0x00")]
    public const int PNG_NO_FILTERS = 0x00;

    [NativeTypeName("#define PNG_FILTER_NONE 0x08")]
    public const int PNG_FILTER_NONE = 0x08;

    [NativeTypeName("#define PNG_FILTER_SUB 0x10")]
    public const int PNG_FILTER_SUB = 0x10;

    [NativeTypeName("#define PNG_FILTER_UP 0x20")]
    public const int PNG_FILTER_UP = 0x20;

    [NativeTypeName("#define PNG_FILTER_AVG 0x40")]
    public const int PNG_FILTER_AVG = 0x40;

    [NativeTypeName("#define PNG_FILTER_PAETH 0x80")]
    public const int PNG_FILTER_PAETH = 0x80;

    [NativeTypeName("#define PNG_FAST_FILTERS (PNG_FILTER_NONE | PNG_FILTER_SUB | PNG_FILTER_UP)")]
    public const int PNG_FAST_FILTERS = (0x08 | 0x10 | 0x20);

    [NativeTypeName("#define PNG_ALL_FILTERS (PNG_FAST_FILTERS | PNG_FILTER_AVG | PNG_FILTER_PAETH)")]
    public const int PNG_ALL_FILTERS = ((0x08 | 0x10 | 0x20) | 0x40 | 0x80);

    [NativeTypeName("#define PNG_FILTER_VALUE_NONE 0")]
    public const int PNG_FILTER_VALUE_NONE = 0;

    [NativeTypeName("#define PNG_FILTER_VALUE_SUB 1")]
    public const int PNG_FILTER_VALUE_SUB = 1;

    [NativeTypeName("#define PNG_FILTER_VALUE_UP 2")]
    public const int PNG_FILTER_VALUE_UP = 2;

    [NativeTypeName("#define PNG_FILTER_VALUE_AVG 3")]
    public const int PNG_FILTER_VALUE_AVG = 3;

    [NativeTypeName("#define PNG_FILTER_VALUE_PAETH 4")]
    public const int PNG_FILTER_VALUE_PAETH = 4;

    [NativeTypeName("#define PNG_FILTER_VALUE_LAST 5")]
    public const int PNG_FILTER_VALUE_LAST = 5;

    [NativeTypeName("#define PNG_FILTER_HEURISTIC_DEFAULT 0")]
    public const int PNG_FILTER_HEURISTIC_DEFAULT = 0;

    [NativeTypeName("#define PNG_FILTER_HEURISTIC_UNWEIGHTED 1")]
    public const int PNG_FILTER_HEURISTIC_UNWEIGHTED = 1;

    [NativeTypeName("#define PNG_FILTER_HEURISTIC_WEIGHTED 2")]
    public const int PNG_FILTER_HEURISTIC_WEIGHTED = 2;

    [NativeTypeName("#define PNG_FILTER_HEURISTIC_LAST 3")]
    public const int PNG_FILTER_HEURISTIC_LAST = 3;

    [NativeTypeName("#define PNG_INTERLACE_ADAM7_PASSES 7")]
    public const int PNG_INTERLACE_ADAM7_PASSES = 7;

    [NativeTypeName("#define PNG_MAXIMUM_INFLATE_WINDOW 2")]
    public const int PNG_MAXIMUM_INFLATE_WINDOW = 2;

    [NativeTypeName("#define PNG_USER_HEIGHT_MAX 1000000")]
    public const int PNG_USER_HEIGHT_MAX = 1000000;

    [NativeTypeName("#define PNG_USER_WIDTH_MAX 1000000")]
    public const int PNG_USER_WIDTH_MAX = 1000000;
}
