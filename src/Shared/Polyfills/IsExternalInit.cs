// Borrowed from
//  https://github.com/dotnet/runtime/blob/release/6.0/src/libraries/Common/src/System/Runtime/CompilerServices/IsExternalInit.cs

// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See third-party-notices in the repository root for more information.

using System.ComponentModel;

namespace System.Runtime.CompilerServices
{
    /// <summary>
    /// Reserved to be used by the compiler for tracking metadata.
    /// This class should not be used by developers in source code.
    /// </summary>
    [EditorBrowsable(EditorBrowsableState.Never)]
#if SYSTEM_PRIVATE_CORELIB
    public
#else
    internal
#endif
    static class IsExternalInit
    {
    }
}