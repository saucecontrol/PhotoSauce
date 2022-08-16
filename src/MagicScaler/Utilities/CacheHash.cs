// Copyright © Clinton Ingram and Contributors.  Licensed under the MIT License.

using System;
using System.Runtime.InteropServices;
#if !BUILTIN_SPAN
using System.Runtime.CompilerServices;
#endif

using Blake2Fast;

namespace PhotoSauce.MagicScaler;

internal static class CacheHash
{
	public const int DigestLength = 5;

	private static ReadOnlySpan<byte> base32Table => "ABCDEFGHIJKLMNOPQRSTUVWXYZ234567"u8;

	// first 40 bits from the crypto hash, base32 encoded
	// https://tools.ietf.org/html/rfc4648#section-6
#if !BUILTIN_SPAN
	unsafe
#endif
	public static string Encode(ReadOnlySpan<byte> bhash)
	{
		if (DigestLength > (uint)bhash.Length)
			throw new ArgumentException($"Hash must be at least {DigestLength} bytes");

		var b32 = base32Table;
		var hash = (Span<char>)stackalloc char[8];

		hash[0] = (char)b32[  bhash[0]         >> 3];
		hash[1] = (char)b32[((bhash[0] & 0x07) << 2) | (bhash[1] >> 6)];
		hash[2] = (char)b32[( bhash[1] & 0x3e) >> 1];
		hash[3] = (char)b32[((bhash[1] & 0x01) << 4) | (bhash[2] >> 4)];
		hash[4] = (char)b32[((bhash[2] & 0x0f) << 1) | (bhash[3] >> 7)];
		hash[5] = (char)b32[( bhash[3] & 0x7c) >> 2];
		hash[6] = (char)b32[((bhash[3] & 0x03) << 3) | (bhash[4] >> 5)];
		hash[7] = (char)b32[  bhash[4] & 0x1f];

#if BUILTIN_SPAN
		return new string(hash);
#else
		return new string((char*)Unsafe.AsPointer(ref MemoryMarshal.GetReference(hash)), 0, 8);
#endif
	}

	public static string Create(string data)
	{
		var hash = (Span<byte>)stackalloc byte[DigestLength];
		Blake2b.ComputeAndWriteHash(DigestLength, MemoryMarshal.AsBytes(data.AsSpan()), hash);

		return Encode(hash);
	}
}
