// Copyright © Clinton Ingram and Contributors. Licensed under the MIT License (MIT).

// Ported from libweb headers (demux.h)
// Original source Copyright 2012 Google Inc. All Rights Reserved.
// See third-party-notices in the repository root for more information.

using System;
using System.Runtime.InteropServices;

namespace PhotoSauce.Interop.Libwebp;

internal static unsafe partial class Libwebpdemux
{
    [DllImport("webpdemux", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    public static extern int WebPGetDemuxVersion();

    [DllImport("webpdemux", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    private static extern IntPtr WebPDemuxInternal([NativeTypeName("const WebPData *")] WebPData* param0, int param1, WebPDemuxState* param2, int param3);

    public static IntPtr WebPDemux([NativeTypeName("const WebPData *")] WebPData* data)
    {
        return WebPDemuxInternal(data, 0, null, 0x0107);
    }

    public static IntPtr WebPDemuxPartial([NativeTypeName("const WebPData *")] WebPData* data, WebPDemuxState* state)
    {
        return WebPDemuxInternal(data, 1, state, 0x0107);
    }

    [DllImport("webpdemux", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    public static extern void WebPDemuxDelete(IntPtr dmux);

    [DllImport("webpdemux", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    [return: NativeTypeName("uint32_t")]
    public static extern uint WebPDemuxGetI([NativeTypeName("const WebPDemuxer *")] IntPtr dmux, WebPFormatFeature feature);

    [DllImport("webpdemux", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    public static extern int WebPDemuxGetFrame([NativeTypeName("const WebPDemuxer *")] IntPtr dmux, int frame_number, WebPIterator* iter);

    [DllImport("webpdemux", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    public static extern int WebPDemuxNextFrame(WebPIterator* iter);

    [DllImport("webpdemux", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    public static extern int WebPDemuxPrevFrame(WebPIterator* iter);

    [DllImport("webpdemux", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    public static extern void WebPDemuxReleaseIterator(WebPIterator* iter);

    [DllImport("webpdemux", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    public static extern int WebPDemuxGetChunk([NativeTypeName("const WebPDemuxer *")] IntPtr dmux, [NativeTypeName("const char[4]")] sbyte* fourcc, int chunk_number, WebPChunkIterator* iter);

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
    private static extern IntPtr WebPAnimDecoderNewInternal([NativeTypeName("const WebPData *")] WebPData* param0, [NativeTypeName("const WebPAnimDecoderOptions *")] WebPAnimDecoderOptions* param1, int param2);

    public static IntPtr WebPAnimDecoderNew([NativeTypeName("const WebPData *")] WebPData* webp_data, [NativeTypeName("const WebPAnimDecoderOptions *")] WebPAnimDecoderOptions* dec_options)
    {
        return WebPAnimDecoderNewInternal(webp_data, dec_options, 0x0107);
    }

    [DllImport("webpdemux", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    public static extern int WebPAnimDecoderGetInfo([NativeTypeName("const WebPAnimDecoder *")] IntPtr dec, WebPAnimInfo* info);

    [DllImport("webpdemux", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    public static extern int WebPAnimDecoderGetNext(IntPtr dec, [NativeTypeName("uint8_t **")] byte** buf, int* timestamp);

    [DllImport("webpdemux", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    public static extern int WebPAnimDecoderHasMoreFrames([NativeTypeName("const WebPAnimDecoder *")] IntPtr dec);

    [DllImport("webpdemux", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    public static extern void WebPAnimDecoderReset(IntPtr dec);

    [DllImport("webpdemux", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    [return: NativeTypeName("const WebPDemuxer *")]
    public static extern IntPtr WebPAnimDecoderGetDemuxer([NativeTypeName("const WebPAnimDecoder *")] IntPtr dec);

    [DllImport("webpdemux", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    public static extern void WebPAnimDecoderDelete(IntPtr dec);

    [NativeTypeName("#define WEBP_DEMUX_ABI_VERSION 0x0107")]
    public const int WEBP_DEMUX_ABI_VERSION = 0x0107;
}
