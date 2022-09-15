// Copyright © Clinton Ingram and Contributors. Licensed under the MIT License (MIT).

// Ported from libjxl headers (decode.h)
// Original source Copyright (c) the JPEG XL Project Authors. All rights reserved.
// See third-party-notices in the repository root for more information.

using System;
using System.Runtime.InteropServices;

namespace PhotoSauce.Interop.Libjxl;

internal static unsafe partial class Libjxl
{
    [DllImport("jxl", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    [return: NativeTypeName("uint32_t")]
    public static extern uint JxlDecoderVersion();

    [DllImport("jxl", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    public static extern JxlSignature JxlSignatureCheck([NativeTypeName("const uint8_t *")] byte* buf, [NativeTypeName("size_t")] nuint len);

    [DllImport("jxl", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    [return: NativeTypeName("JxlDecoder *")]
    public static extern void* JxlDecoderCreate([NativeTypeName("const JxlMemoryManager *")] JxlMemoryManagerStruct* memory_manager);

    [DllImport("jxl", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    public static extern void JxlDecoderReset([NativeTypeName("JxlDecoder *")] void* dec);

    [DllImport("jxl", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    public static extern void JxlDecoderDestroy([NativeTypeName("JxlDecoder *")] void* dec);

    [DllImport("jxl", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    public static extern void JxlDecoderRewind([NativeTypeName("JxlDecoder *")] void* dec);

    [DllImport("jxl", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    public static extern void JxlDecoderSkipFrames([NativeTypeName("JxlDecoder *")] void* dec, [NativeTypeName("size_t")] nuint amount);

    [DllImport("jxl", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    public static extern JxlDecoderStatus JxlDecoderDefaultPixelFormat([NativeTypeName("const JxlDecoder *")] void* dec, JxlPixelFormat* format);

    [DllImport("jxl", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    public static extern JxlDecoderStatus JxlDecoderSetParallelRunner([NativeTypeName("JxlDecoder *")] void* dec, [NativeTypeName("JxlParallelRunner")] delegate* unmanaged[Cdecl]<void*, void*, delegate* unmanaged[Cdecl]<void*, nuint, int>, delegate* unmanaged[Cdecl]<void*, uint, nuint, void>, uint, uint, int> parallel_runner, void* parallel_runner_opaque);

    [DllImport("jxl", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    [return: NativeTypeName("size_t")]
    public static extern nuint JxlDecoderSizeHintBasicInfo([NativeTypeName("const JxlDecoder *")] void* dec);

    [DllImport("jxl", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    public static extern JxlDecoderStatus JxlDecoderSubscribeEvents([NativeTypeName("JxlDecoder *")] void* dec, int events_wanted);

    [DllImport("jxl", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    public static extern JxlDecoderStatus JxlDecoderSetKeepOrientation([NativeTypeName("JxlDecoder *")] void* dec, int keep_orientation);

    [DllImport("jxl", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    public static extern JxlDecoderStatus JxlDecoderProcessInput([NativeTypeName("JxlDecoder *")] void* dec);

    [DllImport("jxl", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    public static extern JxlDecoderStatus JxlDecoderSetInput([NativeTypeName("JxlDecoder *")] void* dec, [NativeTypeName("const uint8_t *")] byte* data, [NativeTypeName("size_t")] nuint size);

    [DllImport("jxl", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    [return: NativeTypeName("size_t")]
    public static extern nuint JxlDecoderReleaseInput([NativeTypeName("JxlDecoder *")] void* dec);

    [DllImport("jxl", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    public static extern JxlDecoderStatus JxlDecoderGetBasicInfo([NativeTypeName("const JxlDecoder *")] void* dec, JxlBasicInfo* info);

    [DllImport("jxl", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    public static extern JxlDecoderStatus JxlDecoderGetExtraChannelInfo([NativeTypeName("const JxlDecoder *")] void* dec, [NativeTypeName("size_t")] nuint index, JxlExtraChannelInfo* info);

    [DllImport("jxl", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    public static extern JxlDecoderStatus JxlDecoderGetExtraChannelName([NativeTypeName("const JxlDecoder *")] void* dec, [NativeTypeName("size_t")] nuint index, [NativeTypeName("char *")] sbyte* name, [NativeTypeName("size_t")] nuint size);

    [DllImport("jxl", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    public static extern JxlDecoderStatus JxlDecoderGetColorAsEncodedProfile([NativeTypeName("const JxlDecoder *")] void* dec, [NativeTypeName("const JxlPixelFormat *")] JxlPixelFormat* format, JxlColorProfileTarget target, JxlColorEncoding* color_encoding);

    [DllImport("jxl", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    public static extern JxlDecoderStatus JxlDecoderGetICCProfileSize([NativeTypeName("const JxlDecoder *")] void* dec, [NativeTypeName("const JxlPixelFormat *")] JxlPixelFormat* format, JxlColorProfileTarget target, [NativeTypeName("size_t *")] nuint* size);

    [DllImport("jxl", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    public static extern JxlDecoderStatus JxlDecoderGetColorAsICCProfile([NativeTypeName("const JxlDecoder *")] void* dec, [NativeTypeName("const JxlPixelFormat *")] JxlPixelFormat* format, JxlColorProfileTarget target, [NativeTypeName("uint8_t *")] byte* icc_profile, [NativeTypeName("size_t")] nuint size);

    [DllImport("jxl", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    public static extern JxlDecoderStatus JxlDecoderSetPreferredColorProfile([NativeTypeName("JxlDecoder *")] void* dec, [NativeTypeName("const JxlColorEncoding *")] JxlColorEncoding* color_encoding);

    [DllImport("jxl", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    public static extern JxlDecoderStatus JxlDecoderPreviewOutBufferSize([NativeTypeName("const JxlDecoder *")] void* dec, [NativeTypeName("const JxlPixelFormat *")] JxlPixelFormat* format, [NativeTypeName("size_t *")] nuint* size);

    [DllImport("jxl", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    public static extern JxlDecoderStatus JxlDecoderSetPreviewOutBuffer([NativeTypeName("JxlDecoder *")] void* dec, [NativeTypeName("const JxlPixelFormat *")] JxlPixelFormat* format, void* buffer, [NativeTypeName("size_t")] nuint size);

    [DllImport("jxl", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    public static extern JxlDecoderStatus JxlDecoderGetFrameHeader([NativeTypeName("const JxlDecoder *")] void* dec, JxlFrameHeader* header);

    [DllImport("jxl", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    public static extern JxlDecoderStatus JxlDecoderGetFrameName([NativeTypeName("const JxlDecoder *")] void* dec, [NativeTypeName("char *")] sbyte* name, [NativeTypeName("size_t")] nuint size);

    [DllImport("jxl", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    [Obsolete]
    public static extern JxlDecoderStatus JxlDecoderDCOutBufferSize([NativeTypeName("const JxlDecoder *")] void* dec, [NativeTypeName("const JxlPixelFormat *")] JxlPixelFormat* format, [NativeTypeName("size_t *")] nuint* size);

    [DllImport("jxl", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    [Obsolete]
    public static extern JxlDecoderStatus JxlDecoderSetDCOutBuffer([NativeTypeName("JxlDecoder *")] void* dec, [NativeTypeName("const JxlPixelFormat *")] JxlPixelFormat* format, void* buffer, [NativeTypeName("size_t")] nuint size);

    [DllImport("jxl", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    public static extern JxlDecoderStatus JxlDecoderImageOutBufferSize([NativeTypeName("const JxlDecoder *")] void* dec, [NativeTypeName("const JxlPixelFormat *")] JxlPixelFormat* format, [NativeTypeName("size_t *")] nuint* size);

    [DllImport("jxl", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    public static extern JxlDecoderStatus JxlDecoderSetImageOutBuffer([NativeTypeName("JxlDecoder *")] void* dec, [NativeTypeName("const JxlPixelFormat *")] JxlPixelFormat* format, void* buffer, [NativeTypeName("size_t")] nuint size);

    [DllImport("jxl", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    public static extern JxlDecoderStatus JxlDecoderSetImageOutCallback([NativeTypeName("JxlDecoder *")] void* dec, [NativeTypeName("const JxlPixelFormat *")] JxlPixelFormat* format, [NativeTypeName("JxlImageOutCallback")] delegate* unmanaged[Cdecl]<void*, nuint, nuint, nuint, void*, void> callback, void* opaque);

    [DllImport("jxl", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    public static extern JxlDecoderStatus JxlDecoderExtraChannelBufferSize([NativeTypeName("const JxlDecoder *")] void* dec, [NativeTypeName("const JxlPixelFormat *")] JxlPixelFormat* format, [NativeTypeName("size_t *")] nuint* size, [NativeTypeName("uint32_t")] uint index);

    [DllImport("jxl", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    public static extern JxlDecoderStatus JxlDecoderSetExtraChannelBuffer([NativeTypeName("JxlDecoder *")] void* dec, [NativeTypeName("const JxlPixelFormat *")] JxlPixelFormat* format, void* buffer, [NativeTypeName("size_t")] nuint size, [NativeTypeName("uint32_t")] uint index);

    [DllImport("jxl", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    public static extern JxlDecoderStatus JxlDecoderSetJPEGBuffer([NativeTypeName("JxlDecoder *")] void* dec, [NativeTypeName("uint8_t *")] byte* data, [NativeTypeName("size_t")] nuint size);

    [DllImport("jxl", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    [return: NativeTypeName("size_t")]
    public static extern nuint JxlDecoderReleaseJPEGBuffer([NativeTypeName("JxlDecoder *")] void* dec);

    [DllImport("jxl", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    public static extern JxlDecoderStatus JxlDecoderFlushImage([NativeTypeName("JxlDecoder *")] void* dec);
}
