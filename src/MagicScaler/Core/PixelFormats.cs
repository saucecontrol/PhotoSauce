using System;
using System.Linq;
using System.Text;
using System.Reflection;
using System.Collections.ObjectModel;
using System.Runtime.InteropServices;

using PhotoSauce.Interop.Wic;

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

	internal enum PixelValueEncoding
	{
		Unspecified,
		Companded,
		Linear,
		scRgb
	}

	internal sealed class PixelFormat : IEquatable<PixelFormat>
	{
		public readonly Guid FormatGuid;
		public readonly string Name;
		public readonly bool IsWicNative;
		public readonly int BitsPerPixel;
		public readonly int ChannelCount;
		public readonly PixelNumericRepresentation NumericRepresentation;
		public readonly PixelColorRepresentation ColorRepresentation;
		public readonly PixelAlphaRepresentation AlphaRepresentation;
		public readonly PixelValueEncoding Encoding;

		public bool Equals(PixelFormat? other) => other is not null && FormatGuid == other.FormatGuid;

		public static bool operator ==(PixelFormat left, PixelFormat right) => left.Equals(right);
		public static bool operator !=(PixelFormat left, PixelFormat right) => !left.Equals(right);

		public override bool Equals(object? o) => o is PixelFormat pf && Equals(pf);
		public override int GetHashCode() => FormatGuid.GetHashCode();

		public int BytesPerPixel => MathUtil.DivCeiling(BitsPerPixel, 8);

		public bool IsBinaryCompatibleWith(PixelFormat other) =>
			BitsPerPixel == other.BitsPerPixel &&
			ChannelCount == other.ChannelCount &&
			NumericRepresentation == other.NumericRepresentation &&
			ColorRepresentation == other.ColorRepresentation &&
			AlphaRepresentation == other.AlphaRepresentation &&
			Encoding == other.Encoding;

		private PixelFormat(Guid guid, string name, int bpp, int channels, PixelNumericRepresentation numericRepresentation,
			PixelColorRepresentation colorRepresentation = PixelColorRepresentation.Unspecified, PixelAlphaRepresentation alphaRepresentation = PixelAlphaRepresentation.None,
			PixelValueEncoding encoding = PixelValueEncoding.Unspecified, bool wicNative = false
		)
		{
			FormatGuid = guid;
			Name = name;
			IsWicNative = wicNative;
			BitsPerPixel = bpp;
			ChannelCount = channels;
			NumericRepresentation = numericRepresentation;
			ColorRepresentation = colorRepresentation;
			AlphaRepresentation = alphaRepresentation;
			Encoding = encoding;
		}

		private static readonly Lazy<ReadOnlyDictionary<Guid, PixelFormat>> cache = new (getFormatCache);

		public static readonly PixelFormat Y8Bpp = new (
			guid: new Guid(0x91B4DB54, 0x2DF9, 0x42F0, 0xB4, 0x49, 0x29, 0x09, 0xBB, 0x3D, 0xF8, 0x8E),
			name: "8bpp Y",
			bpp: 8,
			channels: 1,
			numericRepresentation: PixelNumericRepresentation.UnsignedInteger,
			colorRepresentation: PixelColorRepresentation.Grey,
			encoding: PixelValueEncoding.Companded,
			wicNative: true
		);

		public static readonly PixelFormat Cb8Bpp = new (
			guid: new Guid(0x1339F224, 0x6BFE, 0x4C3E, 0x93, 0x02, 0xE4, 0xF3, 0xA6, 0xD0, 0xCA, 0x2A),
			name: "8bpp Cb",
			bpp: 8,
			channels: 1,
			numericRepresentation: PixelNumericRepresentation.UnsignedInteger,
			wicNative: true
		);

		public static readonly PixelFormat Cr8Bpp = new (
			guid: new Guid(0xB8145053, 0x2116, 0x49F0, 0x88, 0x35, 0xED, 0x84, 0x4B, 0x20, 0x5C, 0x51),
			name: "8bpp Cr",
			bpp: 8,
			channels: 1,
			numericRepresentation: PixelNumericRepresentation.UnsignedInteger,
			wicNative: true
		);

		public static readonly PixelFormat Indexed8Bpp = new (
			guid: new Guid(0x6FDDC324, 0x4E03, 0x4BFE, 0xB1, 0x85, 0x3D, 0x77, 0x76, 0x8D, 0xC9, 0x04),
			name: "8bpp Indexed",
			bpp: 8,
			channels: 1,
			numericRepresentation: PixelNumericRepresentation.Indexed,
			wicNative: true
		);

		public static readonly PixelFormat Grey8Bpp = new (
			guid: new Guid(0x6FDDC324, 0x4E03, 0x4BFE, 0xB1, 0x85, 0x3D, 0x77, 0x76, 0x8D, 0xC9, 0x08),
			name: "8bpp Grey",
			bpp: 8,
			channels: 1,
			numericRepresentation: PixelNumericRepresentation.UnsignedInteger,
			colorRepresentation: PixelColorRepresentation.Grey,
			encoding: PixelValueEncoding.Companded,
			wicNative: true
		);

		public static readonly PixelFormat Bgr24Bpp = new (
			guid: new Guid(0x6FDDC324, 0x4E03, 0x4BFE, 0xB1, 0x85, 0x3D, 0x77, 0x76, 0x8D, 0xC9, 0x0C),
			name: "24bpp BGR",
			bpp: 24,
			channels: 3,
			numericRepresentation: PixelNumericRepresentation.UnsignedInteger,
			colorRepresentation: PixelColorRepresentation.Bgr,
			encoding: PixelValueEncoding.Companded,
			wicNative: true
		);

		public static readonly PixelFormat Bgrx32Bpp = new (
			guid: new Guid(0x6FDDC324, 0x4E03, 0x4BFE, 0xB1, 0x85, 0x3D, 0x77, 0x76, 0x8D, 0xC9, 0x0E),
			name: "32bpp BGRX",
			bpp: 32,
			channels: 4,
			numericRepresentation: PixelNumericRepresentation.UnsignedInteger,
			colorRepresentation: PixelColorRepresentation.Bgr,
			encoding: PixelValueEncoding.Companded,
			wicNative: true
		);

		public static readonly PixelFormat Bgra32Bpp = new (
			guid: new Guid(0x6FDDC324, 0x4E03, 0x4BFE, 0xB1, 0x85, 0x3D, 0x77, 0x76, 0x8D, 0xC9, 0x0F),
			name: "32bpp BGRA",
			bpp: 32,
			channels: 4,
			numericRepresentation: PixelNumericRepresentation.UnsignedInteger,
			colorRepresentation: PixelColorRepresentation.Bgr,
			alphaRepresentation: PixelAlphaRepresentation.Unassociated,
			encoding: PixelValueEncoding.Companded,
			wicNative: true
		);

		public static readonly PixelFormat Pbgra32Bpp = new (
			guid: new Guid(0x6FDDC324, 0x4E03, 0x4BFE, 0xB1, 0x85, 0x3D, 0x77, 0x76, 0x8D, 0xC9, 0x10),
			name: "32bpp pBGRA",
			bpp: 32,
			channels: 4,
			numericRepresentation: PixelNumericRepresentation.UnsignedInteger,
			colorRepresentation: PixelColorRepresentation.Bgr,
			alphaRepresentation: PixelAlphaRepresentation.Associated,
			encoding: PixelValueEncoding.Companded,
			wicNative: true
		);

		public static readonly PixelFormat Cmyk32Bpp = new (
			guid: new Guid(0x6FDDC324, 0x4E03, 0x4BFE, 0xB1, 0x85, 0x3D, 0x77, 0x76, 0x8D, 0xC9, 0x1C),
			name: "32bpp CMYK",
			bpp: 32,
			channels: 4,
			numericRepresentation: PixelNumericRepresentation.UnsignedInteger,
			colorRepresentation: PixelColorRepresentation.Cmyk,
			wicNative: true
		);

		public static readonly PixelFormat Grey16BppUQ15 = new (
			guid: new Guid(0xC175220D, 0x375B, 0x48C9, 0x8D, 0xD9, 0x1D, 0x28, 0x24, 0xFE, 0x88, 0x9F),
			name: "16bpp Grey UQ15",
			bpp: 16,
			channels: 1,
			numericRepresentation: PixelNumericRepresentation.Fixed,
			colorRepresentation: PixelColorRepresentation.Grey,
			encoding: PixelValueEncoding.Companded
		);

		public static readonly PixelFormat Grey16BppLinearUQ15 = new (
			guid: new Guid(0xC175220D, 0x375B, 0x48C9, 0x8D, 0xD9, 0x1D, 0x28, 0x24, 0xFE, 0x88, 0xA0),
			name: "16bpp Grey UQ15 Linear",
			bpp: 16,
			channels: 1,
			numericRepresentation: PixelNumericRepresentation.Fixed,
			colorRepresentation: PixelColorRepresentation.Grey,
			encoding: PixelValueEncoding.Linear
		);

		public static readonly PixelFormat Grey32BppFloat = new (
			guid: new Guid(0xC175220D, 0x375B, 0x48C9, 0x8D, 0xD9, 0x1D, 0x28, 0x24, 0xFE, 0x88, 0x9E),
			name: "32bpp Grey Float",
			bpp: 32,
			channels: 1,
			numericRepresentation: PixelNumericRepresentation.Float,
			colorRepresentation: PixelColorRepresentation.Grey,
			encoding: PixelValueEncoding.Companded
		);

		public static readonly PixelFormat Grey32BppLinearFloat = new (
			guid: new Guid(0xC175220D, 0x375B, 0x48C9, 0x8D, 0xD9, 0x1D, 0x28, 0x24, 0xFE, 0x88, 0xA1),
			name: "32bpp Grey Float Linear",
			bpp: 32,
			channels: 1,
			numericRepresentation: PixelNumericRepresentation.Float,
			colorRepresentation: PixelColorRepresentation.Grey,
			encoding: PixelValueEncoding.Linear
		);

		public static readonly PixelFormat Bgr48BppLinearUQ15 = new (
			guid: new Guid(0xC175220D, 0x375B, 0x48C9, 0x8D, 0xD9, 0x1D, 0x28, 0x24, 0xFE, 0x88, 0xA2),
			name: "48bpp BGR UQ15 Linear",
			bpp: 48,
			channels: 3,
			numericRepresentation: PixelNumericRepresentation.Fixed,
			colorRepresentation: PixelColorRepresentation.Bgr,
			encoding: PixelValueEncoding.Linear
		);

		public static readonly PixelFormat Bgr96BppFloat = new (
			guid: new Guid(0xC175220D, 0x375B, 0x48C9, 0x8D, 0xD9, 0x1D, 0x28, 0x24, 0xFE, 0x88, 0xA3),
			name: "96bpp BGR Float",
			bpp: 96,
			channels: 3,
			numericRepresentation: PixelNumericRepresentation.Float,
			colorRepresentation: PixelColorRepresentation.Bgr,
			encoding: PixelValueEncoding.Companded
		);

		public static readonly PixelFormat Bgr96BppLinearFloat = new (
			guid: new Guid(0xC175220D, 0x375B, 0x48C9, 0x8D, 0xD9, 0x1D, 0x28, 0x24, 0xFE, 0x88, 0xA4),
			name: "96bpp BGR Float Linear",
			bpp: 96,
			channels: 3,
			numericRepresentation: PixelNumericRepresentation.Float,
			colorRepresentation: PixelColorRepresentation.Bgr,
			encoding: PixelValueEncoding.Linear
		);

		public static readonly PixelFormat Pbgra64BppLinearUQ15 = new (
			guid: new Guid(0xC175220D, 0x375B, 0x48C9, 0x8D, 0xD9, 0x1D, 0x28, 0x24, 0xFE, 0x88, 0xA6),
			name: "64bpp pBGRA UQ15 Linear",
			bpp: 64,
			channels: 4,
			numericRepresentation: PixelNumericRepresentation.Fixed,
			colorRepresentation: PixelColorRepresentation.Bgr,
			alphaRepresentation: PixelAlphaRepresentation.Associated,
			encoding: PixelValueEncoding.Linear
		);

		public static readonly PixelFormat Pbgra128BppFloat = new (
			guid: new Guid(0xC175220D, 0x375B, 0x48C9, 0x8D, 0xD9, 0x1D, 0x28, 0x24, 0xFE, 0x88, 0xA7),
			name: "128bpp pBGRA Float",
			bpp: 128,
			channels: 4,
			numericRepresentation: PixelNumericRepresentation.Float,
			colorRepresentation: PixelColorRepresentation.Bgr,
			alphaRepresentation: PixelAlphaRepresentation.Associated,
			encoding: PixelValueEncoding.Companded
		);

		public static readonly PixelFormat Pbgra128BppLinearFloat = new (
			guid: new Guid(0xC175220D, 0x375B, 0x48C9, 0x8D, 0xD9, 0x1D, 0x28, 0x24, 0xFE, 0x88, 0xA8),
			name: "128bpp pBGRA Float Linear",
			bpp: 128,
			channels: 4,
			numericRepresentation: PixelNumericRepresentation.Float,
			colorRepresentation: PixelColorRepresentation.Bgr,
			alphaRepresentation: PixelAlphaRepresentation.Associated,
			encoding: PixelValueEncoding.Linear
		);

		public static readonly PixelFormat Y16BppLinearUQ15 = new (
			guid: new Guid(0xC175220D, 0x375B, 0x48C9, 0x8D, 0xD9, 0x1D, 0x28, 0x24, 0xFE, 0x88, 0xA9),
			name: "16bpp Y UQ15 Linear",
			bpp: 16,
			channels: 1,
			numericRepresentation: PixelNumericRepresentation.Fixed,
			colorRepresentation: PixelColorRepresentation.Grey,
			encoding: PixelValueEncoding.Linear
		);

		public static readonly PixelFormat Y32BppFloat = new (
			guid: new Guid(0xC175220D, 0x375B, 0x48C9, 0x8D, 0xD9, 0x1D, 0x28, 0x24, 0xFE, 0x88, 0xAA),
			name: "32bpp Y Float",
			bpp: 32,
			channels: 1,
			numericRepresentation: PixelNumericRepresentation.Float,
			colorRepresentation: PixelColorRepresentation.Grey,
			encoding: PixelValueEncoding.Companded
		);

		public static readonly PixelFormat Y32BppLinearFloat = new (
			guid: new Guid(0xC175220D, 0x375B, 0x48C9, 0x8D, 0xD9, 0x1D, 0x28, 0x24, 0xFE, 0x88, 0xAB),
			name: "32bpp Y Float Linear",
			bpp: 32,
			channels: 1,
			numericRepresentation: PixelNumericRepresentation.Float,
			colorRepresentation: PixelColorRepresentation.Grey,
			encoding: PixelValueEncoding.Linear
		);

		public static readonly PixelFormat Cb32BppFloat = new (
			guid: new Guid(0xC175220D, 0x375B, 0x48C9, 0x8D, 0xD9, 0x1D, 0x28, 0x24, 0xFE, 0x88, 0xAF),
			name: "32bpp Cb Float",
			bpp: 32,
			channels: 1,
			numericRepresentation: PixelNumericRepresentation.Float
		);

		public static readonly PixelFormat Cr32BppFloat = new (
			guid: new Guid(0xC175220D, 0x375B, 0x48C9, 0x8D, 0xD9, 0x1D, 0x28, 0x24, 0xFE, 0x88, 0xB0),
			name: "32bpp Cr Float",
			bpp: 32,
			channels: 1,
			numericRepresentation: PixelNumericRepresentation.Float
		);

		public static readonly PixelFormat Bgrx128BppFloat = new (
			guid: new Guid(0xC175220D, 0x375B, 0x48C9, 0x8D, 0xD9, 0x1D, 0x28, 0x24, 0xFE, 0x88, 0xAD),
			name: "128bpp BGRX Float",
			bpp: 128,
			channels: 4,
			numericRepresentation: PixelNumericRepresentation.Float,
			colorRepresentation: PixelColorRepresentation.Bgr,
			encoding: PixelValueEncoding.Companded
		);

		public static readonly PixelFormat Bgrx128BppLinearFloat = new (
			guid: new Guid(0xC175220D, 0x375B, 0x48C9, 0x8D, 0xD9, 0x1D, 0x28, 0x24, 0xFE, 0x88, 0xAE),
			name: "128bpp BGRX Float Linear",
			bpp: 128,
			channels: 4,
			numericRepresentation: PixelNumericRepresentation.Float,
			colorRepresentation: PixelColorRepresentation.Bgr,
			encoding: PixelValueEncoding.Linear
		);

		public static PixelFormat FromGuid(Guid guid) => cache.Value.TryGetValue(guid, out var pf) ? pf : throw new NotSupportedException("Unsupported pixel format.");

		private static ReadOnlyDictionary<Guid, PixelFormat> getFormatCache()
		{
			var dic = typeof(PixelFormat)
				.GetFields(BindingFlags.Public | BindingFlags.Static)
				.Where(f => f.FieldType == typeof(PixelFormat))
				.ToDictionary(f => ((PixelFormat)f.GetValue(null)!).FormatGuid, f => (PixelFormat)f.GetValue(null)!);

			if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
			{
				uint count = 10u;
				var formats = new object[count];
				using var cenum = ComHandle.Wrap(Wic.Factory.CreateComponentEnumerator(WICComponentType.WICPixelFormat, WICComponentEnumerateOptions.WICComponentEnumerateDefault));

				do
				{
					count = cenum.ComObject.Next(count, formats);
					for (int i = 0; i < count; i++)
					{
						using var pixh = ComHandle.QueryInterface<IWICPixelFormatInfo2>(formats[i]);
						var pix = pixh.ComObject;

						var guid = pix.GetFormatGUID();
						if (dic.ContainsKey(guid))
							continue;

						uint cch = pix.GetFriendlyName(0, null);
						var sbn = new StringBuilder((int)cch);
						pix.GetFriendlyName(cch, sbn);
						string pfn = sbn.ToString();

						var numericRep = (PixelNumericRepresentation)pix.GetNumericRepresentation();
						var colorRep =
							pfn.Contains("BGR") ? PixelColorRepresentation.Bgr :
							pfn.Contains("RGB") ? PixelColorRepresentation.Rgb :
							pfn.Contains("CMYK") ? PixelColorRepresentation.Cmyk :
							pfn.Contains("Gray") || pfn.EndsWith(" Y") ? PixelColorRepresentation.Grey :
							PixelColorRepresentation.Unspecified;
						var valEncoding = colorRep == PixelColorRepresentation.Grey || colorRep == PixelColorRepresentation.Bgr || colorRep == PixelColorRepresentation.Rgb ?
							numericRep == PixelNumericRepresentation.Fixed || numericRep == PixelNumericRepresentation.Float ? PixelValueEncoding.scRgb :
							PixelValueEncoding.Companded :
							PixelValueEncoding.Unspecified;

						var fmt = new PixelFormat(
							guid: guid,
							name: pfn,
							bpp: (int)pix.GetBitsPerPixel(),
							channels: (int)pix.GetChannelCount(),
							numericRepresentation: numericRep,
							colorRepresentation: colorRep,
							alphaRepresentation: pfn.Contains("pBGRA") || pfn.Contains("pRGBA") ? PixelAlphaRepresentation.Associated :
								pix.SupportsTransparency() ? PixelAlphaRepresentation.Unassociated :
								PixelAlphaRepresentation.None,
							encoding: valEncoding,
							wicNative: true
						);

						dic.Add(fmt.FormatGuid, fmt);
					}
				} while (count > 0);
			}

			return new ReadOnlyDictionary<Guid, PixelFormat>(dic);
		}
	}

	/// <summary>Contains standard pixel formats available as output from an <see cref="IPixelSource" />.</summary>
	public static class PixelFormats
	{
		/// <summary>Greyscale data with 1 byte per pixel.</summary>
		/// <value>6FDDC324-4E03-4BFE-B185-3D77768DC908</value>
		public static readonly Guid Grey8bpp = PixelFormat.Grey8Bpp.FormatGuid;
		/// <summary>RGB data with 1 byte per channel in BGR byte order.</summary>
		/// <value>6FDDC324-4E03-4BFE-B185-3D77768DC90C</value>
		public static readonly Guid Bgr24bpp = PixelFormat.Bgr24Bpp.FormatGuid;
		/// <summary>RGBA data with 1 byte per channel in BGRA byte order.</summary>
		/// <value>6FDDC324-4E03-4BFE-B185-3D77768DC90F</value>
		public static readonly Guid Bgra32bpp = PixelFormat.Bgra32Bpp.FormatGuid;

		/// <summary>Contains standard pixel formats for <see cref="IYccImageFrame"/> implementations.</summary>
		public static class Planar
		{
			/// <summary>Planar luma data with 1 byte per pixel.</summary>
			/// <value>91B4DB54-2DF9-42F0-B449-2909BB3DF88E</value>
			public static readonly Guid Y8bpp = PixelFormat.Y8Bpp.FormatGuid;
			/// <summary>Planar blue-yellow chroma data with 1 byte per pixel.</summary>
			/// <value>1339F224-6BFE-4C3E-9302E4F3A6D0CA2A</value>
			public static readonly Guid Cb8bpp = PixelFormat.Cb8Bpp.FormatGuid;
			/// <summary>Planar red-green chroma data with 1 byte per pixel.</summary>
			/// <value>B8145053-2116-49F0-8835ED844B205C51</value>
			public static readonly Guid Cr8bpp = PixelFormat.Cr8Bpp.FormatGuid;
		}
	}
}
