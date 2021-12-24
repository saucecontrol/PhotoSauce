// Copyright © Clinton Ingram and Contributors. Licensed under the MIT License (MIT).

// Ported from libjxl headers
// Original source Copyright (c) the JPEG XL Project Authors. All rights reserved.
// See third-party-notices in the repository root for more information.

using System;
using System.Runtime.InteropServices;

using TerraFX.Interop;

namespace PhotoSauce.Interop.Libjxl
{
    internal static unsafe partial class Libjxl
    {
        [DllImport("jxl", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        [return: NativeTypeName("uint32_t")]
        public static extern uint JxlEncoderVersion();

        [DllImport("jxl", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        [return: NativeTypeName("JxlEncoder *")]
        public static extern IntPtr JxlEncoderCreate([NativeTypeName("const JxlMemoryManager *")] JxlMemoryManagerStruct* memory_manager);

        [DllImport("jxl", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern void JxlEncoderReset([NativeTypeName("JxlEncoder *")] IntPtr enc);

        [DllImport("jxl", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern void JxlEncoderDestroy([NativeTypeName("JxlEncoder *")] IntPtr enc);

        [DllImport("jxl", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern JxlEncoderStatus JxlEncoderSetParallelRunner([NativeTypeName("JxlEncoder *")] IntPtr enc, [NativeTypeName("JxlParallelRunner")] delegate* unmanaged[Cdecl]<void*, void*, delegate* unmanaged[Cdecl]<void*, nuint, int>, delegate* unmanaged[Cdecl]<void*, uint, nuint, void>, uint, uint, int> parallel_runner, void* parallel_runner_opaque);

        [DllImport("jxl", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern JxlEncoderStatus JxlEncoderProcessOutput([NativeTypeName("JxlEncoder *")] IntPtr enc, [NativeTypeName("uint8_t **")] byte** next_out, [NativeTypeName("size_t *")] nuint* avail_out);

        [DllImport("jxl", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern JxlEncoderStatus JxlEncoderAddJPEGFrame([NativeTypeName("const JxlEncoderOptions *")] IntPtr options, [NativeTypeName("const uint8_t *")] byte* buffer, [NativeTypeName("size_t")] nuint size);

        [DllImport("jxl", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern JxlEncoderStatus JxlEncoderAddImageFrame([NativeTypeName("const JxlEncoderOptions *")] IntPtr options, [NativeTypeName("const JxlPixelFormat *")] JxlPixelFormat* pixel_format, [NativeTypeName("const void *")] void* buffer, [NativeTypeName("size_t")] nuint size);

        [DllImport("jxl", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern void JxlEncoderCloseInput([NativeTypeName("JxlEncoder *")] IntPtr enc);

        [DllImport("jxl", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern JxlEncoderStatus JxlEncoderSetColorEncoding([NativeTypeName("JxlEncoder *")] IntPtr enc, [NativeTypeName("const JxlColorEncoding *")] JxlColorEncoding* color);

        [DllImport("jxl", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern JxlEncoderStatus JxlEncoderSetICCProfile([NativeTypeName("JxlEncoder *")] IntPtr enc, [NativeTypeName("const uint8_t *")] byte* icc_profile, [NativeTypeName("size_t")] nuint size);

        [DllImport("jxl", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern void JxlEncoderInitBasicInfo(JxlBasicInfo* info);

        [DllImport("jxl", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern JxlEncoderStatus JxlEncoderSetBasicInfo([NativeTypeName("JxlEncoder *")] IntPtr enc, [NativeTypeName("const JxlBasicInfo *")] JxlBasicInfo* info);

        [DllImport("jxl", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern JxlEncoderStatus JxlEncoderStoreJPEGMetadata([NativeTypeName("JxlEncoder *")] IntPtr enc, int store_jpeg_metadata);

        [DllImport("jxl", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern JxlEncoderStatus JxlEncoderUseContainer([NativeTypeName("JxlEncoder *")] IntPtr enc, int use_container);

        [DllImport("jxl", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern JxlEncoderStatus JxlEncoderOptionsSetLossless([NativeTypeName("JxlEncoderOptions *")] IntPtr options, int lossless);

        [DllImport("jxl", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern JxlEncoderStatus JxlEncoderOptionsSetDecodingSpeed([NativeTypeName("JxlEncoderOptions *")] IntPtr options, int tier);

        [DllImport("jxl", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern JxlEncoderStatus JxlEncoderOptionsSetEffort([NativeTypeName("JxlEncoderOptions *")] IntPtr options, int effort);

        [DllImport("jxl", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern JxlEncoderStatus JxlEncoderOptionsSetDistance([NativeTypeName("JxlEncoderOptions *")] IntPtr options, float distance);

        [DllImport("jxl", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        [return: NativeTypeName("JxlEncoderOptions *")]
        public static extern IntPtr JxlEncoderOptionsCreate([NativeTypeName("JxlEncoder *")] IntPtr enc, [NativeTypeName("const JxlEncoderOptions *")] IntPtr source);

        [DllImport("jxl", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern void JxlColorEncodingSetToSRGB(JxlColorEncoding* color_encoding, int is_gray);

        [DllImport("jxl", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern void JxlColorEncodingSetToLinearSRGB(JxlColorEncoding* color_encoding, int is_gray);
    }
}
