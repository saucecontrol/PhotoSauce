// Copyright © Clinton Ingram and Contributors.  Licensed under the MIT License.

using System;
using System.IO;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;

namespace PhotoSauce.MagicScaler;

[StackTraceHidden]
internal static class Guard
{
	public static void NotNull([NotNull] object? arg, [CallerArgumentExpression("arg")] string? name = null)
	{
		if (arg is null)
			@throw(name);

		[DoesNotReturn]
		static void @throw(string? name) => throw new ArgumentNullException(name);
	}

	public static void NotNullOrEmpty([NotNull] string? arg, [CallerArgumentExpression("arg")] string? name = null)
	{
		if (string.IsNullOrEmpty(arg))
			@throw(name);

		[DoesNotReturn]
		static void @throw(string? name) => throw new ArgumentNullException(name, "String must not be empty.");
	}

	public static void NotEmpty<T>(ReadOnlySpan<T> arg, [CallerArgumentExpression("arg")] string? name = null)
	{
		if (arg.IsEmpty)
			@throw(name);

		[DoesNotReturn]
		static void @throw(string? name) => throw new ArgumentNullException(name, "Buffer must not be empty or zero-length.");
	}

	public static void ValidForInput([NotNull] Stream? stm, [CallerArgumentExpression("stm")] string? name = null)
	{
		NotNull(stm, name);

		if (!stm.CanSeek || !stm.CanRead)
			throw new ArgumentException("Input Stream must allow Seek and Read.", name);

		if ((ulong)stm.Position >= (ulong)stm.Length)
			throw new ArgumentException("Input Stream is empty or positioned at its end.", name);
	}

	public static void ValidForOutput([NotNull] Stream stm, [CallerArgumentExpression("stm")] string? name = null)
	{
		NotNull(stm, name);

		if (!stm.CanSeek || !stm.CanWrite)
			throw new ArgumentException("Output Stream must allow Seek and Write.", name);
	}

	public static void NonNegative(int val, [CallerArgumentExpression("val")] string? name = null)
	{
		if (val < 0)
			@throw(name);

		[DoesNotReturn]
		static void @throw(string? name) => throw new ArgumentOutOfRangeException(name, "Value must not be negative.");
	}
}
