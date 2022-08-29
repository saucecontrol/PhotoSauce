// Copyright © Clinton Ingram and Contributors. Licensed under the MIT License (MIT).

// Ported from libjpeg headers (jpeglib.h)
// This software is based in part on the work of the Independent JPEG Group.
// Original source copyright (C) 1991-1998, Thomas G. Lane. All Rights Reserved
// See third-party-notices in the repository root for more information.

using System.Runtime.InteropServices;

namespace PhotoSauce.Interop.Libjpeg;

internal unsafe partial struct jpeg_error_mgr
{
    [NativeTypeName("void (*)(j_common_ptr)")]
    public delegate* unmanaged[Cdecl]<jpeg_common_struct*, void> error_exit;

    [NativeTypeName("void (*)(j_common_ptr, int)")]
    public delegate* unmanaged[Cdecl]<jpeg_common_struct*, int, void> emit_message;

    [NativeTypeName("void (*)(j_common_ptr)")]
    public delegate* unmanaged[Cdecl]<jpeg_common_struct*, void> output_message;

    [NativeTypeName("void (*)(j_common_ptr, char *)")]
    public delegate* unmanaged[Cdecl]<jpeg_common_struct*, sbyte*, void> format_message;

    [NativeTypeName("void (*)(j_common_ptr)")]
    public delegate* unmanaged[Cdecl]<jpeg_common_struct*, void> reset_error_mgr;

    public int msg_code;

    [NativeTypeName("union (anonymous union at C:/gitlocal/photosauce/out/vcpkg/install/x64-windows-staticdependencies/include/jpeglib.h:738:3)")]
    public _msg_parm_e__Union msg_parm;

    public int trace_level;

    [NativeTypeName("long")]
    public int num_warnings;

    [NativeTypeName("const char *const *")]
    public sbyte** jpeg_message_table;

    public int last_jpeg_message;

    [NativeTypeName("const char *const *")]
    public sbyte** addon_message_table;

    public int first_addon_message;

    public int last_addon_message;

    [StructLayout(LayoutKind.Explicit)]
    internal unsafe partial struct _msg_parm_e__Union
    {
        [FieldOffset(0)]
        [NativeTypeName("int[8]")]
        public fixed int i[8];

        [FieldOffset(0)]
        [NativeTypeName("char[80]")]
        public fixed sbyte s[80];
    }
}
