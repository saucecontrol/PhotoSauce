// Copyright © Clinton Ingram and Contributors.  Licensed under the MIT License.

using System;
using System.IO;
using System.Linq;
using System.Buffers;
using System.Drawing;
using System.Collections.Generic;
using System.Runtime.InteropServices;

#if NETFRAMEWORK
using System.Configuration;
using System.Collections.Specialized;
#endif

using Blake2Fast;

namespace PhotoSauce.MagicScaler
{
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

		public static void TryReturn<T>(this ArrayPool<T> pool, T[]? buff)
		{
			if (buff is not null)
				pool.Return(buff);
		}

		public static void FillBuffer(this Stream stm, Span<byte> buff)
		{
			int rem = buff.Length;
			while (rem > 0)
				rem -= stm.Read(buff[^rem..]);
		}

		public static int TryFillBuffer(this Stream stm, Span<byte> buff)
		{
			int cb = buff.Length;
			int rb;
			do
			{
				rb = stm.Read(buff);
				buff = buff[rb..];
			}
			while (rb != 0 && buff.Length != 0);

			return cb - buff.Length;
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
}
