using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;

#if NETFRAMEWORK
using System.Linq;
using System.Configuration;
using System.Collections.Specialized;
#endif

using PhotoSauce.Interop.Wic;

namespace PhotoSauce.MagicScaler
{
	internal static class MiscExtensions
	{
		public static WICBitmapTransformOptions ToWicTransformOptions(this Orientation o)
		{
			int orientation = (int)o;

			var opt = WICBitmapTransformOptions.WICBitmapTransformRotate0;
			if (orientation == 3 || orientation == 4)
				opt = WICBitmapTransformOptions.WICBitmapTransformRotate180;
			else if (orientation == 6 || orientation == 7)
				opt = WICBitmapTransformOptions.WICBitmapTransformRotate90;
			else if (orientation == 5 || orientation == 8)
				opt = WICBitmapTransformOptions.WICBitmapTransformRotate270;

			if (orientation == 2 || orientation == 4 || orientation == 5 || orientation == 7)
				opt |= WICBitmapTransformOptions.WICBitmapTransformFlipHorizontal;

			return opt;
		}

		public static bool SwapsDimensions(this Orientation o) => o > Orientation.FlipVertical;

		public static bool RequiresCache(this Orientation o) => o > Orientation.FlipHorizontal;

		public static bool FlipsX(this Orientation o) => o == Orientation.FlipHorizontal || o == Orientation.Rotate180 || o == Orientation.Rotate270 || o == Orientation.Transverse;

		public static bool FlipsY(this Orientation o) => o == Orientation.FlipVertical || o == Orientation.Rotate180 || o == Orientation.Rotate90 || o == Orientation.Transverse;

		public static Orientation Invert(this Orientation o) => o == Orientation.Rotate270 ? Orientation.Rotate90 : o == Orientation.Rotate90 ? Orientation.Rotate270 : o;

		public static bool IsSubsampledX(this WICJpegYCrCbSubsamplingOption o) => o == WICJpegYCrCbSubsamplingOption.WICJpegYCrCbSubsampling420 || o == WICJpegYCrCbSubsamplingOption.WICJpegYCrCbSubsampling422;

		public static bool IsSubsampledY(this WICJpegYCrCbSubsamplingOption o) => o == WICJpegYCrCbSubsamplingOption.WICJpegYCrCbSubsampling420 || o == WICJpegYCrCbSubsamplingOption.WICJpegYCrCbSubsampling440;

		public static bool InsensitiveEquals(this string s1, string s2) => string.Equals(s1, s2, StringComparison.OrdinalIgnoreCase);

		public static string GetFileExtension(this FileFormat fmt, string? preferredExtension = null)
		{
			if (fmt == FileFormat.Png8)
				fmt = FileFormat.Png;

			string ext = fmt.ToString().ToLower();
			if (!string.IsNullOrEmpty(preferredExtension))
			{
				if (preferredExtension[0] == '.')
					preferredExtension = preferredExtension.Substring(1);

				if (preferredExtension.InsensitiveEquals(ext) || (preferredExtension.InsensitiveEquals("jpg") && fmt == FileFormat.Jpeg) || (preferredExtension.InsensitiveEquals("tif") && fmt == FileFormat.Tiff))
					return preferredExtension;
			}

			return ext;
		}

		[return: MaybeNull]
		public static TValue GetValueOrDefault<TKey, TValue>(this IDictionary<TKey, TValue> dic, TKey key, TValue defaultValue = default) where TKey : notnull =>
			dic.TryGetValue(key, out var value) ? value : defaultValue;

		[return: MaybeNull]
		public static TValue GetValueOrDefault<TKey, TValue>(this IReadOnlyDictionary<TKey, TValue> dic, TKey key, TValue defaultValue = default) where TKey : notnull =>
			dic.TryGetValue(key, out var value) ? value : defaultValue;

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
	}
}
