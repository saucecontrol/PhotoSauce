// Copyright © Clinton Ingram and Contributors. Licensed under the MIT License (MIT).

// Ported from libweb headers (decode.h)
// Original source Copyright 2012 Google Inc. All Rights Reserved.
// See third-party-notices in the repository root for more information.

using System;
using System.Runtime.InteropServices;
using static PhotoSauce.Interop.Libwebp.WEBP_CSP_MODE;

namespace PhotoSauce.Interop.Libwebp;

internal static unsafe partial class Libwebp
{
    [DllImport("webp", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    public static extern int WebPGetDecoderVersion();

    [DllImport("webp", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    public static extern int WebPGetInfo([NativeTypeName("const uint8_t *")] byte* data, [NativeTypeName("size_t")] nuint data_size, int* width, int* height);

    [DllImport("webp", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    [return: NativeTypeName("uint8_t *")]
    public static extern byte* WebPDecodeRGBA([NativeTypeName("const uint8_t *")] byte* data, [NativeTypeName("size_t")] nuint data_size, int* width, int* height);

    [DllImport("webp", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    [return: NativeTypeName("uint8_t *")]
    public static extern byte* WebPDecodeARGB([NativeTypeName("const uint8_t *")] byte* data, [NativeTypeName("size_t")] nuint data_size, int* width, int* height);

    [DllImport("webp", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    [return: NativeTypeName("uint8_t *")]
    public static extern byte* WebPDecodeBGRA([NativeTypeName("const uint8_t *")] byte* data, [NativeTypeName("size_t")] nuint data_size, int* width, int* height);

    [DllImport("webp", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    [return: NativeTypeName("uint8_t *")]
    public static extern byte* WebPDecodeRGB([NativeTypeName("const uint8_t *")] byte* data, [NativeTypeName("size_t")] nuint data_size, int* width, int* height);

    [DllImport("webp", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    [return: NativeTypeName("uint8_t *")]
    public static extern byte* WebPDecodeBGR([NativeTypeName("const uint8_t *")] byte* data, [NativeTypeName("size_t")] nuint data_size, int* width, int* height);

    [DllImport("webp", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    [return: NativeTypeName("uint8_t *")]
    public static extern byte* WebPDecodeYUV([NativeTypeName("const uint8_t *")] byte* data, [NativeTypeName("size_t")] nuint data_size, int* width, int* height, [NativeTypeName("uint8_t **")] byte** u, [NativeTypeName("uint8_t **")] byte** v, int* stride, int* uv_stride);

    [DllImport("webp", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    [return: NativeTypeName("uint8_t *")]
    public static extern byte* WebPDecodeRGBAInto([NativeTypeName("const uint8_t *")] byte* data, [NativeTypeName("size_t")] nuint data_size, [NativeTypeName("uint8_t *")] byte* output_buffer, [NativeTypeName("size_t")] nuint output_buffer_size, int output_stride);

    [DllImport("webp", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    [return: NativeTypeName("uint8_t *")]
    public static extern byte* WebPDecodeARGBInto([NativeTypeName("const uint8_t *")] byte* data, [NativeTypeName("size_t")] nuint data_size, [NativeTypeName("uint8_t *")] byte* output_buffer, [NativeTypeName("size_t")] nuint output_buffer_size, int output_stride);

    [DllImport("webp", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    [return: NativeTypeName("uint8_t *")]
    public static extern byte* WebPDecodeBGRAInto([NativeTypeName("const uint8_t *")] byte* data, [NativeTypeName("size_t")] nuint data_size, [NativeTypeName("uint8_t *")] byte* output_buffer, [NativeTypeName("size_t")] nuint output_buffer_size, int output_stride);

    [DllImport("webp", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    [return: NativeTypeName("uint8_t *")]
    public static extern byte* WebPDecodeRGBInto([NativeTypeName("const uint8_t *")] byte* data, [NativeTypeName("size_t")] nuint data_size, [NativeTypeName("uint8_t *")] byte* output_buffer, [NativeTypeName("size_t")] nuint output_buffer_size, int output_stride);

    [DllImport("webp", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    [return: NativeTypeName("uint8_t *")]
    public static extern byte* WebPDecodeBGRInto([NativeTypeName("const uint8_t *")] byte* data, [NativeTypeName("size_t")] nuint data_size, [NativeTypeName("uint8_t *")] byte* output_buffer, [NativeTypeName("size_t")] nuint output_buffer_size, int output_stride);

    [DllImport("webp", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    [return: NativeTypeName("uint8_t *")]
    public static extern byte* WebPDecodeYUVInto([NativeTypeName("const uint8_t *")] byte* data, [NativeTypeName("size_t")] nuint data_size, [NativeTypeName("uint8_t *")] byte* luma, [NativeTypeName("size_t")] nuint luma_size, int luma_stride, [NativeTypeName("uint8_t *")] byte* u, [NativeTypeName("size_t")] nuint u_size, int u_stride, [NativeTypeName("uint8_t *")] byte* v, [NativeTypeName("size_t")] nuint v_size, int v_stride);

    public static int WebPIsPremultipliedMode(WEBP_CSP_MODE mode)
    {
        return (mode == MODE_rgbA || mode == MODE_bgrA || mode == MODE_Argb || mode == MODE_rgbA_4444) ? 1 : 0;
    }

    public static int WebPIsAlphaMode(WEBP_CSP_MODE mode)
    {
        return (mode == MODE_RGBA || mode == MODE_BGRA || mode == MODE_ARGB || mode == MODE_RGBA_4444 || mode == MODE_YUVA || (WebPIsPremultipliedMode(mode)) != 0) ? 1 : 0;
    }

    public static int WebPIsRGBMode(WEBP_CSP_MODE mode)
    {
        return (mode < MODE_YUV) ? 1 : 0;
    }

    [DllImport("webp", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    private static extern int WebPInitDecBufferInternal(WebPDecBuffer* param0, int param1);

    public static int WebPInitDecBuffer(WebPDecBuffer* buffer)
    {
        return WebPInitDecBufferInternal(buffer, 0x0209);
    }

    [DllImport("webp", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    public static extern void WebPFreeDecBuffer(WebPDecBuffer* buffer);

    [DllImport("webp", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    public static extern IntPtr WebPINewDecoder(WebPDecBuffer* output_buffer);

    [DllImport("webp", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    public static extern IntPtr WebPINewRGB(WEBP_CSP_MODE csp, [NativeTypeName("uint8_t *")] byte* output_buffer, [NativeTypeName("size_t")] nuint output_buffer_size, int output_stride);

    [DllImport("webp", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    public static extern IntPtr WebPINewYUVA([NativeTypeName("uint8_t *")] byte* luma, [NativeTypeName("size_t")] nuint luma_size, int luma_stride, [NativeTypeName("uint8_t *")] byte* u, [NativeTypeName("size_t")] nuint u_size, int u_stride, [NativeTypeName("uint8_t *")] byte* v, [NativeTypeName("size_t")] nuint v_size, int v_stride, [NativeTypeName("uint8_t *")] byte* a, [NativeTypeName("size_t")] nuint a_size, int a_stride);

    [DllImport("webp", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    public static extern IntPtr WebPINewYUV([NativeTypeName("uint8_t *")] byte* luma, [NativeTypeName("size_t")] nuint luma_size, int luma_stride, [NativeTypeName("uint8_t *")] byte* u, [NativeTypeName("size_t")] nuint u_size, int u_stride, [NativeTypeName("uint8_t *")] byte* v, [NativeTypeName("size_t")] nuint v_size, int v_stride);

    [DllImport("webp", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    public static extern void WebPIDelete(IntPtr idec);

    [DllImport("webp", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    public static extern VP8StatusCode WebPIAppend(IntPtr idec, [NativeTypeName("const uint8_t *")] byte* data, [NativeTypeName("size_t")] nuint data_size);

    [DllImport("webp", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    public static extern VP8StatusCode WebPIUpdate(IntPtr idec, [NativeTypeName("const uint8_t *")] byte* data, [NativeTypeName("size_t")] nuint data_size);

    [DllImport("webp", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    [return: NativeTypeName("uint8_t *")]
    public static extern byte* WebPIDecGetRGB([NativeTypeName("const WebPIDecoder *")] IntPtr idec, int* last_y, int* width, int* height, int* stride);

    [DllImport("webp", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    [return: NativeTypeName("uint8_t *")]
    public static extern byte* WebPIDecGetYUVA([NativeTypeName("const WebPIDecoder *")] IntPtr idec, int* last_y, [NativeTypeName("uint8_t **")] byte** u, [NativeTypeName("uint8_t **")] byte** v, [NativeTypeName("uint8_t **")] byte** a, int* width, int* height, int* stride, int* uv_stride, int* a_stride);

    [return: NativeTypeName("uint8_t *")]
    public static byte* WebPIDecGetYUV([NativeTypeName("const WebPIDecoder *")] IntPtr idec, int* last_y, [NativeTypeName("uint8_t **")] byte** u, [NativeTypeName("uint8_t **")] byte** v, int* width, int* height, int* stride, int* uv_stride)
    {
        return WebPIDecGetYUVA(idec, last_y, u, v, null, width, height, stride, uv_stride, null);
    }

    [DllImport("webp", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    [return: NativeTypeName("const WebPDecBuffer *")]
    public static extern WebPDecBuffer* WebPIDecodedArea([NativeTypeName("const WebPIDecoder *")] IntPtr idec, int* left, int* top, int* width, int* height);

    [DllImport("webp", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    private static extern VP8StatusCode WebPGetFeaturesInternal([NativeTypeName("const uint8_t *")] byte* param0, [NativeTypeName("size_t")] nuint param1, WebPBitstreamFeatures* param2, int param3);

    public static VP8StatusCode WebPGetFeatures([NativeTypeName("const uint8_t *")] byte* data, [NativeTypeName("size_t")] nuint data_size, WebPBitstreamFeatures* features)
    {
        return WebPGetFeaturesInternal(data, data_size, features, 0x0209);
    }

    [DllImport("webp", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    private static extern int WebPInitDecoderConfigInternal(WebPDecoderConfig* param0, int param1);

    public static int WebPInitDecoderConfig(WebPDecoderConfig* config)
    {
        return WebPInitDecoderConfigInternal(config, 0x0209);
    }

    [DllImport("webp", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    public static extern IntPtr WebPIDecode([NativeTypeName("const uint8_t *")] byte* data, [NativeTypeName("size_t")] nuint data_size, WebPDecoderConfig* config);

    [DllImport("webp", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    public static extern VP8StatusCode WebPDecode([NativeTypeName("const uint8_t *")] byte* data, [NativeTypeName("size_t")] nuint data_size, WebPDecoderConfig* config);

    [NativeTypeName("#define WEBP_DECODER_ABI_VERSION 0x0209")]
    public const int WEBP_DECODER_ABI_VERSION = 0x0209;
}
