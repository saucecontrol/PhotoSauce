// Copyright © Clinton Ingram and Contributors. Licensed under the MIT License (MIT).

// Ported from libjxl headers (encode.h)
// Original source Copyright (c) the JPEG XL Project Authors. All rights reserved.
// See third-party-notices in the repository root for more information.

using System.Runtime.InteropServices;

namespace PhotoSauce.Interop.Libjxl;

internal static unsafe partial class Libjxl
{
    [DllImport("jxl", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    [return: NativeTypeName("uint32_t")]
    public static extern uint JxlEncoderVersion();

    [DllImport("jxl", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    [return: NativeTypeName("JxlEncoder *")]
    public static extern void* JxlEncoderCreate([NativeTypeName("const JxlMemoryManager *")] JxlMemoryManagerStruct* memory_manager);

    [DllImport("jxl", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    public static extern void JxlEncoderReset([NativeTypeName("JxlEncoder *")] void* enc);

    [DllImport("jxl", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    public static extern void JxlEncoderDestroy([NativeTypeName("JxlEncoder *")] void* enc);

    [DllImport("jxl", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    public static extern JxlEncoderStatus JxlEncoderSetParallelRunner([NativeTypeName("JxlEncoder *")] void* enc, [NativeTypeName("JxlParallelRunner")] delegate* unmanaged[Cdecl]<void*, void*, delegate* unmanaged[Cdecl]<void*, nuint, int>, delegate* unmanaged[Cdecl]<void*, uint, nuint, void>, uint, uint, int> parallel_runner, void* parallel_runner_opaque);

    [DllImport("jxl", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    public static extern JxlEncoderStatus JxlEncoderProcessOutput([NativeTypeName("JxlEncoder *")] void* enc, [NativeTypeName("uint8_t **")] byte** next_out, [NativeTypeName("size_t *")] nuint* avail_out);

    [DllImport("jxl", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    public static extern JxlEncoderStatus JxlEncoderAddJPEGFrame([NativeTypeName("const JxlEncoderOptions *")] void* options, [NativeTypeName("const uint8_t *")] byte* buffer, [NativeTypeName("size_t")] nuint size);

    [DllImport("jxl", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    public static extern JxlEncoderStatus JxlEncoderAddImageFrame([NativeTypeName("const JxlEncoderOptions *")] void* options, [NativeTypeName("const JxlPixelFormat *")] JxlPixelFormat* pixel_format, [NativeTypeName("const void *")] void* buffer, [NativeTypeName("size_t")] nuint size);

    [DllImport("jxl", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    public static extern void JxlEncoderCloseInput([NativeTypeName("JxlEncoder *")] void* enc);

    [DllImport("jxl", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    public static extern JxlEncoderStatus JxlEncoderSetColorEncoding([NativeTypeName("JxlEncoder *")] void* enc, [NativeTypeName("const JxlColorEncoding *")] JxlColorEncoding* color);

    [DllImport("jxl", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    public static extern JxlEncoderStatus JxlEncoderSetICCProfile([NativeTypeName("JxlEncoder *")] void* enc, [NativeTypeName("const uint8_t *")] byte* icc_profile, [NativeTypeName("size_t")] nuint size);

    [DllImport("jxl", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    public static extern void JxlEncoderInitBasicInfo(JxlBasicInfo* info);

    [DllImport("jxl", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    public static extern JxlEncoderStatus JxlEncoderSetBasicInfo([NativeTypeName("JxlEncoder *")] void* enc, [NativeTypeName("const JxlBasicInfo *")] JxlBasicInfo* info);

    [DllImport("jxl", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    public static extern JxlEncoderStatus JxlEncoderStoreJPEGMetadata([NativeTypeName("JxlEncoder *")] void* enc, int store_jpeg_metadata);

    [DllImport("jxl", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    public static extern JxlEncoderStatus JxlEncoderUseContainer([NativeTypeName("JxlEncoder *")] void* enc, int use_container);

    [DllImport("jxl", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    public static extern JxlEncoderStatus JxlEncoderOptionsSetLossless([NativeTypeName("JxlEncoderOptions *")] void* options, int lossless);

    [DllImport("jxl", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    public static extern JxlEncoderStatus JxlEncoderOptionsSetDecodingSpeed([NativeTypeName("JxlEncoderOptions *")] void* options, int tier);

    [DllImport("jxl", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    public static extern JxlEncoderStatus JxlEncoderOptionsSetEffort([NativeTypeName("JxlEncoderOptions *")] void* options, int effort);

    [DllImport("jxl", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    public static extern JxlEncoderStatus JxlEncoderOptionsSetDistance([NativeTypeName("JxlEncoderOptions *")] void* options, float distance);

    [DllImport("jxl", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    [return: NativeTypeName("JxlEncoderOptions *")]
    public static extern void* JxlEncoderOptionsCreate([NativeTypeName("JxlEncoder *")] void* enc, [NativeTypeName("const JxlEncoderOptions *")] void* source);

    [DllImport("jxl", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    public static extern void JxlColorEncodingSetToSRGB(JxlColorEncoding* color_encoding, int is_gray);

    [DllImport("jxl", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    public static extern void JxlColorEncodingSetToLinearSRGB(JxlColorEncoding* color_encoding, int is_gray);
}
