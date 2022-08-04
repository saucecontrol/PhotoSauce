// Copyright © Clinton Ingram and Contributors.  Licensed under the MIT License.

using System;
using System.Linq;
using System.Collections.Generic;
using System.Runtime.Versioning;
using System.Runtime.InteropServices;

using TerraFX.Interop.Windows;
using static TerraFX.Interop.Windows.GUID;
using static TerraFX.Interop.Windows.Windows;

using PhotoSauce.Interop.Wic;

namespace PhotoSauce.MagicScaler
{
	/// <summary>Extension methods for managing registered codecs.</summary>
	[SupportedOSPlatform(nameof(OSPlatform.Windows))]
	public static class WindowsCodecExtensions
	{
		/// <summary>Registers codecs with the pipeline.  Call from <see cref="CodecManager.Configure(Action{CodecCollection}?)" />.</summary>
		/// <param name="codecs">The current codec collection.</param>
		/// <param name="policy">Policy that determines which registered WIC codecs are allowed.</param>
		public static void UseWicCodecs(this CodecCollection codecs, WicCodecPolicy policy)
		{
			Guard.NotNull(codecs);

			// If there is more than one Camera RAW codec installed, generally the newer one is more capable, so we deprioritize the original one.
			codecs.AddRange(getWicCodecs(WICComponentType.WICDecoder, policy).OrderBy(static c => c.Format == GUID_ContainerFormatRaw ? 1 : 0).Select(static c => c.Codec));
			codecs.AddRange(getWicCodecs(WICComponentType.WICEncoder, policy).Select(static c => c.Codec));
		}

		private static unsafe List<(Guid Format, IImageCodecInfo Codec)> getWicCodecs(WICComponentType type, WicCodecPolicy policy)
		{
			const int bch = 512;
			const int bcc = 16;

			using var cenum = default(ComPtr<IEnumUnknown>);
			var enumOptions = policy == WicCodecPolicy.BuiltIn ? WICComponentEnumerateOptions.WICComponentEnumerateBuiltInOnly : WICComponentEnumerateOptions.WICComponentEnumerateDefault;
			HRESULT.Check(Wic.Factory->CreateComponentEnumerator((uint)type, (uint)enumOptions, cenum.GetAddressOf()));

			var pbuff = stackalloc char[bch];
			var formats = stackalloc IUnknown*[bcc];
			var codecs = new List<(Guid, IImageCodecInfo)>();

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
					int pos = author.IndexOfOrdinal(" ");
					if (pos >= 0)
						fname = author[..pos];

					if (!name.StartsWithOrdinal(fname))
						name = string.Concat(author, " ", name);

					BOOL anim, mult;
					pCod.Get()->DoesSupportAnimation(&anim);
					pCod.Get()->DoesSupportMultiframe(&mult);

					var cuid = default(Guid);
					HRESULT.Check(pCod.Get()->GetCLSID(&cuid));

					var ctid = default(Guid);
					HRESULT.Check(pCod.Get()->GetContainerFormat(&ctid));

					HRESULT.Check(pCod.Get()->GetPixelFormats(0, null, &cch));
					var pix = new Guid[cch];
					fixed (Guid* pg = pix)
						HRESULT.Check(pCod.Get()->GetPixelFormats(cch, pg, &cch));

					if (extensions.Length != 0 && !ImageFileExtensions.All.ContainsInsensitive(extensions[0]))
						extensions = extensions.OrderBy(static e => ImageFileExtensions.All.ContainsInsensitive(e) ? 0 : 1).ToArray();

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

						bool raw = ctid == GUID_ContainerFormatRaw || ctid == GUID_ContainerFormatRaw2;
						string mime = raw ? "image/RAW" : mimes.First();
						raw |= ctid == GUID_ContainerFormatAdng;

						var clsid = cuid;
						var options = mime switch {
							ImageMimeTypes.Gif  => GifDecoderOptions.Default,
							ImageMimeTypes.Jpeg => JpegDecoderOptions.Default,
							ImageMimeTypes.Tiff => TiffDecoderOptions.Default,
							_                   => raw ? CameraRawDecoderOptions.Default : default(IDecoderOptions)
						};

						codecs.Add((ctid, new DecoderInfo(name, mimes, extensions, patterns, options, (stm, opt) => WicImageDecoder.TryLoad(clsid, mime, stm, opt))));
					}
					else
					{
						string mime = mimes.First();
						bool prof = mime is not (ImageMimeTypes.Bmp or ImageMimeTypes.Gif or ImageMimeTypes.Dds);

						var clsid = cuid;
						var options = mime switch {
							ImageMimeTypes.Gif  => GifEncoderOptions.Default,
							ImageMimeTypes.Png  => PngEncoderOptions.Default,
							ImageMimeTypes.Jpeg => JpegEncoderOptions.Default,
							ImageMimeTypes.Tiff => TiffEncoderOptions.Default,
							_                   => default(IEncoderOptions)
						};

						var encinfo = (IImageEncoderInfo)(new EncoderInfo(name, mimes, extensions, pix, options, (stm, opt) => new WicImageEncoder(clsid, mime, stm, opt), mult, anim, prof));
						if (mime is ImageMimeTypes.Jpeg)
						{
							var subs = new[] { ChromaSubsampleMode.Subsample420, ChromaSubsampleMode.Subsample422, ChromaSubsampleMode.Subsample444 };
							pix = pix.Concat(new[] { PixelFormat.Y8.FormatGuid, PixelFormat.Cb8.FormatGuid, PixelFormat.Cr8.FormatGuid }).ToArray();
							encinfo = new PlanarEncoderInfo(name, mimes, extensions, pix, options, (stm, opt) => new WicImageEncoder(clsid, mime, stm, opt), mult, anim, prof, subs, ChromaPosition.Center, YccMatrix.Rec601, false);
						}

						codecs.Add((ctid, encinfo));
					}
				}
			}

			return codecs;
		}
	}
}
