// Copyright © Clinton Ingram and Contributors
// SPDX-License-Identifier: MIT

// Ported from libweb headers (mux_types.h)
// Original source Copyright 2012 Google Inc. All Rights Reserved.
// See third-party-notices in the repository root for more information.

using System.Runtime.CompilerServices;

namespace PhotoSauce.Interop.Libwebp;

internal static unsafe partial class Libwebp
{
    public static void WebPDataInit(WebPData* webp_data)
    {
        if (webp_data != null)
        {
            Unsafe.InitBlockUnaligned(webp_data, 0, (uint)(sizeof(WebPData)));
        }
    }

    public static void WebPDataClear(WebPData* webp_data)
    {
        if (webp_data != null)
        {
            WebPFree(webp_data->bytes);
            WebPDataInit(webp_data);
        }
    }

    public static int WebPDataCopy([NativeTypeName("const WebPData *")] WebPData* src, WebPData* dst)
    {
        if (src == null || dst == null)
        {
            return 0;
        }

        WebPDataInit(dst);
        if (src->bytes != null && src->size != 0)
        {
            dst->bytes = (byte*)(WebPMalloc(src->size));
            if (dst->bytes == null)
            {
                return 0;
            }

            Unsafe.CopyBlockUnaligned(dst->bytes, src->bytes, (uint)src->size);
            dst->size = src->size;
        }

        return 1;
    }
}
