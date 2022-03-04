// Copyright © Clinton Ingram and Contributors.  Licensed under the MIT License.

using System;
using System.Linq;
using System.Collections.Generic;

using TerraFX.Interop;
using static TerraFX.Interop.Windows;

using PhotoSauce.Interop.Wic;

namespace PhotoSauce.MagicScaler
{
	/// <summary>Extension methods for managing registered codecs.</summary>
	public static class WindowsCodecExtensions
	{
		/// <summary>Registers codecs with the pipeline.  Call from <see cref="CodecManager.Configure(Action{CodecCollection}?)" />.</summary>
		/// <param name="codecs">The current codec collection.</param>
		/// <param name="policy">Policy that determines which registered WIC codecs are allowed.</param>
		public static void UseWicCodecs(this CodecCollection codecs, WicCodecPolicy policy)
		{
			codecs.AddRange(getWicCodecs(WICComponentType.WICDecoder, policy));
			codecs.AddRange(getWicCodecs(WICComponentType.WICEncoder, policy));
		}

		private static unsafe List<IImageCodecInfo> getWicCodecs(WICComponentType type, WicCodecPolicy policy)
		{
			const int bch = 512;
			const int bcc = 16;

			using var cenum = default(ComPtr<IEnumUnknown>);
			var enumOptions = policy == WicCodecPolicy.BuiltIn ? WICComponentEnumerateOptions.WICComponentEnumerateBuiltInOnly : WICComponentEnumerateOptions.WICComponentEnumerateDefault;
			HRESULT.Check(Wic.Factory->CreateComponentEnumerator((uint)type, (uint)enumOptions, cenum.GetAddressOf()));

			var pbuff = stackalloc char[bch];
			var formats = stackalloc IUnknown*[bcc];
			var codecs = new List<IImageCodecInfo>();

			uint count = bcc;
			while (count > 0)
			{
				HRESULT.Check(cenum.Get()->Next(count, formats, &count));
				for (uint i = 0; i < count; i++)
				{
					using var pUnk = default(ComPtr<IUnknown>);
					using var pCod = default(ComPtr<IWICBitmapCodecInfo>);
					pUnk.Attach(formats[i]);
					HRESULT.Check(pUnk.As(&pCod));

					var vuid = default(Guid);
					HRESULT.Check(pCod.Get()->GetVendorGUID(&vuid));

					if ((policy < WicCodecPolicy.All && vuid != GUID_VendorMicrosoftBuiltIn && vuid != GUID_VendorMicrosoft) || (policy < WicCodecPolicy.Microsoft && vuid != GUID_VendorMicrosoft))
						continue;

					uint cch;
					HRESULT.Check(pCod.Get()->GetMimeTypes(bch, (ushort*)pbuff, &cch));
					string[] mimes = new string(pbuff).Split(',');

					// We only care about actual image codecs, which should have at least one MIME type.
					// The only built-in WIC codec that fails this check is the one for .cur files, which
					// doesn't have its CLSID registered and can't be created with CoCreateInstance anyway.
					if (string.IsNullOrEmpty(mimes.FirstOrDefault()))
						continue;

					HRESULT.Check(pCod.Get()->GetFileExtensions(bch, (ushort*)pbuff, &cch));
					string[] extensions = new string(pbuff).Split(',');

					HRESULT.Check(pCod.Get()->GetFriendlyName(bch, (ushort*)pbuff, &cch));
					string name = new(pbuff);

					HRESULT.Check(pCod.Get()->GetAuthor(bch, (ushort*)pbuff, &cch));
					string author = new(pbuff);

					string fname = author;
					int pos = author.IndexOf(' ');
					if (pos >= 0)
						fname = author.Substring(0, pos);

					if (!name.StartsWith(fname))
						name = string.Concat(author, " ", name);

					int chrm, anim, mult;
					pCod.Get()->DoesSupportChromakey(&chrm);
					pCod.Get()->DoesSupportAnimation(&anim);
					pCod.Get()->DoesSupportMultiframe(&mult);

					var cuid = default(Guid);
					HRESULT.Check(pCod.Get()->GetCLSID(&cuid));

					HRESULT.Check(pCod.Get()->GetPixelFormats(0, null, &cch));
					var pix = new Guid[cch];
					fixed (Guid* pg = pix)
						HRESULT.Check(pCod.Get()->GetPixelFormats(cch, pg, &cch));

					bool trans = chrm != 0;
					if (!trans)
						trans = pix.Any(f => PixelFormat.FromGuid(f).AlphaRepresentation != PixelAlphaRepresentation.None);

					if (type is WICComponentType.WICDecoder)
					{
						using var pDec = default(ComPtr<IWICBitmapDecoderInfo>);
						HRESULT.Check(pCod.As(&pDec));

						uint cpt;
						HRESULT.Check(pDec.Get()->GetPatterns(bch * sizeof(char), (WICBitmapPattern*)pbuff, &cpt, &cch));

						var patterns = new List<ContainerPattern>();
						for (uint j = 0; j < cpt; j++)
						{
							var p = ((WICBitmapPattern*)pbuff)[j];
							patterns.Add(new ContainerPattern {
								Offset = (int)p.Position.QuadPart,
								Pattern = new ReadOnlySpan<byte>(p.Pattern, (int)p.Length).ToArray(),
								Mask = new ReadOnlySpan<byte>(p.Mask, (int)p.Length).ToArray()
							});
						}

						var clsid = cuid;
						var options = mimes.First() switch {
							KnownMimeTypes.Gif  => GifDecoderOptions.Default,
							KnownMimeTypes.Jpeg => JpegDecoderOptions.Default,
							KnownMimeTypes.Tiff => TiffDecoderOptions.Default,
							_                   => default(IDecoderOptions)
						};

						codecs.Add(new DecoderInfo(name, mimes, extensions, patterns, options, (stm, opt) => WicImageDecoder.TryLoad(clsid, stm, opt), trans, mult != 0, anim != 0));
					}
					else
					{
						string mime = mimes.First();
						bool prof = mime is not (KnownMimeTypes.Bmp or KnownMimeTypes.Gif or KnownMimeTypes.Dds);
						var clsid = cuid;
						var options = mime switch {
							KnownMimeTypes.Gif  => GifEncoderOptions.Default,
							KnownMimeTypes.Png  => PngEncoderOptions.Default,
							KnownMimeTypes.Jpeg => JpegEncoderOptions.Default,
							KnownMimeTypes.Tiff => TiffEncoderOptions.Default,
							_                   => default(IEncoderOptions)
						};

						pix = mime is KnownMimeTypes.Jpeg ? pix.Concat(new[] { PixelFormat.Y8.FormatGuid }).ToArray() : pix;

						codecs.Add(new EncoderInfo(name, mimes, extensions, pix, options, (stm, opt) => new WicImageEncoder(clsid, stm, opt), trans, mult != 0, anim != 0, prof));
					}
				}
			}

			return codecs;
		}
	}
}
