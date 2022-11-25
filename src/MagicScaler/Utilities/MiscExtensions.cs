// Copyright © Clinton Ingram and Contributors.  Licensed under the MIT License.

using System;
using System.IO;
using System.Linq;
using System.Buffers;
using System.Drawing;
using System.Reflection;
using System.Collections.Generic;
using System.Runtime.InteropServices;

#if NETFRAMEWORK
using System.Configuration;
using System.Collections.Specialized;
#endif

using Blake2Fast;

namespace PhotoSauce.MagicScaler;

internal static class MiscExtensions
{
	public static bool EqualsInsensitive(this string s1, string s2) => string.Equals(s1, s2, StringComparison.OrdinalIgnoreCase);

	public static bool ContainsInsensitive(this IEnumerable<string> c, string s) => c.Contains(s, StringComparer.OrdinalIgnoreCase);

	public static int IndexOfOrdinal(this string s1, string s2) => s1.IndexOf(s2, StringComparison.Ordinal);

	public static bool ContainsOrdinal(this string s1, string s2) => s1.IndexOfOrdinal(s2) >= 0;

	public static bool StartsWithOrdinal(this string s1, string s2) => s1.StartsWith(s2, StringComparison.Ordinal);

	public static bool EndsWithOrdinal(this string s1, string s2) => s1.EndsWith(s2, StringComparison.Ordinal);

	public static bool IsTransparent(this Color c) => c.A < byte.MaxValue;

	public static bool IsGrey(this Color c) => c.R == c.G && c.G == c.B;

	public static bool HasAlpha(this IIndexedPixelSource s)
	{
		foreach (uint c in s.Palette)
		{
			if (c < 0xff000000)
				return true;
		}

		return false;
	}

	public static bool IsGreyscale(this IIndexedPixelSource s)
	{
		foreach (uint c in s.Palette)
		{
			uint c0 = (byte)c;
			uint c1 = (byte)(c >> 8);
			uint c2 = (byte)(c >> 16);

			if (c0 != c1 || c1 != c2)
				return false;
		}

		return true;
	}

	public static void TryReturn<T>(this ArrayPool<T> pool, T[]? buff)
	{
		if (buff is not null)
			pool.Return(buff);
	}

	public static void FillBuffer(this Stream stm, Span<byte> buff)
	{
#if NET7_0_OR_GREATER
		stm.ReadExactly(buff);
#else
		int rem = buff.Length;
		while (rem > 0)
			rem -= stm.Read(buff[^rem..]);
#endif
	}

	public static int TryFillBuffer(this Stream stm, Span<byte> buff)
	{
#if NET7_0_OR_GREATER
		return stm.ReadAtLeast(buff, buff.Length, false);
#else
		int cb = buff.Length;
		int rb;
		do
		{
			rb = stm.Read(buff);
			buff = buff[rb..];
		}
		while (rb != 0 && buff.Length != 0);

		return cb - buff.Length;
#endif
	}

	public static Guid FinalizeToGuid<T>(this T hasher) where T : IBlake2Incremental
	{
		var hash = (Span<byte>)stackalloc byte[hasher.DigestLength];
		hasher.Finish(hash);

		return MemoryMarshal.Read<Guid>(hash);
	}

	public static TValue GetValueOrDefault<TKey, TValue>(this Dictionary<TKey, TValue> dic, TKey key, TValue defaultValue) where TKey : notnull where TValue : notnull =>
		dic.TryGetValue(key, out var value) ? value : defaultValue;

	public static TValue? GetValueOrDefault<TKey, TValue>(this IDictionary<TKey, TValue> dic, TKey key) where TKey : notnull =>
		dic.TryGetValue(key, out var value) ? value : default;

#if NETFRAMEWORK
	public static IDictionary<string, string> ToDictionary(this NameValueCollection nv) =>
		nv.AllKeys.Where(k => !string.IsNullOrEmpty(k)).ToDictionary(k => k, k => nv.GetValues(k).LastOrDefault(), StringComparer.OrdinalIgnoreCase);

	public static IDictionary<string, string> ToDictionary(this KeyValueConfigurationCollection kv) =>
		kv.AllKeys.Where(k => !string.IsNullOrEmpty(k)).ToDictionary(k => k, k => kv[k].Value, StringComparer.OrdinalIgnoreCase);

	public static string GetArchDirectory(this Assembly asm)
	{
		string dir = RuntimeInformation.ProcessArchitecture.ToString();
		if (asm.CodeBase?.StartsWithOrdinal("file:") ?? false)
			dir = Path.Combine(Path.GetDirectoryName(new Uri(asm.CodeBase).LocalPath), dir);

		return dir;
	}
#endif

	public static IDictionary<TKey, TValue> Coalesce<TKey, TValue>(this IDictionary<TKey, TValue> dic1, IDictionary<TKey, TValue> dic2) where TKey : notnull
	{
		foreach (var kv in dic2)
			dic1[kv.Key] = kv.Value;

		return dic1;
	}

#if !BUILTIN_SPAN
	public static int Read(this Stream stream, Span<byte> buffer)
	{
		if (stream is PoolBufferedStream ps)
			return ps.Read(buffer);

		using var buff = BufferPool.RentLocalArray<byte>(buffer.Length);

		int cb = stream.Read(buff.Array, 0, buff.Length);
		if (cb > 0)
			buff.Array.AsSpan(0, cb).CopyTo(buffer);

		return cb;
	}

	public static void Write(this Stream stream, ReadOnlySpan<byte> buffer)
	{
		if (stream is PoolBufferedStream ps)
		{
			ps.Write(buffer);
			return;
		}

		using var buff = BufferPool.RentLocalArray<byte>(buffer.Length);

		buffer.CopyTo(buff.Span);
		stream.Write(buff.Array, 0, buff.Length);
	}

	public static ArraySegment<T> Slice<T>(this ArraySegment<T> s, int index) => new(s.Array!, s.Offset + index, s.Count - index);

	public static ArraySegment<T> Slice<T>(this ArraySegment<T> s, int index, int count) => new(s.Array!, s.Offset + index, count);
#endif
}
