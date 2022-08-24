// Copyright © Clinton Ingram and Contributors.  Licensed under the MIT License.

using System;
using System.IO;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;

namespace PhotoSauce.MagicScaler;

[StackTraceHidden]
internal static class ThrowHelper
{
	public static void ThrowIfNull([NotNull] object? arg, [CallerArgumentExpression(nameof(arg))] string? name = null)
	{
		if (arg is null)
			@throw(name);

		[DoesNotReturn]
		static void @throw(string? name) => throw new ArgumentNullException(name);
	}

	public static void ThrowIfNullOrEmpty([NotNull] string? arg, [CallerArgumentExpression(nameof(arg))] string? name = null)
	{
		if (string.IsNullOrEmpty(arg))
			@throw(name);

		[DoesNotReturn]
		static void @throw(string? name) => throw new ArgumentNullException(name, "String must not be empty.");
	}

	public static void ThrowIfEmpty<T>(ReadOnlySpan<T> arg, [CallerArgumentExpression(nameof(arg))] string? name = null)
	{
		if (arg.IsEmpty)
			@throw(name);

		[DoesNotReturn]
		static void @throw(string? name) => throw new ArgumentNullException(name, "Buffer must not be empty or zero-length.");
	}

	public static void ThrowIfNotValidForInput([NotNull] Stream? stm, [CallerArgumentExpression(nameof(stm))] string? name = null)
	{
		ThrowIfNull(stm, name);

		if (!stm.CanSeek || !stm.CanRead)
			throw new ArgumentException("Input Stream must allow Seek and Read.", name);

		if ((ulong)stm.Position >= (ulong)stm.Length)
			throw new ArgumentException("Input Stream is empty or positioned at its end.", name);
	}

	public static void ThrowIfNotValidForOutput([NotNull] Stream stm, [CallerArgumentExpression(nameof(stm))] string? name = null)
	{
		ThrowIfNull(stm, name);

		if (!stm.CanSeek || !stm.CanWrite)
			throw new ArgumentException("Output Stream must allow Seek and Write.", name);
	}

	public static void ThrowIfNegative(int val, [CallerArgumentExpression(nameof(val))] string? name = null)
	{
		if (val < 0)
			@throw(name);

		[DoesNotReturn]
		static void @throw(string? name) => throw new ArgumentOutOfRangeException(name, "Value must not be negative.");
	}

	public static void ThrowIfFinalizerExceptionsEnabled([CallerMemberName] string? name = null)
	{
		Debug.WriteLine($"Object not disposed: {name}");

		if (AppConfig.ThrowOnFinalizer)
			@throw(name);

		[DoesNotReturn]
		static void @throw(string? name) => throw new InvalidOperationException($"Object not disposed: {name}");
	}

	[DoesNotReturn]
	public static void ThrowObjectDisposed(string name) => throw new ObjectDisposedException(name);
}
