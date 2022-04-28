// Copyright © Clinton Ingram and Contributors. Licensed under the MIT License (MIT).

// Ported from libweb headers (encode.h)
// Original source Copyright 2012 Google Inc. All Rights Reserved.
// See third-party-notices in the repository root for more information.

using System.Runtime.CompilerServices;

namespace PhotoSauce.Interop.Libwebp;

internal unsafe partial struct WebPPicture
{
    public int use_argb;

    public WebPEncCSP colorspace;

    public int width;

    public int height;

    [NativeTypeName("uint8_t *")]
    public byte* y;

    [NativeTypeName("uint8_t *")]
    public byte* u;

    [NativeTypeName("uint8_t *")]
    public byte* v;

    public int y_stride;

    public int uv_stride;

    [NativeTypeName("uint8_t *")]
    public byte* a;

    public int a_stride;

    [NativeTypeName("uint32_t[2]")]
    private fixed uint pad1[2];

    [NativeTypeName("uint32_t *")]
    public uint* argb;

    public int argb_stride;

    [NativeTypeName("uint32_t[3]")]
    private fixed uint pad2[3];

    [NativeTypeName("WebPWriterFunction")]
    public delegate* unmanaged[Cdecl]<byte*, nuint, WebPPicture*, int> writer;

    public void* custom_ptr;

    public int extra_info_type;

    [NativeTypeName("uint8_t *")]
    public byte* extra_info;

    public WebPAuxStats* stats;

    public WebPEncodingError error_code;

    [NativeTypeName("WebPProgressHook")]
    public delegate* unmanaged[Cdecl]<int, WebPPicture*, int> progress_hook;

    public void* user_data;

    [NativeTypeName("uint32_t[3]")]
    private fixed uint pad3[3];

    [NativeTypeName("uint8_t *")]
    private byte* pad4;

    [NativeTypeName("uint8_t *")]
    private byte* pad5;

    [NativeTypeName("uint32_t[8]")]
    private fixed uint pad6[8];

    private void* memory_;

    private void* memory_argb_;

    [NativeTypeName("void *[2]")]
    private _pad7_e__FixedBuffer pad7;

    private unsafe partial struct _pad7_e__FixedBuffer
    {
        private void* e0;
        private void* e1;

        public ref void* this[int index]
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get
            {
                fixed (void** pThis = &e0)
                {
                    return ref pThis[index];
                }
            }
        }
    }
}
