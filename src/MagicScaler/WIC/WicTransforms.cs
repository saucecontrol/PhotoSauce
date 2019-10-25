using System;
using System.Linq;
using System.Buffers;
using System.Diagnostics;
using System.Collections.Generic;
using System.Runtime.InteropServices;

using PhotoSauce.Interop.Wic;

namespace PhotoSauce.MagicScaler
{
	internal class WicFrameReader
	{
		public double DpiX { get; } = 96d;
		public double DpiY { get; } = 96d;
		public Orientation ExifOrientation { get; set; } = Orientation.Normal;
		public IDictionary<string, PropVariant> Metadata { get; } = new Dictionary<string, PropVariant>();

		public bool SupportsNativeScale { get; }
		public bool SupportsNativeTransform { get; }
		public bool SupportsPlanarProcessing { get; }

		public WICJpegYCrCbSubsamplingOption ChromaSubsampling { get; }

		public IWICBitmapFrameDecode? Frame { get; }
		public IWICBitmapSource? Source { get; }

		public WicFrameReader(IImageContainer container, ProcessImageSettings settings, WicPipelineContext wicContext)
		{
			if (!(container is WicDecoder wicDecoder))
				return;

			if (wicDecoder.IsRawContainer && settings.FrameIndex == 0 && wicDecoder.Decoder.TryGetPreview(out var preview))
				Source = wicContext.AddRef(preview);

			if (Source is null)
				Source = Frame = wicContext.AddRef(((WicFrame)wicDecoder.GetFrame(settings.FrameIndex)).Frame);

			Source.GetResolution(out double dpix, out double dpiy);
			DpiX = dpix;
			DpiY = dpiy;

			if (PixelFormat.FromGuid(Source.GetPixelFormat()).NumericRepresentation == PixelNumericRepresentation.Indexed)
			{
				var pal = wicContext.AddRef(Wic.Factory.CreatePalette());
				Source.CopyPalette(pal);

				var newFormat = Consts.GUID_WICPixelFormat24bppBGR;
				if (pal.HasAlpha())
					newFormat = Consts.GUID_WICPixelFormat32bppBGRA;
				else if (pal.IsGrayscale() || pal.IsBlackWhite())
					newFormat = Consts.GUID_WICPixelFormat8bppGray;

				var conv = wicContext.AddRef(Wic.Factory.CreateFormatConverter());
				conv.Initialize(Source, newFormat, WICBitmapDitherType.WICBitmapDitherTypeNone, null, 0.0, WICBitmapPaletteType.WICBitmapPaletteTypeCustom);
				Source = conv;
			}

			if (Source is IWICBitmapSourceTransform trans)
			{
				uint pw = 1, ph = 1;
				Source.GetSize(out uint ow, out uint oh);
				trans.GetClosestSize(ref pw, ref ph);
				SupportsNativeScale = pw < ow || ph < oh;
				SupportsNativeTransform = trans.DoesSupportTransform(WICBitmapTransformOptions.WICBitmapTransformRotate270);
			}

			if (Source is IWICPlanarBitmapSourceTransform ptrans && settings.Interpolation.WeightingFunction.Support >= 0.5d)
			{
				Source.GetSize(out uint ow, out uint oh);
				var desc = new WICBitmapPlaneDescription[WicTransforms.PlanarPixelFormats.Length];
				SupportsPlanarProcessing = ptrans.DoesSupportTransform(ref ow, ref oh, WICBitmapTransformOptions.WICBitmapTransformRotate0, WICPlanarOptions.WICPlanarOptionsDefault, WicTransforms.PlanarPixelFormats, desc, (uint)desc.Length);
				ChromaSubsampling =
					desc[1].Width < desc[0].Width && desc[1].Height < desc[0].Height ? WICJpegYCrCbSubsamplingOption.WICJpegYCrCbSubsampling420 :
					desc[1].Width < desc[0].Width ? WICJpegYCrCbSubsamplingOption.WICJpegYCrCbSubsampling422 :
					desc[1].Height < desc[0].Height ? WICJpegYCrCbSubsamplingOption.WICJpegYCrCbSubsampling440 :
					WICJpegYCrCbSubsamplingOption.WICJpegYCrCbSubsampling444;
			}
		}
	}

	internal static class WicTransforms
	{
		private const string orientationWindowsPolicy = "System.Photo.Orientation";
		private const string orientationExifPath = "/ifd/{ushort=274}";
		private const string orientationJpegPath = "/app1" + orientationExifPath;

		internal static readonly Guid[] PlanarPixelFormats = new[] { Consts.GUID_WICPixelFormat8bppY, Consts.GUID_WICPixelFormat16bppCbCr };

		public static void AddMetadataReader(PipelineContext ctx, bool basicOnly = false)
		{
			if (ctx.ImageContainer is null || ctx.DecoderFrame?.Frame is null)
				return;

			if (ctx.DecoderFrame.Frame.TryGetMetadataQueryReader(out var metareader))
			{
				ctx.WicContext.AddRef(metareader);

				// Exif orientation
				string orientationPathIn = MagicImageProcessor.EnableXmpOrientation ? orientationWindowsPolicy : ctx.ImageContainer.ContainerFormat == FileFormat.Jpeg ? orientationJpegPath : orientationExifPath;
				string orientationPathOut = ctx.Settings.SaveFormat == FileFormat.Jpeg ? orientationJpegPath : orientationExifPath;

				var pvorient = default(PropVariant);
				if (ctx.Settings.OrientationMode != OrientationMode.Ignore && metareader.TryGetMetadataByName(orientationPathIn, out pvorient))
				{
					if (ctx.Settings.OrientationMode == OrientationMode.Normalize && pvorient.UnmanagedType == VarEnum.VT_UI2)
						ctx.DecoderFrame.ExifOrientation = (Orientation)Math.Min(Math.Max((ushort)Orientation.Normal, (ushort)pvorient.Value!), (ushort)Orientation.Rotate270);
				}

				if (basicOnly)
					return;

				// other requested properties
				var propdic = ctx.DecoderFrame.Metadata;
				foreach (string prop in ctx.Settings.MetadataNames ?? Enumerable.Empty<string>())
				{
					if (metareader.TryGetMetadataByName(prop, out var pvar) && !(pvar.Value is null))
						propdic[prop] = pvar;
				}

				if (ctx.Settings.OrientationMode == OrientationMode.Preserve && !(pvorient is null))
					propdic[orientationPathOut] = pvorient;
			}

			if (basicOnly)
				return;

			// ICC profiles
			// http://ninedegreesbelow.com/photography/embedded-color-space-information.html
			uint ccc = ctx.DecoderFrame.Frame.GetColorContextCount();
			var fmt = ctx.Source.Format;
			var profiles = new IWICColorContext[ccc];
			var profile = default(IWICColorContext);

			if (ccc > 0)
			{
				for (int i = 0; i < ccc; i++)
					profiles[i] = ctx.WicContext.AddRef(Wic.Factory.CreateColorContext());

				ctx.DecoderFrame.Frame.GetColorContexts(ccc, profiles);
			}

			foreach (var cc in profiles)
			{
				var cct = cc.GetType();
				if (cct == WICColorContextType.WICColorContextProfile)
				{
					int ccs = (int)cc.GetProfileBytes(0, null);

					// don't try to read giant profiles. 4MiB is more than enough
					if ((uint)ccs > (1024 * 1024 * 4))
						continue;

					using var ccb = MemoryPool<byte>.Shared.Rent(ccs);
					var cca = ccb.GetOwnedArraySegment(ccs);

					cc.GetProfileBytes((uint)cca.Count, cca.Array);
					var cpi = ColorProfile.Cache.GetOrAdd(cca);

					// match only color profiles that match our intended use. if we have a standard sRGB profile, don't save it; we don't need to convert
					if (cpi.IsValid && cpi.IsCompatibleWith(fmt) && !cpi.IsSrgb)
					{
						profile = cc;
						if (cpi.ProfileType == ColorProfileType.Matrix || cpi.ProfileType == ColorProfileType.Curve)
							ctx.SourceColorProfile = cpi;
						break;
					}
				}
				else if (cct == WICColorContextType.WICColorContextExifColorSpace && cc.GetExifColorSpace() == ExifColorSpace.AdobeRGB)
				{
					profile = cc;
					break;
				}
			}

			var defaultColorContext = fmt.ColorRepresentation == PixelColorRepresentation.Grey ? Wic.GreyContext.Value : Wic.SrgbContext.Value;
			ctx.WicContext.SourceColorContext = profile ?? (fmt.ColorRepresentation == PixelColorRepresentation.Cmyk ? Wic.CmykContext.Value : null);
			ctx.WicContext.DestColorContext = ctx.Settings.ColorProfileMode <= ColorProfileMode.NormalizeAndEmbed || ctx.WicContext.SourceColorContext is null ? defaultColorContext : ctx.WicContext.SourceColorContext;

			var defaultColorProfile = fmt.ColorRepresentation == PixelColorRepresentation.Grey ? ColorProfile.sGrey : ColorProfile.sRGB;
			ctx.SourceColorProfile ??= defaultColorProfile;
			ctx.DestColorProfile = ctx.Settings.ColorProfileMode <= ColorProfileMode.NormalizeAndEmbed ? defaultColorProfile : ctx.SourceColorProfile;
		}

		public static void AddConditionalCache(PipelineContext ctx)
		{
			if (!ctx.DecoderFrame.ExifOrientation.RequiresCache())
				return;

			var crop = ctx.Settings.Crop;
			var bmp = ctx.WicContext.AddRef(Wic.Factory.CreateBitmapFromSourceRect(ctx.Source.WicSource, (uint)crop.X, (uint)crop.Y, (uint)crop.Width, (uint)crop.Height));

			ctx.Source = bmp.AsPixelSource(nameof(IWICBitmap));
			ctx.Settings.Crop = ctx.Source.Area.ToGdiRect();
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
				Debug.Assert(ctx.WicContext.SourceColorContext != null && ctx.WicContext.DestColorContext != null);

				//TODO WIC doesn't support proper CMYKA conversion with color profile
				if (curFormat.AlphaRepresentation == PixelAlphaRepresentation.None)
				{
					// WIC doesn't support 16bpc CMYK conversion with color profile
					if (curFormat.BitsPerPixel == 64)
						ctx.Source = ctx.AddDispose(new FormatConversionTransformInternal(ctx.Source, null, null, Consts.GUID_WICPixelFormat32bppCMYK));

					var trans = ctx.WicContext.AddRef(Wic.Factory.CreateColorTransformer());
					if (trans.TryInitialize(ctx.Source.WicSource, ctx.WicContext.SourceColorContext, ctx.WicContext.DestColorContext, Consts.GUID_WICPixelFormat24bppBGR))
					{
						ctx.Source = trans.AsPixelSource(nameof(IWICColorTransform));
						curFormat = ctx.Source.Format;
					}
				}

				ctx.WicContext.SourceColorContext = null;
			}

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
			if (ctx.DecoderFrame.ExifOrientation == Orientation.Normal)
				return;

			var rotator = ctx.WicContext.AddRef(Wic.Factory.CreateBitmapFlipRotator());
			rotator.Initialize(ctx.Source.WicSource, ctx.DecoderFrame.ExifOrientation.ToWicTransformOptions());

			ctx.Source = rotator.AsPixelSource(nameof(IWICBitmapFlipRotator));
			AddConditionalCache(ctx);
			ctx.DecoderFrame.ExifOrientation = Orientation.Normal;
		}

		public static void AddCropper(PipelineContext ctx)
		{
			var crop = PixelArea.FromGdiRect(ctx.Settings.Crop);
			if (crop == ctx.Source.Area)
				return;

			var cropper = ctx.WicContext.AddRef(Wic.Factory.CreateBitmapClipper());
			cropper.Initialize(ctx.Source.WicSource, crop.ToWicRect());

			ctx.Source = cropper.AsPixelSource(nameof(IWICBitmapClipper));
		}

		public static void AddScaler(PipelineContext ctx, bool hybrid = false)
		{
			bool swap = ctx.DecoderFrame.ExifOrientation.SwapsDimensions();
			var srect = ctx.Settings.InnerRect;

			int width = swap ? srect.Height : srect.Width, height = swap? srect.Width : srect.Height;
			int ratio = (int)ctx.Settings.HybridScaleRatio;

			if ((ctx.Source.Width == width && ctx.Source.Height == height) || (hybrid && ratio == 1))
				return;

			if (hybrid)
			{
				width = MathUtil.DivCeiling(ctx.Source.Width, ratio);
				height = MathUtil.DivCeiling(ctx.Source.Height, ratio);
				ctx.Settings.HybridMode = HybridScaleMode.Off;
			}

			var mode = hybrid ? WICBitmapInterpolationMode.WICBitmapInterpolationModeFant :
			           ctx.Settings.Interpolation.WeightingFunction.Support < 0.1 ? WICBitmapInterpolationMode.WICBitmapInterpolationModeNearestNeighbor :
			           ctx.Settings.Interpolation.WeightingFunction.Support < 1.0 ? ctx.Settings.ScaleRatio > 1.0 ? WICBitmapInterpolationMode.WICBitmapInterpolationModeFant : WICBitmapInterpolationMode.WICBitmapInterpolationModeNearestNeighbor :
			           ctx.Settings.Interpolation.WeightingFunction.Support > 1.0 ? ctx.Settings.ScaleRatio > 1.0 ? WICBitmapInterpolationMode.WICBitmapInterpolationModeHighQualityCubic :WICBitmapInterpolationMode.WICBitmapInterpolationModeCubic :
			           ctx.Settings.ScaleRatio > 1.0 ? WICBitmapInterpolationMode.WICBitmapInterpolationModeFant :
			           WICBitmapInterpolationMode.WICBitmapInterpolationModeLinear;

			if (ctx.Source.WicSource is IWICBitmapSourceTransform)
				ctx.Source = ctx.Source.WicSource.AsPixelSource(nameof(IWICBitmapFrameDecode));

			var scaler = ctx.WicContext.AddRef(Wic.Factory.CreateBitmapScaler());
			scaler.Initialize(ctx.Source.WicSource, (uint)width, (uint)height, mode);

			ctx.Source = scaler.AsPixelSource(nameof(IWICBitmapScaler));
			ctx.Settings.Crop = ctx.Source.Area.ReOrient(ctx.DecoderFrame.ExifOrientation, ctx.Source.Width, ctx.Source.Height).ToGdiRect();
		}

		public static void AddNativeScaler(PipelineContext ctx)
		{
			int ratio = (int)ctx.Settings.HybridScaleRatio;
			if (ratio == 1 || !ctx.DecoderFrame.SupportsNativeScale || !(ctx.Source.WicSource is IWICBitmapSourceTransform trans))
				return;

			uint ow = (uint)ctx.Source.Width, oh = (uint)ctx.Source.Height;
			uint cw = (uint)MathUtil.DivCeiling((int)ow, ratio), ch = (uint)MathUtil.DivCeiling((int)oh, ratio);
			trans.GetClosestSize(ref cw, ref ch);

			if (cw == ow && ch == oh)
				return;

			var orient = ctx.DecoderFrame.ExifOrientation;
			var scaler = ctx.WicContext.AddRef(Wic.Factory.CreateBitmapScaler());
			scaler.Initialize(ctx.Source.WicSource, cw, ch, WICBitmapInterpolationMode.WICBitmapInterpolationModeFant);

			ctx.Source = scaler.AsPixelSource(nameof(IWICBitmapSourceTransform));
			ctx.Settings.Crop = PixelArea.FromGdiRect(ctx.Settings.Crop).DeOrient(orient, (int)ow, (int)oh).ProportionalScale((int)ow, (int)oh, (int)cw, (int)ch).ReOrient(orient, (int)cw, (int)ch).ToGdiRect();
		}

		public static void AddPlanarCache(PipelineContext ctx)
		{
			if (!(ctx.Source.WicSource is IWICPlanarBitmapSourceTransform trans))
				throw new NotSupportedException("Transform chain doesn't support planar mode.  Only JPEG Decoder, Rotator, Scaler, and PixelFormatConverter are allowed");

			int ratio = ((int)ctx.Settings.HybridScaleRatio).Clamp(1, 8);
			uint ow = (uint)ctx.Source.Width, oh = (uint)ctx.Source.Height;
			uint cw = (uint)MathUtil.DivCeiling((int)ow, ratio), ch = (uint)MathUtil.DivCeiling((int)oh, ratio);

			var desc = new WICBitmapPlaneDescription[PlanarPixelFormats.Length];
			if (!trans.DoesSupportTransform(ref cw, ref ch, WICBitmapTransformOptions.WICBitmapTransformRotate0, WICPlanarOptions.WICPlanarOptionsDefault, PlanarPixelFormats, desc, (uint)desc.Length))
				throw new NotSupportedException("Requested planar transform not supported");

			var crop = PixelArea.FromGdiRect(ctx.Settings.Crop).DeOrient(ctx.DecoderFrame.ExifOrientation, (int)ow, (int)oh).ProportionalScale((int)ow, (int)oh, (int)cw, (int)ch);
			ctx.WicContext.PlanarCache = ctx.AddDispose(new WicPlanarCache(trans, desc, WICBitmapTransformOptions.WICBitmapTransformRotate0, cw, ch, crop));

			ctx.Source = ctx.WicContext.PlanarCache.GetPlane(WicPlane.Y);
			ctx.Settings.Crop = ctx.Source.Area.ReOrient(ctx.DecoderFrame.ExifOrientation, ctx.Source.Width, ctx.Source.Height).ToGdiRect();
		}

		public static void AddPlanarConverter(PipelineContext ctx)
		{
			Debug.Assert(!(ctx.PlanarSourceY is null || ctx.PlanarSourceCbCr is null));

			var planes = new[] { ctx.PlanarSourceY.WicSource, ctx.PlanarSourceCbCr.WicSource };
			var conv = (IWICPlanarFormatConverter)ctx.WicContext.AddRef(Wic.Factory.CreateFormatConverter());
			conv.Initialize(planes, (uint)planes.Length, Consts.GUID_WICPixelFormat24bppBGR, WICBitmapDitherType.WICBitmapDitherTypeNone, null, 0.0, WICBitmapPaletteType.WICBitmapPaletteTypeCustom);

			ctx.Source = conv.AsPixelSource(nameof(IWICPlanarFormatConverter));
			ctx.PlanarSourceY = ctx.PlanarSourceCbCr = null;
		}
	}
}