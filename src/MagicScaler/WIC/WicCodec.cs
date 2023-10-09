// Copyright © Clinton Ingram and Contributors.  Licensed under the MIT License.

using System;
using System.IO;
using System.Drawing;
using System.Buffers;
using System.Buffers.Binary;
using System.Diagnostics;
using System.Runtime.InteropServices;
#if WICPROCESSOR
using System.Collections.Generic;
#endif

using TerraFX.Interop.Windows;
using static TerraFX.Interop.Windows.GUID;
using static TerraFX.Interop.Windows.WINCODEC;
using static TerraFX.Interop.Windows.Windows;

using PhotoSauce.Interop.Wic;

namespace PhotoSauce.MagicScaler;

internal static unsafe class WicImageDecoder
{
#if WICPROCESSOR
	private static readonly Dictionary<Guid, string> formatMap = new() {
		[GUID_ContainerFormatBmp] = ImageMimeTypes.Bmp,
		[GUID_ContainerFormatGif] = ImageMimeTypes.Gif,
		[GUID_ContainerFormatPng] = ImageMimeTypes.Png,
		[GUID_ContainerFormatJpeg] = ImageMimeTypes.Jpeg,
		[GUID_ContainerFormatTiff] = ImageMimeTypes.Tiff,
		[GUID_ContainerFormatHeif] = ImageMimeTypes.Heic
	};

	private static IWICBitmapDecoder* createDecoder(IStream* pStream)
	{
		Wic.EnsureFreeThreaded();

		using var decoder = default(ComPtr<IWICBitmapDecoder>);
		int hr = Wic.Factory->CreateDecoderFromStream(pStream, null, WICDecodeOptions.WICDecodeMetadataCacheOnDemand, decoder.GetAddressOf());
		if (hr == WINCODEC_ERR_COMPONENTNOTFOUND)
			throw new InvalidDataException("Image format not supported.  Please ensure the input file is an image and that a WIC codec capable of reading the image is installed.", Marshal.GetExceptionForHR(hr));

		HRESULT.Check(hr);
		return decoder.Detach();
	}

	private static string? getMimeType(IWICBitmapDecoder* dec)
	{
		var guid = default(Guid);
		HRESULT.Check(dec->GetContainerFormat(&guid));

		return formatMap.GetValueOrDefault(guid);
	}

	public static WicImageContainer Load(string fileName, IDecoderOptions? options)
	{
		using var stm = default(ComPtr<IWICStream>);
		HRESULT.Check(Wic.Factory->CreateStream(stm.GetAddressOf()));
		fixed (char* pname = fileName)
			HRESULT.Check(stm.Get()->InitializeFromFilename((ushort*)pname, GENERIC_READ));

		var dec = createDecoder((IStream*)stm.Get());
		return WicImageContainer.Create(dec, getMimeType(dec), options);
	}

	public static WicImageContainer Load(Stream inStream, IDecoderOptions? options)
	{
		using var ccw = new ComPtr<IStream>(IStreamImpl.Wrap(inStream));
		var dec = createDecoder(ccw);
		return WicImageContainer.Create(dec, getMimeType(dec), options);
	}

	public static WicImageContainer Load(byte* pbBuffer, int cbBuffer, IDecoderOptions? options)
	{
		using var stream = default(ComPtr<IWICStream>);
		HRESULT.Check(Wic.Factory->CreateStream(stream.GetAddressOf()));

		HRESULT.Check(stream.Get()->InitializeFromMemory(pbBuffer, (uint)cbBuffer));

		var dec = createDecoder((IStream*)stream.Get());
		return WicImageContainer.Create(dec, getMimeType(dec), options);
	}
#endif

	public static WicImageContainer? TryLoad(Guid clsid, string mime, Stream stream, IDecoderOptions? options)
	{
		Wic.EnsureFreeThreaded();

		using var decoder = default(ComPtr<IWICBitmapDecoder>);
		if (FAILED(CoCreateInstance(&clsid, null, (uint)CLSCTX.CLSCTX_INPROC_SERVER, __uuidof<IWICBitmapDecoder>(), (void**)decoder.GetAddressOf())))
			throw new NotSupportedException($"The WIC decoder with CLSID '{clsid}' for MIME type '{mime}' could not be instantiated.  This codec should be unregistered.");

		using var ccw = new ComPtr<IStream>(IStreamImpl.Wrap(stream));
		if (FAILED(decoder.Get()->Initialize(ccw, WICDecodeOptions.WICDecodeMetadataCacheOnDemand)))
			return null;

		return WicImageContainer.Create(decoder.Detach(), mime, options);
	}
}

internal sealed unsafe class WicImageEncoder : IAnimatedImageEncoder, IDisposable
{
	public IWICBitmapEncoder* WicEncoder { get; private set; }
	public IEncoderOptions? Options { get; }

	public WicImageEncoder(Guid clsid, string mime, Stream stm, IEncoderOptions? options)
	{
		Wic.EnsureFreeThreaded();

		using var encoder = default(ComPtr<IWICBitmapEncoder>);
		if (FAILED(CoCreateInstance(&clsid, null, (uint)CLSCTX.CLSCTX_INPROC_SERVER, __uuidof<IWICBitmapEncoder>(), (void**)encoder.GetAddressOf())))
			throw new NotSupportedException($"The WIC encoder with CLSID '{clsid}' for MIME type '{mime}' could not be instantiated.  This codec should be unregistered.");

		using var ccw = new ComPtr<IStream>(IStreamImpl.Wrap(stm));
		HRESULT.Check(encoder.Get()->Initialize(ccw, WICBitmapEncoderCacheOption.WICBitmapEncoderNoCache));

		var guid = default(Guid);
		HRESULT.Check(encoder.Get()->GetContainerFormat(&guid));

		Options = options;
		WicEncoder = encoder.Detach();
	}

	public void WriteAnimationMetadata(IMetadataSource metadata)
	{
		var anicnt = metadata.TryGetMetadata<AnimationContainer>(out var ani) ? ani : default;

		using var encmeta = default(ComPtr<IWICMetadataQueryWriter>);
		HRESULT.Check(WicEncoder->GetMetadataQueryWriter(encmeta.GetAddressOf()));

		if (anicnt.LoopCount != 1)
		{
			var appext = WicGifContainer.Netscape2_0;
			var pvae = new PROPVARIANT { vt = (ushort)(VARENUM.VT_UI1 | VARENUM.VT_VECTOR) };
			pvae.Anonymous.blob.cbSize = (uint)appext.Length;
			pvae.Anonymous.blob.pBlobData = appext.GetAddressOf();
			HRESULT.Check(encmeta.Get()->SetMetadataByName(Wic.Metadata.Gif.AppExtension, &pvae));

			byte* pvdd = stackalloc byte[] { 3, 1, 0, 0 };
			BinaryPrimitives.WriteUInt16LittleEndian(new Span<byte>(pvdd + 2, 2), (ushort)anicnt.LoopCount);

			var pvad = new PROPVARIANT { vt = (ushort)(VARENUM.VT_UI1 | VARENUM.VT_VECTOR) };
			pvad.Anonymous.blob.cbSize = 4;
			pvad.Anonymous.blob.pBlobData = pvdd;
			HRESULT.Check(encmeta.Get()->SetMetadataByName(Wic.Metadata.Gif.AppExtensionData, &pvad));
		}

		// TODO WIC ignores these and sets the logical screen descriptor dimensions from the first frame
		//pv.vt = (ushort)VARENUM.VT_UI2;
		//pv.Anonymous.uiVal = (ushort)context.Source.Width;
		//HRESULT.Check(encmeta.Get()->SetMetadataByName(Wic.Metadata.Gif.LogicalScreenWidth, &pv));
		//pv.Anonymous.uiVal = (ushort)context.Source.Height;
		//HRESULT.Check(encmeta.Get()->SetMetadataByName(Wic.Metadata.Gif.LogicalScreenHeight, &pv));

		if (anicnt.BackgroundColor != default)
		{
			uint bg = (uint)anicnt.BackgroundColor;

			using var pal = default(ComPtr<IWICPalette>);
			HRESULT.Check(Wic.Factory->CreatePalette(pal.GetAddressOf()));
			pal.Get()->InitializeCustom(&bg, 1);

			var pvbg = new PROPVARIANT { vt = (ushort)VARENUM.VT_UI1 };
			pvbg.Anonymous.bVal = 0;

			HRESULT.Check(WicEncoder->SetPalette(pal));
			HRESULT.Check(encmeta.Get()->SetMetadataByName(Wic.Metadata.Gif.BackgroundColorIndex, &pvbg));
		}

		if (anicnt.PixelAspectRatio != default && anicnt.PixelAspectRatio != 1f)
		{
			var pvar = new PROPVARIANT { vt = (ushort)VARENUM.VT_UI1 };
			pvar.Anonymous.bVal = (byte)((int)(anicnt.PixelAspectRatio * 64f - 15f)).Clamp(byte.MinValue, byte.MaxValue);
			HRESULT.Check(encmeta.Get()->SetMetadataByName(Wic.Metadata.Gif.PixelAspectRatio, &pvar));
		}
	}

	public void WriteFrame(IPixelSource source, IMetadataSource meta, Rectangle area)
	{
		var src = source.AsPixelSource();
		var encArea = area.IsEmpty ? src.Area : (PixelArea)area;

		var fmt = default(Guid);
		HRESULT.Check(WicEncoder->GetContainerFormat(&fmt));

		using var frame = default(ComPtr<IWICBitmapFrameEncode>);
		using (var pbag = default(ComPtr<IPropertyBag2>))
		{
			HRESULT.Check(WicEncoder->CreateNewFrame(frame.GetAddressOf(), pbag.GetAddressOf()));

			if (fmt == GUID_ContainerFormatJpeg)
			{
				int qual = Options is ILossyEncoderOptions { Quality: not 0 } lopt
					? lopt.Quality
					: SettingsUtil.GetDefaultQuality(Math.Max(source.Width, source.Height));

				var subs = SettingsUtil.GetDefaultSubsampling(qual);
				if (src is PlanarPixelSource psrc)
					subs = psrc.GetSubsampling();
				else if (Options is IPlanarEncoderOptions { Subsample: not ChromaSubsampleMode.Default } popt)
					subs = popt.Subsample;

				pbag.Write("ImageQuality", qual / 100f);
				pbag.Write("JpegYCrCbSubsampling", (byte)subs);

				if (Options is JpegEncoderOptions { SuppressApp0: true } jopt)
					pbag.Write("SuppressApp0", jopt.SuppressApp0);
			}
			else if (fmt == GUID_ContainerFormatPng && Options is IPngEncoderOptions pconf)
			{
				if (pconf.Filter != PngFilter.Unspecified)
					pbag.Write("FilterOption", (byte)pconf.Filter);
				if (pconf.Interlace)
					pbag.Write("InterlaceOption", pconf.Interlace);
			}
			else if (fmt == GUID_ContainerFormatTiff && Options is TiffEncoderOptions tconf)
			{
				var comp = tconf.Compression.ToWicTiffCompressionOptions();
				if (comp != WICTiffCompressionOption.WICTiffCompressionDontCare)
					pbag.Write("TiffCompressionMethod", (byte)comp);
			}
			else if (fmt == GUID_ContainerFormatBmp)
			{
				if (src.Format.AlphaRepresentation != PixelAlphaRepresentation.None)
					pbag.Write("EnableV5Header32bppBGRA", true);
			}
			else if (fmt == GUID_ContainerFormatWmp || fmt == GUID_ContainerFormatHeif)
			{
				int qual = Options is ILossyEncoderOptions { Quality: not 0 } opt
					? opt.Quality
					: SettingsUtil.GetDefaultQuality(Math.Max(source.Width, source.Height));

				pbag.Write("ImageQuality", qual / 100f);
			}

			HRESULT.Check(frame.Get()->Initialize(pbag));
		}

		using var metawriter = default(ComPtr<IWICMetadataQueryWriter>);
		bool hasWriter = SUCCEEDED(frame.Get()->GetMetadataQueryWriter(metawriter.GetAddressOf()));

		HRESULT.Check(frame.Get()->SetSize((uint)encArea.Width, (uint)encArea.Height));

		if (meta.TryGetMetadata<ResolutionMetadata>(out var res) && res.IsValid)
			HRESULT.Check(frame.Get()->SetResolution((double)res.ResolutionX, (double)res.ResolutionY));

		if (meta.TryGetMetadata<ColorProfileMetadata>(out var prof))
		{
			var cp = prof.Profile;
			var cc = default(IWICColorContext*);

			// We give preference to V2 compact profiles when possible, for the file size and compatibility advantages.
			// However, WIC's PNG encoder writes gAMA and cHRM chunks along with iCCP when a V2 ICC profile is written, as is
			// recommended by the PNG spec. When reading, libpng gets clever and may ignore the iCCP tag if it sees cHRM values it
			// thinks are invalid, so for PNG we keep the V4 reference profile, which forces iCCP to be written without the others.
			if (cp == ColorProfile.sRGB)
				cc = (fmt == GUID_ContainerFormatPng ? WicColorProfile.Srgb : WicColorProfile.SrgbCompact).Value.WicColorContext;
			else if (cp == ColorProfile.sGrey)
				cc = (fmt == GUID_ContainerFormatPng ? WicColorProfile.Grey : WicColorProfile.GreyCompact).Value.WicColorContext;
			else if (cp == ColorProfile.AdobeRgb)
				cc = (fmt == GUID_ContainerFormatPng ? WicColorProfile.AdobeRgb : WicColorProfile.AdobeRgbCompact).Value.WicColorContext;
			else if (cp == ColorProfile.DisplayP3)
				cc = (fmt == GUID_ContainerFormatPng ? WicColorProfile.DisplayP3 : WicColorProfile.DisplayP3Compact).Value.WicColorContext;

			using var wcc = default(ComPtr<IWICColorContext>);
			if (cc is null)
			{
				wcc.Attach(WicColorProfile.CreateContextFromProfile(cp.ProfileBytes));
				cc = wcc;
			}

			_ = frame.Get()->SetColorContexts(1, &cc);
		}

		if (hasWriter && meta.TryGetMetadata<OrientationMetadata>(out var orient))
		{
			string orientationPath = fmt == GUID_ContainerFormatJpeg ? Wic.Metadata.OrientationJpeg : Wic.Metadata.OrientationExif;
			var pv = new PROPVARIANT { vt = (ushort)VARENUM.VT_UI2 };
			pv.Anonymous.uiVal = (ushort)orient.Orientation;
			_ = metawriter.Get()->SetMetadataByName(orientationPath, &pv);
		}

		if (hasWriter && meta is AnimationEncoder.AnimationBufferFrame && meta.TryGetMetadata<AnimationFrame>(out var anifrm))
		{
			metawriter.SetValue(Wic.Metadata.Gif.FrameDisposal, (byte)anifrm.Disposal);
			metawriter.SetValue(Wic.Metadata.Gif.FrameDelay, (ushort)anifrm.Duration.NormalizeTo(100).Numerator);

			if (anifrm.OffsetLeft != 0)
				metawriter.SetValue(Wic.Metadata.Gif.FrameLeft, (ushort)anifrm.OffsetLeft);
			if (anifrm.OffsetTop != 0)
				metawriter.SetValue(Wic.Metadata.Gif.FrameTop, (ushort)anifrm.OffsetTop);
		}

		if (hasWriter && fmt == GUID_ContainerFormatGif && source is IIndexedPixelSource idxs)
		{
			var pal = idxs.Palette;
			if (idxs.HasAlpha())
			{
				metawriter.SetValue(Wic.Metadata.Gif.TransparencyFlag, true);
				metawriter.SetValue(Wic.Metadata.Gif.TransparentColorIndex, (byte)(pal.Length - 1));
			}
		}

		if (hasWriter && meta.TryGetMetadata<WicFrameMetadataReader>(out var wicmeta))
		{
			foreach (string prop in wicmeta.CopyNames)
			{
				var pv = default(PROPVARIANT);
				if (SUCCEEDED(wicmeta.Reader->GetMetadataByName(prop, &pv)) && pv.vt != (ushort)VARENUM.VT_EMPTY)
				{
					_ = metawriter.Get()->SetMetadataByName(prop, &pv);
					_ = PropVariantClear(&pv);
				}
			}
		}

		writeSource(frame, src, area);
	}

	private static void writeSource(IWICBitmapFrameEncode* frame, PixelSource src, PixelArea area)
	{
		var wicRect = (WICRect)area;

		if (src is PlanarPixelSource plsrc)
		{
			using var srcY = new ComPtr<IWICBitmapSource>(plsrc.SourceY.AsIWICBitmapSource());
			using var srcCb = new ComPtr<IWICBitmapSource>(plsrc.SourceCb.AsIWICBitmapSource());
			using var srcCr = new ComPtr<IWICBitmapSource>(plsrc.SourceCr.AsIWICBitmapSource());
			var planes = stackalloc[] { srcY.Get(), srcCb.Get(), srcCr.Get() };

			using var pframe = default(ComPtr<IWICPlanarBitmapFrameEncode>);
			HRESULT.Check(frame->QueryInterface(__uuidof<IWICPlanarBitmapFrameEncode>(), (void**)pframe.GetAddressOf()));
			HRESULT.Check(pframe.Get()->WriteSource(planes, 3, area.IsEmpty ? null : &wicRect));
		}
		else
		{
			var oformat = src.Format.FormatGuid;
			HRESULT.Check(frame->SetPixelFormat(&oformat));
			if (oformat != src.Format.FormatGuid)
			{
				Debug.WriteLine($"Conversion Missing {PixelFormat.FromGuid(src.Format.FormatGuid).Name} -> {PixelFormat.FromGuid(oformat).Name}");

				using var conv = default(ComPtr<IWICFormatConverter>);
				HRESULT.Check(Wic.Factory->CreateFormatConverter(conv.GetAddressOf()));
				HRESULT.Check(conv.Get()->Initialize(src.AsIWICBitmapSource(), &oformat, WICBitmapDitherType.WICBitmapDitherTypeNone, null, 0.0, WICBitmapPaletteType.WICBitmapPaletteTypeCustom));

				HRESULT.Check(frame->WriteSource((IWICBitmapSource*)conv.Get(), area.IsEmpty ? null : &wicRect));
			}
			else
			{
				if (oformat == PixelFormat.Indexed8.FormatGuid)
				{
					using var pal = default(ComPtr<IWICPalette>);
					HRESULT.Check(Wic.Factory->CreatePalette(pal.GetAddressOf()));

					if (src is IIndexedPixelSource idxs)
					{
						var palspan = idxs.Palette;
						fixed (uint* ppal = palspan)
							HRESULT.Check(pal.Get()->InitializeCustom(ppal, (uint)palspan.Length));
					}
					else if (src is WicPixelSource wicsrc)
					{
						wicsrc.CopyPalette(pal);
					}

					HRESULT.Check(frame->SetPalette(pal));
				}

				using var bmpsrc = new ComPtr<IWICBitmapSource>(src.AsIWICBitmapSource());
				HRESULT.Check(frame->WriteSource(bmpsrc, area.IsEmpty ? null : &wicRect));
			}
		}

		HRESULT.Check(frame->Commit());
	}

	public void Commit() => HRESULT.Check(WicEncoder->Commit());

	private void dispose(bool disposing)
	{
		if (WicEncoder is null)
			return;

		WicEncoder->Release();
		WicEncoder = null;

		if (disposing)
			GC.SuppressFinalize(this);
	}

	public void Dispose() => dispose(true);

	~WicImageEncoder()
	{
		ThrowHelper.ThrowIfFinalizerExceptionsEnabled(nameof(WicImageEncoder));

		dispose(false);
	}
}