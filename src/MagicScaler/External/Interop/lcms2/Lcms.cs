// Copyright © Clinton Ingram and Contributors. Licensed under the MIT License.

// Ported from LittleCMS headers (lcms2.h)
// Original source Copyright (c) 1998-2022 Marti Maria Saguer
// See third-party-notices in the repository root for more information.

using System.Runtime.InteropServices;

namespace PhotoSauce.Interop.Lcms;

internal static unsafe partial class Lcms
{
    [DllImport("lcms2", ExactSpelling = true)]
    public static extern int cmsGetEncodedCMMversion();

    [DllImport("lcms2", ExactSpelling = true)]
    [return: NativeTypeName("cmsHPROFILE")]
    public static extern void* cmsOpenProfileFromMem([NativeTypeName("const void *")] void* MemPtr, [NativeTypeName("cmsUInt32Number")] uint dwSize);

    [DllImport("lcms2", ExactSpelling = true)]
    [return: NativeTypeName("cmsBool")]
    public static extern int cmsCloseProfile([NativeTypeName("cmsHPROFILE")] void* hProfile);

    [DllImport("lcms2", ExactSpelling = true)]
    [return: NativeTypeName("cmsHTRANSFORM")]
    public static extern void* cmsCreateTransform([NativeTypeName("cmsHPROFILE")] void* Input, [NativeTypeName("cmsUInt32Number")] uint InputFormat, [NativeTypeName("cmsHPROFILE")] void* Output, [NativeTypeName("cmsUInt32Number")] uint OutputFormat, [NativeTypeName("cmsUInt32Number")] uint Intent, [NativeTypeName("cmsUInt32Number")] uint dwFlags);

    [DllImport("lcms2", ExactSpelling = true)]
    public static extern void cmsDeleteTransform([NativeTypeName("cmsHTRANSFORM")] void* hTransform);

    [DllImport("lcms2", ExactSpelling = true)]
    public static extern void cmsDoTransform([NativeTypeName("cmsHTRANSFORM")] void* Transform, [NativeTypeName("const void *")] void* InputBuffer, void* OutputBuffer, [NativeTypeName("cmsUInt32Number")] uint Size);

    [NativeTypeName("#define LCMS_VERSION 2140")]
    public const int LCMS_VERSION = 2140;

    [NativeTypeName("#define TYPE_BGR_8 (COLORSPACE_SH(PT_RGB)|CHANNELS_SH(3)|BYTES_SH(1)|DOSWAP_SH(1))")]
    public const int TYPE_BGR_8 = (((4) << 16) | ((3) << 3) | (1) | ((1) << 10));

    [NativeTypeName("#define TYPE_BGRA_8 (COLORSPACE_SH(PT_RGB)|EXTRA_SH(1)|CHANNELS_SH(3)|BYTES_SH(1)|DOSWAP_SH(1)|SWAPFIRST_SH(1))")]
    public const int TYPE_BGRA_8 = (((4) << 16) | ((1) << 7) | ((3) << 3) | (1) | ((1) << 10) | ((1) << 14));

    [NativeTypeName("#define TYPE_BGRA_8_PREMUL (COLORSPACE_SH(PT_RGB)|EXTRA_SH(1)|CHANNELS_SH(3)|BYTES_SH(1)|DOSWAP_SH(1)|SWAPFIRST_SH(1)|PREMUL_SH(1))")]
    public const int TYPE_BGRA_8_PREMUL = (((4) << 16) | ((1) << 7) | ((3) << 3) | (1) | ((1) << 10) | ((1) << 14) | ((1) << 23));

    [NativeTypeName("#define TYPE_CMYK_8 (COLORSPACE_SH(PT_CMYK)|CHANNELS_SH(4)|BYTES_SH(1))")]
    public const int TYPE_CMYK_8 = (((6) << 16) | ((4) << 3) | (1));

    [NativeTypeName("#define TYPE_CMYKA_8 (COLORSPACE_SH(PT_CMYK)|EXTRA_SH(1)|CHANNELS_SH(4)|BYTES_SH(1))")]
    public const int TYPE_CMYKA_8 = (((6) << 16) | ((1) << 7) | ((4) << 3) | (1));

    [NativeTypeName("#define TYPE_CMYK_16 (COLORSPACE_SH(PT_CMYK)|CHANNELS_SH(4)|BYTES_SH(2))")]
    public const int TYPE_CMYK_16 = (((6) << 16) | ((4) << 3) | (2));

    [NativeTypeName("#define INTENT_PERCEPTUAL 0")]
    public const int INTENT_PERCEPTUAL = 0;

    [NativeTypeName("#define cmsFLAGS_COPY_ALPHA 0x04000000")]
    public const int cmsFLAGS_COPY_ALPHA = 0x04000000;
}
