// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

#if !NET46
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;

namespace System.Drawing.Temp
{
    public class ColorConverter : TypeConverter
    {
#if NETCOREAPP10
        private static readonly Lazy<StandardValuesCollection> s_valuesLazy = new Lazy<StandardValuesCollection>(() => {
            // We must take the value from each hashtable and combine them.
            //
            HashSet<Color> set = new HashSet<Color>(ColorTable.Colors.Values);

            return new StandardValuesCollection(set.OrderBy(c => c, new ColorComparer()).ToList());
        });
#endif

        public ColorConverter()
        {
        }

        public override bool CanConvertFrom(ITypeDescriptorContext context, Type sourceType)
        {
            if (sourceType == typeof(string))
            {
                return true;
            }
            return base.CanConvertFrom(context, sourceType);
        }

        public override bool CanConvertTo(ITypeDescriptorContext context, Type destinationType)
        {
            return base.CanConvertTo(context, destinationType);
        }

        [SuppressMessage("Microsoft.Performance", "CA1808:AvoidCallsThatBoxValueTypes")]
        public override object ConvertFrom(ITypeDescriptorContext context, CultureInfo culture, object value)
        {
            string strValue = value as string;
            if (strValue != null)
            {
                return ColorConverterCommon.ConvertFromString(strValue, culture);
            }
            return base.ConvertFrom(context, culture, value);
        }

        public override object ConvertTo(ITypeDescriptorContext context, CultureInfo culture, object value, Type destinationType)
        {
            if (destinationType == null)
            {
                throw new ArgumentNullException(nameof(destinationType));
            }

            if (value is Color)
            {
                if (destinationType == typeof(string))
                {
                    Color c = (Color)value;

                    if (c == Color.Empty)
                    {
                        return string.Empty;
                    }
                    else
                    {
                        // If this is a known color, then Color can provide its own
                        // name.  Otherwise, we fabricate an ARGB value for it.
                        //
                        if (ColorTable.IsKnownNamedColor(c.Name))
                        {
                            return c.Name;
                        }
                        else if (c.IsNamedColor)
                        {
                            return "'" + c.Name + "'";
                        }
                        else
                        {
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

                            // Now slam all of these together with the fantastic Join
                            // method.
                            //
                            return string.Join(sep, args);
                        }
                    }
                }
            }

            return base.ConvertTo(context, culture, value, destinationType);
        }
#if NETCOREAPP10
        public override StandardValuesCollection GetStandardValues(ITypeDescriptorContext context)
        {
            return s_valuesLazy.Value;
        }

        public override bool GetStandardValuesSupported(ITypeDescriptorContext context)
        {
            return true;
        }
#endif
        private class ColorComparer : IComparer<Color>
        {
            public int Compare(Color left, Color right)
            {
                return string.CompareOrdinal(left.Name, right.Name);
            }
        }
    }
}
#endif