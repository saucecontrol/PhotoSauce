// Copyright © Clinton Ingram and Contributors. Licensed under the MIT License (MIT).

// Ported from libjxl headers (encode.h)
// Original source Copyright (c) the JPEG XL Project Authors. All rights reserved.
// See third-party-notices in the repository root for more information.

using System;
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
    public static extern void JxlEncoderSetCms([NativeTypeName("JxlEncoder *")] void* enc, JxlCmsInterface cms);

    [DllImport("jxl", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    public static extern JxlEncoderStatus JxlEncoderSetParallelRunner([NativeTypeName("JxlEncoder *")] void* enc, [NativeTypeName("JxlParallelRunner")] delegate* unmanaged[Cdecl]<void*, void*, delegate* unmanaged[Cdecl]<void*, nuint, int>, delegate* unmanaged[Cdecl]<void*, uint, nuint, void>, uint, uint, int> parallel_runner, void* parallel_runner_opaque);

    [DllImport("jxl", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    public static extern JxlEncoderError JxlEncoderGetError([NativeTypeName("JxlEncoder *")] void* enc);

    [DllImport("jxl", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    public static extern JxlEncoderStatus JxlEncoderProcessOutput([NativeTypeName("JxlEncoder *")] void* enc, [NativeTypeName("uint8_t **")] byte** next_out, [NativeTypeName("size_t *")] nuint* avail_out);

    [DllImport("jxl", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    public static extern JxlEncoderStatus JxlEncoderSetFrameHeader([NativeTypeName("JxlEncoderFrameSettings *")] void* frame_settings, [NativeTypeName("const JxlFrameHeader *")] JxlFrameHeader* frame_header);

    [DllImport("jxl", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    public static extern JxlEncoderStatus JxlEncoderSetExtraChannelBlendInfo([NativeTypeName("JxlEncoderFrameSettings *")] void* frame_settings, [NativeTypeName("size_t")] nuint index, [NativeTypeName("const JxlBlendInfo *")] JxlBlendInfo* blend_info);

    [DllImport("jxl", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    public static extern JxlEncoderStatus JxlEncoderSetFrameName([NativeTypeName("JxlEncoderFrameSettings *")] void* frame_settings, [NativeTypeName("const char *")] sbyte* frame_name);

    [DllImport("jxl", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    public static extern JxlEncoderStatus JxlEncoderAddJPEGFrame([NativeTypeName("const JxlEncoderFrameSettings *")] void* frame_settings, [NativeTypeName("const uint8_t *")] byte* buffer, [NativeTypeName("size_t")] nuint size);

    [DllImport("jxl", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    public static extern JxlEncoderStatus JxlEncoderAddImageFrame([NativeTypeName("const JxlEncoderFrameSettings *")] void* frame_settings, [NativeTypeName("const JxlPixelFormat *")] JxlPixelFormat* pixel_format, [NativeTypeName("const void *")] void* buffer, [NativeTypeName("size_t")] nuint size);

    [DllImport("jxl", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    public static extern JxlEncoderStatus JxlEncoderSetExtraChannelBuffer([NativeTypeName("const JxlEncoderFrameSettings *")] void* frame_settings, [NativeTypeName("const JxlPixelFormat *")] JxlPixelFormat* pixel_format, [NativeTypeName("const void *")] void* buffer, [NativeTypeName("size_t")] nuint size, [NativeTypeName("uint32_t")] uint index);

    [DllImport("jxl", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    public static extern JxlEncoderStatus JxlEncoderAddBox([NativeTypeName("JxlEncoder *")] void* enc, [NativeTypeName("const JxlBoxType")] sbyte* type, [NativeTypeName("const uint8_t *")] byte* contents, [NativeTypeName("size_t")] nuint size, int compress_box);

    [DllImport("jxl", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    public static extern JxlEncoderStatus JxlEncoderUseBoxes([NativeTypeName("JxlEncoder *")] void* enc);

    [DllImport("jxl", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    public static extern void JxlEncoderCloseBoxes([NativeTypeName("JxlEncoder *")] void* enc);

    [DllImport("jxl", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    public static extern void JxlEncoderCloseFrames([NativeTypeName("JxlEncoder *")] void* enc);

    [DllImport("jxl", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    public static extern void JxlEncoderCloseInput([NativeTypeName("JxlEncoder *")] void* enc);

    [DllImport("jxl", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    public static extern JxlEncoderStatus JxlEncoderSetColorEncoding([NativeTypeName("JxlEncoder *")] void* enc, [NativeTypeName("const JxlColorEncoding *")] JxlColorEncoding* color);

    [DllImport("jxl", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    public static extern JxlEncoderStatus JxlEncoderSetICCProfile([NativeTypeName("JxlEncoder *")] void* enc, [NativeTypeName("const uint8_t *")] byte* icc_profile, [NativeTypeName("size_t")] nuint size);

    [DllImport("jxl", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    public static extern void JxlEncoderInitBasicInfo(JxlBasicInfo* info);

    [DllImport("jxl", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    public static extern void JxlEncoderInitFrameHeader(JxlFrameHeader* frame_header);

    [DllImport("jxl", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    public static extern void JxlEncoderInitBlendInfo(JxlBlendInfo* blend_info);

    [DllImport("jxl", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    public static extern JxlEncoderStatus JxlEncoderSetBasicInfo([NativeTypeName("JxlEncoder *")] void* enc, [NativeTypeName("const JxlBasicInfo *")] JxlBasicInfo* info);

    [DllImport("jxl", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    public static extern void JxlEncoderInitExtraChannelInfo(JxlExtraChannelType type, JxlExtraChannelInfo* info);

    [DllImport("jxl", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    public static extern JxlEncoderStatus JxlEncoderSetExtraChannelInfo([NativeTypeName("JxlEncoder *")] void* enc, [NativeTypeName("size_t")] nuint index, [NativeTypeName("const JxlExtraChannelInfo *")] JxlExtraChannelInfo* info);

    [DllImport("jxl", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    public static extern JxlEncoderStatus JxlEncoderSetExtraChannelName([NativeTypeName("JxlEncoder *")] void* enc, [NativeTypeName("size_t")] nuint index, [NativeTypeName("const char *")] sbyte* name, [NativeTypeName("size_t")] nuint size);

    [DllImport("jxl", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    public static extern JxlEncoderStatus JxlEncoderFrameSettingsSetOption([NativeTypeName("JxlEncoderFrameSettings *")] void* frame_settings, JxlEncoderFrameSettingId option, [NativeTypeName("int64_t")] long value);

    [DllImport("jxl", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    public static extern JxlEncoderStatus JxlEncoderFrameSettingsSetFloatOption([NativeTypeName("JxlEncoderFrameSettings *")] void* frame_settings, JxlEncoderFrameSettingId option, float value);

    [DllImport("jxl", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    public static extern JxlEncoderStatus JxlEncoderUseContainer([NativeTypeName("JxlEncoder *")] void* enc, int use_container);

    [DllImport("jxl", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    public static extern JxlEncoderStatus JxlEncoderStoreJPEGMetadata([NativeTypeName("JxlEncoder *")] void* enc, int store_jpeg_metadata);

    [DllImport("jxl", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    public static extern JxlEncoderStatus JxlEncoderSetCodestreamLevel([NativeTypeName("JxlEncoder *")] void* enc, int level);

    [DllImport("jxl", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    public static extern int JxlEncoderGetRequiredCodestreamLevel([NativeTypeName("const JxlEncoder *")] void* enc);

    [DllImport("jxl", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    public static extern JxlEncoderStatus JxlEncoderSetFrameLossless([NativeTypeName("JxlEncoderFrameSettings *")] void* frame_settings, int lossless);

    [DllImport("jxl", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    public static extern JxlEncoderStatus JxlEncoderOptionsSetLossless([NativeTypeName("JxlEncoderFrameSettings *")] void* param0, int param1);

    [DllImport("jxl", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    [Obsolete]
    public static extern JxlEncoderStatus JxlEncoderOptionsSetEffort([NativeTypeName("JxlEncoderFrameSettings *")] void* frame_settings, int effort);

    [DllImport("jxl", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    [Obsolete]
    public static extern JxlEncoderStatus JxlEncoderOptionsSetDecodingSpeed([NativeTypeName("JxlEncoderFrameSettings *")] void* frame_settings, int tier);

    [DllImport("jxl", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    public static extern JxlEncoderStatus JxlEncoderSetFrameDistance([NativeTypeName("JxlEncoderFrameSettings *")] void* frame_settings, float distance);

    [DllImport("jxl", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    [Obsolete]
    public static extern JxlEncoderStatus JxlEncoderOptionsSetDistance([NativeTypeName("JxlEncoderFrameSettings *")] void* param0, float param1);

    [DllImport("jxl", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    [return: NativeTypeName("JxlEncoderFrameSettings *")]
    public static extern void* JxlEncoderFrameSettingsCreate([NativeTypeName("JxlEncoder *")] void* enc, [NativeTypeName("const JxlEncoderFrameSettings *")] void* source);

    [DllImport("jxl", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    [return: NativeTypeName("JxlEncoderFrameSettings *")]
    [Obsolete]
    public static extern void* JxlEncoderOptionsCreate([NativeTypeName("JxlEncoder *")] void* param0, [NativeTypeName("const JxlEncoderFrameSettings *")] void* param1);

    [DllImport("jxl", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    public static extern void JxlColorEncodingSetToSRGB(JxlColorEncoding* color_encoding, int is_gray);

    [DllImport("jxl", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    public static extern void JxlColorEncodingSetToLinearSRGB(JxlColorEncoding* color_encoding, int is_gray);
}
