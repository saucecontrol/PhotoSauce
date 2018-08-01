// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

#if DRAWING_SHIM_COLORCONVERTER
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Linq;

namespace System.Drawing.ColorShim
{
    internal class ColorConverter : TypeConverter
    {
#if !NETSTANDARD1_3
        private static readonly Lazy<StandardValuesCollection> s_valuesLazy = new Lazy<StandardValuesCollection>(() =>
        {
            // We must take the value from each hashtable and combine them.
            var set = new HashSet<Color>(ColorTable.Colors.Values);
            return new StandardValuesCollection(set.OrderBy(c => c, new ColorComparer()).ToList());
        });
#endif

        public ColorConverter()
        {
        }

        public override bool CanConvertFrom(ITypeDescriptorContext context, Type sourceType)
        {
            return sourceType == typeof(string) || base.CanConvertFrom(context, sourceType);
        }

        public override bool CanConvertTo(ITypeDescriptorContext context, Type destinationType)
        {
            return base.CanConvertTo(context, destinationType);
        }

        [SuppressMessage("Microsoft.Performance", "CA1808:AvoidCallsThatBoxValueTypes")]
        public override object ConvertFrom(ITypeDescriptorContext context, CultureInfo culture, object value)
        {
            if (value is string strValue)
            {
                return ColorConverterCommon.ConvertFromString(strValue, culture ?? CultureInfo.CurrentCulture);
            }

            return base.ConvertFrom(context, culture, value);
        }

        public override object ConvertTo(ITypeDescriptorContext context, CultureInfo culture, object value, Type destinationType)
        {
            if (destinationType == null)
            {
                throw new ArgumentNullException(nameof(destinationType));
            }

            if (value is Color c)
            {
                if (destinationType == typeof(string))
                {
                    if (c == Color.Empty)
                    {
                        return string.Empty;
                    }
                    
                    // If this is a known color, then Color can provide its own name.
                    // Otherwise, we fabricate an ARGB value for it.
                    if (ColorTable.IsKnownNamedColor(c.Name))
                    {
                        return c.Name;
                    }
                    else if (c.IsNamedColor)
                    {
                        return "'" + c.Name + "'";
                    }

                    if (culture == null)
                    {
                        culture = CultureInfo.CurrentCulture;
                    }

                    string sep = culture.TextInfo.ListSeparator + " ";
                    TypeConverter intConverter = TypeDescriptor.GetConverter(typeof(int));
                    string[] args;
                    int nArg = 0;

                    if (c.A < 255)
                    {
                        args = new string[4];
                        args[nArg++] = intConverter.ConvertToString(context, culture, (object)c.A);
                    }
                    else
                    {
                        args = new string[3];
                    }

                    // Note: ConvertToString will raise exception if value cannot be converted.
                    args[nArg++] = intConverter.ConvertToString(context, culture, (object)c.R);
                    args[nArg++] = intConverter.ConvertToString(context, culture, (object)c.G);
                    args[nArg++] = intConverter.ConvertToString(context, culture, (object)c.B);

                    return string.Join(sep, args);
                }
            }

            return base.ConvertTo(context, culture, value, destinationType);
        }
#if !NETSTANDARD1_3
        public override StandardValuesCollection GetStandardValues(ITypeDescriptorContext context)
        {
            return s_valuesLazy.Value;
        }

        public override bool GetStandardValuesSupported(ITypeDescriptorContext context) => true;
#endif
        private class ColorComparer : IComparer<Color>
        {
            public int Compare(Color left, Color right) => string.CompareOrdinal(left.Name, right.Name);
        }
    }
}
#endif