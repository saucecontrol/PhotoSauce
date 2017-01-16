using System;
using System.Linq;
using System.Configuration;
using System.Collections.Generic;
using System.Collections.Specialized;

namespace PhotoSauce.MagicScaler
{
	internal static class MiscExtensions
	{
		public static TValue GetValueOrDefault<TKey, TValue>(this IDictionary<TKey, TValue> dic, TKey key, Func<TValue> valueFactory = null)
		{
			TValue value;
			return dic.TryGetValue(key, out value) ? value : valueFactory == null ? default(TValue) : valueFactory();
		}

		public static IDictionary<string, string> ToDictionary(this NameValueCollection nv)
		{
			return nv.AllKeys.Where(k => !string.IsNullOrEmpty(k)).ToDictionary(k => k, k => nv.GetValues(k).LastOrDefault(), StringComparer.OrdinalIgnoreCase);
		}

		public static IDictionary<string, string> ToDictionary(this KeyValueConfigurationCollection kv)
		{
			return kv.AllKeys.Where(k => !string.IsNullOrEmpty(k)).ToDictionary(k => k, k => kv[k].Value, StringComparer.OrdinalIgnoreCase);
		}

		public static IDictionary<TKey, TValue> Coalesce<TKey, TValue>(this IDictionary<TKey, TValue> dic1, IDictionary<TKey, TValue> dic2)
		{
			dic2.ToList().ForEach(i => dic1[i.Key] = i.Value);
			return dic1;
		}
	}
}
