using System;
using System.Linq;
using System.Buffers;
using System.Diagnostics;
using System.Collections.Generic;
using System.Runtime.InteropServices;

using PhotoSauce.Interop.Wic;

namespace PhotoSauce.MagicScaler
{
	internal class WicImageFrame : IImageFrame
	{
		private static readonly IDictionary<string, PropVariant> emptyDic = new Dictionary<string, PropVariant>();

		private PixelSource? source;
		private IPixelSource? iSource;

		public double DpiX { get; } = 96d;
		public double DpiY { get; } = 96d;
		public Orientation ExifOrientation { get; set; } = Orientation.Normal;
		public IDictionary<string, PropVariant> Metadata { get; set; } = emptyDic;

		public bool SupportsNativeScale { get; }
		public bool SupportsNativeTransform { get; }
		public bool SupportsPlanarProcessing { get; }

		public WICJpegYCrCbSubsamplingOption ChromaSubsampling { get; }

		public IWICBitmapFrameDecode WicFrame { get; }
		public IWICBitmapSource WicSource { get; }

		public PixelSource Source => source ??= WicSource.AsPixelSource(nameof(IWICBitmapFrameDecode), false);

		public IPixelSource PixelSource => iSource ??= Source.AsIPixelSource();

		public WicImageFrame(WicImageContainer decoder, uint index, WicPipelineContext ctx)
		{
			if (index >= (uint)decoder.FrameCount) throw new IndexOutOfRangeException("Frame index does not exist");

			WicFrame = ctx.AddRef(decoder.WicDecoder.GetFrame(index));
			WicSource = WicFrame;
			WicFrame.GetSize(out uint frameWidth, out uint frameHeight);

			if (decoder.IsRawContainer && index == 0 && decoder.WicDecoder.TryGetPreview(out var preview))
			{
				preview.GetSize(out uint pw, out uint ph);

				if (pw == frameWidth && ph == frameHeight)
					WicSource = ctx.AddRef(preview);
				else
					Marshal.ReleaseComObject(preview);
			}

			WicFrame.GetResolution(out double dpix, out double dpiy);
			DpiX = dpix;
			DpiY = dpiy;

			if (PixelFormat.FromGuid(WicSource.GetPixelFormat()).NumericRepresentation == PixelNumericRepresentation.Indexed)
			{
				var pal = ctx.AddRef(Wic.Factory.CreatePalette());
				WicSource.CopyPalette(pal);

				var newFormat = Consts.GUID_WICPixelFormat24bppBGR;
				if (pal.HasAlpha())
					newFormat = Consts.GUID_WICPixelFormat32bppBGRA;
				else if (pal.IsGrayscale() || pal.IsBlackWhite())
					newFormat = Consts.GUID_WICPixelFormat8bppGray;

				var conv = ctx.AddRef(Wic.Factory.CreateFormatConverter());
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
				var desc = new WICBitmapPlaneDescription[WicTransforms.PlanarPixelFormats.Length];

				SupportsPlanarProcessing = ptrans.DoesSupportTransform(ref frameWidth, ref frameHeight, WICBitmapTransformOptions.WICBitmapTransformRotate0, WICPlanarOptions.WICPlanarOptionsDefault, WicTransforms.PlanarPixelFormats, desc, (uint)desc.Length);
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
		public const string OrientationExifPath = "/ifd/{ushort=274}";
		public const string OrientationJpegPath = "/app1" + OrientationExifPath;

		internal static readonly Guid[] PlanarPixelFormats = new[] { Consts.GUID_WICPixelFormat8bppY, Consts.GUID_WICPixelFormat8bppCb, Consts.GUID_WICPixelFormat8bppCr };

		public static void AddMetadataReader(PipelineContext ctx, bool basicOnly = false)
		{
			if (!(ctx.ImageFrame is WicImageFrame frame))
				return;

			if (frame.WicFrame.TryGetMetadataQueryReader(out var metareader))
			{
				ctx.WicContext.AddRef(metareader);

				string orientationPath = MagicImageProcessor.EnableXmpOrientation ? orientationWindowsPolicy : ctx.ImageContainer.ContainerFormat == FileFormat.Jpeg ? OrientationJpegPath : OrientationExifPath;

				if (metareader.TryGetMetadataByName(orientationPath, out var pvorient) && pvorient.UnmanagedType == VarEnum.VT_UI2)
					frame.ExifOrientation = (Orientation)Math.Min(Math.Max((ushort)Orientation.Normal, (ushort)pvorient.Value!), (ushort)Orientation.Rotate270);

				if (basicOnly)
					return;

				if (ctx.Settings.MetadataNames.Any())
				{
					var propdic = frame.Metadata = new Dictionary<string, PropVariant>();
					foreach (string prop in ctx.Settings.MetadataNames)
					{
						if (metareader.TryGetMetadataByName(prop, out var pvar) && !(pvar.Value is null))
							propdic[prop] = pvar;
					}
				}
			}

			if (basicOnly)
				return;

			// ICC profiles
			// http://ninedegreesbelow.com/photography/embedded-color-space-information.html
			uint ccc = frame.WicFrame.GetColorContextCount();
			var fmt = ctx.Source.Format;
			var profiles = new IWICColorContext[ccc];
			var profile = default(IWICColorContext);

			if (ccc > 0)
			{
				for (int i = 0; i < ccc; i++)
					profiles[i] = ctx.WicContext.AddRef(Wic.Factory.CreateColorContext());

				frame.WicFrame.GetColorContexts(ccc, profiles);
			}

			foreach (var cc in profiles)
			{
				var cct = cc.GetType();
				if (cct == WICColorContextType.WICColorContextProfile)
				{
					uint ccs = cc.GetProfileBytes(0, null);

					// don't try to read giant profiles. 4MiB is more than enough
					if (ccs > (1024 * 1024 * 4))
						continue;

					using var ccb = MemoryPool<byte>.Shared.Rent((int)ccs);
					var cca = ccb.GetOwnedArraySegment((int)ccs);

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
			if (!ctx.Orientation.RequiresCache())
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
			AddConditionalCache(ctx);
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
		}

		public static void AddScaler(PipelineContext ctx, bool hybrid = false)
		{
			bool swap = ctx.Orientation.SwapsDimensions();
			var srect = ctx.Settings.InnerRect;

			int width = swap ? srect.Height : srect.Width, height = swap? srect.Width : srect.Height;
			int ratio = ctx.Settings.HybridScaleRatio;

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
			ctx.Settings.Crop = ctx.Source.Area.ReOrient(ctx.Orientation, ctx.Source.Width, ctx.Source.Height).ToGdiRect();
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
		}

		public static void AddPlanarCache(PipelineContext ctx)
		{
			if (!(ctx.Source.WicSource is IWICPlanarBitmapSourceTransform trans))
				throw new NotSupportedException("Transform chain doesn't support planar mode.  Only JPEG Decoder, Rotator, Scaler, and PixelFormatConverter are allowed");

			int ratio = ctx.Settings.HybridScaleRatio.Clamp(1, 8);
			uint ow = (uint)ctx.Source.Width, oh = (uint)ctx.Source.Height;
			uint cw = (uint)MathUtil.DivCeiling((int)ow, ratio), ch = (uint)MathUtil.DivCeiling((int)oh, ratio);

			var desc = new WICBitmapPlaneDescription[PlanarPixelFormats.Length];
			if (!trans.DoesSupportTransform(ref cw, ref ch, WICBitmapTransformOptions.WICBitmapTransformRotate0, WICPlanarOptions.WICPlanarOptionsDefault, PlanarPixelFormats, desc, (uint)desc.Length))
				throw new NotSupportedException("Requested planar transform not supported");

			var crop = PixelArea.FromGdiRect(ctx.Settings.Crop).DeOrient(ctx.Orientation, (int)ow, (int)oh).ProportionalScale((int)ow, (int)oh, (int)cw, (int)ch);
			var cache = ctx.AddDispose(new WicPlanarCache(trans, desc, WICBitmapTransformOptions.WICBitmapTransformRotate0, cw, ch, crop));
			ctx.PlanarContext = new PipelineContext.PlanarPipelineContext(cache.SourceY, cache.SourceCb, cache.SourceCr);

			ctx.Source = ctx.PlanarContext.SourceY;
			ctx.Settings.Crop = ctx.Source.Area.ReOrient(ctx.Orientation, ctx.Source.Width, ctx.Source.Height).ToGdiRect();
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