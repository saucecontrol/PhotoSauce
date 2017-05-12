using System;
using System.Text;
using System.Collections.Generic;
using System.Collections.ObjectModel;

using PhotoSauce.MagicScaler.Interop;
using System.Runtime.InteropServices;

namespace PhotoSauce.MagicScaler
{
	internal enum PixelNumericRepresentation
	{
		Unspecified = 0,
		Indexed = 1,
		UnsignedInteger = 2,
		SignedInteger = 3,
		Fixed = 4,
		Float = 5
	}

	internal enum PixelColorRepresentation
	{
		Unspecified,
		Grey,
		Bgr,
		Rgb,
		Cmyk
	}

	internal enum PixelAlphaRepresentation
	{
		None,
		Associated,
		Unassociated
	}

	internal enum PixelColorspace
	{
		Unspecified,
		sRgb,
		scRgb,
		LinearRgb
	}

	internal struct PixelFormat : IEquatable<PixelFormat>
	{
		public Guid FormatGuid;
		public string Name;
		public bool IsWicNative;
		public int BitsPerPixel;
		public int ChannelCount;
		public PixelNumericRepresentation NumericRepresentation;
		public PixelColorRepresentation ColorRepresentation;
		public PixelAlphaRepresentation AlphaRepresentation;
		public PixelColorspace Colorspace;

		public int ColorChannelCount => ChannelCount - (AlphaRepresentation == PixelAlphaRepresentation.None ? 0 : 1);

		public bool Equals(PixelFormat other) => FormatGuid == other.FormatGuid;

		public static bool operator ==(PixelFormat left, PixelFormat right) => left.Equals(right);
		public static bool operator !=(PixelFormat left, PixelFormat right) => !left.Equals(right);

		public override bool Equals(object o) => o is PixelFormat pf ? Equals(pf) : false;
		public override int GetHashCode() => FormatGuid.GetHashCode();

		public static readonly PixelFormat Grey16BppLinearUQ15 = new PixelFormat {
			FormatGuid = new Guid(0xC175220D, 0x375B, 0x48C9, 0x8D, 0xD9, 0x1D, 0x28, 0x24, 0xFE, 0x88, 0xA0),
			Name = "16bpp Grey UQ15 Linear",
			BitsPerPixel = 16,
			ChannelCount = 1,
			NumericRepresentation = PixelNumericRepresentation.Fixed,
			ColorRepresentation = PixelColorRepresentation.Grey,
			Colorspace = PixelColorspace.LinearRgb
		};

		public static readonly PixelFormat Grey32BppLinearFloat = new PixelFormat {
			FormatGuid = new Guid(0xC175220D, 0x375B, 0x48C9, 0x8D, 0xD9, 0x1D, 0x28, 0x24, 0xFE, 0x88, 0xA1),
			Name = "32bpp Grey Float Linear",
			BitsPerPixel = 32,
			ChannelCount = 1,
			NumericRepresentation = PixelNumericRepresentation.Float,
			ColorRepresentation = PixelColorRepresentation.Grey,
			Colorspace = PixelColorspace.LinearRgb
		};

		public static readonly PixelFormat Bgr48BppLinearUQ15 = new PixelFormat {
			FormatGuid = new Guid(0xC175220D, 0x375B, 0x48C9, 0x8D, 0xD9, 0x1D, 0x28, 0x24, 0xFE, 0x88, 0xA2),
			Name = "48bpp BGR UQ15 Linear",
			BitsPerPixel = 48,
			ChannelCount = 3,
			NumericRepresentation = PixelNumericRepresentation.Fixed,
			ColorRepresentation = PixelColorRepresentation.Bgr,
			Colorspace = PixelColorspace.LinearRgb
		};

		public static readonly PixelFormat Bgr96BppFloat = new PixelFormat {
			FormatGuid = new Guid(0xC175220D, 0x375B, 0x48C9, 0x8D, 0xD9, 0x1D, 0x28, 0x24, 0xFE, 0x88, 0xA3),
			Name = "96bpp BGR Float",
			BitsPerPixel = 96,
			ChannelCount = 3,
			NumericRepresentation = PixelNumericRepresentation.Float,
			ColorRepresentation = PixelColorRepresentation.Bgr,
			Colorspace = PixelColorspace.sRgb
		};

		public static readonly PixelFormat Bgr96BppLinearFloat = new PixelFormat {
			FormatGuid = new Guid(0xC175220D, 0x375B, 0x48C9, 0x8D, 0xD9, 0x1D, 0x28, 0x24, 0xFE, 0x88, 0xA4),
			Name = "96bpp BGR Float Linear",
			BitsPerPixel = 96,
			ChannelCount = 3,
			NumericRepresentation = PixelNumericRepresentation.Float,
			ColorRepresentation = PixelColorRepresentation.Bgr,
			Colorspace = PixelColorspace.LinearRgb
		};

		public static readonly PixelFormat Bgra64BppLinearUQ15 = new PixelFormat {
			FormatGuid = new Guid(0xC175220D, 0x375B, 0x48C9, 0x8D, 0xD9, 0x1D, 0x28, 0x24, 0xFE, 0x88, 0xA5),
			Name = "64bpp BGRA UQ15 Linear",
			BitsPerPixel = 64,
			ChannelCount = 4,
			NumericRepresentation = PixelNumericRepresentation.Fixed,
			ColorRepresentation = PixelColorRepresentation.Bgr,
			AlphaRepresentation = PixelAlphaRepresentation.Unassociated,
			Colorspace = PixelColorspace.LinearRgb
		};

		public static readonly PixelFormat Pbgra64BppLinearUQ15 = new PixelFormat {
			FormatGuid = new Guid(0xC175220D, 0x375B, 0x48C9, 0x8D, 0xD9, 0x1D, 0x28, 0x24, 0xFE, 0x88, 0xA6),
			Name = "64bpp pBGRA UQ15 Linear",
			BitsPerPixel = 64,
			ChannelCount = 4,
			NumericRepresentation = PixelNumericRepresentation.Fixed,
			ColorRepresentation = PixelColorRepresentation.Bgr,
			AlphaRepresentation = PixelAlphaRepresentation.Associated,
			Colorspace = PixelColorspace.LinearRgb
		};

		public static readonly PixelFormat Pbgra128BppFloat = new PixelFormat {
			FormatGuid = new Guid(0xC175220D, 0x375B, 0x48C9, 0x8D, 0xD9, 0x1D, 0x28, 0x24, 0xFE, 0x88, 0xA7),
			Name = "128bpp pBGRA Float",
			BitsPerPixel = 128,
			ChannelCount = 4,
			NumericRepresentation = PixelNumericRepresentation.Float,
			ColorRepresentation = PixelColorRepresentation.Bgr,
			AlphaRepresentation = PixelAlphaRepresentation.Associated,
			Colorspace = PixelColorspace.sRgb
		};

		public static readonly PixelFormat Pbgra128BppLinearFloat = new PixelFormat {
			FormatGuid = new Guid(0xC175220D, 0x375B, 0x48C9, 0x8D, 0xD9, 0x1D, 0x28, 0x24, 0xFE, 0x88, 0xA8),
			Name = "128bpp pBGRA Float Linear",
			BitsPerPixel = 128,
			ChannelCount = 4,
			NumericRepresentation = PixelNumericRepresentation.Float,
			ColorRepresentation = PixelColorRepresentation.Bgr,
			AlphaRepresentation = PixelAlphaRepresentation.Associated,
			Colorspace = PixelColorspace.LinearRgb
		};

		public static readonly PixelFormat Y16BppLinearUQ15 = new PixelFormat {
			FormatGuid = new Guid(0xC175220D, 0x375B, 0x48C9, 0x8D, 0xD9, 0x1D, 0x28, 0x24, 0xFE, 0x88, 0xA9),
			Name = "16bpp Y UQ15 Linear",
			BitsPerPixel = 16,
			ChannelCount = 1,
			NumericRepresentation = PixelNumericRepresentation.Fixed,
			ColorRepresentation = PixelColorRepresentation.Grey,
			Colorspace = PixelColorspace.LinearRgb
		};

		public static readonly PixelFormat Y32BppFloat = new PixelFormat {
			FormatGuid = new Guid(0xC175220D, 0x375B, 0x48C9, 0x8D, 0xD9, 0x1D, 0x28, 0x24, 0xFE, 0x88, 0xAA),
			Name = "32bpp Y Float",
			BitsPerPixel = 32,
			ChannelCount = 1,
			NumericRepresentation = PixelNumericRepresentation.Float,
			ColorRepresentation = PixelColorRepresentation.Grey,
			Colorspace = PixelColorspace.sRgb
		};

		public static readonly PixelFormat Y32BppLinearFloat = new PixelFormat {
			FormatGuid = new Guid(0xC175220D, 0x375B, 0x48C9, 0x8D, 0xD9, 0x1D, 0x28, 0x24, 0xFE, 0x88, 0xAB),
			Name = "32bpp Y Float Linear",
			BitsPerPixel = 32,
			ChannelCount = 1,
			NumericRepresentation = PixelNumericRepresentation.Float,
			ColorRepresentation = PixelColorRepresentation.Grey,
			Colorspace = PixelColorspace.LinearRgb
		};

		public static readonly PixelFormat CbCr64BppFloat = new PixelFormat {
			FormatGuid = new Guid(0xC175220D, 0x375B, 0x48C9, 0x8D, 0xD9, 0x1D, 0x28, 0x24, 0xFE, 0x88, 0xAC),
			Name = "64bpp CbCr Float",
			BitsPerPixel = 64,
			ChannelCount = 2,
			NumericRepresentation = PixelNumericRepresentation.Float
		};

		public static ReadOnlyDictionary<Guid, PixelFormat> Cache;

		static PixelFormat()
		{
			var dic = new Dictionary<Guid, PixelFormat> {
				[Grey16BppLinearUQ15.FormatGuid]    = Grey16BppLinearUQ15,
				[Grey32BppLinearFloat.FormatGuid]   = Grey32BppLinearFloat,
				[Bgr48BppLinearUQ15.FormatGuid]     = Bgr48BppLinearUQ15,
				[Bgr96BppFloat.FormatGuid]          = Bgr96BppFloat,
				[Bgr96BppLinearFloat.FormatGuid]    = Bgr96BppLinearFloat,
				[Bgra64BppLinearUQ15.FormatGuid]    = Bgra64BppLinearUQ15,
				[Pbgra64BppLinearUQ15.FormatGuid]   = Pbgra64BppLinearUQ15,
				[Pbgra128BppFloat.FormatGuid]       = Pbgra128BppFloat,
				[Pbgra128BppLinearFloat.FormatGuid] = Pbgra128BppLinearFloat,
				[Y16BppLinearUQ15.FormatGuid]       = Y16BppLinearUQ15,
				[Y32BppFloat.FormatGuid]            = Y32BppFloat,
				[Y32BppLinearFloat.FormatGuid]      = Y32BppLinearFloat,
				[CbCr64BppFloat.FormatGuid]         = CbCr64BppFloat,
			};

			uint fet = 10u;
			var wic = new WICImagingFactory2() as IWICImagingFactory;
			var cen = wic.CreateComponentEnumerator(WICComponentType.WICPixelFormat, WICComponentEnumerateOptions.WICComponentEnumerateDefault);
			var oar = new object[fet];
			do
			{
				fet = cen.Next(fet, oar);
				for (int i = 0; i < fet; i++)
				{
					var pix = oar[i] as IWICPixelFormatInfo2;
					uint cch = pix.GetFriendlyName(0, null);
					var sbn = new StringBuilder((int)cch);
					pix.GetFriendlyName(cch, sbn);
					var pfn = sbn.ToString();

					var fmt = new PixelFormat {
						FormatGuid = pix.GetFormatGUID(),
						Name = pfn,
						IsWicNative = true,
						BitsPerPixel = (int)pix.GetBitsPerPixel(),
						ChannelCount = (int)pix.GetChannelCount(),
						NumericRepresentation = (PixelNumericRepresentation)pix.GetNumericRepresentation(),
						ColorRepresentation = pfn.Contains("BGR") ? PixelColorRepresentation.Bgr :
						                      pfn.Contains("RGB") ? PixelColorRepresentation.Rgb :
						                      pfn.Contains("CMYK") ? PixelColorRepresentation.Cmyk :
						                      pfn.Contains("Gray") || pfn.EndsWith(" Y") ? PixelColorRepresentation.Grey :
						                      PixelColorRepresentation.Unspecified,
						AlphaRepresentation = pfn.Contains("pBGRA") || pfn.Contains("pRGBA") ? PixelAlphaRepresentation.Associated :
						                      pix.SupportsTransparency() ? PixelAlphaRepresentation.Unassociated :
						                      PixelAlphaRepresentation.None
					};

					Marshal.ReleaseComObject(pix);

					if (fmt.ColorRepresentation == PixelColorRepresentation.Grey || fmt.ColorRepresentation == PixelColorRepresentation.Bgr || fmt.ColorRepresentation == PixelColorRepresentation.Rgb)
						fmt.Colorspace = fmt.NumericRepresentation == PixelNumericRepresentation.Fixed || fmt.NumericRepresentation == PixelNumericRepresentation.Float ? PixelColorspace.scRgb : PixelColorspace.sRgb;

					dic.Add(fmt.FormatGuid, fmt);
				}
			} while (fet > 0);

			Marshal.ReleaseComObject(cen);
			Marshal.ReleaseComObject(wic);

			Cache = new ReadOnlyDictionary<Guid, PixelFormat>(dic);
		}
	}

	public static class PixelFormats
	{
		public static readonly Guid Grey8bpp = Consts.GUID_WICPixelFormat8bppGray;
		public static readonly Guid Bgr24bpp = Consts.GUID_WICPixelFormat24bppBGR;
		public static readonly Guid Bgra32bpp = Consts.GUID_WICPixelFormat32bppBGRA;
	}
}
