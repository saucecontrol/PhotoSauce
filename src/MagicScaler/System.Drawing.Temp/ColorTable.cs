// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

#pragma warning disable IDE1006 // Naming Styles

#if DRAWING_SHIM_COLORCONVERTER
using System.Reflection;
using System.Collections.Generic;

namespace System.Drawing.ColorShim
{
    internal static class ColorTable
    {
        private static readonly Lazy<Dictionary<string, Color>> s_colorConstants = new Lazy<Dictionary<string, Color>>(GetColors);

        private static Dictionary<string, Color> GetColors()
        {
            var dict = new Dictionary<string, Color>(StringComparer.OrdinalIgnoreCase);
            FillConstants(dict);
            return dict;
        }

        internal static Dictionary<string, Color> Colors => s_colorConstants.Value;

        private static void FillConstants(Dictionary<string, Color> colors)
        {
#if DRAWING_SHIM_COLOR
            int kcfirst = (int)KnownColor.Transparent;
            int kclast = (int)KnownColor.YellowGreen;
#else
            var ctype = typeof(Color);
            var kctype = ctype.Assembly.GetType("System.Drawing.KnownColor");
            var kcttype = ctype.Assembly.GetType("System.Drawing.KnownColorTable");

            var cconst = ctype.GetConstructor(BindingFlags.Instance | BindingFlags.NonPublic, null, new[] { kctype }, null);
            var kctoname = kcttype.GetMethod("KnownColorToName", BindingFlags.Static | BindingFlags.Public);
            var kcvals = Enum.GetValues(kctype);

            int kcfirst = 26; //KnownColor.Transparent - 1
            int kclast = 166; //KnownColor.YellowGreen - 1
#endif

            for (int i = kcfirst; i <= kclast; i++)
            {
#if DRAWING_SHIM_COLOR
                colors[KnownColorTable.KnownColorToName((KnownColor)i)] = new Color((KnownColor)i);
#else
                var cinst = cconst.Invoke(new[] { kcvals.GetValue(i) });
                var kcname = kctoname.Invoke(null, new[] { kcvals.GetValue(i) });
                colors[(string)kcname] = (Color)cinst;
#endif
            }
        }

        internal static bool TryGetNamedColor(string name, out Color result) =>
            Colors.TryGetValue(name, out result);

        internal static bool IsKnownNamedColor(string name)
        {
            return Colors.TryGetValue(name, out _);
        }
    }
}
#endif