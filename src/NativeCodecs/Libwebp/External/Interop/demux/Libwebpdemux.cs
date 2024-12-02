// Copyright © Clinton Ingram and Contributors
// SPDX-License-Identifier: MIT

// Ported from libweb headers (demux.h)
// Original source Copyright 2012 Google Inc. All Rights Reserved.
// See third-party-notices in the repository root for more information.

using System.Runtime.InteropServices;

namespace PhotoSauce.Interop.Libwebp;

internal static unsafe partial class Libwebpdemux
{
    [DllImport("webpdemux", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    public static extern int WebPGetDemuxVersion();

    [DllImport("webpdemux", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    private static extern void* WebPDemuxInternal([NativeTypeName("const WebPData *")] WebPData* param0, int param1, WebPDemuxState* param2, int param3);

    public static void* WebPDemux([NativeTypeName("const WebPData *")] WebPData* data)
    {
        return WebPDemuxInternal(data, 0, null, 0x0107);
    }

    public static void* WebPDemuxPartial([NativeTypeName("const WebPData *")] WebPData* data, WebPDemuxState* state)
    {
        return WebPDemuxInternal(data, 1, state, 0x0107);
    }

    [DllImport("webpdemux", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    public static extern void WebPDemuxDelete(void* dmux);

    [DllImport("webpdemux", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    [return: NativeTypeName("uint32_t")]
    public static extern uint WebPDemuxGetI([NativeTypeName("const WebPDemuxer *")] void* dmux, WebPFormatFeature feature);

    [DllImport("webpdemux", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    public static extern int WebPDemuxGetFrame([NativeTypeName("const WebPDemuxer *")] void* dmux, int frame_number, WebPIterator* iter);

    [DllImport("webpdemux", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    public static extern int WebPDemuxNextFrame(WebPIterator* iter);

    [DllImport("webpdemux", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    public static extern int WebPDemuxPrevFrame(WebPIterator* iter);

    [DllImport("webpdemux", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    public static extern void WebPDemuxReleaseIterator(WebPIterator* iter);

    [DllImport("webpdemux", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    public static extern int WebPDemuxGetChunk([NativeTypeName("const WebPDemuxer *")] void* dmux, [NativeTypeName("const char[4]")] sbyte* fourcc, int chunk_number, WebPChunkIterator* iter);

    [DllImport("webpdemux", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    public static extern int WebPDemuxNextChunk(WebPChunkIterator* iter);

    [DllImport("webpdemux", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    public static extern int WebPDemuxPrevChunk(WebPChunkIterator* iter);

    [DllImport("webpdemux", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    public static extern void WebPDemuxReleaseChunkIterator(WebPChunkIterator* iter);

    [DllImport("webpdemux", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    private static extern int WebPAnimDecoderOptionsInitInternal(WebPAnimDecoderOptions* param0, int param1);

    public static int WebPAnimDecoderOptionsInit(WebPAnimDecoderOptions* dec_options)
    {
        return WebPAnimDecoderOptionsInitInternal(dec_options, 0x0107);
    }

    [DllImport("webpdemux", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    private static extern void* WebPAnimDecoderNewInternal([NativeTypeName("const WebPData *")] WebPData* param0, [NativeTypeName("const WebPAnimDecoderOptions *")] WebPAnimDecoderOptions* param1, int param2);

    public static void* WebPAnimDecoderNew([NativeTypeName("const WebPData *")] WebPData* webp_data, [NativeTypeName("const WebPAnimDecoderOptions *")] WebPAnimDecoderOptions* dec_options)
    {
        return WebPAnimDecoderNewInternal(webp_data, dec_options, 0x0107);
    }

    [DllImport("webpdemux", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    public static extern int WebPAnimDecoderGetInfo([NativeTypeName("const WebPAnimDecoder *")] void* dec, WebPAnimInfo* info);

    [DllImport("webpdemux", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    public static extern int WebPAnimDecoderGetNext(void* dec, [NativeTypeName("uint8_t **")] byte** buf, int* timestamp);

    [DllImport("webpdemux", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    public static extern int WebPAnimDecoderHasMoreFrames([NativeTypeName("const WebPAnimDecoder *")] void* dec);

    [DllImport("webpdemux", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    public static extern void WebPAnimDecoderReset(void* dec);

    [DllImport("webpdemux", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    [return: NativeTypeName("const WebPDemuxer *")]
    public static extern void* WebPAnimDecoderGetDemuxer([NativeTypeName("const WebPAnimDecoder *")] void* dec);

    [DllImport("webpdemux", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    public static extern void WebPAnimDecoderDelete(void* dec);

    [NativeTypeName("#define WEBP_DEMUX_ABI_VERSION 0x0107")]
    public const int WEBP_DEMUX_ABI_VERSION = 0x0107;
}
