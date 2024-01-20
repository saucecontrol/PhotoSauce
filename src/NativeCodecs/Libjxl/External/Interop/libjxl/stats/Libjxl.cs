// Copyright © Clinton Ingram and Contributors. Licensed under the MIT License (MIT).

// Ported from libjxl headers (stats.h)
// Original source Copyright (c) the JPEG XL Project Authors. All rights reserved.
// See third-party-notices in the repository root for more information.

using System.Runtime.InteropServices;

namespace PhotoSauce.Interop.Libjxl;

internal static unsafe partial class Libjxl
{
    [DllImport("jxl", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    [return: NativeTypeName("JxlEncoderStats *")]
    public static extern void* JxlEncoderStatsCreate();

    [DllImport("jxl", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    public static extern void JxlEncoderStatsDestroy([NativeTypeName("JxlEncoderStats *")] void* stats);

    [DllImport("jxl", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    [return: NativeTypeName("size_t")]
    public static extern nuint JxlEncoderStatsGet([NativeTypeName("const JxlEncoderStats *")] void* stats, JxlEncoderStatsKey key);

    [DllImport("jxl", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    public static extern void JxlEncoderStatsMerge([NativeTypeName("JxlEncoderStats *")] void* stats, [NativeTypeName("const JxlEncoderStats *")] void* other);
}
