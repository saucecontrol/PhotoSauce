// Copyright © Clinton Ingram and Contributors. Licensed under the MIT License (MIT).

// Ported from libjxl headers (cms_interface.h)
// Original source Copyright (c) the JPEG XL Project Authors. All rights reserved.
// See third-party-notices in the repository root for more information.

namespace PhotoSauce.Interop.Libjxl;

internal unsafe partial struct JxlCmsInterface
{
    public void* init_data;

    [NativeTypeName("jpegxl_cms_init_func")]
#if NET5_0_OR_GREATER
    public delegate* unmanaged[Cdecl]<void*, nuint, nuint, JxlColorProfile*, JxlColorProfile*, float, void*> init;
#else
    public void* _init;

    public delegate* unmanaged[Cdecl]<void*, nuint, nuint, JxlColorProfile*, JxlColorProfile*, float, void*> init
    {
        get => (delegate* unmanaged[Cdecl]<void*, nuint, nuint, JxlColorProfile*, JxlColorProfile*, float, void*>)_init;
        set => _init = value;
    }
#endif

    [NativeTypeName("jpegxl_cms_get_buffer_func")]
#if NET5_0_OR_GREATER
    public delegate* unmanaged[Cdecl]<void*, nuint, float*> get_src_buf;
#else
    public void* _get_src_buf;

    public delegate* unmanaged[Cdecl]<void*, nuint, float*> get_src_buf
    {
        get => (delegate* unmanaged[Cdecl]<void*, nuint, float*>)_get_src_buf;
        set => _get_src_buf = value;
    }
#endif

    [NativeTypeName("jpegxl_cms_get_buffer_func")]
#if NET5_0_OR_GREATER
    public delegate* unmanaged[Cdecl]<void*, nuint, float*> get_dst_buf;
#else
    public void* _get_dst_buf;

    public delegate* unmanaged[Cdecl]<void*, nuint, float*> get_dst_buf
    {
        get => (delegate* unmanaged[Cdecl]<void*, nuint, float*>)_get_dst_buf;
        set => _get_dst_buf = value;
    }
#endif

    [NativeTypeName("jpegxl_cms_run_func")]
#if NET5_0_OR_GREATER
    public delegate* unmanaged[Cdecl]<void*, nuint, float*, float*, nuint, int> run;
#else
    public void* _run;

    public delegate* unmanaged[Cdecl]<void*, nuint, float*, float*, nuint, int> run
    {
        get => (delegate* unmanaged[Cdecl]<void*, nuint, float*, float*, nuint, int>)_run;
        set => _run = value;
    }
#endif

    [NativeTypeName("jpegxl_cms_destroy_func")]
#if NET5_0_OR_GREATER
    public delegate* unmanaged[Cdecl]<void*, void> destroy;
#else
    public void* _destroy;

    public delegate* unmanaged[Cdecl]<void*, void> destroy
    {
        get => (delegate* unmanaged[Cdecl]<void*, void>)_destroy;
        set => _destroy = value;
    }
#endif
}
