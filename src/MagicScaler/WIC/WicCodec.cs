// Copyright © Clinton Ingram and Contributors.  Licensed under the MIT License.

using System;
using System.IO;
using System.Linq;
using System.Buffers;
using System.Threading;
using System.Diagnostics;
using System.Collections.Generic;
using System.Runtime.InteropServices;

using TerraFX.Interop;
using static TerraFX.Interop.Windows;

using PhotoSauce.Interop.Wic;
using PhotoSauce.MagicScaler.Transforms;

namespace PhotoSauce.MagicScaler
{
	internal static unsafe class WicImageDecoder
	{
		public static readonly IReadOnlyDictionary<Guid, FileFormat> FormatMap = new Dictionary<Guid, FileFormat> {
			[GUID_ContainerFormatBmp] = FileFormat.Bmp,
			[GUID_ContainerFormatGif] = FileFormat.Gif,
			[GUID_ContainerFormatJpeg] = FileFormat.Jpeg,
			[GUID_ContainerFormatPng] = FileFormat.Png,
			[GUID_ContainerFormatTiff] = FileFormat.Tiff
		};

		private static IWICBitmapDecoder* createDecoder(IStream* pStream)
		{
			if (Thread.CurrentThread.GetApartmentState() == ApartmentState.STA)
				throw new NotSupportedException("WIC integration is not supported on an STA thread, such as the UI thread in a WinForms or WPF application. Use a background thread (e.g. using Task.Run()) instead.");

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

		public static WicImageContainer Load(byte* pbBuffer, int cbBuffer, bool ownCopy = false)
		{
			using var stream = default(ComPtr<IWICStream>);
			HRESULT.Check(Wic.Factory->CreateStream(stream.GetAddressOf()));
			var ptr = (IntPtr)pbBuffer;

			using var mem = default(SafeHandleReleaser);
			if (ownCopy)
			{
				ptr = mem.Attach(new SafeHGlobalHandle(cbBuffer)).DangerousGetHandle();
				Buffer.MemoryCopy(pbBuffer, ptr.ToPointer(), cbBuffer, cbBuffer);
			}

			HRESULT.Check(stream.Get()->InitializeFromMemory((byte*)ptr, (uint)cbBuffer));

			var dec = createDecoder((IStream*)stream.Get());
			return WicImageContainer.Create(dec, mem.Detach());
		}
	}

	internal sealed unsafe class WicImageEncoder : IDisposable
	{
		private static readonly IReadOnlyDictionary<FileFormat, Guid> formatMap = new Dictionary<FileFormat, Guid> {
			[FileFormat.Bmp] = GUID_ContainerFormatBmp,
			[FileFormat.Gif] = GUID_ContainerFormatGif,
			[FileFormat.Jpeg] = GUID_ContainerFormatJpeg,
			[FileFormat.Png] = GUID_ContainerFormatPng,
			[FileFormat.Png8] = GUID_ContainerFormatPng,
			[FileFormat.Tiff] = GUID_ContainerFormatTiff
		};

		public IWICBitmapEncoder* WicEncoder { get; private set; }

		public WicImageEncoder(FileFormat format, Stream stm)
		{
			var fmt = formatMap.GetValueOrDefault(format, GUID_ContainerFormatPng);

			using var ccw = new ComPtr<IStream>(IStreamImpl.Wrap(stm));
			using var encoder = default(ComPtr<IWICBitmapEncoder>);
			HRESULT.Check(Wic.Factory->CreateEncoder(&fmt, null, encoder.GetAddressOf()));
			HRESULT.Check(encoder.Get()->Initialize(ccw, WICBitmapEncoderCacheOption.WICBitmapEncoderNoCache));

			WicEncoder = encoder.Detach();
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

	internal sealed unsafe class WicImageEncoderFrame : IDisposable
	{
		public IWICBitmapFrameEncode* WicEncoderFrame { get; private set; }

		public WicImageEncoderFrame(PipelineContext ctx, WicImageEncoder encoder, PixelArea area = default)
		{
			var fmt = ctx.Settings.SaveFormat;
			var encArea = area.IsEmpty ? ctx.Source.Area : area;
			var colorMode = ctx.Settings.ColorProfileMode;

			using var frame = default(ComPtr<IWICBitmapFrameEncode>);
			using (var pbag = default(ComPtr<IPropertyBag2>))
			{
				HRESULT.Check(encoder.WicEncoder->CreateNewFrame(frame.GetAddressOf(), pbag.GetAddressOf()));

				if (fmt == FileFormat.Jpeg)
					pbag.Write("ImageQuality", ctx.Settings.JpegQuality / 100f);

				if (fmt == FileFormat.Jpeg && ctx.Settings.JpegSubsampleMode != ChromaSubsampleMode.Default)
					pbag.Write("JpegYCrCbSubsampling", (byte)ctx.Settings.JpegSubsampleMode);

				if (fmt == FileFormat.Tiff)
					pbag.Write("TiffCompressionMethod", (byte)WICTiffCompressionOption.WICTiffCompressionNone);

				if (fmt == FileFormat.Bmp && ctx.Source.Format.AlphaRepresentation != PixelAlphaRepresentation.None)
					pbag.Write("EnableV5Header32bppBGRA", true);

				HRESULT.Check(frame.Get()->Initialize(pbag));
			}

			HRESULT.Check(frame.Get()->SetSize((uint)encArea.Width, (uint)encArea.Height));
			HRESULT.Check(frame.Get()->SetResolution(ctx.Settings.DpiX > 0d ? ctx.Settings.DpiX : ctx.ImageFrame.DpiX, ctx.Settings.DpiY > 0d ? ctx.Settings.DpiY : ctx.ImageFrame.DpiY));

			bool copySourceMetadata = ctx.ImageFrame is WicImageFrame srcFrame && srcFrame.WicMetadataReader is not null && ctx.Settings.MetadataNames != Enumerable.Empty<string>();
			bool writeOrientation = ctx.Settings.OrientationMode == OrientationMode.Preserve && ctx.ImageFrame.ExifOrientation != Orientation.Normal;
			bool writeColorContext = colorMode == ColorProfileMode.NormalizeAndEmbed || colorMode == ColorProfileMode.Preserve || (colorMode == ColorProfileMode.Normalize && ctx.DestColorProfile != ColorProfile.sRGB && ctx.DestColorProfile != ColorProfile.sGrey);

			using var metawriter = default(ComPtr<IWICMetadataQueryWriter>);
			if ((copySourceMetadata || writeOrientation) && SUCCEEDED(frame.Get()->GetMetadataQueryWriter(metawriter.GetAddressOf())))
			{
				if (copySourceMetadata)
				{
					var wicFrame = (WicImageFrame)ctx.ImageFrame;
					foreach (string prop in ctx.Settings.MetadataNames)
					{
						var pv = default(PROPVARIANT);
						if (SUCCEEDED(wicFrame.WicMetadataReader->GetMetadataByName(prop, &pv)) && pv.vt != (ushort)VARENUM.VT_EMPTY)
							_ = metawriter.Get()->SetMetadataByName(prop, &pv);
					}
				}

				if (writeOrientation)
				{
					string orientationPath = ctx.Settings.SaveFormat == FileFormat.Jpeg ? Wic.Metadata.OrientationJpeg : Wic.Metadata.OrientationExif;
					var pv = new PROPVARIANT { vt = (ushort)VARENUM.VT_UI2 };
					pv.Anonymous.uiVal = (ushort)ctx.ImageFrame.ExifOrientation;
					_ = metawriter.Get()->SetMetadataByName(orientationPath, &pv);
				}
			}

			if (writeColorContext)
			{
				Debug.Assert(ctx.WicContext.DestColorContext is not null || ctx.DestColorProfile is not null);

				// WIC writes gAMA and cHRM tags along with iCCP when a V2 ICC profile is written to a PNG frame.
				// Chromium ignores the iCCP tag if the others are present, so we keep the V4 reference profiles for PNG.
				var cc = ctx.WicContext.DestColorContext;
				if (fmt != FileFormat.Png)
				{
					if (ctx.DestColorProfile == ColorProfile.sRGB)
						cc = WicColorProfile.SrgbCompact.Value.WicColorContext;
					else if (ctx.DestColorProfile == ColorProfile.sGrey)
						cc = WicColorProfile.GreyCompact.Value.WicColorContext;
					else if (ctx.DestColorProfile == ColorProfile.AdobeRgb)
						cc = WicColorProfile.AdobeRgb.Value.WicColorContext;
					else if (ctx.DestColorProfile == ColorProfile.DisplayP3)
						cc = WicColorProfile.DisplayP3Compact.Value.WicColorContext;
				}

				using var wcc = default(ComPtr<IWICColorContext>);
				if (cc is null)
				{
					wcc.Attach(WicColorProfile.CreateContextFromProfile(ctx.DestColorProfile!.ProfileBytes));
					cc = wcc;
				}

				_ = frame.Get()->SetColorContexts(1, &cc);
			}

			WicEncoderFrame = frame.Detach();
		}

		public void WriteSource(PipelineContext ctx, PixelArea area = default)
		{
			var wicFrame = WicEncoderFrame;
			var wicRect = area.ToWicRect();

			if (ctx.PlanarContext is not null)
			{
				var oformat = GUID_WICPixelFormat24bppBGR;
				HRESULT.Check(wicFrame->SetPixelFormat(&oformat));

				using var srcY = new ComPtr<IWICBitmapSource>(ctx.PlanarContext.SourceY.AsIWICBitmapSource());
				using var srcCb = new ComPtr<IWICBitmapSource>(ctx.PlanarContext.SourceCb.AsIWICBitmapSource());
				using var srcCr = new ComPtr<IWICBitmapSource>(ctx.PlanarContext.SourceCr.AsIWICBitmapSource());
				var planes = stackalloc[] { srcY.Get(), srcCb.Get(), srcCr.Get() };

				using var pframe = default(ComPtr<IWICPlanarBitmapFrameEncode>);
				HRESULT.Check(wicFrame->QueryInterface(__uuidof<IWICPlanarBitmapFrameEncode>(), (void**)pframe.GetAddressOf()));
				HRESULT.Check(pframe.Get()->WriteSource(planes, 3, area.IsEmpty ? null : &wicRect));
			}
			else
			{
				var oformat = ctx.Source.Format.FormatGuid;
				HRESULT.Check(wicFrame->SetPixelFormat(&oformat));
				if (oformat != ctx.Source.Format.FormatGuid)
				{
					var ptt = WICBitmapPaletteType.WICBitmapPaletteTypeCustom;
					using var pal = default(ComPtr<IWICPalette>);
					if (PixelFormat.FromGuid(oformat).NumericRepresentation == PixelNumericRepresentation.Indexed)
					{
						HRESULT.Check(Wic.Factory->CreatePalette(pal.GetAddressOf()));
						HRESULT.Check(pal.Get()->InitializePredefined(WICBitmapPaletteType.WICBitmapPaletteTypeFixedGray256, 0));
						ptt = WICBitmapPaletteType.WICBitmapPaletteTypeFixedGray256;

						HRESULT.Check(wicFrame->SetPalette(pal));
					}

					using var conv = default(ComPtr<IWICFormatConverter>);
					HRESULT.Check(Wic.Factory->CreateFormatConverter(conv.GetAddressOf()));
					HRESULT.Check(conv.Get()->Initialize(ctx.Source.AsIWICBitmapSource(), &oformat, WICBitmapDitherType.WICBitmapDitherTypeNone, pal, 0.0, ptt));

					ctx.Source = ctx.AddDispose(new ComPtr<IWICBitmapSource>((IWICBitmapSource*)conv.Get()).AsPixelSource($"{nameof(IWICFormatConverter)}: {ctx.Source.Format.Name}->{PixelFormat.FromGuid(oformat).Name}", false));
				}
				else if (oformat == PixelFormat.Indexed8Bpp.FormatGuid)
				{
					Debug.Assert(ctx.WicContext.DestPalette is not null);

					HRESULT.Check(wicFrame->SetPalette(ctx.WicContext.DestPalette));
				}

				using var src = new ComPtr<IWICBitmapSource>(ctx.Source.AsIWICBitmapSource());
				HRESULT.Check(wicFrame->WriteSource(src, area.IsEmpty ? null : &wicRect));
			}

			HRESULT.Check(wicFrame->Commit());
		}

		public void Dispose()
		{
			dispose(true);
			GC.SuppressFinalize(this);
		}

		private void dispose(bool disposing)
		{
			if (WicEncoderFrame is null)
				return;

			WicEncoderFrame->Release();
			WicEncoderFrame = null;
		}

		~WicImageEncoderFrame() => dispose(false);
	}

	internal sealed unsafe class WicColorProfile : IDisposable
	{
		public static readonly Lazy<WicColorProfile> Cmyk = new(() => new WicColorProfile(getDefaultColorContext(PixelFormat.Cmyk32Bpp.FormatGuid), null));
		public static readonly Lazy<WicColorProfile> Srgb = new(() => new WicColorProfile(CreateContextFromProfile(IccProfiles.sRgbV4.Value), ColorProfile.sRGB));
		public static readonly Lazy<WicColorProfile> Grey = new(() => new WicColorProfile(CreateContextFromProfile(IccProfiles.sGreyV4.Value), ColorProfile.sGrey));
		public static readonly Lazy<WicColorProfile> DisplayP3 = new(() => new WicColorProfile(CreateContextFromProfile(IccProfiles.DisplayP3V4.Value), ColorProfile.DisplayP3));
		public static readonly Lazy<WicColorProfile> SrgbCompact = new(() => new WicColorProfile(CreateContextFromProfile(IccProfiles.sRgbCompact.Value), ColorProfile.sRGB));
		public static readonly Lazy<WicColorProfile> GreyCompact = new(() => new WicColorProfile(CreateContextFromProfile(IccProfiles.sGreyCompact.Value), ColorProfile.sGrey));
		public static readonly Lazy<WicColorProfile> AdobeRgb = new(() => new WicColorProfile(CreateContextFromProfile(IccProfiles.AdobeRgb.Value), ColorProfile.AdobeRgb));
		public static readonly Lazy<WicColorProfile> DisplayP3Compact = new(() => new WicColorProfile(CreateContextFromProfile(IccProfiles.DisplayP3Compact.Value), ColorProfile.DisplayP3));

		public static WicColorProfile GetDefaultFor(PixelFormat fmt) => fmt.ColorRepresentation switch {
			PixelColorRepresentation.Cmyk => Cmyk.Value,
			PixelColorRepresentation.Grey => Grey.Value,
			_ => Srgb.Value
		};

		public static WicColorProfile GetSourceProfile(WicColorProfile wicprof, ColorProfileMode mode)
		{
			var prof = ColorProfile.GetSourceProfile(wicprof.ParsedProfile, mode);
			return MapKnownProfile(prof) ?? wicprof;
		}

		public static WicColorProfile GetDestProfile(WicColorProfile wicprof, ColorProfileMode mode)
		{
			var prof = ColorProfile.GetDestProfile(wicprof.ParsedProfile, mode);
			return MapKnownProfile(prof) ?? wicprof;
		}

		public static WicColorProfile? MapKnownProfile(ColorProfile prof)
		{
			if (prof == ColorProfile.sGrey)
				return Grey.Value;
			if (prof == ColorProfile.sRGB)
				return Srgb.Value;
			if (prof == ColorProfile.AdobeRgb)
				return AdobeRgb.Value;
			if (prof == ColorProfile.DisplayP3)
				return DisplayP3.Value;

			return null;
		}

		public static IWICColorContext* CreateContextFromProfile(Span<byte> profile)
		{
			fixed (byte* pprop = profile)
			{
				using var cc = default(ComPtr<IWICColorContext>);
				HRESULT.Check(Wic.Factory->CreateColorContext(cc.GetAddressOf()));
				HRESULT.Check(cc.Get()->InitializeFromMemory(pprop, (uint)profile.Length));
				return cc.Detach();
			}
		}

		public static ColorProfile GetProfileFromContext(IWICColorContext* cc, uint cb)
		{
			if (cb == 0u)
				HRESULT.Check(cc->GetProfileBytes(0, null, &cb));

			using var buff = new PoolBuffer<byte>((int)cb);
			fixed (byte* pbuff = buff.Span)
				HRESULT.Check(cc->GetProfileBytes(cb, pbuff, &cb));

			return ColorProfile.Cache.GetOrAdd(buff.Span);
		}

		private static IWICColorContext* getDefaultColorContext(Guid pixelFormat)
		{
			using var wci = default(ComPtr<IWICComponentInfo>);
			using var pfi = default(ComPtr<IWICPixelFormatInfo>);
			HRESULT.Check(Wic.Factory->CreateComponentInfo(&pixelFormat, wci.GetAddressOf()));
			HRESULT.Check(wci.As(&pfi));

			using var wcc = default(ComPtr<IWICColorContext>);
			HRESULT.Check(pfi.Get()->GetColorContext(wcc.GetAddressOf()));
			return wcc.Detach();
		}

		public IWICColorContext* WicColorContext { get; private set; }
		public ColorProfile ParsedProfile { get; }

		private readonly bool ownContext;

		public WicColorProfile(IWICColorContext* cc, ColorProfile? prof, bool ownctx = false)
		{
			ownContext = ownctx;
			WicColorContext = cc;
			ParsedProfile = prof ?? GetProfileFromContext(cc, 0);

			if (ownctx && cc is not null)
				cc->AddRef();
		}

		public void Dispose()
		{
			dispose(true);
			GC.SuppressFinalize(this);
		}

		private void dispose(bool disposing)
		{
			if (!ownContext || WicColorContext is null)
				return;

			WicColorContext->Release();
			WicColorContext = null;
		}

		~WicColorProfile() => dispose(false);
	}

	internal sealed unsafe class WicAnimatedGifEncoder
	{
		public class BufferFrame
		{
			public readonly FrameBufferSource Source;
			public PixelArea Area;
			public GifDisposalMethod Disposal;
			public int Delay;
			public bool Trans;

			public BufferFrame(int width, int height, PixelFormat format) =>
				Source = new FrameBufferSource(width, height, format);
		}

		private readonly PipelineContext ctx;
		private readonly WicImageEncoder encoder;
		private readonly PixelSource lastSource;
		private readonly BufferFrame[] frames = new BufferFrame[3];
		private readonly int lastFrame;

		private int currentFrame;

		public BufferFrame EncodeFrame { get; }
		public BufferFrame Current => frames[currentFrame % 3];
		public BufferFrame? Previous => currentFrame == 0 ? null : frames[(currentFrame - 1) % 3];
		public BufferFrame? Next => currentFrame == lastFrame ? null : frames[(currentFrame + 1) % 3];

		public WicAnimatedGifEncoder(PipelineContext ctx, WicImageEncoder enc)
		{
			this.ctx = ctx;
			encoder = enc;

			lastSource = ctx.Source;
			lastFrame = ctx.ImageContainer.FrameCount - 1;

			EncodeFrame = new BufferFrame(lastSource.Width, lastSource.Height, lastSource.Format);
			for (int i = 0; i < frames.Length; i++)
				frames[i] = new BufferFrame(lastSource.Width, lastSource.Height, lastSource.Format);

			loadFrame(Current);
			Current.Source.Span.CopyTo(EncodeFrame.Source.Span);

			moveToFrame(1);
			loadFrame(Next!);
		}

		public void WriteGlobalMetadata()
		{
			var cnt = ctx.ImageContainer as WicGifContainer ?? throw new InvalidOperationException("Source must be a GIF");

			using var decmeta = default(ComPtr<IWICMetadataQueryReader>);
			using var encmeta = default(ComPtr<IWICMetadataQueryWriter>);

			HRESULT.Check(cnt.WicDecoder->GetMetadataQueryReader(decmeta.GetAddressOf()));
			HRESULT.Check(encoder.WicEncoder->GetMetadataQueryWriter(encmeta.GetAddressOf()));

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
			//pv.Anonymous.uiVal = (ushort)ctx.Source.Width;
			//HRESULT.Check(encmeta.Get()->SetMetadataByName(Wic.Metadata.Gif.LogicalScreenWidth, &pv));
			//pv.Anonymous.uiVal = (ushort)ctx.Source.Height;
			//HRESULT.Check(encmeta.Get()->SetMetadataByName(Wic.Metadata.Gif.LogicalScreenHeight, &pv));

			if (decmeta.GetValueOrDefault<bool>(Wic.Metadata.Gif.GlobalPaletteFlag))
			{
				using var pal = default(ComPtr<IWICPalette>);
				HRESULT.Check(Wic.Factory->CreatePalette(pal.GetAddressOf()));
				HRESULT.Check(cnt.WicDecoder->CopyPalette(pal));
				HRESULT.Check(encoder.WicEncoder->SetPalette(pal));

				if (SUCCEEDED(decmeta.Get()->GetMetadataByName(Wic.Metadata.Gif.BackgroundColorIndex, &pv)))
				{
					HRESULT.Check(encmeta.Get()->SetMetadataByName(Wic.Metadata.Gif.BackgroundColorIndex, &pv));
					HRESULT.Check(PropVariantClear(&pv));
				}
			}
		}

		public void WriteFrames()
		{
			uint bgColor = ((WicGifContainer)ctx.ImageContainer).BackgroundColor;

			writeFrame(Current);

			while (moveNext())
			{
				TemporalFilters.Dedupe(this, bgColor);

				writeFrame(Current);
			}
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
			ctx.Settings.FrameIndex = index;

			ctx.ImageFrame.Dispose();
			ctx.ImageFrame = ctx.ImageContainer.GetFrame(index);

			if (ctx.ImageFrame is WicImageFrame wicFrame)
				ctx.Source = wicFrame.Source;
			else
				ctx.Source = ctx.ImageFrame.PixelSource.AsPixelSource();

			MagicTransforms.AddGifFrameBuffer(ctx, false);

			if (lastSource is ChainedPixelSource chain && chain.Passthrough)
			{
				chain.ReInit(ctx.Source);
				ctx.Source = chain;
			}
		}

		private void loadFrame(BufferFrame frame)
		{
			using var srcmeta = new ComPtr<IWICMetadataQueryReader>(((WicImageFrame)ctx.ImageFrame).WicMetadataReader);

			frame.Disposal = (GifDisposalMethod)srcmeta.GetValueOrDefault<byte>(Wic.Metadata.Gif.FrameDisposal) == GifDisposalMethod.RestoreBackground ? GifDisposalMethod.RestoreBackground : GifDisposalMethod.Preserve;
			frame.Delay = srcmeta.GetValueOrDefault<ushort>(Wic.Metadata.Gif.FrameDelay);
			frame.Trans = srcmeta.GetValueOrDefault<bool>(Wic.Metadata.Gif.TransparencyFlag);
			frame.Area = ctx.Source.Area;

			var buff = frame.Source;
			fixed (byte* pbuff = buff.Span)
				ctx.Source.CopyPixels(frame.Area, buff.Stride, buff.Span.Length, (IntPtr)pbuff);
		}

		private void writeFrame(BufferFrame src)
		{
			using var quant = new OctreeQuantizer();
			using var buffI = new FrameBufferSource(ctx.Source.Width, ctx.Source.Height, PixelFormat.Indexed8Bpp);
			var buffC = EncodeFrame.Source;

			quant.CreateHistorgram(buffC.Span, buffC.Width, buffC.Height, buffC.Stride);
			quant.Quantize(buffC.Span, buffI.Span, buffC.Width, buffC.Height, buffC.Stride, buffI.Stride);
			var palette = quant.Palette;

			using var pal = default(ComPtr<IWICPalette>);
			HRESULT.Check(Wic.Factory->CreatePalette(pal.GetAddressOf()));

			fixed(uint* ppal = palette)
				HRESULT.Check(pal.Get()->InitializeCustom(ppal, (uint)palette.Length));

			ctx.WicContext.DestPalette = pal;
			ctx.Source = buffI;

			using var frm = new WicImageEncoderFrame(ctx, encoder, src.Area);
			using var frmmeta = default(ComPtr<IWICMetadataQueryWriter>);
			HRESULT.Check(frm.WicEncoderFrame->GetMetadataQueryWriter(frmmeta.GetAddressOf()));

			frmmeta.SetValue(Wic.Metadata.Gif.FrameDisposal, (byte)src.Disposal);
			frmmeta.SetValue(Wic.Metadata.Gif.FrameDelay, (ushort)src.Delay);

			if (src.Area.X != 0)
				frmmeta.SetValue(Wic.Metadata.Gif.FrameLeft, (ushort)src.Area.X);
			if (src.Area.Y != 0)
				frmmeta.SetValue(Wic.Metadata.Gif.FrameTop, (ushort)src.Area.Y);

			frmmeta.SetValue(Wic.Metadata.Gif.TransparencyFlag, true);
			frmmeta.SetValue(Wic.Metadata.Gif.TransparentColorIndex, (byte)(palette.Length - 1));

			frm.WriteSource(ctx, src.Area);

			ctx.WicContext.DestPalette = null;
		}
	}
}