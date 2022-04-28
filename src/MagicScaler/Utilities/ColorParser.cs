// Copyright © Clinton Ingram and Contributors.  Licensed under the MIT License.

using System;
using System.Linq;
using System.Drawing;
using System.Reflection;
using System.Globalization;
using System.Collections.Generic;

namespace PhotoSauce.MagicScaler
{
	internal static class ColorParser
	{
		private static readonly Lazy<Dictionary<string, Color>> namedColors = new(() =>
			typeof(Color)
				.GetProperties(BindingFlags.Public | BindingFlags.Static)
				.Where(p => p.PropertyType == typeof(Color))
				.ToDictionary(p => p.Name, p => (Color)p.GetValue(null)!, StringComparer.OrdinalIgnoreCase)
		);

		public static bool TryParse(string value, out Color color)
		{
			color = Color.Empty;

			if (string.IsNullOrWhiteSpace(value))
				return false;

			if (namedColors.Value.TryGetValue(value, out color))
				return true;

			if (value[0] == '#')
				value = value.Substring(1);

			if (value.Length != 6 && value.Length != 8)
				return false;

			if (!uint.TryParse(value, NumberStyles.HexNumber, NumberFormatInfo.InvariantInfo, out uint ival))
				return false;

			color = Color.FromArgb((int)(value.Length == 6 ? 0xff000000 | ival : ival));
			return true;
		}
	}
}
