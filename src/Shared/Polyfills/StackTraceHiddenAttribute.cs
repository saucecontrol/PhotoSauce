// Borrowed from
//  https://github.com/dotnet/runtime/blob/release/6.0/src/libraries/System.Private.CoreLib/src/System/Diagnostics/StackTraceHiddenAttribute.cs

// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See third-party-notices in the repository root for more information.

#if !NET6_0_OR_GREATER
namespace System.Diagnostics
{
    /// <summary>
    /// Types and Methods attributed with StackTraceHidden will be omitted from the stack trace text shown in StackTrace.ToString()
    /// and Exception.StackTrace
    /// </summary>
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Method | AttributeTargets.Constructor | AttributeTargets.Struct, Inherited = false)]
    internal sealed class StackTraceHiddenAttribute : Attribute
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="StackTraceHiddenAttribute"/> class.
        /// </summary>
        public StackTraceHiddenAttribute() { }
    }
}
#endif