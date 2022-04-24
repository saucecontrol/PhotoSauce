// Copyright © Clinton Ingram and Contributors. Licensed under the MIT License (MIT).

// Ported from libweb headers (encode.h)
// Original source Copyright 2012 Google Inc. All Rights Reserved.
// See third-party-notices in the repository root for more information.

using System.Runtime.InteropServices;
using static PhotoSauce.Interop.Libwebp.WebPPreset;

namespace PhotoSauce.Interop.Libwebp;

internal static unsafe partial class Libwebp
{
    [DllImport("webp", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    public static extern int WebPGetEncoderVersion();

    [DllImport("webp", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    [return: NativeTypeName("size_t")]
    public static extern nuint WebPEncodeRGB([NativeTypeName("const uint8_t *")] byte* rgb, int width, int height, int stride, float quality_factor, [NativeTypeName("uint8_t **")] byte** output);

    [DllImport("webp", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    [return: NativeTypeName("size_t")]
    public static extern nuint WebPEncodeBGR([NativeTypeName("const uint8_t *")] byte* bgr, int width, int height, int stride, float quality_factor, [NativeTypeName("uint8_t **")] byte** output);

    [DllImport("webp", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    [return: NativeTypeName("size_t")]
    public static extern nuint WebPEncodeRGBA([NativeTypeName("const uint8_t *")] byte* rgba, int width, int height, int stride, float quality_factor, [NativeTypeName("uint8_t **")] byte** output);

    [DllImport("webp", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    [return: NativeTypeName("size_t")]
    public static extern nuint WebPEncodeBGRA([NativeTypeName("const uint8_t *")] byte* bgra, int width, int height, int stride, float quality_factor, [NativeTypeName("uint8_t **")] byte** output);

    [DllImport("webp", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    [return: NativeTypeName("size_t")]
    public static extern nuint WebPEncodeLosslessRGB([NativeTypeName("const uint8_t *")] byte* rgb, int width, int height, int stride, [NativeTypeName("uint8_t **")] byte** output);

    [DllImport("webp", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    [return: NativeTypeName("size_t")]
    public static extern nuint WebPEncodeLosslessBGR([NativeTypeName("const uint8_t *")] byte* bgr, int width, int height, int stride, [NativeTypeName("uint8_t **")] byte** output);

    [DllImport("webp", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    [return: NativeTypeName("size_t")]
    public static extern nuint WebPEncodeLosslessRGBA([NativeTypeName("const uint8_t *")] byte* rgba, int width, int height, int stride, [NativeTypeName("uint8_t **")] byte** output);

    [DllImport("webp", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    [return: NativeTypeName("size_t")]
    public static extern nuint WebPEncodeLosslessBGRA([NativeTypeName("const uint8_t *")] byte* bgra, int width, int height, int stride, [NativeTypeName("uint8_t **")] byte** output);

    [DllImport("webp", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    private static extern int WebPConfigInitInternal(WebPConfig* param0, WebPPreset param1, float param2, int param3);

    public static int WebPConfigInit(WebPConfig* config)
    {
        return WebPConfigInitInternal(config, WEBP_PRESET_DEFAULT, 75.0f, 0x020f);
    }

    public static int WebPConfigPreset(WebPConfig* config, WebPPreset preset, float quality)
    {
        return WebPConfigInitInternal(config, preset, quality, 0x020f);
    }

    [DllImport("webp", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    public static extern int WebPConfigLosslessPreset(WebPConfig* config, int level);

    [DllImport("webp", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    public static extern int WebPValidateConfig([NativeTypeName("const WebPConfig *")] WebPConfig* config);

    [DllImport("webp", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    public static extern void WebPMemoryWriterInit(WebPMemoryWriter* writer);

    [DllImport("webp", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    public static extern void WebPMemoryWriterClear(WebPMemoryWriter* writer);

    [DllImport("webp", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    public static extern int WebPMemoryWrite([NativeTypeName("const uint8_t *")] byte* data, [NativeTypeName("size_t")] nuint data_size, [NativeTypeName("const WebPPicture *")] WebPPicture* picture);

    [DllImport("webp", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    private static extern int WebPPictureInitInternal(WebPPicture* param0, int param1);

    public static int WebPPictureInit(WebPPicture* picture)
    {
        return WebPPictureInitInternal(picture, 0x020f);
    }

    [DllImport("webp", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    public static extern int WebPPictureAlloc(WebPPicture* picture);

    [DllImport("webp", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    public static extern void WebPPictureFree(WebPPicture* picture);

    [DllImport("webp", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    public static extern int WebPPictureCopy([NativeTypeName("const WebPPicture *")] WebPPicture* src, WebPPicture* dst);

    [DllImport("webp", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    public static extern int WebPPlaneDistortion([NativeTypeName("const uint8_t *")] byte* src, [NativeTypeName("size_t")] nuint src_stride, [NativeTypeName("const uint8_t *")] byte* @ref, [NativeTypeName("size_t")] nuint ref_stride, int width, int height, [NativeTypeName("size_t")] nuint x_step, int type, float* distortion, float* result);

    [DllImport("webp", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    public static extern int WebPPictureDistortion([NativeTypeName("const WebPPicture *")] WebPPicture* src, [NativeTypeName("const WebPPicture *")] WebPPicture* @ref, int metric_type, [NativeTypeName("float[5]")] float* result);

    [DllImport("webp", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    public static extern int WebPPictureCrop(WebPPicture* picture, int left, int top, int width, int height);

    [DllImport("webp", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    public static extern int WebPPictureView([NativeTypeName("const WebPPicture *")] WebPPicture* src, int left, int top, int width, int height, WebPPicture* dst);

    [DllImport("webp", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    public static extern int WebPPictureIsView([NativeTypeName("const WebPPicture *")] WebPPicture* picture);

    [DllImport("webp", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    public static extern int WebPPictureRescale(WebPPicture* pic, int width, int height);

    [DllImport("webp", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    public static extern int WebPPictureImportRGB(WebPPicture* picture, [NativeTypeName("const uint8_t *")] byte* rgb, int rgb_stride);

    [DllImport("webp", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    public static extern int WebPPictureImportRGBA(WebPPicture* picture, [NativeTypeName("const uint8_t *")] byte* rgba, int rgba_stride);

    [DllImport("webp", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    public static extern int WebPPictureImportRGBX(WebPPicture* picture, [NativeTypeName("const uint8_t *")] byte* rgbx, int rgbx_stride);

    [DllImport("webp", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    public static extern int WebPPictureImportBGR(WebPPicture* picture, [NativeTypeName("const uint8_t *")] byte* bgr, int bgr_stride);

    [DllImport("webp", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    public static extern int WebPPictureImportBGRA(WebPPicture* picture, [NativeTypeName("const uint8_t *")] byte* bgra, int bgra_stride);

    [DllImport("webp", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    public static extern int WebPPictureImportBGRX(WebPPicture* picture, [NativeTypeName("const uint8_t *")] byte* bgrx, int bgrx_stride);

    [DllImport("webp", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    public static extern int WebPPictureARGBToYUVA(WebPPicture* picture, WebPEncCSP param1);

    [DllImport("webp", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    public static extern int WebPPictureARGBToYUVADithered(WebPPicture* picture, WebPEncCSP colorspace, float dithering);

    [DllImport("webp", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    public static extern int WebPPictureSharpARGBToYUVA(WebPPicture* picture);

    [DllImport("webp", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    public static extern int WebPPictureSmartARGBToYUVA(WebPPicture* picture);

    [DllImport("webp", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    public static extern int WebPPictureYUVAToARGB(WebPPicture* picture);

    [DllImport("webp", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    public static extern void WebPCleanupTransparentArea(WebPPicture* picture);

    [DllImport("webp", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    public static extern int WebPPictureHasTransparency([NativeTypeName("const WebPPicture *")] WebPPicture* picture);

    [DllImport("webp", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    public static extern void WebPBlendAlpha(WebPPicture* pic, [NativeTypeName("uint32_t")] uint background_rgb);

    [DllImport("webp", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    public static extern int WebPEncode([NativeTypeName("const WebPConfig *")] WebPConfig* config, WebPPicture* picture);

    [NativeTypeName("#define WEBP_ENCODER_ABI_VERSION 0x020f")]
    public const int WEBP_ENCODER_ABI_VERSION = 0x020f;

    [NativeTypeName("#define WEBP_MAX_DIMENSION 16383")]
    public const int WEBP_MAX_DIMENSION = 16383;
}
