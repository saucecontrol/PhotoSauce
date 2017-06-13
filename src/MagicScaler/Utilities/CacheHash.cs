using System;
using System.Text;
using System.Security.Cryptography;

namespace PhotoSauce.MagicScaler
{
	internal static class CacheHash
	{
		private static readonly char[] base32Table = { 'A','B','C','D','E','F','G','H','I','J','K','L','M','N','O','P',
		                                               'Q','R','S','T','U','V','W','X','Y','Z','2','3','4','5','6','7' };

		// first 40 bits from the crypto hash, base32 encoded
		// https://tools.ietf.org/html/rfc4648#section-6
		private static string encode(byte[] bhash)
		{
			char[] hash = new char[8], b32 = base32Table;
			hash[0] = b32[  bhash[0]         >> 3];
			hash[1] = b32[((bhash[0] & 0x07) << 2) | (bhash[1] >> 6)];
			hash[2] = b32[( bhash[1] & 0x3e) >> 1];
			hash[3] = b32[((bhash[1] & 0x01) << 4) | (bhash[2] >> 4)];
			hash[4] = b32[((bhash[2] & 0x0f) << 1) | (bhash[3] >> 7)];
			hash[5] = b32[( bhash[3] & 0x7c) >> 2];
			hash[6] = b32[((bhash[3] & 0x03) << 3) | (bhash[4] >> 5)];
			hash[7] = b32[  bhash[4] & 0x1f];

			return new string(hash);
		}

		public static string Create(ArraySegment<byte> data)
		{
			using (var alg = SHA256.Create())
				return encode(alg.ComputeHash(data.Array, data.Offset, data.Count));
		}

		public static string Create(string data) => Create(new ArraySegment<byte>(Encoding.UTF8.GetBytes(data)));
	}
}
