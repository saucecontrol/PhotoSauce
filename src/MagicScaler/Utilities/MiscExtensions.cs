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
		public static Orientation Clamp(this Orientation o) => o < Orientation.Normal? Orientation.Normal : o > Orientation.Rotate270 ? Orientation.Rotate270 : o;

		public static FrameDisposalMethod Clamp(this FrameDisposalMethod m) => m < FrameDisposalMethod.Preserve || m > FrameDisposalMethod.RestorePrevious ? FrameDisposalMethod.Preserve : m;

		public static bool SwapsDimensions(this Orientation o) => o > Orientation.FlipVertical;

		public static bool RequiresCache(this Orientation o) => o > Orientation.FlipHorizontal;

		public static bool FlipsX(this Orientation o) => o == Orientation.FlipHorizontal || o == Orientation.Rotate180 || o == Orientation.Rotate270 || o == Orientation.Transverse;

		public static bool FlipsY(this Orientation o) => o == Orientation.FlipVertical || o == Orientation.Rotate180 || o == Orientation.Rotate90 || o == Orientation.Transverse;

		public static Orientation Invert(this Orientation o) => o == Orientation.Rotate270 ? Orientation.Rotate90 : o == Orientation.Rotate90 ? Orientation.Rotate270 : o;

		public static bool IsSubsampledX(this ChromaSubsampleMode o) => o == ChromaSubsampleMode.Subsample420 || o == ChromaSubsampleMode.Subsample422;

		public static bool IsSubsampledY(this ChromaSubsampleMode o) => o == ChromaSubsampleMode.Subsample420 || o == ChromaSubsampleMode.Subsample440;

		public static bool EqualsInsensitive(this string s1, string s2) => string.Equals(s1, s2, StringComparison.OrdinalIgnoreCase);

		public static bool ContainsInsensitive(this IEnumerable<string> c, string s) => c.Contains(s, StringComparer.OrdinalIgnoreCase);

		public static bool IsTransparent(this Color c) => c.A < byte.MaxValue;

		public static bool IsGrey(this Color c) => c.R == c.G && c.G == c.B;

		public static void EnsureValidForInput(this Stream stm)
		{
			if (!stm.CanSeek || !stm.CanRead)
				throw new InvalidOperationException("Input Stream must allow Seek and Read.");

			if ((ulong)stm.Position >= (ulong)stm.Length)
				throw new InvalidOperationException("Input Stream is empty or positioned at its end.");
		}

		public static void EnsureValidForOutput(this Stream stm)
		{
			if (!stm.CanSeek || !stm.CanWrite)
				throw new InvalidOperationException("Output Stream must allow Seek and Write.");
		}

		public static void TryReturn<T>(this ArrayPool<T> pool, T[]? buff)
		{
			if (buff is not null)
				pool.Return(buff);
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
