// Borrowed from
//  https://github.com/dotnet/runtime/blob/release/6.0/src/libraries/System.Private.CoreLib/src/System/Runtime/CompilerServices/CallerArgumentExpressionAttribute.cs

// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See third-party-notices in the repository root for more information.

#if !NETCOREAPP3_0_OR_GREATER
namespace System.Runtime.CompilerServices
{
    [AttributeUsage(AttributeTargets.Parameter, AllowMultiple = false, Inherited = false)]
    internal sealed class CallerArgumentExpressionAttribute : Attribute
    {
        public CallerArgumentExpressionAttribute(string parameterName)
        {
            ParameterName = parameterName;
        }

        public string ParameterName { get; }
    }
}
#endif