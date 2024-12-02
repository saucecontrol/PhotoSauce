// Copyright © Clinton Ingram and Contributors
// SPDX-License-Identifier: MIT

// Ported from libweb headers (types.h)
// Original source Copyright 2012 Google Inc. All Rights Reserved.
// See third-party-notices in the repository root for more information.

using System.Runtime.InteropServices;

namespace PhotoSauce.Interop.Libwebp;

internal static unsafe partial class Libwebp
{
    [DllImport("webp", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    public static extern void* WebPMalloc([NativeTypeName("size_t")] nuint size);

    [DllImport("webp", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    public static extern void WebPFree(void* ptr);
}
