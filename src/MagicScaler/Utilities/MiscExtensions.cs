using System;
using System.Linq;
using System.Drawing;
using System.Collections.Generic;

#if NET46
using System.Configuration;
using System.Collections.Specialized;
#endif

using PhotoSauce.MagicScaler.Interop;

namespace PhotoSauce.MagicScaler
{
	internal static class MiscExtensions
	{
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

		public static bool SwapDimensions(this Orientation o) => o > Orientation.FlipVertical;

		public static bool RequiresCache(this Orientation o) => o > Orientation.FlipHorizontal;

		public static ArraySegment<T> Zero<T>(this ArraySegment<T> a)
		{
			Array.Clear(a.Array, a.Offset, a.Count);
			return a;
		}

		public static TValue GetValueOrDefault<TKey, TValue>(this IDictionary<TKey, TValue> dic, TKey key, Func<TValue> valueFactory = null)
		{
			return dic.TryGetValue(key, out var value) ? value : valueFactory == null ? default(TValue) : valueFactory();
		}

#if NET46
		public static IDictionary<string, string> ToDictionary(this NameValueCollection nv)
		{
			return nv.AllKeys.Where(k => !string.IsNullOrEmpty(k)).ToDictionary(k => k, k => nv.GetValues(k).LastOrDefault(), StringComparer.OrdinalIgnoreCase);
		}

		public static IDictionary<string, string> ToDictionary(this KeyValueConfigurationCollection kv)
		{
			return kv.AllKeys.Where(k => !string.IsNullOrEmpty(k)).ToDictionary(k => k, k => kv[k].Value, StringComparer.OrdinalIgnoreCase);
		}
#endif

		public static IDictionary<TKey, TValue> Coalesce<TKey, TValue>(this IDictionary<TKey, TValue> dic1, IDictionary<TKey, TValue> dic2)
		{
			dic2.ToList().ForEach(i => dic1[i.Key] = i.Value);
			return dic1;
		}
	}
}
