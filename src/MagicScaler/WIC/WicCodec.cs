// Copyright © Clinton Ingram and Contributors.  Licensed under the MIT License.

using System;
using System.IO;
using System.Linq;
using System.Buffers;
using System.Threading;
using System.Diagnostics;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Runtime.CompilerServices;

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

#if WICPROCESSOR
		public static WicImageContainer Load(string fileName)
		{
			using var stm = default(ComPtr<IWICStream>);
			HRESULT.Check(Wic.Factory->CreateStream(stm.GetAddressOf()));
			fixed (char* pname = fileName)
				HRESULT.Check(stm.Get()->InitializeFromFilename((ushort*)pname, GENERIC_READ));

			var dec = createDecoder((IStream*)stm.Get());
			return WicImageContainer.Create(dec);
		}
#endif

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
						cc = WicColorProfile.AdobeRgbCompact.Value.WicColorContext;
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

			if (ctx.Source is PlanarPixelSource plsrc)
			{
				var oformat = GUID_WICPixelFormat24bppBGR;
				HRESULT.Check(wicFrame->SetPixelFormat(&oformat));

				using var srcY = new ComPtr<IWICBitmapSource>(plsrc.SourceY.AsIWICBitmapSource());
				using var srcCb = new ComPtr<IWICBitmapSource>(plsrc.SourceCb.AsIWICBitmapSource());
				using var srcCr = new ComPtr<IWICBitmapSource>(plsrc.SourceCr.AsIWICBitmapSource());
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
					//Debug.Fail("Conversion Missing");

					using var conv = default(ComPtr<IWICFormatConverter>);
					HRESULT.Check(Wic.Factory->CreateFormatConverter(conv.GetAddressOf()));
					HRESULT.Check(conv.Get()->Initialize(ctx.Source.AsIWICBitmapSource(), &oformat, WICBitmapDitherType.WICBitmapDitherTypeNone, null, 0.0, WICBitmapPaletteType.WICBitmapPaletteTypeCustom));

					HRESULT.Check(wicFrame->WriteSource((IWICBitmapSource*)conv.Get(), area.IsEmpty ? null : &wicRect));
				}
				else
				{
					if (oformat == PixelFormat.Indexed8.FormatGuid)
					{
						using var pal = default(ComPtr<IWICPalette>);
						HRESULT.Check(Wic.Factory->CreatePalette(pal.GetAddressOf()));

						if (ctx.Source is IndexedColorTransform iconv)
						{
							var palspan = iconv.Palette;
							fixed (uint* ppal = palspan)
								HRESULT.Check(pal.Get()->InitializeCustom(ppal, (uint)palspan.Length));
						}
						else if (ctx.Source is WicPixelSource wicsrc)
						{
							wicsrc.CopyPalette(pal);
						}

						HRESULT.Check(wicFrame->SetPalette(pal));
					}

					using var src = new ComPtr<IWICBitmapSource>(ctx.Source.AsIWICBitmapSource());
					HRESULT.Check(wicFrame->WriteSource(src, area.IsEmpty ? null : &wicRect));
				}
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

	internal sealed unsafe class WicAnimatedGifEncoder : IDisposable
	{
		public sealed class AnimationBufferFrame : IDisposable
		{
			public readonly FrameBufferSource Source;
			public PixelArea Area;
			public FrameDisposalMethod Disposal;
			public int Delay;
			public bool Trans;

			public AnimationBufferFrame(int width, int height, PixelFormat format) =>
				Source = new FrameBufferSource(width, height, format);

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
			else
			{
				var anicnt = context.ImageContainer as IAnimationContainer ?? throw new InvalidOperationException("Source must be an animation container.");
				if (context.ImageContainer.FrameCount > 1)
				{
					var pvae = new PROPVARIANT { vt = (ushort)(VARENUM.VT_UI1 | VARENUM.VT_VECTOR) };
					pvae.Anonymous.blob.cbSize = 11;
					pvae.Anonymous.blob.pBlobData = (byte*)Unsafe.AsPointer(ref MemoryMarshal.GetReference(WicGifContainer.Netscape2_0));
					HRESULT.Check(encmeta.Get()->SetMetadataByName(Wic.Metadata.Gif.AppExtension, &pvae));

					byte* pvdd = stackalloc byte[4] { 3, 1, 0, 0 };
					BinaryPrimitives.WriteUInt16LittleEndian(new Span<byte>(pvdd + 2, 2), (ushort)anicnt.LoopCount);

					var pvad = new PROPVARIANT { vt = (ushort)(VARENUM.VT_UI1 | VARENUM.VT_VECTOR) };
					pvad.Anonymous.blob.cbSize = 4;
					pvad.Anonymous.blob.pBlobData = pvdd;
					HRESULT.Check(encmeta.Get()->SetMetadataByName(Wic.Metadata.Gif.AppExtensionData, &pvad));
				}
			}
		}

		public void WriteFrames()
		{
			uint bgColor = (uint)((IAnimationContainer)context.ImageContainer).BackgroundColor.ToArgb();
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
			var aniFrame = (IAnimationFrame)context.ImageFrame;

			frame.Disposal = aniFrame.Disposal == FrameDisposalMethod.RestoreBackground ? FrameDisposalMethod.RestoreBackground : FrameDisposalMethod.Preserve;
			frame.Delay = aniFrame.Duration.Numerator;
			frame.Trans = aniFrame.HasAlpha;
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
			}

			using var frm = new WicImageEncoderFrame(context, encoder, src.Area);
			using var frmmeta = default(ComPtr<IWICMetadataQueryWriter>);
			HRESULT.Check(frm.WicEncoderFrame->GetMetadataQueryWriter(frmmeta.GetAddressOf()));

			frmmeta.SetValue(Wic.Metadata.Gif.FrameDisposal, (byte)src.Disposal);
			frmmeta.SetValue(Wic.Metadata.Gif.FrameDelay, (ushort)src.Delay);

			if (src.Area.X != 0)
				frmmeta.SetValue(Wic.Metadata.Gif.FrameLeft, (ushort)src.Area.X);
			if (src.Area.Y != 0)
				frmmeta.SetValue(Wic.Metadata.Gif.FrameTop, (ushort)src.Area.Y);

			frmmeta.SetValue(Wic.Metadata.Gif.TransparencyFlag, true);
			frmmeta.SetValue(Wic.Metadata.Gif.TransparentColorIndex, (byte)(IndexedSource.Palette.Length - 1));

			frm.WriteSource(context, src.Area);
		}
	}
}