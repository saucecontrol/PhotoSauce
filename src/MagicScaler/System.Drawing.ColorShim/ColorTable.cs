// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

#pragma warning disable IDE1006 // Naming Styles

#if DRAWING_SHIM_COLORCONVERTER
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace System.Drawing.ColorShim
{
    internal static class ColorTable
    {
        private static readonly Lazy<Dictionary<string, Color>> s_colorConstants = new Lazy<Dictionary<string, Color>>(GetColors);

        private static Dictionary<string, Color> GetColors()
        {
            var colors = new Dictionary<string, Color>(StringComparer.OrdinalIgnoreCase);

            foreach (var prop in typeof(Color).GetProperties(BindingFlags.Public | BindingFlags.Static).Where(p => p.PropertyType == typeof(Color)))
                colors[prop.Name] = (Color)prop.GetValue(null);

            return colors;
        }

        internal static Dictionary<string, Color> Colors => s_colorConstants.Value;

        internal static bool TryGetNamedColor(string name, out Color result) => Colors.TryGetValue(name, out result);

        internal static bool IsKnownNamedColor(string name) => Colors.ContainsKey(name);
    }
}
#endif