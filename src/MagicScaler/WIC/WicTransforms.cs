using System;
using System.Buffers;
using System.Diagnostics;
using System.Runtime.InteropServices;

using PhotoSauce.Interop.Wic;

namespace PhotoSauce.MagicScaler
{
	internal class WicImageFrame : IImageFrame
	{
		private readonly WicImageContainer container;
		private readonly ComHandleCollection comHandles = new ComHandleCollection(4);

		private PixelSource? source;
		private IPixelSource? iSource;
		private WicColorProfile? colorProfile;

		public double DpiX { get; }
		public double DpiY { get; }
		public Orientation ExifOrientation { get; } = Orientation.Normal;
		public ReadOnlySpan<byte> IccProfile => ColorProfileSource.ParsedProfile.ProfileBytes;

		public bool SupportsNativeScale { get; }
		public bool SupportsNativeTransform { get; }
		public bool SupportsPlanarProcessing { get; }

		public WICJpegYCrCbSubsamplingOption ChromaSubsampling { get; }

		public IWICBitmapFrameDecode WicFrame { get; }
		public IWICBitmapSource WicSource { get; }
		public IWICMetadataQueryReader? WicMetadataReader { get; }

		public PixelSource Source => source ??= WicSource.AsPixelSource(nameof(IWICBitmapFrameDecode), false);

		public IPixelSource PixelSource => iSource ??= Source.AsIPixelSource();

		public WicColorProfile ColorProfileSource => colorProfile ??= getColorProfile();

		public WicImageFrame(WicImageContainer decoder, uint index)
		{
			if (index >= (uint)decoder.FrameCount) throw new IndexOutOfRangeException("Frame index does not exist");

			WicFrame = comHandles.AddRef(decoder.WicDecoder.GetFrame(index));
			WicSource = WicFrame;
			WicFrame.GetSize(out uint frameWidth, out uint frameHeight);
			container = decoder;

			if (decoder.IsRawContainer && index == 0 && decoder.WicDecoder.TryGetPreview(out var preview))
			{
				using var pvwSource = new ComHandle<IWICBitmapSource>(preview);
				preview.GetSize(out uint pw, out uint ph);

				if (pw == frameWidth && ph == frameHeight)
					WicSource = comHandles.AddOwnRef(preview);
			}

			WicFrame.GetResolution(out double dpix, out double dpiy);
			DpiX = dpix;
			DpiY = dpiy;

			if (PixelFormat.FromGuid(WicSource.GetPixelFormat()).NumericRepresentation == PixelNumericRepresentation.Indexed)
			{
				var pal = comHandles.AddRef(Wic.Factory.CreatePalette());
				WicSource.CopyPalette(pal);

				var newFormat = Consts.GUID_WICPixelFormat24bppBGR;
				if (pal.HasAlpha())
					newFormat = Consts.GUID_WICPixelFormat32bppBGRA;
				else if (pal.IsGrayscale() || pal.IsBlackWhite())
					newFormat = Consts.GUID_WICPixelFormat8bppGray;

				var conv = comHandles.AddRef(Wic.Factory.CreateFormatConverter());
				conv.Initialize(WicSource, newFormat, WICBitmapDitherType.WICBitmapDitherTypeNone, null, 0.0, WICBitmapPaletteType.WICBitmapPaletteTypeCustom);
				WicSource = conv;
			}

			if (WicSource is IWICBitmapSourceTransform trans)
			{
				uint pw = 1, ph = 1;
				trans.GetClosestSize(ref pw, ref ph);

				SupportsNativeScale = pw < frameWidth || ph < frameHeight;
				SupportsNativeTransform = trans.DoesSupportTransform(WICBitmapTransformOptions.WICBitmapTransformRotate270);
			}

			if (WicSource is IWICPlanarBitmapSourceTransform ptrans)
			{
				var desc = ArrayPool<WICBitmapPlaneDescription>.Shared.Rent(WicTransforms.PlanarPixelFormats.Length);

				SupportsPlanarProcessing = ptrans.DoesSupportTransform(ref frameWidth, ref frameHeight, WICBitmapTransformOptions.WICBitmapTransformRotate0, WICPlanarOptions.WICPlanarOptionsDefault, WicTransforms.PlanarPixelFormats, desc, (uint)WicTransforms.PlanarPixelFormats.Length);
				ChromaSubsampling =
					desc[1].Width < desc[0].Width && desc[1].Height < desc[0].Height ? WICJpegYCrCbSubsamplingOption.WICJpegYCrCbSubsampling420 :
					desc[1].Width < desc[0].Width ? WICJpegYCrCbSubsamplingOption.WICJpegYCrCbSubsampling422 :
					desc[1].Height < desc[0].Height ? WICJpegYCrCbSubsamplingOption.WICJpegYCrCbSubsampling440 :
					WICJpegYCrCbSubsamplingOption.WICJpegYCrCbSubsampling444;

				ArrayPool<WICBitmapPlaneDescription>.Shared.Return(desc);
			}

			if (WicFrame.TryGetMetadataQueryReader(out var metareader))
			{
				WicMetadataReader = comHandles.AddRef(metareader);

				string orientationPath =
					MagicImageProcessor.EnableXmpOrientation ? Wic.Metadata.OrientationWindowsPolicy :
					decoder.ContainerFormat == FileFormat.Jpeg ? Wic.Metadata.OrientationJpegPath :
					Wic.Metadata.OrientationExifPath;

				if (metareader.TryGetMetadataByName(orientationPath, out var pvorient) && pvorient.UnmanagedType == VarEnum.VT_UI2)
					ExifOrientation = (Orientation)Math.Min(Math.Max((ushort)Orientation.Normal, (ushort)pvorient.Value!), (ushort)Orientation.Rotate270);
			}
		}

		public void Dispose() => comHandles.Dispose();

		private WicColorProfile getColorProfile()
		{
			var fmt = PixelFormat.FromGuid(WicSource.GetPixelFormat());
			uint ccc = WicFrame.GetColorContextCount();
			if (ccc == 0)
				return WicColorProfile.GetDefaultFor(fmt);

			var profiles = ArrayPool<IWICColorContext>.Shared.Rent((int)ccc);

			for (int i = 0; i < (int)ccc; i++)
				profiles[i] = comHandles.AddRef(Wic.Factory.CreateColorContext());

			WicFrame.GetColorContexts(ccc, profiles);
			var match = matchProfile(profiles.AsSpan(0, (int)ccc), fmt);

			ArrayPool<IWICColorContext>.Shared.Return(profiles);

			return match ?? WicColorProfile.GetDefaultFor(fmt);
		}

		private WicColorProfile? matchProfile(ReadOnlySpan<IWICColorContext> profiles, PixelFormat fmt)
		{
			foreach (var cc in profiles)
			{
				var cct = cc.GetType();
				if (cct == WICColorContextType.WICColorContextProfile)
				{
					uint cb = cc.GetProfileBytes(0, null);

					// Don't try to read giant profiles. 4MiB should be enough, and more might indicate corrupt metadata.
					if (cb > 1024 * 1024 * 4)
						continue;

					var buff = ArrayPool<byte>.Shared.Rent((int)cb);

					cc.GetProfileBytes(cb, buff);
					var cpi = ColorProfile.Cache.GetOrAdd(new ReadOnlySpan<byte>(buff, 0, (int)cb));

					ArrayPool<byte>.Shared.Return(buff);

					// Use the profile only if it matches the frame's pixel format.  Ignore embedded sRGB-compatible profiles -- they will be upgraded to the internal sRGB/sGrey definintion.
					if (cpi.IsValid && cpi.IsCompatibleWith(fmt) && !cpi.IsSrgb)
						return new WicColorProfile(cc, cpi);
				}
				else if (cct == WICColorContextType.WICColorContextExifColorSpace && WicMetadataReader != null)
				{
					// Although WIC defines the non-standard AdobeRGB ExifColorSpace value, most software (including Adobe's) only supports the Uncalibrated/InteropIndex=R03 convention.
					// http://ninedegreesbelow.com/photography/embedded-color-space-information.html
					var ecs = cc.GetExifColorSpace();
					if (
						ecs == ExifColorSpace.AdobeRGB || (
						ecs == ExifColorSpace.Uncalibrated
						&& WicMetadataReader.TryGetMetadataByName(container.ContainerFormat == FileFormat.Jpeg ? Wic.Metadata.InteropIndexJpegPath : Wic.Metadata.InteropIndexExifPath, out var interopIdx)
						&& interopIdx.UnmanagedType == VarEnum.VT_LPSTR
						&& (string)interopIdx.Value! == "R03")
					) return WicColorProfile.AdobeRgb.Value;
				}
			}

			return null;
		}
	}

	internal static class WicTransforms
	{
		public static readonly Guid[] PlanarPixelFormats = new[] { Consts.GUID_WICPixelFormat8bppY, Consts.GUID_WICPixelFormat8bppCb, Consts.GUID_WICPixelFormat8bppCr };

		public static void AddColorProfileReader(PipelineContext ctx)
		{
			if (!(ctx.ImageFrame is WicImageFrame wicFrame))
				return;

			ctx.WicContext.SourceColorContext = wicFrame.ColorProfileSource.WicColorContext;
			ctx.WicContext.DestColorContext = ctx.Settings.ColorProfileMode <= ColorProfileMode.NormalizeAndEmbed ? WicColorProfile.GetDefaultFor(ctx.Source.Format).WicColorContext : ctx.WicContext.SourceColorContext;
		}

		public static void AddColorspaceConverter(PipelineContext ctx)
		{
			if (ctx.WicContext.SourceColorContext is null || ctx.WicContext.DestColorContext is null || ctx.WicContext.SourceColorContext == ctx.WicContext.DestColorContext)
				return;

			var trans = ctx.WicContext.AddRef(Wic.Factory.CreateColorTransformer());
			if (trans.TryInitialize(ctx.Source.WicSource, ctx.WicContext.SourceColorContext, ctx.WicContext.DestColorContext, ctx.Source.Format.FormatGuid))
				ctx.Source = trans.AsPixelSource(nameof(IWICColorTransform));
		}

		public static void AddPixelFormatConverter(PipelineContext ctx, bool allowPbgra = true)
		{
			var curFormat = ctx.Source.Format;
			if (curFormat.ColorRepresentation == PixelColorRepresentation.Cmyk)
			{
				var sRgbContext = WicColorProfile.Srgb.Value;
				Debug.Assert(ctx.WicContext.SourceColorContext != null);

				// TODO WIC doesn't support proper CMYKA conversion with color profile
				if (curFormat.AlphaRepresentation == PixelAlphaRepresentation.None)
				{
					// WIC doesn't support 16bpc CMYK conversion with color profile
					if (curFormat.BitsPerPixel == 64)
						ctx.Source = ctx.AddDispose(new ConversionTransform(ctx.Source, null, null, Consts.GUID_WICPixelFormat32bppCMYK));

					var trans = ctx.WicContext.AddRef(Wic.Factory.CreateColorTransformer());
					if (trans.TryInitialize(ctx.Source.WicSource, ctx.WicContext.SourceColorContext, sRgbContext.WicColorContext, Consts.GUID_WICPixelFormat24bppBGR))
					{
						ctx.Source = trans.AsPixelSource(nameof(IWICColorTransform));
						curFormat = ctx.Source.Format;
					}
				}

				ctx.WicContext.DestColorContext = ctx.WicContext.SourceColorContext = sRgbContext.WicColorContext;
				ctx.DestColorProfile = ctx.SourceColorProfile = sRgbContext.ParsedProfile;
			}

			if (curFormat.FormatGuid == Consts.GUID_WICPixelFormat8bppY || curFormat.FormatGuid == Consts.GUID_WICPixelFormat8bppCb || curFormat.FormatGuid == Consts.GUID_WICPixelFormat8bppCr)
				return;

			var newFormat = PixelFormat.FromGuid(Consts.GUID_WICPixelFormat24bppBGR);
			if (curFormat.AlphaRepresentation == PixelAlphaRepresentation.Associated && allowPbgra && ctx.Settings.BlendingMode != GammaMode.Linear && ctx.Settings.MatteColor.IsEmpty)
				newFormat = PixelFormat.FromGuid(Consts.GUID_WICPixelFormat32bppPBGRA);
			else if (curFormat.AlphaRepresentation != PixelAlphaRepresentation.None)
				newFormat = PixelFormat.FromGuid(Consts.GUID_WICPixelFormat32bppBGRA);
			else if (curFormat.ColorRepresentation == PixelColorRepresentation.Grey)
				newFormat = PixelFormat.FromGuid(Consts.GUID_WICPixelFormat8bppGray);

			if (curFormat.FormatGuid == newFormat.FormatGuid)
				return;

			var conv = ctx.WicContext.AddRef(Wic.Factory.CreateFormatConverter());
			if (!conv.CanConvert(curFormat.FormatGuid, newFormat.FormatGuid))
				throw new NotSupportedException("Can't convert to destination pixel format");

			conv.Initialize(ctx.Source.WicSource, newFormat.FormatGuid, WICBitmapDitherType.WICBitmapDitherTypeNone, null, 0.0, WICBitmapPaletteType.WICBitmapPaletteTypeCustom);
			ctx.Source = conv.AsPixelSource($"{nameof(IWICFormatConverter)}: {curFormat.Name}->{newFormat.Name}");
		}

		public static void AddIndexedColorConverter(PipelineContext ctx)
		{
			var curFormat = ctx.Source.Format;
			var newFormat = PixelFormat.FromGuid(Consts.GUID_WICPixelFormat8bppIndexed);

			if (!ctx.Settings.IndexedColor || curFormat.NumericRepresentation == PixelNumericRepresentation.Indexed || curFormat.ColorRepresentation == PixelColorRepresentation.Grey)
				return;

			var conv = ctx.WicContext.AddRef(Wic.Factory.CreateFormatConverter());
			if (!conv.CanConvert(curFormat.FormatGuid, newFormat.FormatGuid))
				throw new NotSupportedException("Can't convert to destination pixel format");

			var bmp = ctx.WicContext.AddRef(Wic.Factory.CreateBitmapFromSource(ctx.Source.WicSource, WICBitmapCreateCacheOption.WICBitmapCacheOnDemand));
			ctx.Source = bmp.AsPixelSource(nameof(IWICBitmap));

			var pal = ctx.WicContext.AddRef(Wic.Factory.CreatePalette());
			pal.InitializeFromBitmap(ctx.Source.WicSource, 256u, curFormat.AlphaRepresentation != PixelAlphaRepresentation.None);
			ctx.WicContext.DestPalette = pal;

			conv.Initialize(ctx.Source.WicSource, newFormat.FormatGuid, WICBitmapDitherType.WICBitmapDitherTypeErrorDiffusion, pal, 10.0, WICBitmapPaletteType.WICBitmapPaletteTypeCustom);
			ctx.Source = conv.AsPixelSource($"{nameof(IWICFormatConverter)}: {curFormat.Name}->{newFormat.Name}", false);
		}

		public static void AddExifFlipRotator(PipelineContext ctx)
		{
			if (ctx.Orientation == Orientation.Normal)
				return;

			var rotator = ctx.WicContext.AddRef(Wic.Factory.CreateBitmapFlipRotator());
			rotator.Initialize(ctx.Source.WicSource, ctx.Orientation.ToWicTransformOptions());

			ctx.Source = rotator.AsPixelSource(nameof(IWICBitmapFlipRotator));

			if (ctx.Orientation.RequiresCache())
			{
				var crop = ctx.Settings.Crop;
				var bmp = ctx.WicContext.AddRef(Wic.Factory.CreateBitmapFromSourceRect(ctx.Source.WicSource, (uint)crop.X, (uint)crop.Y, (uint)crop.Width, (uint)crop.Height));

				ctx.Source = bmp.AsPixelSource(nameof(IWICBitmap));
				ctx.Settings.Crop = ctx.Source.Area.ToGdiRect();
			}

			ctx.Orientation = Orientation.Normal;
		}

		public static void AddCropper(PipelineContext ctx)
		{
			var crop = PixelArea.FromGdiRect(ctx.Settings.Crop);
			if (crop == ctx.Source.Area)
				return;

			var cropper = ctx.WicContext.AddRef(Wic.Factory.CreateBitmapClipper());
			cropper.Initialize(ctx.Source.WicSource, crop.ToWicRect());

			ctx.Source = cropper.AsPixelSource(nameof(IWICBitmapClipper));
			ctx.Settings.Crop = ctx.Source.Area.ToGdiRect();
		}

		public static void AddScaler(PipelineContext ctx)
		{
			bool swap = ctx.Orientation.SwapsDimensions();
			var tsize = ctx.Settings.InnerSize;

			int width = swap ? tsize.Height : tsize.Width, height = swap ? tsize.Width : tsize.Height;
			if (ctx.Source.Width == width && ctx.Source.Height == height)
				return;

			var mode =
				ctx.Settings.Interpolation.WeightingFunction.Support < 0.1 ? WICBitmapInterpolationMode.WICBitmapInterpolationModeNearestNeighbor :
				ctx.Settings.Interpolation.WeightingFunction.Support < 1.0 ? ctx.Settings.ScaleRatio > 1.0 ? WICBitmapInterpolationMode.WICBitmapInterpolationModeFant : WICBitmapInterpolationMode.WICBitmapInterpolationModeNearestNeighbor :
				ctx.Settings.Interpolation.WeightingFunction.Support > 1.0 ? ctx.Settings.ScaleRatio > 1.0 ? WICBitmapInterpolationMode.WICBitmapInterpolationModeHighQualityCubic :WICBitmapInterpolationMode.WICBitmapInterpolationModeCubic :
				ctx.Settings.ScaleRatio > 1.0 ? WICBitmapInterpolationMode.WICBitmapInterpolationModeFant :
				WICBitmapInterpolationMode.WICBitmapInterpolationModeLinear;

			var scaler = ctx.WicContext.AddRef(Wic.Factory.CreateBitmapScaler());
			scaler.Initialize(ctx.Source.WicSource, (uint)width, (uint)height, mode);

			ctx.Source = scaler.AsPixelSource(nameof(IWICBitmapScaler));
		}

		public static void AddHybridScaler(PipelineContext ctx, int ratio = default)
		{
			ratio = ratio == default ? ctx.Settings.HybridScaleRatio : ratio;
			if (ratio == 1 || ctx.Settings.Interpolation.WeightingFunction.Support < 0.1)
				return;

			uint width = (uint)MathUtil.DivCeiling(ctx.Source.Width, ratio);
			uint height = (uint)MathUtil.DivCeiling(ctx.Source.Height, ratio);

			if (ctx.Source.WicSource is IWICBitmapSourceTransform)
				ctx.Source = ctx.Source.WicSource.AsPixelSource(nameof(IWICBitmapFrameDecode));

			var scaler = ctx.WicContext.AddRef(Wic.Factory.CreateBitmapScaler());
			scaler.Initialize(ctx.Source.WicSource, width, height, WICBitmapInterpolationMode.WICBitmapInterpolationModeFant);

			ctx.Source = scaler.AsPixelSource(nameof(IWICBitmapScaler) + " (hybrid)");
			ctx.Settings.HybridMode = HybridScaleMode.Off;
		}

		public static void AddNativeScaler(PipelineContext ctx)
		{
			int ratio = ctx.Settings.HybridScaleRatio;
			if (ratio == 1 || !(ctx.ImageFrame is WicImageFrame wicFrame) || !wicFrame.SupportsNativeScale || !(ctx.Source.WicSource is IWICBitmapSourceTransform trans))
				return;

			uint ow = (uint)ctx.Source.Width, oh = (uint)ctx.Source.Height;
			uint cw = (uint)MathUtil.DivCeiling((int)ow, ratio), ch = (uint)MathUtil.DivCeiling((int)oh, ratio);
			trans.GetClosestSize(ref cw, ref ch);

			if (cw == ow && ch == oh)
				return;

			var orient = ctx.Orientation;
			var scaler = ctx.WicContext.AddRef(Wic.Factory.CreateBitmapScaler());
			scaler.Initialize(ctx.Source.WicSource, cw, ch, WICBitmapInterpolationMode.WICBitmapInterpolationModeFant);

			ctx.Source = scaler.AsPixelSource(nameof(IWICBitmapSourceTransform));
			ctx.Settings.Crop = PixelArea.FromGdiRect(ctx.Settings.Crop).DeOrient(orient, (int)ow, (int)oh).ProportionalScale((int)ow, (int)oh, (int)cw, (int)ch).ReOrient(orient, (int)cw, (int)ch).ToGdiRect();
			ctx.Settings.HybridMode = HybridScaleMode.Off;
		}

		public static void AddPlanarCache(PipelineContext ctx)
		{
			if (!(ctx.Source.WicSource is IWICPlanarBitmapSourceTransform trans))
				throw new NotSupportedException("Transform chain doesn't support planar mode.  Only JPEG Decoder, Rotator, Scaler, and PixelFormatConverter are allowed");

			int ratio = ctx.Settings.HybridScaleRatio.Clamp(1, 8);
			uint ow = (uint)ctx.Source.Width, oh = (uint)ctx.Source.Height;
			uint cw = (uint)MathUtil.DivCeiling((int)ow, ratio), ch = (uint)MathUtil.DivCeiling((int)oh, ratio);

			var desc = ArrayPool<WICBitmapPlaneDescription>.Shared.Rent(PlanarPixelFormats.Length);
			if (!trans.DoesSupportTransform(ref cw, ref ch, WICBitmapTransformOptions.WICBitmapTransformRotate0, WICPlanarOptions.WICPlanarOptionsDefault, PlanarPixelFormats, desc, (uint)PlanarPixelFormats.Length))
				throw new NotSupportedException("Requested planar transform not supported");

			var crop = PixelArea.FromGdiRect(ctx.Settings.Crop).DeOrient(ctx.Orientation, (int)ow, (int)oh).ProportionalScale((int)ow, (int)oh, (int)cw, (int)ch);
			var cache = ctx.AddDispose(new WicPlanarCache(trans, desc, WICBitmapTransformOptions.WICBitmapTransformRotate0, cw, ch, crop));

			ArrayPool<WICBitmapPlaneDescription>.Shared.Return(desc);

			ctx.PlanarContext = new PipelineContext.PlanarPipelineContext(cache.SourceY, cache.SourceCb, cache.SourceCr);
			ctx.Source = ctx.PlanarContext.SourceY;
			ctx.Settings.Crop = ctx.Source.Area.ReOrient(ctx.Orientation, ctx.Source.Width, ctx.Source.Height).ToGdiRect();
			ctx.Settings.HybridMode = HybridScaleMode.Off;
		}

		public static void AddPlanarConverter(PipelineContext ctx)
		{
			Debug.Assert(ctx.PlanarContext != null);

			var planes = new[] { ctx.PlanarContext.SourceY.WicSource, ctx.PlanarContext.SourceCb.WicSource, ctx.PlanarContext.SourceCr.WicSource };
			var conv = (IWICPlanarFormatConverter)ctx.WicContext.AddRef(Wic.Factory.CreateFormatConverter());
			conv.Initialize(planes, (uint)planes.Length, Consts.GUID_WICPixelFormat24bppBGR, WICBitmapDitherType.WICBitmapDitherTypeNone, null, 0.0, WICBitmapPaletteType.WICBitmapPaletteTypeCustom);

			ctx.Source = conv.AsPixelSource(nameof(IWICPlanarFormatConverter));
			ctx.PlanarContext = null;
		}
	}
}