// Copyright © Clinton Ingram and Contributors. Licensed under the MIT License (MIT).

// Ported from libheif headers (heif.h)
// Original source Copyright (c) struktur AG, Dirk Farin
// See third-party-notices in the repository root for more information.

namespace PhotoSauce.Interop.Libheif;

internal unsafe partial struct heif_reader_range_request_result
{
    [NativeTypeName("enum heif_reader_grow_status")]
    public heif_reader_grow_status status;

    [NativeTypeName("uint64_t")]
    public ulong range_end;

    public int reader_error_code;

    [NativeTypeName("const char *")]
    public sbyte* reader_error_msg;
}
