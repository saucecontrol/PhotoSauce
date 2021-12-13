// Copyright © Clinton Ingram and Contributors.  Licensed under the MIT License.

using System;
using System.Linq;
using System.Reflection;
using System.Collections.Generic;
using System.Runtime.InteropServices;

using TerraFX.Interop;

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

		private static readonly Lazy<Dictionary<Guid, PixelFormat>> cache = new(getFormatCache);

		public static readonly PixelFormat Y8 = new(
			guid: Windows.GUID_WICPixelFormat8bppY,
			name: "8bpp Y",
			bpp: 8,
			channels: 1,
			numericRepresentation: PixelNumericRepresentation.UnsignedInteger,
			colorRepresentation: PixelColorRepresentation.Grey,
			encoding: PixelValueEncoding.Companded,
			wicNative: true
		);

		public static readonly PixelFormat Cb8 = new(
			guid: Windows.GUID_WICPixelFormat8bppCb,
			name: "8bpp Cb",
			bpp: 8,
			channels: 1,
			numericRepresentation: PixelNumericRepresentation.UnsignedInteger,
			wicNative: true
		);

		public static readonly PixelFormat Cr8 = new(
			guid: Windows.GUID_WICPixelFormat8bppCr,
			name: "8bpp Cr",
			bpp: 8,
			channels: 1,
			numericRepresentation: PixelNumericRepresentation.UnsignedInteger,
			wicNative: true
		);

		public static readonly PixelFormat Indexed8 = new(
			guid: Windows.GUID_WICPixelFormat8bppIndexed,
			name: "8bpp Indexed",
			bpp: 8,
			channels: 1,
			numericRepresentation: PixelNumericRepresentation.Indexed,
			wicNative: true
		);

		public static readonly PixelFormat Grey8 = new(
			guid: Windows.GUID_WICPixelFormat8bppGray,
			name: "8bpp Grey",
			bpp: 8,
			channels: 1,
			numericRepresentation: PixelNumericRepresentation.UnsignedInteger,
			colorRepresentation: PixelColorRepresentation.Grey,
			encoding: PixelValueEncoding.Companded,
			wicNative: true
		);

		public static readonly PixelFormat Bgr24 = new(
			guid: Windows.GUID_WICPixelFormat24bppBGR,
			name: "24bpp BGR",
			bpp: 24,
			channels: 3,
			numericRepresentation: PixelNumericRepresentation.UnsignedInteger,
			colorRepresentation: PixelColorRepresentation.Bgr,
			encoding: PixelValueEncoding.Companded,
			wicNative: true
		);

		public static readonly PixelFormat Rgb24 = new(
			guid: Windows.GUID_WICPixelFormat24bppRGB,
			name: "24bpp RGB",
			bpp: 24,
			channels: 3,
			numericRepresentation: PixelNumericRepresentation.UnsignedInteger,
			colorRepresentation: PixelColorRepresentation.Rgb,
			encoding: PixelValueEncoding.Companded,
			wicNative: true
		);

		public static readonly PixelFormat Bgrx32 = new(
			guid: Windows.GUID_WICPixelFormat32bppBGR,
			name: "32bpp BGRX",
			bpp: 32,
			channels: 4,
			numericRepresentation: PixelNumericRepresentation.UnsignedInteger,
			colorRepresentation: PixelColorRepresentation.Bgr,
			encoding: PixelValueEncoding.Companded,
			wicNative: true
		);

		public static readonly PixelFormat Bgra32 = new(
			guid: Windows.GUID_WICPixelFormat32bppBGRA,
			name: "32bpp BGRA",
			bpp: 32,
			channels: 4,
			numericRepresentation: PixelNumericRepresentation.UnsignedInteger,
			colorRepresentation: PixelColorRepresentation.Bgr,
			alphaRepresentation: PixelAlphaRepresentation.Unassociated,
			encoding: PixelValueEncoding.Companded,
			wicNative: true
		);

		public static readonly PixelFormat Rgba32 = new(
			guid: Windows.GUID_WICPixelFormat32bppRGBA,
			name: "32bpp RGBA",
			bpp: 32,
			channels: 4,
			numericRepresentation: PixelNumericRepresentation.UnsignedInteger,
			colorRepresentation: PixelColorRepresentation.Rgb,
			alphaRepresentation: PixelAlphaRepresentation.Unassociated,
			encoding: PixelValueEncoding.Companded,
			wicNative: true
		);

		public static readonly PixelFormat Pbgra32 = new(
			guid: Windows.GUID_WICPixelFormat32bppPBGRA,
			name: "32bpp pBGRA",
			bpp: 32,
			channels: 4,
			numericRepresentation: PixelNumericRepresentation.UnsignedInteger,
			colorRepresentation: PixelColorRepresentation.Bgr,
			alphaRepresentation: PixelAlphaRepresentation.Associated,
			encoding: PixelValueEncoding.Companded,
			wicNative: true
		);

		public static readonly PixelFormat Cmyk32 = new(
			guid: Windows.GUID_WICPixelFormat32bppCMYK,
			name: "32bpp CMYK",
			bpp: 32,
			channels: 4,
			numericRepresentation: PixelNumericRepresentation.UnsignedInteger,
			colorRepresentation: PixelColorRepresentation.Cmyk,
			wicNative: true
		);

		public static readonly PixelFormat Grey16UQ15 = new(
			guid: new(0xc175220d, 0x375b, 0x48c9, 0x8d, 0xd9, 0x1d, 0x28, 0x24, 0xfe, 0x88, 0x9f),
			name: "16bpp Grey UQ15",
			bpp: 16,
			channels: 1,
			numericRepresentation: PixelNumericRepresentation.Fixed,
			colorRepresentation: PixelColorRepresentation.Grey,
			encoding: PixelValueEncoding.Companded
		);

		public static readonly PixelFormat Grey16UQ15Linear = new(
			guid: new(0xc175220d, 0x375b, 0x48c9, 0x8d, 0xd9, 0x1d, 0x28, 0x24, 0xfe, 0x88, 0xa0),
			name: "16bpp Grey UQ15 Linear",
			bpp: 16,
			channels: 1,
			numericRepresentation: PixelNumericRepresentation.Fixed,
			colorRepresentation: PixelColorRepresentation.Grey,
			encoding: PixelValueEncoding.Linear
		);

		public static readonly PixelFormat Grey32Float = new(
			guid: new(0xc175220d, 0x375b, 0x48c9, 0x8d, 0xd9, 0x1d, 0x28, 0x24, 0xfe, 0x88, 0x9e),
			name: "32bpp Grey Float",
			bpp: 32,
			channels: 1,
			numericRepresentation: PixelNumericRepresentation.Float,
			colorRepresentation: PixelColorRepresentation.Grey,
			encoding: PixelValueEncoding.Companded
		);

		public static readonly PixelFormat Grey32FloatLinear = new(
			guid: new(0xc175220d, 0x375b, 0x48c9, 0x8d, 0xd9, 0x1d, 0x28, 0x24, 0xfe, 0x88, 0xa1),
			name: "32bpp Grey Float Linear",
			bpp: 32,
			channels: 1,
			numericRepresentation: PixelNumericRepresentation.Float,
			colorRepresentation: PixelColorRepresentation.Grey,
			encoding: PixelValueEncoding.Linear
		);

		public static readonly PixelFormat Bgr48UQ15Linear = new(
			guid: new(0xc175220d, 0x375b, 0x48c9, 0x8d, 0xd9, 0x1d, 0x28, 0x24, 0xfe, 0x88, 0xa2),
			name: "48bpp BGR UQ15 Linear",
			bpp: 48,
			channels: 3,
			numericRepresentation: PixelNumericRepresentation.Fixed,
			colorRepresentation: PixelColorRepresentation.Bgr,
			encoding: PixelValueEncoding.Linear
		);

		public static readonly PixelFormat Bgr96Float = new(
			guid: new(0xc175220d, 0x375b, 0x48c9, 0x8d, 0xd9, 0x1d, 0x28, 0x24, 0xfe, 0x88, 0xa3),
			name: "96bpp BGR Float",
			bpp: 96,
			channels: 3,
			numericRepresentation: PixelNumericRepresentation.Float,
			colorRepresentation: PixelColorRepresentation.Bgr,
			encoding: PixelValueEncoding.Companded
		);

		public static readonly PixelFormat Bgr96FloatLinear = new(
			guid: new(0xc175220d, 0x375b, 0x48c9, 0x8d, 0xd9, 0x1d, 0x28, 0x24, 0xfe, 0x88, 0xa4),
			name: "96bpp BGR Float Linear",
			bpp: 96,
			channels: 3,
			numericRepresentation: PixelNumericRepresentation.Float,
			colorRepresentation: PixelColorRepresentation.Bgr,
			encoding: PixelValueEncoding.Linear
		);

		public static readonly PixelFormat Pbgra64UQ15Linear = new(
			guid: new(0xc175220d, 0x375b, 0x48c9, 0x8d, 0xd9, 0x1d, 0x28, 0x24, 0xfe, 0x88, 0xa6),
			name: "64bpp pBGRA UQ15 Linear",
			bpp: 64,
			channels: 4,
			numericRepresentation: PixelNumericRepresentation.Fixed,
			colorRepresentation: PixelColorRepresentation.Bgr,
			alphaRepresentation: PixelAlphaRepresentation.Associated,
			encoding: PixelValueEncoding.Linear
		);

		public static readonly PixelFormat Pbgra128Float = new(
			guid: new(0xc175220d, 0x375b, 0x48c9, 0x8d, 0xd9, 0x1d, 0x28, 0x24, 0xfe, 0x88, 0xa7),
			name: "128bpp pBGRA Float",
			bpp: 128,
			channels: 4,
			numericRepresentation: PixelNumericRepresentation.Float,
			colorRepresentation: PixelColorRepresentation.Bgr,
			alphaRepresentation: PixelAlphaRepresentation.Associated,
			encoding: PixelValueEncoding.Companded
		);

		public static readonly PixelFormat Pbgra128FloatLinear = new(
			guid: new(0xc175220d, 0x375b, 0x48c9, 0x8d, 0xd9, 0x1d, 0x28, 0x24, 0xfe, 0x88, 0xa8),
			name: "128bpp pBGRA Float Linear",
			bpp: 128,
			channels: 4,
			numericRepresentation: PixelNumericRepresentation.Float,
			colorRepresentation: PixelColorRepresentation.Bgr,
			alphaRepresentation: PixelAlphaRepresentation.Associated,
			encoding: PixelValueEncoding.Linear
		);

		public static readonly PixelFormat Y16UQ15Linear = new(
			guid: new(0xc175220d, 0x375b, 0x48c9, 0x8d, 0xd9, 0x1d, 0x28, 0x24, 0xfe, 0x88, 0xa9),
			name: "16bpp Y UQ15 Linear",
			bpp: 16,
			channels: 1,
			numericRepresentation: PixelNumericRepresentation.Fixed,
			colorRepresentation: PixelColorRepresentation.Grey,
			encoding: PixelValueEncoding.Linear
		);

		public static readonly PixelFormat Y32Float = new(
			guid: new(0xc175220d, 0x375b, 0x48c9, 0x8d, 0xd9, 0x1d, 0x28, 0x24, 0xfe, 0x88, 0xaa),
			name: "32bpp Y Float",
			bpp: 32,
			channels: 1,
			numericRepresentation: PixelNumericRepresentation.Float,
			colorRepresentation: PixelColorRepresentation.Grey,
			encoding: PixelValueEncoding.Companded
		);

		public static readonly PixelFormat Y32FloatLinear = new(
			guid: new(0xc175220d, 0x375b, 0x48c9, 0x8d, 0xd9, 0x1d, 0x28, 0x24, 0xfe, 0x88, 0xab),
			name: "32bpp Y Float Linear",
			bpp: 32,
			channels: 1,
			numericRepresentation: PixelNumericRepresentation.Float,
			colorRepresentation: PixelColorRepresentation.Grey,
			encoding: PixelValueEncoding.Linear
		);

		public static readonly PixelFormat Cb32Float = new(
			guid: new(0xc175220d, 0x375b, 0x48c9, 0x8d, 0xd9, 0x1d, 0x28, 0x24, 0xfe, 0x88, 0xaf),
			name: "32bpp Cb Float",
			bpp: 32,
			channels: 1,
			numericRepresentation: PixelNumericRepresentation.Float
		);

		public static readonly PixelFormat Cr32Float = new(
			guid: new(0xc175220d, 0x375b, 0x48c9, 0x8d, 0xd9, 0x1d, 0x28, 0x24, 0xfe, 0x88, 0xb0),
			name: "32bpp Cr Float",
			bpp: 32,
			channels: 1,
			numericRepresentation: PixelNumericRepresentation.Float
		);

		public static readonly PixelFormat Bgrx128Float = new(
			guid: new(0xc175220d, 0x375b, 0x48c9, 0x8d, 0xd9, 0x1d, 0x28, 0x24, 0xfe, 0x88, 0xad),
			name: "128bpp BGRX Float",
			bpp: 128,
			channels: 4,
			numericRepresentation: PixelNumericRepresentation.Float,
			colorRepresentation: PixelColorRepresentation.Bgr,
			encoding: PixelValueEncoding.Companded
		);

		public static readonly PixelFormat Bgrx128FloatLinear = new(
			guid: new(0xc175220d, 0x375b, 0x48c9, 0x8d, 0xd9, 0x1d, 0x28, 0x24, 0xfe, 0x88, 0xae),
			name: "128bpp BGRX Float Linear",
			bpp: 128,
			channels: 4,
			numericRepresentation: PixelNumericRepresentation.Float,
			colorRepresentation: PixelColorRepresentation.Bgr,
			encoding: PixelValueEncoding.Linear
		);

		public static PixelFormat FromGuid(Guid guid) => cache.Value.TryGetValue(guid, out var pf) ? pf : throw new NotSupportedException("Unsupported pixel format.");

		private static unsafe Dictionary<Guid, PixelFormat> getFormatCache()
		{
			var dic = typeof(PixelFormat)
				.GetFields(BindingFlags.Public | BindingFlags.Static)
				.Where(f => f.FieldType == typeof(PixelFormat))
				.Select(f => (PixelFormat)f.GetValue(null)!)
				.ToDictionary(f => f.FormatGuid, f => f);

			if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
			{
				using var cenum = default(ComPtr<IEnumUnknown>);
				HRESULT.Check(Wic.Factory->CreateComponentEnumerator((uint)WICComponentType.WICPixelFormat, (uint)WICComponentEnumerateOptions.WICComponentEnumerateDefault, cenum.GetAddressOf()));

				using var chbuff = BufferPool.RentLocal<char>(1024);
				var formats = stackalloc IUnknown*[10];
				uint count = 10u;

				do
				{
					HRESULT.Check(cenum.Get()->Next(count, formats, &count));
					for (uint i = 0; i < count; i++)
					{
						using var pUnk = default(ComPtr<IUnknown>);
						pUnk.Attach(formats[i]);

						using var pPix = default(ComPtr<IWICPixelFormatInfo2>);
						HRESULT.Check(pUnk.As(&pPix));

						var guid = default(Guid);
						HRESULT.Check(pPix.Get()->GetFormatGUID(&guid));
						if (dic.ContainsKey(guid))
							continue;

						uint cch;
						string name = string.Empty;
						HRESULT.Check(pPix.Get()->GetFriendlyName(0, null, &cch));
						if (cch <= chbuff.Length)
						{
							fixed (char* pbuff = chbuff.Span)
							{
								HRESULT.Check(pPix.Get()->GetFriendlyName(cch, (ushort*)pbuff, &cch));
								name = new string(pbuff);
							}
						}

						var numericRep = default(PixelNumericRepresentation);
						HRESULT.Check(pPix.Get()->GetNumericRepresentation((WICPixelFormatNumericRepresentation*)&numericRep));

						uint bpp, channels, trans;
						HRESULT.Check(pPix.Get()->GetBitsPerPixel(&bpp));
						HRESULT.Check(pPix.Get()->GetChannelCount(&channels));
						HRESULT.Check(pPix.Get()->SupportsTransparency((int*)&trans));

						var colorRep =
							name.Contains("BGR") ? PixelColorRepresentation.Bgr :
							name.Contains("RGB") ? PixelColorRepresentation.Rgb :
							name.Contains("CMYK") ? PixelColorRepresentation.Cmyk :
							name.Contains("Gray") || name.EndsWith(" Y") ? PixelColorRepresentation.Grey :
							PixelColorRepresentation.Unspecified;
						var valEncoding = colorRep == PixelColorRepresentation.Grey || colorRep == PixelColorRepresentation.Bgr || colorRep == PixelColorRepresentation.Rgb ?
							numericRep == PixelNumericRepresentation.Fixed || numericRep == PixelNumericRepresentation.Float ? PixelValueEncoding.scRgb :
							PixelValueEncoding.Companded :
							PixelValueEncoding.Unspecified;

						var fmt = new PixelFormat(
							guid: guid,
							name: name,
							bpp: (int)bpp,
							channels: (int)channels,
							numericRepresentation: numericRep,
							colorRepresentation: colorRep,
							alphaRepresentation: name.Contains("pBGRA") || name.Contains("pRGBA") ? PixelAlphaRepresentation.Associated :
								trans != 0 ? PixelAlphaRepresentation.Unassociated :
								PixelAlphaRepresentation.None,
							encoding: valEncoding,
							wicNative: true
						);

						dic.Add(fmt.FormatGuid, fmt);
					}
				} while (count > 0);
			}

			return dic;
		}
	}

	/// <summary>Contains standard pixel formats available as output from an <see cref="IPixelSource" />.</summary>
	public static class PixelFormats
	{
		/// <summary>Greyscale data with 1 byte per pixel.</summary>
		/// <value>6FDDC324-4E03-4BFE-B185-3D77768DC908</value>
		public static readonly Guid Grey8bpp = PixelFormat.Grey8.FormatGuid;
		/// <summary>RGB data with 1 byte per channel in BGR byte order.</summary>
		/// <value>6FDDC324-4E03-4BFE-B185-3D77768DC90C</value>
		public static readonly Guid Bgr24bpp = PixelFormat.Bgr24.FormatGuid;
		/// <summary>RGBA data with 1 byte per channel in BGRA byte order.</summary>
		/// <value>6FDDC324-4E03-4BFE-B185-3D77768DC90F</value>
		public static readonly Guid Bgra32bpp = PixelFormat.Bgra32.FormatGuid;

		/// <summary>Contains standard pixel formats for <see cref="IYccImageFrame"/> implementations.</summary>
		public static class Planar
		{
			/// <summary>Planar luma data with 1 byte per pixel.</summary>
			/// <value>91B4DB54-2DF9-42F0-B449-2909BB3DF88E</value>
			public static readonly Guid Y8bpp = PixelFormat.Y8.FormatGuid;
			/// <summary>Planar blue-yellow chroma data with 1 byte per pixel.</summary>
			/// <value>1339F224-6BFE-4C3E-9302E4F3A6D0CA2A</value>
			public static readonly Guid Cb8bpp = PixelFormat.Cb8.FormatGuid;
			/// <summary>Planar red-green chroma data with 1 byte per pixel.</summary>
			/// <value>B8145053-2116-49F0-8835ED844B205C51</value>
			public static readonly Guid Cr8bpp = PixelFormat.Cr8.FormatGuid;
		}
	}
}
