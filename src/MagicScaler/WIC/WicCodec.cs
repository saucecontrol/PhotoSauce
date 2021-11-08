// Copyright © Clinton Ingram and Contributors.  Licensed under the MIT License.

using System;
using System.IO;
using System.Drawing;
using System.Buffers;
using System.Buffers.Binary;
using System.Diagnostics;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Diagnostics.CodeAnalysis;

using TerraFX.Interop;
using static TerraFX.Interop.Windows;

using PhotoSauce.Interop.Wic;
using PhotoSauce.MagicScaler.Transforms;

namespace PhotoSauce.MagicScaler
{
	internal static unsafe class WicImageDecoder
	{
		public static readonly Dictionary<Guid, FileFormat> FormatMap = new() {
			[GUID_ContainerFormatBmp] = FileFormat.Bmp,
			[GUID_ContainerFormatGif] = FileFormat.Gif,
			[GUID_ContainerFormatJpeg] = FileFormat.Jpeg,
			[GUID_ContainerFormatPng] = FileFormat.Png,
			[GUID_ContainerFormatTiff] = FileFormat.Tiff
		};

#if WICPROCESSOR
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

		public static WicImageContainer Load(string fileName)
		{
			using var stm = default(ComPtr<IWICStream>);
			HRESULT.Check(Wic.Factory->CreateStream(stm.GetAddressOf()));
			fixed (char* pname = fileName)
				HRESULT.Check(stm.Get()->InitializeFromFilename((ushort*)pname, GENERIC_READ));

			var dec = createDecoder((IStream*)stm.Get());
			return WicImageContainer.Create(dec);
		}

		public static WicImageContainer Load(Stream inStream)
		{
			using var ccw = new ComPtr<IStream>(IStreamImpl.Wrap(inStream));
			var dec = createDecoder(ccw);
			return WicImageContainer.Create(dec);
		}

		public static WicImageContainer Load(byte* pbBuffer, int cbBuffer)
		{
			using var stream = default(ComPtr<IWICStream>);
			HRESULT.Check(Wic.Factory->CreateStream(stream.GetAddressOf()));
			var ptr = (IntPtr)pbBuffer;

			HRESULT.Check(stream.Get()->InitializeFromMemory((byte*)ptr, (uint)cbBuffer));

			var dec = createDecoder((IStream*)stream.Get());
			return WicImageContainer.Create(dec);
		}
#endif

		public static WicImageContainer? Load(Guid clsid, Stream stream, IDecoderOptions? options)
		{
			Wic.EnsureFreeThreaded();

			using var ccw = new ComPtr<IStream>(IStreamImpl.Wrap(stream));
			using var decoder = default(ComPtr<IWICBitmapDecoder>);
			HRESULT.Check(CoCreateInstance(&clsid, null, (uint)CLSCTX.CLSCTX_INPROC_SERVER, __uuidof<IWICBitmapDecoder>(), (void**)decoder.GetAddressOf()));

			if (FAILED(decoder.Get()->Initialize(ccw, WICDecodeOptions.WICDecodeMetadataCacheOnDemand)))
				return null;

			return WicImageContainer.Create(decoder.Detach(), options);
		}
	}

	internal sealed unsafe class WicImageEncoder : IImageEncoder, IDisposable
	{
		public static readonly Dictionary<FileFormat, string> FormatMap = new() {
			[FileFormat.Bmp] = KnownMimeTypes.Bmp,
			[FileFormat.Gif] = KnownMimeTypes.Gif,
			[FileFormat.Jpeg] = KnownMimeTypes.Jpeg,
			[FileFormat.Png] = KnownMimeTypes.Png,
			[FileFormat.Png8] = KnownMimeTypes.Png,
			[FileFormat.Tiff] = KnownMimeTypes.Tiff
		};

		public IWICBitmapEncoder* WicEncoder { get; private set; }
		public FileFormat Format { get; }
		public IEncoderOptions? Options { get; }

		public WicImageEncoder(Guid clsid, Stream stm, IEncoderOptions? options)
		{
			using var ccw = new ComPtr<IStream>(IStreamImpl.Wrap(stm));
			using var encoder = default(ComPtr<IWICBitmapEncoder>);
			HRESULT.Check(CoCreateInstance(&clsid, null, (uint)CLSCTX.CLSCTX_INPROC_SERVER, __uuidof<IWICBitmapEncoder>(), (void**)encoder.GetAddressOf()));
			HRESULT.Check(encoder.Get()->Initialize(ccw, WICBitmapEncoderCacheOption.WICBitmapEncoderNoCache));

			var guid = default(Guid);
			HRESULT.Check(encoder.Get()->GetContainerFormat(&guid));

			Format = WicImageDecoder.FormatMap.GetValueOrDefault(guid, FileFormat.Unknown);
			Options = options;
			WicEncoder = encoder.Detach();
		}

		public void WriteFrame(IPixelSource source, IMetadataSource meta, Rectangle area)
		{
			var fmt = Format;
			var src = source.AsPixelSource();
			var encArea = area.IsEmpty ? src.Area : (PixelArea)area;

			using var frame = default(ComPtr<IWICBitmapFrameEncode>);
			using (var pbag = default(ComPtr<IPropertyBag2>))
			{
				HRESULT.Check(WicEncoder->CreateNewFrame(frame.GetAddressOf(), pbag.GetAddressOf()));

				if (fmt == FileFormat.Jpeg && Options is JpegEncoderOptions jconf)
				{
					pbag.Write("ImageQuality", jconf.Quality / 100f);
					if (jconf.Subsample != ChromaSubsampleMode.Default)
						pbag.Write("JpegYCrCbSubsampling", (byte)jconf.Subsample);
				}
				else if (fmt == FileFormat.Tiff && Options is TiffEncoderOptions tconf)
				{
					pbag.Write("TiffCompressionMethod", (byte)tconf.Compression);
				}
				else if (fmt == FileFormat.Bmp && src.Format.AlphaRepresentation != PixelAlphaRepresentation.None)
				{
					pbag.Write("EnableV5Header32bppBGRA", true);
				}

				HRESULT.Check(frame.Get()->Initialize(pbag));
			}

			using var metawriter = default(ComPtr<IWICMetadataQueryWriter>);
			bool hasWriter = SUCCEEDED(frame.Get()->GetMetadataQueryWriter(metawriter.GetAddressOf()));

			HRESULT.Check(frame.Get()->SetSize((uint)encArea.Width, (uint)encArea.Height));

			if (meta.TryGetMetadata<BaseImageProperties>(out var baseprops))
			{
				HRESULT.Check(frame.Get()->SetResolution(baseprops.DpiX, baseprops.DpiY));

				if (baseprops.ColorProfile is ColorProfile prof)
				{
					// We give preference to V2 compact profiles when possible, for the file size and compatibility advantages.
					// However, WIC writes gAMA and cHRM tags along with iCCP when a V2 ICC profile is written to a PNG frame.
					// Chromium ignores the iCCP tag if the others are present, so we keep the V4 reference profiles for PNG.
					var cc = default(IWICColorContext*);
					if (prof == ColorProfile.sRGB)
						cc = (fmt == FileFormat.Png ? WicColorProfile.Srgb : WicColorProfile.SrgbCompact).Value.WicColorContext;
					else if (prof == ColorProfile.sGrey)
						cc = (fmt == FileFormat.Png ? WicColorProfile.Grey : WicColorProfile.GreyCompact).Value.WicColorContext;
					else if (prof == ColorProfile.AdobeRgb)
						cc = (fmt == FileFormat.Png ? WicColorProfile.AdobeRgb : WicColorProfile.AdobeRgbCompact).Value.WicColorContext;
					else if (prof == ColorProfile.DisplayP3)
						cc = (fmt == FileFormat.Png ? WicColorProfile.DisplayP3 : WicColorProfile.DisplayP3Compact).Value.WicColorContext;

					using var wcc = default(ComPtr<IWICColorContext>);
					if (cc is null)
					{
						wcc.Attach(WicColorProfile.CreateContextFromProfile(prof.ProfileBytes));
						cc = wcc;
					}

					_ = frame.Get()->SetColorContexts(1, &cc);
				}

				if (hasWriter && baseprops.Orientation >= Orientation.Normal)
				{
					string orientationPath = fmt == FileFormat.Jpeg ? Wic.Metadata.OrientationJpeg : Wic.Metadata.OrientationExif;
					var pv = new PROPVARIANT { vt = (ushort)VARENUM.VT_UI2 };
					pv.Anonymous.uiVal = (ushort)baseprops.Orientation;
					_ = metawriter.Get()->SetMetadataByName(orientationPath, &pv);
				}
			}

			if (hasWriter && meta is WicAnimatedGifEncoder.AnimationBufferFrame && meta.TryGetMetadata<AnimationFrame>(out var anifrm))
			{
				metawriter.SetValue(Wic.Metadata.Gif.FrameDisposal, (byte)anifrm.Disposal);
				metawriter.SetValue(Wic.Metadata.Gif.FrameDelay, (ushort)anifrm.Duration.Numerator);

				if (anifrm.OffsetLeft != 0)
					metawriter.SetValue(Wic.Metadata.Gif.FrameLeft, (ushort)anifrm.OffsetLeft);
				if (anifrm.OffsetTop != 0)
					metawriter.SetValue(Wic.Metadata.Gif.FrameTop, (ushort)anifrm.OffsetTop);

				metawriter.SetValue(Wic.Metadata.Gif.TransparencyFlag, anifrm.HasAlpha);
				metawriter.SetValue(Wic.Metadata.Gif.TransparentColorIndex, (byte)(((IndexedColorTransform)src).Palette.Length - 1));
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
					Debug.Fail("Conversion Missing");

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

		public void Dispose()
		{
			dispose(true);
			GC.SuppressFinalize(this);
		}

		private void dispose(bool disposing)
		{
			if (WicEncoder is null)
				return;

			WicEncoder->Release();
			WicEncoder = null;
		}

		~WicImageEncoder() => dispose(false);
	}

	internal sealed unsafe class WicAnimatedGifEncoder : IDisposable
	{
		public sealed class AnimationBufferFrame : IMetadataSource, IDisposable
		{
			public readonly FrameBufferSource Source;
			public PixelArea Area;
			public FrameDisposalMethod Disposal;
			public int Delay;
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
				ctx.Source = ctx.AddProfiler(new ConversionTransform(ctx.Source, null, null, PixelFormat.Bgra32));

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

				if (decmeta.GetValueOrDefault<bool>(Wic.Metadata.Gif.GlobalPaletteFlag))
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
					anicnt = new AnimationContainer(context.Source.Width, context.Source.Height);

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
			var ppq = context.AddProfiler(nameof(OctreeQuantizer) + ": " + nameof(OctreeQuantizer.CreatePalette));

			writeFrame(Current, ppq);

			while (moveNext())
			{
				ppt.ResumeTiming(Current.Source.Area);
				TemporalFilters.Dedupe(this, bgColor);
				ppt.PauseTiming();

				writeFrame(Current, ppq);
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
			context.Settings.FrameIndex = index;

			context.ImageFrame.Dispose();
			context.ImageFrame = context.ImageContainer.GetFrame(index);

			if (context.ImageFrame is WicImageFrame wicFrame)
				context.Source = context.AddProfiler(wicFrame.Source);
			else if (context.ImageFrame is IYccImageFrame yccFrame)
				context.Source = new PlanarPixelSource(yccFrame.PixelSource.AsPixelSource(), yccFrame.PixelSourceCb.AsPixelSource(), yccFrame.PixelSourceCr.AsPixelSource(), !yccFrame.IsFullRange);
			else
				context.Source = context.ImageFrame.PixelSource.AsPixelSource();

			MagicTransforms.AddGifFrameBuffer(context, false);

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
			frame.Delay = ((int)Math.Round(anifrm.Duration.Numerator / (double)anifrm.Duration.Denominator.Clamp(1, int.MaxValue) * 100d)).Clamp(ushort.MinValue, ushort.MaxValue);
			frame.Trans = anifrm.HasAlpha;
			frame.Area = context.Source.Area;

			var buff = frame.Source;
			fixed (byte* pbuff = buff.Span)
				context.Source.CopyPixels(frame.Area, buff.Stride, buff.Span.Length, (IntPtr)pbuff);
		}

		private void writeFrame(AnimationBufferFrame src, IProfiler ppq)
		{
			using (var quant = new OctreeQuantizer())
			{
				var buffC = EncodeFrame.Source;
				var buffCSpan = buffC.Span.Slice(src.Area.Y * buffC.Stride + src.Area.X * buffC.Format.BytesPerPixel);

				ppq.ResumeTiming(src.Area);
				bool isExact = quant.CreatePalette(buffCSpan, src.Area.Width, src.Area.Height, buffC.Stride);
				ppq.PauseTiming();

				IndexedSource.SetPalette(quant.Palette, isExact);
				IndexedSource.ReInit(buffC);

				context.Source = IndexedSource;
				context.Metadata = src;
			}

			encoder.WriteFrame(context.Source, context.Metadata, src.Area);
		}
	}
}