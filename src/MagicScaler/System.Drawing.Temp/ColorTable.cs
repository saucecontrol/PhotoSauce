// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

#if !NET46
using System.Collections.Generic;
using System.Reflection;

namespace System.Drawing.Temp
{
    internal static class ColorTable
    {
        private static readonly Lazy<Dictionary<string, Color>> s_colorConstants = new Lazy<Dictionary<string, Color>>(GetColors);
        private static readonly Lazy<Dictionary<string, Color>> s_systemColorConstants = new Lazy<Dictionary<string, Color>>(GetSystemColors);

        private static Dictionary<string, Color> GetColors()
        {
            var dict = new Dictionary<string, Color>(StringComparer.OrdinalIgnoreCase);
            FillConstants(dict, typeof(Color));
            return dict;
        }

        private static Dictionary<string, Color> GetSystemColors()
        {
            var dict = new Dictionary<string, Color>(StringComparer.OrdinalIgnoreCase);
            FillConstants(dict, typeof(SystemColors));
            return dict;
        }

        internal static Dictionary<string, Color> Colors => s_colorConstants.Value;

        internal static Dictionary<string, Color> SystemColors => s_systemColorConstants.Value;

        private static void FillConstants(Dictionary<string, Color> colors, Type enumType)
        {
            bool systemColors = enumType.Equals(typeof(SystemColors));
            int end = (int)KnownColor.MenuHighlight + 1;
            for (int i = 1; i < end; i ++)
            {
                bool systemColor = i < (int)KnownColor.Transparent || i > (int)KnownColor.YellowGreen;
                if (systemColor == systemColors)
                    colors[KnownColorTable.KnownColorToName((KnownColor)i)] = new Color((KnownColor)i);
            }
        }

        internal static bool TryGetNamedColor(string name, out Color result) =>
            Colors.TryGetValue(name, out result) || SystemColors.TryGetValue(name, out result);
    }
}
#endif