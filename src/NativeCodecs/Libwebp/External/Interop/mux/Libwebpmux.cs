// Copyright © Clinton Ingram and Contributors. Licensed under the MIT License (MIT).

// Ported from libweb headers (mux.h)
// Original source Copyright 2012 Google Inc. All Rights Reserved.
// See third-party-notices in the repository root for more information.

using System.Runtime.InteropServices;

namespace PhotoSauce.Interop.Libwebp;

internal static unsafe partial class Libwebpmux
{
    [DllImport("webpmux", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    public static extern int WebPGetMuxVersion();

    [DllImport("webpmux", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    private static extern void* WebPNewInternal(int param0);

    public static void* WebPMuxNew()
    {
        return WebPNewInternal(0x0108);
    }

    [DllImport("webpmux", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    public static extern void WebPMuxDelete(void* mux);

    [DllImport("webpmux", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    private static extern void* WebPMuxCreateInternal([NativeTypeName("const WebPData *")] WebPData* param0, int param1, int param2);

    public static void* WebPMuxCreate([NativeTypeName("const WebPData *")] WebPData* bitstream, int copy_data)
    {
        return WebPMuxCreateInternal(bitstream, copy_data, 0x0108);
    }

    [DllImport("webpmux", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    public static extern WebPMuxError WebPMuxSetChunk(void* mux, [NativeTypeName("const char[4]")] sbyte* fourcc, [NativeTypeName("const WebPData *")] WebPData* chunk_data, int copy_data);

    [DllImport("webpmux", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    public static extern WebPMuxError WebPMuxGetChunk([NativeTypeName("const WebPMux *")] void* mux, [NativeTypeName("const char[4]")] sbyte* fourcc, WebPData* chunk_data);

    [DllImport("webpmux", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    public static extern WebPMuxError WebPMuxDeleteChunk(void* mux, [NativeTypeName("const char[4]")] sbyte* fourcc);

    [DllImport("webpmux", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    public static extern WebPMuxError WebPMuxSetImage(void* mux, [NativeTypeName("const WebPData *")] WebPData* bitstream, int copy_data);

    [DllImport("webpmux", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    public static extern WebPMuxError WebPMuxPushFrame(void* mux, [NativeTypeName("const WebPMuxFrameInfo *")] WebPMuxFrameInfo* frame, int copy_data);

    [DllImport("webpmux", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    public static extern WebPMuxError WebPMuxGetFrame([NativeTypeName("const WebPMux *")] void* mux, [NativeTypeName("uint32_t")] uint nth, WebPMuxFrameInfo* frame);

    [DllImport("webpmux", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    public static extern WebPMuxError WebPMuxDeleteFrame(void* mux, [NativeTypeName("uint32_t")] uint nth);

    [DllImport("webpmux", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    public static extern WebPMuxError WebPMuxSetAnimationParams(void* mux, [NativeTypeName("const WebPMuxAnimParams *")] WebPMuxAnimParams* @params);

    [DllImport("webpmux", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    public static extern WebPMuxError WebPMuxGetAnimationParams([NativeTypeName("const WebPMux *")] void* mux, WebPMuxAnimParams* @params);

    [DllImport("webpmux", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    public static extern WebPMuxError WebPMuxSetCanvasSize(void* mux, int width, int height);

    [DllImport("webpmux", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    public static extern WebPMuxError WebPMuxGetCanvasSize([NativeTypeName("const WebPMux *")] void* mux, int* width, int* height);

    [DllImport("webpmux", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    public static extern WebPMuxError WebPMuxGetFeatures([NativeTypeName("const WebPMux *")] void* mux, [NativeTypeName("uint32_t *")] uint* flags);

    [DllImport("webpmux", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    public static extern WebPMuxError WebPMuxNumChunks([NativeTypeName("const WebPMux *")] void* mux, WebPChunkId id, int* num_elements);

    [DllImport("webpmux", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    public static extern WebPMuxError WebPMuxAssemble(void* mux, WebPData* assembled_data);

    [DllImport("webpmux", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    private static extern int WebPAnimEncoderOptionsInitInternal(WebPAnimEncoderOptions* param0, int param1);

    public static int WebPAnimEncoderOptionsInit(WebPAnimEncoderOptions* enc_options)
    {
        return WebPAnimEncoderOptionsInitInternal(enc_options, 0x0108);
    }

    [DllImport("webpmux", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    private static extern void* WebPAnimEncoderNewInternal(int param0, int param1, [NativeTypeName("const WebPAnimEncoderOptions *")] WebPAnimEncoderOptions* param2, int param3);

    public static void* WebPAnimEncoderNew(int width, int height, [NativeTypeName("const WebPAnimEncoderOptions *")] WebPAnimEncoderOptions* enc_options)
    {
        return WebPAnimEncoderNewInternal(width, height, enc_options, 0x0108);
    }

    [DllImport("webpmux", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    public static extern int WebPAnimEncoderAdd(void* enc, [NativeTypeName("struct WebPPicture *")] WebPPicture* frame, int timestamp_ms, [NativeTypeName("const struct WebPConfig *")] WebPConfig* config);

    [DllImport("webpmux", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    public static extern int WebPAnimEncoderAssemble(void* enc, WebPData* webp_data);

    [DllImport("webpmux", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    [return: NativeTypeName("const char *")]
    public static extern sbyte* WebPAnimEncoderGetError(void* enc);

    [DllImport("webpmux", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    public static extern void WebPAnimEncoderDelete(void* enc);

    [NativeTypeName("#define WEBP_MUX_ABI_VERSION 0x0108")]
    public const int WEBP_MUX_ABI_VERSION = 0x0108;
}
