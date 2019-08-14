using System;
using System.Linq;
using System.Buffers;
using System.Drawing;
using System.Collections.Generic;
using System.Runtime.InteropServices;

using PhotoSauce.MagicScaler.Interop;

namespace PhotoSauce.MagicScaler
{
	internal class WicFrameReader
	{
		public IWICBitmapFrameDecode Frame { get; private set; }
		public double DpiX { get; private set; }
		public double DpiY { get; private set; }
		public bool SupportsNativeScale { get; private set; }
		public bool SupportsNativeTransform { get; private set; }
		public bool SupportsPlanarPipeline { get; set; }
		public Orientation ExifOrientation { get; set; } = Orientation.Normal;
		public IDictionary<string, PropVariant> Metadata { get; set; }

		public WicFrameReader(PipelineContext ctx, bool planar = false)
		{
			ctx.DecoderFrame = this;

			if(ctx.Decoder.Decoder is null)
			{
				DpiX = DpiY = 96d;
				return;
			}

			var source = default(IWICBitmapSource);
			source = Frame = ctx.WicContext.AddRef(ctx.Decoder.Decoder.GetFrame((uint)ctx.Settings.FrameIndex));

			if (ctx.Decoder.WicContainerFormat == Consts.GUID_ContainerFormatRaw && ctx.Settings.FrameIndex == 0 && ctx.Decoder.Decoder.TryGetPreview(out var preview))
				source = ctx.WicContext.AddRef(preview);

			source.GetResolution(out double dpix, out double dpiy);
			DpiX = dpix;
			DpiY = dpiy;

			if (PixelFormat.Cache[source.GetPixelFormat()].NumericRepresentation == PixelNumericRepresentation.Indexed)
			{
				var pal = ctx.WicContext.AddRef(Wic.Factory.CreatePalette());
				source.CopyPalette(pal);

				var newFormat = Consts.GUID_WICPixelFormat24bppBGR;
				if (pal.HasAlpha())
					newFormat = Consts.GUID_WICPixelFormat32bppBGRA;
				else if (pal.IsGrayscale() || pal.IsBlackWhite())
					newFormat = Consts.GUID_WICPixelFormat8bppGray;

				var conv = ctx.WicContext.AddRef(Wic.Factory.CreateFormatConverter());
				conv.Initialize(source, newFormat, WICBitmapDitherType.WICBitmapDitherTypeNone, null, 0.0, WICBitmapPaletteType.WICBitmapPaletteTypeCustom);
				source = conv;
			}

			if (source is IWICBitmapSourceTransform trans)
			{
				uint pw = 1, ph = 1;
				source.GetSize(out uint ow, out uint oh);
				trans.GetClosestSize(ref pw, ref ph);
				SupportsNativeScale = pw < ow || ph < oh;
				SupportsNativeTransform = trans.DoesSupportTransform(WICBitmapTransformOptions.WICBitmapTransformRotate270);
			}

			if (planar && source is IWICPlanarBitmapSourceTransform ptrans && ctx.Settings.Interpolation.WeightingFunction.Support >= 0.5d)
			{
				uint pw = 1, ph = 1;
				var pdesc = new WICBitmapPlaneDescription[2];
				var pfmts = new[] { Consts.GUID_WICPixelFormat8bppY, Consts.GUID_WICPixelFormat16bppCbCr };
				SupportsPlanarPipeline = ptrans.DoesSupportTransform(ref pw, ref ph, WICBitmapTransformOptions.WICBitmapTransformRotate0, WICPlanarOptions.WICPlanarOptionsDefault, pfmts, pdesc, 2);
			}

			bool preserveNative = SupportsPlanarPipeline || SupportsNativeTransform || (SupportsNativeScale && ctx.Settings.HybridScaleRatio > 1d);
			ctx.Source = source.AsPixelSource(nameof(IWICBitmapFrameDecode), !preserveNative);
		}
	}

	internal static class WicTransforms
	{
		private static readonly Guid[] planarPixelFormats = new[] { Consts.GUID_WICPixelFormat8bppY, Consts.GUID_WICPixelFormat16bppCbCr };

		public static void AddMetadataReader(PipelineContext ctx, bool basicOnly = false)
		{
			if (ctx.DecoderFrame.Frame is null)
				return;

			if (ctx.DecoderFrame.Frame.TryGetMetadataQueryReader(out var metareader))
			{
				ctx.WicContext.AddRef(metareader);

				// Exif orientation
				string orientationPath = MagicImageProcessor.EnableXmpOrientation ? "System.Photo.Orientation" : "/app1/ifd/{ushort=274}";
				var pvorient = default(PropVariant);
				if (ctx.Settings.OrientationMode != OrientationMode.Ignore && metareader.TryGetMetadataByName(orientationPath, out pvorient))
				{
					if (ctx.Settings.OrientationMode == OrientationMode.Normalize && pvorient.UnmanagedType == VarEnum.VT_UI2)
						ctx.DecoderFrame.ExifOrientation = (Orientation)Math.Min(Math.Max((ushort)Orientation.Normal, (ushort)pvorient.Value!), (ushort)Orientation.Rotate270);

					var opt = ctx.DecoderFrame.ExifOrientation.ToWicTransformOptions();
					if (ctx.DecoderFrame.SupportsPlanarPipeline && opt != WICBitmapTransformOptions.WICBitmapTransformRotate0 && ctx.DecoderFrame.Frame is IWICPlanarBitmapSourceTransform ptrans)
					{
						uint pw = 1, ph = 1;
						var desc = new WICBitmapPlaneDescription[2];
						ctx.DecoderFrame.SupportsPlanarPipeline = ptrans.DoesSupportTransform(ref pw, ref ph, opt, WICPlanarOptions.WICPlanarOptionsDefault, planarPixelFormats, desc, 2);
					}
				}

				if (basicOnly)
					return;

				// other requested properties
				var propdic = new Dictionary<string, PropVariant>();
				foreach (string prop in ctx.Settings.MetadataNames ?? Enumerable.Empty<string>())
				{
					if (metareader.TryGetMetadataByName(prop, out var pvar) && pvar.Value != null)
						propdic[prop] = pvar;
				}

				if (ctx.Settings.OrientationMode == OrientationMode.Preserve && !(pvorient is null))
					propdic[orientationPath] = pvorient;

				ctx.DecoderFrame.Metadata = propdic;
			}

			if (basicOnly)
				return;

			// ICC profiles
			//http://ninedegreesbelow.com/photography/embedded-color-space-information.html
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
					if (MemoryMarshal.TryGetArray(ccb.Memory.Slice(0, ccs), out ArraySegment<byte> cca))
					{
						cc.GetProfileBytes((uint)cca.Count, cca.Array);
						var cpi = ColorProfile.Cache.GetOrAdd(cca);

						// match only color profiles that match our intended use. if we have a standard sRGB profile, don't save it; we don't need to convert
						if (cpi.IsValid && (
							   (cpi.DataColorSpace == ColorProfile.ProfileColorSpace.Rgb && (fmt.ColorRepresentation == PixelColorRepresentation.Bgr || fmt.ColorRepresentation == PixelColorRepresentation.Rgb) && !cpi.IsSrgb)
							|| (cpi.DataColorSpace == ColorProfile.ProfileColorSpace.Grey && fmt.ColorRepresentation == PixelColorRepresentation.Grey && !cpi.IsSrgb)
							|| (cpi.DataColorSpace == ColorProfile.ProfileColorSpace.Cmyk && fmt.ColorRepresentation == PixelColorRepresentation.Cmyk)
						))
						{
							profile = cc;
							if (cpi.ProfileType == ColorProfileType.Matrix || cpi.ProfileType == ColorProfileType.Curve)
								ctx.SourceColorProfile = cpi;
							break;
						}
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

			ctx.Settings.Crop = new Rectangle(0, 0, crop.Width, crop.Height);
		}

		public static void AddColorspaceConverter(PipelineContext ctx)
		{
			if (ctx.WicContext.SourceColorContext is null || ctx.WicContext.SourceColorContext == ctx.WicContext.DestColorContext)
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

			var newFormat = PixelFormat.Cache[Consts.GUID_WICPixelFormat24bppBGR];
			if (curFormat.AlphaRepresentation == PixelAlphaRepresentation.Associated && allowPbgra && ctx.Settings.BlendingMode != GammaMode.Linear && ctx.Settings.MatteColor.IsEmpty)
				newFormat = PixelFormat.Cache[Consts.GUID_WICPixelFormat32bppPBGRA];
			else if (curFormat.AlphaRepresentation != PixelAlphaRepresentation.None)
				newFormat = PixelFormat.Cache[Consts.GUID_WICPixelFormat32bppBGRA];
			else if (curFormat.ColorRepresentation == PixelColorRepresentation.Grey)
				newFormat = PixelFormat.Cache[Consts.GUID_WICPixelFormat8bppGray];

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
			var newFormat = PixelFormat.Cache[Consts.GUID_WICPixelFormat8bppIndexed];

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

		public static void AddNativeExifRotator(PipelineContext ctx)
		{
			if (ctx.DecoderFrame.ExifOrientation == Orientation.Normal || !ctx.DecoderFrame.SupportsNativeTransform)
				return;

			var rotator = ctx.WicContext.AddRef(Wic.Factory.CreateBitmapFlipRotator());
			rotator.Initialize(ctx.Source.WicSource, ctx.DecoderFrame.ExifOrientation.ToWicTransformOptions());
			ctx.Source = rotator.AsPixelSource(nameof(IWICBitmapFlipRotator), !ctx.DecoderFrame.SupportsPlanarPipeline);

			AddConditionalCache(ctx);

			ctx.Settings.OrientationMode = OrientationMode.Ignore;
		}

		public static void AddExifRotator(PipelineContext ctx)
		{
			if (ctx.DecoderFrame.ExifOrientation == Orientation.Normal || ctx.Settings.OrientationMode != OrientationMode.Normalize)
				return;

			var rotator = ctx.WicContext.AddRef(Wic.Factory.CreateBitmapFlipRotator());
			rotator.Initialize(ctx.Source.WicSource, ctx.DecoderFrame.ExifOrientation.ToWicTransformOptions());
			ctx.Source = rotator.AsPixelSource(nameof(IWICBitmapFlipRotator), !ctx.DecoderFrame.SupportsPlanarPipeline && !ctx.DecoderFrame.SupportsNativeTransform);

			AddConditionalCache(ctx);
		}

		public static void AddCropper(PipelineContext ctx)
		{
			if (ctx.Settings.Crop == new Rectangle(0, 0, (int)ctx.Source.Width, (int)ctx.Source.Height))
				return;

			var cropper = ctx.WicContext.AddRef(Wic.Factory.CreateBitmapClipper());
			cropper.Initialize(ctx.Source.WicSource, ctx.Settings.Crop.ToWicRect());
			ctx.Source = cropper.AsPixelSource(nameof(IWICBitmapClipper));
		}

		public static void AddScaler(PipelineContext ctx, bool hybrid = false)
		{
			uint width = (uint)ctx.Settings.InnerRect.Width, height = (uint)ctx.Settings.InnerRect.Height;
			double rat = ctx.Settings.HybridScaleRatio;

			if ((ctx.Source.Width == width && ctx.Source.Height == height) || (hybrid && rat == 1d))
				return;

			if (hybrid)
			{
				width = (uint)Math.Ceiling(ctx.Source.Width / rat);
				height = (uint)Math.Ceiling(ctx.Source.Height / rat);
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
			scaler.Initialize(ctx.Source.WicSource, width, height, mode);
			ctx.Source = scaler.AsPixelSource(nameof(IWICBitmapScaler));
		}

		public static void AddNativeScaler(PipelineContext ctx)
		{
			double rat = ctx.Settings.HybridScaleRatio;
			if (rat == 1d || !ctx.DecoderFrame.SupportsNativeScale || !(ctx.Source.WicSource is IWICBitmapSourceTransform trans))
				return;

			uint ow = ctx.Source.Width, oh = ctx.Source.Height;
			uint cw = (uint)Math.Ceiling(ow / rat), ch = (uint)Math.Ceiling(oh / rat);
			trans.GetClosestSize(ref cw, ref ch);

			if (cw == ow && ch == oh)
				return;

			bool swap = ctx.DecoderFrame.ExifOrientation.RequiresDimensionSwap();
			double wrat = swap ? (double)oh / ch : (double)ow / cw;
			double hrat = swap ? (double)ow / cw : (double)oh / ch;

			var crop = ctx.Settings.Crop;
			ctx.Settings.Crop = new Rectangle(
				(int)Math.Floor(crop.X / wrat),
				(int)Math.Floor(crop.Y / hrat),
				Math.Min((int)Math.Ceiling(crop.Width / wrat), (int)(swap ? ch : cw)),
				Math.Min((int)Math.Ceiling(crop.Height / hrat), (int)(swap ? cw : ch))
			);

			var scaler = ctx.WicContext.AddRef(Wic.Factory.CreateBitmapScaler());
			scaler.Initialize(ctx.Source.WicSource, cw, ch, WICBitmapInterpolationMode.WICBitmapInterpolationModeFant);
			ctx.Source = scaler.AsPixelSource(nameof(IWICBitmapSourceTransform));
		}

		public static void AddPlanarCache(PipelineContext ctx)
		{
			if (!(ctx.Source.WicSource is IWICPlanarBitmapSourceTransform trans))
				throw new NotSupportedException("Transform chain doesn't support planar mode.  Only JPEG Decoder, Rotator, Scaler, and PixelFormatConverter are allowed");

			double rat = ctx.Settings.HybridScaleRatio.Clamp(1d, 8d);
			uint width = (uint)Math.Ceiling(ctx.Source.Width / rat);
			uint height = (uint)Math.Ceiling(ctx.Source.Height / rat);

			var opt = ctx.DecoderFrame.ExifOrientation.ToWicTransformOptions();
			var desc = new WICBitmapPlaneDescription[2];
			if (!trans.DoesSupportTransform(ref width, ref height, opt, WICPlanarOptions.WICPlanarOptionsDefault, planarPixelFormats, desc, 2))
				throw new NotSupportedException("Requested planar transform not supported");

			ctx.WicContext.PlanarCache = ctx.AddDispose(new WicPlanarCache(trans, desc[0], desc[1], PixelArea.FromGdiRect(ctx.Settings.Crop), opt, width, height, (int)rat));
			ctx.Source = ctx.WicContext.PlanarCache.GetPlane(WicPlane.Luma);
		}

		public static void AddPlanarConverter(PipelineContext ctx)
		{
			var conv = (IWICPlanarFormatConverter)ctx.WicContext.AddRef(Wic.Factory.CreateFormatConverter());
			conv.Initialize(new[] { ctx.PlanarLumaSource.WicSource, ctx.PlanarChromaSource.WicSource }, 2, Consts.GUID_WICPixelFormat24bppBGR, WICBitmapDitherType.WICBitmapDitherTypeNone, null, 0.0, WICBitmapPaletteType.WICBitmapPaletteTypeCustom);
			ctx.Source = conv.AsPixelSource(nameof(IWICPlanarFormatConverter));
			ctx.PlanarLumaSource = ctx.PlanarChromaSource = null;
		}
	}
}