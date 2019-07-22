using System;
using System.Linq;
using System.Drawing;
using System.Diagnostics;
using System.Collections.Generic;

#if NETFRAMEWORK
using System.Configuration;
using System.Collections.Specialized;
#endif

using PhotoSauce.MagicScaler.Interop;

namespace PhotoSauce.MagicScaler
{
	internal static class MiscExtensions
	{
		public static WICRect FromGdiRect(this WICRect wr, in Rectangle r)
		{
			wr.X = r.X;
			wr.Y = r.Y;
			wr.Width = r.Width;
			wr.Height = r.Height;

			return wr;
		}

		public static WICRect ToWicRect(this Rectangle r) => new WICRect { X = r.X, Y = r.Y, Width = r.Width, Height = r.Height };

		public static Rectangle ToGdiRect(this WICRect r) => new Rectangle(r.X, r.Y, r.Width, r.Height);

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

		public static bool RequiresDimensionSwap(this Orientation o) => o > Orientation.FlipVertical;

		public static bool RequiresCache(this Orientation o) => o > Orientation.FlipHorizontal;

		public static bool InsensitiveEquals(this string s1, string s2) => string.Equals(s1, s2, StringComparison.OrdinalIgnoreCase);

		public static string GetFileExtension(this FileFormat fmt, string preferredExtension = null)
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

		public static ArraySegment<T> Clear<T>(this ArraySegment<T> a)
		{
			Array.Clear(a.Array, a.Offset, a.Count);
			return a;
		}

		public static TValue GetValueOrDefault<TKey, TValue>(this IDictionary<TKey, TValue> dic, TKey key, TValue defaultValue) =>
			dic.TryGetValue(key, out var value) ? value : defaultValue;

		public static TValue GetValueOrDefault<TKey, TValue>(this IDictionary<TKey, TValue> dic, TKey key, Func<TValue> valueFactory = null) =>
			dic.TryGetValue(key, out var value) ? value : valueFactory is null ? default : valueFactory();

#if NETFRAMEWORK
		public static IDictionary<string, string> ToDictionary(this NameValueCollection nv) =>
			nv.AllKeys.Where(k => !string.IsNullOrEmpty(k)).ToDictionary(k => k, k => nv.GetValues(k).LastOrDefault(), StringComparer.OrdinalIgnoreCase);

		public static IDictionary<string, string> ToDictionary(this KeyValueConfigurationCollection kv) =>
			kv.AllKeys.Where(k => !string.IsNullOrEmpty(k)).ToDictionary(k => k, k => kv[k].Value, StringComparer.OrdinalIgnoreCase);
#endif

		public static IDictionary<TKey, TValue> Coalesce<TKey, TValue>(this IDictionary<TKey, TValue> dic1, IDictionary<TKey, TValue> dic2)
		{
			foreach (var kv in dic2)
				dic1[kv.Key] = kv.Value;

			return dic1;
		}

		public static double ElapsedMilliseconds(this Stopwatch s) => (double)s.ElapsedTicks / Stopwatch.Frequency * 1000;
	}
}
