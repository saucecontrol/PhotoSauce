// Copyright © Clinton Ingram and Contributors.  Licensed under the MIT License.

using System;
using System.IO;
using System.Drawing;
using System.Buffers;
using System.Buffers.Binary;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Diagnostics.CodeAnalysis;
#if WICPROCESSOR
using System.Collections.Generic;
#endif

using TerraFX.Interop.Windows;
using static TerraFX.Interop.Windows.GUID;
using static TerraFX.Interop.Windows.WINCODEC;
using static TerraFX.Interop.Windows.Windows;

using PhotoSauce.Interop.Wic;
using PhotoSauce.MagicScaler.Transforms;

namespace PhotoSauce.MagicScaler
{
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

	internal sealed unsafe class WicImageEncoder : IImageEncoder, IDisposable
	{
		public IWICBitmapEncoder* WicEncoder { get; private set; }
		public IEncoderOptions? Options { get; }

		public WicImageEncoder(Guid clsid, string mime, Stream stm, IEncoderOptions? options)
		{
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
					int qual = Options is ILossyEncoderOptions lopt && lopt.Quality != default
						? lopt.Quality
						: SettingsUtil.GetDefaultQuality(Math.Max(source.Width, source.Height));

					var subs = Options is IPlanarEncoderOptions popt && popt.Subsample != default
						? popt.Subsample
						: SettingsUtil.GetDefaultSubsampling(qual);

					pbag.Write("ImageQuality", qual / 100f);
					pbag.Write("JpegYCrCbSubsampling", (byte)subs);

					if (Options is JpegEncoderOptions jconf && jconf.SuppressApp0)
						pbag.Write("SuppressApp0", jconf.SuppressApp0);
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
					int qual = Options is ILossyEncoderOptions opt && opt.Quality != 0
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

				// We give preference to V2 compact profiles when possible, for the file size and compatibility advantages.
				// However, WIC writes gAMA and cHRM tags along with iCCP when a V2 ICC profile is written to a PNG frame.
				// Chromium ignores the iCCP tag if the others are present, so we keep the V4 reference profiles for PNG.
				var cc = default(IWICColorContext*);
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

			if (hasWriter && meta is WicAnimatedGifEncoder.AnimationBufferFrame && meta.TryGetMetadata<AnimationFrame>(out var anifrm))
			{
				metawriter.SetValue(Wic.Metadata.Gif.FrameDisposal, (byte)anifrm.Disposal);
				metawriter.SetValue(Wic.Metadata.Gif.FrameDelay, (ushort)anifrm.Duration.Numerator);

				if (anifrm.OffsetLeft != 0)
					metawriter.SetValue(Wic.Metadata.Gif.FrameLeft, (ushort)anifrm.OffsetLeft);
				if (anifrm.OffsetTop != 0)
					metawriter.SetValue(Wic.Metadata.Gif.FrameTop, (ushort)anifrm.OffsetTop);
			}

			if (hasWriter && fmt == GUID_ContainerFormatGif && source is IndexedColorTransform idxt)
			{
				var pal = idxt.Palette;
				if (pal[^1] <= 0x00ffffffu)
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
				var oformat = GUID_WICPixelFormat24bppBGR;
				HRESULT.Check(frame->SetPixelFormat(&oformat));

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

						if (src is IndexedColorTransform iconv)
						{
							var palspan = iconv.Palette;
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

		~WicImageEncoder() => dispose(false);
	}

	internal sealed unsafe class WicAnimatedGifEncoder : IDisposable
	{
		public sealed class AnimationBufferFrame : IMetadataSource, IDisposable
		{
			public readonly FrameBufferSource Source;
			public PixelArea Area;
			public FrameDisposalMethod Disposal;
			public uint Delay;
			public bool Trans;

			public AnimationBufferFrame(int width, int height, PixelFormat format) =>
				Source = new FrameBufferSource(width, height, format);

			public bool TryGetMetadata<T>([NotNullWhen(true)] out T? metadata) where T : IMetadata
			{
				if (typeof(T) == typeof(AnimationFrame))
				{
					metadata = (T)(object)(new AnimationFrame(Area.X, Area.Y, new Rational(Delay, 100), Disposal, true));
					return true;
				}

				metadata = default;
				return false;
			}

			public void Dispose() => Source.Dispose();
		}

		private readonly PipelineContext context;
		private readonly WicImageEncoder encoder;
		private readonly PixelSource lastSource;
		private readonly AnimationBufferFrame[] frames = new AnimationBufferFrame[3];
		private readonly int lastFrame;

		private int currentFrame;

		public IndexedColorTransform IndexedSource { get; }
		public AnimationBufferFrame EncodeFrame { get; }
		public AnimationBufferFrame Current => frames[currentFrame % 3];
		public AnimationBufferFrame? Previous => currentFrame == 0 ? null : frames[(currentFrame - 1) % 3];
		public AnimationBufferFrame? Next => currentFrame == lastFrame ? null : frames[(currentFrame + 1) % 3];

		public WicAnimatedGifEncoder(PipelineContext ctx, WicImageEncoder enc)
		{
			context = ctx;
			encoder = enc;

			if (ctx.Source.Format != PixelFormat.Bgra32)
				ctx.Source = ctx.AddProfiler(new ConversionTransform(ctx.Source, PixelFormat.Bgra32));

			lastSource = ctx.Source;
			lastFrame = ctx.ImageContainer.FrameCount - 1;

			EncodeFrame = new AnimationBufferFrame(lastSource.Width, lastSource.Height, lastSource.Format);
			IndexedSource = ctx.AddProfiler(new IndexedColorTransform(EncodeFrame.Source));
			for (int i = 0; i < frames.Length; i++)
				frames[i] = new AnimationBufferFrame(lastSource.Width, lastSource.Height, lastSource.Format);

			loadFrame(Current);
			Current.Source.Span.CopyTo(EncodeFrame.Source.Span);

			moveToFrame(1);
			loadFrame(Next!);
		}

		public void WriteGlobalMetadata()
		{
			using var encmeta = default(ComPtr<IWICMetadataQueryWriter>);
			HRESULT.Check(encoder.WicEncoder->GetMetadataQueryWriter(encmeta.GetAddressOf()));

			if (context.ImageContainer is WicGifContainer cnt)
			{
				using var decmeta = default(ComPtr<IWICMetadataQueryReader>);
				HRESULT.Check(cnt.WicDecoder->GetMetadataQueryReader(decmeta.GetAddressOf()));

				var pv = default(PROPVARIANT);

				if (SUCCEEDED(decmeta.Get()->GetMetadataByName(Wic.Metadata.Gif.AppExtension, &pv)))
				{
					HRESULT.Check(encmeta.Get()->SetMetadataByName(Wic.Metadata.Gif.AppExtension, &pv));
					HRESULT.Check(PropVariantClear(&pv));
				}

				if (SUCCEEDED(decmeta.Get()->GetMetadataByName(Wic.Metadata.Gif.AppExtensionData, &pv)))
				{
					HRESULT.Check(encmeta.Get()->SetMetadataByName(Wic.Metadata.Gif.AppExtensionData, &pv));
					HRESULT.Check(PropVariantClear(&pv));
				}

				if (SUCCEEDED(decmeta.Get()->GetMetadataByName(Wic.Metadata.Gif.PixelAspectRatio, &pv)))
				{
					HRESULT.Check(encmeta.Get()->SetMetadataByName(Wic.Metadata.Gif.PixelAspectRatio, &pv));
					HRESULT.Check(PropVariantClear(&pv));
				}

				// TODO WIC ignores these and sets the logical screen descriptor dimensions from the first frame
				//pv.vt = (ushort)VARENUM.VT_UI2;
				//pv.Anonymous.uiVal = (ushort)context.Source.Width;
				//HRESULT.Check(encmeta.Get()->SetMetadataByName(Wic.Metadata.Gif.LogicalScreenWidth, &pv));
				//pv.Anonymous.uiVal = (ushort)context.Source.Height;
				//HRESULT.Check(encmeta.Get()->SetMetadataByName(Wic.Metadata.Gif.LogicalScreenHeight, &pv));

				if (decmeta.Get()->GetValueOrDefault<bool>(Wic.Metadata.Gif.GlobalPaletteFlag))
				{
					// TODO We don't need the entire global palette if we're only using the background color
					if (SUCCEEDED(decmeta.Get()->GetMetadataByName(Wic.Metadata.Gif.BackgroundColorIndex, &pv)))
					{
						using var pal = default(ComPtr<IWICPalette>);
						HRESULT.Check(Wic.Factory->CreatePalette(pal.GetAddressOf()));
						HRESULT.Check(cnt.WicDecoder->CopyPalette(pal));
						HRESULT.Check(encoder.WicEncoder->SetPalette(pal));

						HRESULT.Check(encmeta.Get()->SetMetadataByName(Wic.Metadata.Gif.BackgroundColorIndex, &pv));
						HRESULT.Check(PropVariantClear(&pv));
					}
				}
			}
			else if (context.ImageContainer.FrameCount > 1)
			{
				if (context.ImageContainer is not IMetadataSource cmsrc || !cmsrc.TryGetMetadata<AnimationContainer>(out var anicnt))
					anicnt = new AnimationContainer(context.Source.Width, context.Source.Height, context.ImageContainer.FrameCount);

				var pvae = new PROPVARIANT { vt = (ushort)(VARENUM.VT_UI1 | VARENUM.VT_VECTOR) };
				pvae.Anonymous.blob.cbSize = 11;
				pvae.Anonymous.blob.pBlobData = WicGifContainer.Netscape2_0.GetAddressOf();
				HRESULT.Check(encmeta.Get()->SetMetadataByName(Wic.Metadata.Gif.AppExtension, &pvae));

				byte* pvdd = stackalloc byte[] { 3, 1, 0, 0 };
				BinaryPrimitives.WriteUInt16LittleEndian(new Span<byte>(pvdd + 2, 2), (ushort)anicnt.LoopCount);

				var pvad = new PROPVARIANT { vt = (ushort)(VARENUM.VT_UI1 | VARENUM.VT_VECTOR) };
				pvad.Anonymous.blob.cbSize = 4;
				pvad.Anonymous.blob.pBlobData = pvdd;
				HRESULT.Check(encmeta.Get()->SetMetadataByName(Wic.Metadata.Gif.AppExtensionData, &pvad));
			}
		}

		public void WriteFrames()
		{
			uint bgColor = 0;
			if (context.ImageContainer is IMetadataSource cmsrc && cmsrc.TryGetMetadata<AnimationContainer>(out var anicnt))
				bgColor = (uint)anicnt.BackgroundColor;

			var ppt = context.AddProfiler(nameof(TemporalFilters));
			var ppq = context.AddProfiler($"{nameof(OctreeQuantizer)}: {nameof(OctreeQuantizer.CreatePalette)}");

			var encopt = context.Settings.EncoderOptions is GifEncoderOptions gifopt ? gifopt : GifEncoderOptions.Default;
			writeFrame(Current, encopt, ppq);

			while (moveNext())
			{
				ppt.ResumeTiming(Current.Source.Area);
				TemporalFilters.Dedupe(this, bgColor);
				ppt.PauseTiming();

				writeFrame(Current, encopt, ppq);
			}
		}

		public void Dispose()
		{
			lastSource.Dispose();
			EncodeFrame.Dispose();
			IndexedSource.Dispose();
			for (int i = 0; i < frames.Length; i++)
				frames[i].Dispose();
		}

		private bool moveNext()
		{
			if (currentFrame == lastFrame)
				return false;

			if (++currentFrame != lastFrame)
			{
				moveToFrame(currentFrame + 1);
				loadFrame(Next!);
			}

			return true;
		}

		private void moveToFrame(int index)
		{
			context.ImageFrame.Dispose();
			context.ImageFrame = context.ImageContainer.GetFrame(index);

			if (context.ImageFrame is IYccImageFrame yccFrame)
				context.Source = new PlanarPixelSource(yccFrame);
			else
				context.Source = context.ImageFrame.PixelSource.AsPixelSource();

			MagicTransforms.AddAnimationFrameBuffer(context, false);

			if ((context.Source is PlanarPixelSource plan ? plan.SourceY : context.Source) is IProfileSource prof)
				context.AddProfiler(prof);

			if (lastSource is ChainedPixelSource chain && chain.Passthrough)
			{
				chain.ReInit(context.Source);
				context.Source = chain;
			}
		}

		private void loadFrame(AnimationBufferFrame frame)
		{
			if (context.ImageFrame is not IMetadataSource fmsrc || !fmsrc.TryGetMetadata<AnimationFrame>(out var anifrm))
				anifrm = AnimationFrame.Default;

			frame.Disposal = anifrm.Disposal == FrameDisposalMethod.RestoreBackground ? FrameDisposalMethod.RestoreBackground : FrameDisposalMethod.Preserve;
			frame.Delay = ((uint)Math.Round(anifrm.Duration.Numerator / (double)anifrm.Duration.Denominator.Clamp(1, int.MaxValue) * 100d)).Clamp(ushort.MinValue, ushort.MaxValue);
			frame.Trans = anifrm.HasAlpha;
			frame.Area = context.Source.Area;

			var buff = frame.Source;
			fixed (byte* pbuff = buff.Span)
				context.Source.CopyPixels(frame.Area, buff.Stride, buff.Span.Length, pbuff);
		}

		private void writeFrame(AnimationBufferFrame src, in GifEncoderOptions gifopt, IProfiler ppq)
		{
			if (gifopt.PredefinedPalette is not null)
			{
				IndexedSource.SetPalette(MemoryMarshal.Cast<int, uint>(gifopt.PredefinedPalette.AsSpan()), gifopt.Dither == DitherMode.None);
			}
			else
			{
				using var quant = new OctreeQuantizer(ppq);
				var buffC = EncodeFrame.Source;
				var buffCSpan = buffC.Span[(src.Area.Y * buffC.Stride + src.Area.X * buffC.Format.BytesPerPixel)..];

				bool isExact = quant.CreatePalette(gifopt.MaxPaletteSize, buffCSpan, src.Area.Width, src.Area.Height, buffC.Stride);
				IndexedSource.SetPalette(quant.Palette, isExact || gifopt.Dither == DitherMode.None);
			}

			IndexedSource.ReInit(EncodeFrame.Source);

			context.Source = IndexedSource;
			context.Metadata = src;

			encoder.WriteFrame(context.Source, context.Metadata, src.Area);
		}
	}
}