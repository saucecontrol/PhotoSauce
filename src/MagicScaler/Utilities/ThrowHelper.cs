// Copyright © Clinton Ingram and Contributors.  Licensed under the MIT License.

using System;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;

namespace PhotoSauce.MagicScaler;

[StackTraceHidden]
internal static class ThrowHelper
{
	[DoesNotReturn]
	public static void ThrowArgNonNeg() => throw new ArgumentException("Value must not be negative.");

	[DoesNotReturn]
	public static void ThrowArgNonZero() => throw new ArgumentException("Value must not be zero.");

	[DoesNotReturn]
	public static void ThrowArgOutOfRange() => throw new ArgumentOutOfRangeException("Value is not in the valid range.");
}
