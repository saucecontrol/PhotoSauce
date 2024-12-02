// Copyright © Clinton Ingram and Contributors
// SPDX-License-Identifier: MIT

// Ported from libjxl headers (encode.h)
// Original source Copyright (c) the JPEG XL Project Authors. All rights reserved.
// See third-party-notices in the repository root for more information.

namespace PhotoSauce.Interop.Libjxl;

internal unsafe partial struct JxlChunkedFrameInputSource
{
    public void* opaque;

    [NativeTypeName("void (*)(void *, JxlPixelFormat *)")]
#if NET5_0_OR_GREATER
    public delegate* unmanaged[Cdecl]<void*, JxlPixelFormat*, void> get_color_channels_pixel_format;
#else
    public void* _get_color_channels_pixel_format;

    public delegate* unmanaged[Cdecl]<void*, JxlPixelFormat*, void> get_color_channels_pixel_format
    {
        get => (delegate* unmanaged[Cdecl]<void*, JxlPixelFormat*, void>)_get_color_channels_pixel_format;
        set => _get_color_channels_pixel_format = value;
    }
#endif

    [NativeTypeName("const void *(*)(void *, size_t, size_t, size_t, size_t, size_t *)")]
#if NET5_0_OR_GREATER
    public delegate* unmanaged[Cdecl]<void*, nuint, nuint, nuint, nuint, nuint*, void*> get_color_channel_data_at;
#else
    public void* _get_color_channel_data_at;

    public delegate* unmanaged[Cdecl]<void*, nuint, nuint, nuint, nuint, nuint*, void*> get_color_channel_data_at
    {
        get => (delegate* unmanaged[Cdecl]<void*, nuint, nuint, nuint, nuint, nuint*, void*>)_get_color_channel_data_at;
        set => _get_color_channel_data_at = value;
    }
#endif

    [NativeTypeName("void (*)(void *, size_t, JxlPixelFormat *)")]
#if NET5_0_OR_GREATER
    public delegate* unmanaged[Cdecl]<void*, nuint, JxlPixelFormat*, void> get_extra_channel_pixel_format;
#else
    public void* _get_extra_channel_pixel_format;

    public delegate* unmanaged[Cdecl]<void*, nuint, JxlPixelFormat*, void> get_extra_channel_pixel_format
    {
        get => (delegate* unmanaged[Cdecl]<void*, nuint, JxlPixelFormat*, void>)_get_extra_channel_pixel_format;
        set => _get_extra_channel_pixel_format = value;
    }
#endif

    [NativeTypeName("const void *(*)(void *, size_t, size_t, size_t, size_t, size_t, size_t *)")]
#if NET5_0_OR_GREATER
    public delegate* unmanaged[Cdecl]<void*, nuint, nuint, nuint, nuint, nuint, nuint*, void*> get_extra_channel_data_at;
#else
    public void* _get_extra_channel_data_at;

    public delegate* unmanaged[Cdecl]<void*, nuint, nuint, nuint, nuint, nuint, nuint*, void*> get_extra_channel_data_at
    {
        get => (delegate* unmanaged[Cdecl]<void*, nuint, nuint, nuint, nuint, nuint, nuint*, void*>)_get_extra_channel_data_at;
        set => _get_extra_channel_data_at = value;
    }
#endif

    [NativeTypeName("void (*)(void *, const void *)")]
#if NET5_0_OR_GREATER
    public delegate* unmanaged[Cdecl]<void*, void*, void> release_buffer;
#else
    public void* _release_buffer;

    public delegate* unmanaged[Cdecl]<void*, void*, void> release_buffer
    {
        get => (delegate* unmanaged[Cdecl]<void*, void*, void>)_release_buffer;
        set => _release_buffer = value;
    }
#endif
}
