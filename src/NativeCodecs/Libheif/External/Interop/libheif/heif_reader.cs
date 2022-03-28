// Copyright © Clinton Ingram and Contributors. Licensed under the MIT License (MIT).

// Ported from libheif headers (heif.h)
// Original source Copyright (c) struktur AG, Dirk Farin <farin@struktur.de>
// See third-party-notices in the repository root for more information.

namespace PhotoSauce.Interop.Libheif;

internal unsafe partial struct heif_reader
{
    public int reader_api_version;

    [NativeTypeName("int64_t (*)(void *)")]
#if NET5_0_OR_GREATER
    public delegate* unmanaged[Cdecl]<void*, long> get_position;
#else
    public void* _get_position;

    public delegate* unmanaged[Cdecl]<void*, long> get_position
    {
        get => (delegate* unmanaged[Cdecl]<void*, long>)_get_position;
        set => _get_position = value;
    }
#endif

    [NativeTypeName("int (*)(void *, size_t, void *)")]
#if NET5_0_OR_GREATER
    public delegate* unmanaged[Cdecl]<void*, nuint, void*, int> read;
#else
    public void* _read;

    public delegate* unmanaged[Cdecl]<void*, nuint, void*, int> read
    {
        get => (delegate* unmanaged[Cdecl]<void*, nuint, void*, int>)_read;
        set => _read = value;
    }
#endif

    [NativeTypeName("int (*)(int64_t, void *)")]
#if NET5_0_OR_GREATER
    public delegate* unmanaged[Cdecl]<long, void*, int> seek;
#else
    public void* _seek;

    public delegate* unmanaged[Cdecl]<long, void*, int> seek
    {
        get => (delegate* unmanaged[Cdecl]<long, void*, int>)_seek;
        set => _seek = value;
    }
#endif

    [NativeTypeName("enum heif_reader_grow_status (*)(int64_t, void *)")]
#if NET5_0_OR_GREATER
    public delegate* unmanaged[Cdecl]<long, void*, heif_reader_grow_status> wait_for_file_size;
#else
    public void* _wait_for_file_size;

    public delegate* unmanaged[Cdecl]<long, void*, heif_reader_grow_status> wait_for_file_size
    {
        get => (delegate* unmanaged[Cdecl]<long, void*, heif_reader_grow_status>)_wait_for_file_size;
        set => _wait_for_file_size = value;
    }
#endif
}
