using System;
using System.Linq;
using System.Drawing;
using System.Collections.Generic;
using System.Runtime.InteropServices;

using PhotoSauce.MagicScaler.Interop;

namespace PhotoSauce.MagicScaler
{
	internal class WicTransform : WicBase
	{
		public WicProcessingContext Context { get; protected set; }
		public IWICBitmapFrameDecode Frame { get; protected set; }
		public IWICBitmapSource Source { get; protected set; }

		protected WicTransform() { }

		public WicTransform(WicTransform prev)
		{
			Frame = prev.Frame;
			Source = prev.Source;
			Context = prev.Context;
		}

		public WicTransform(IWICBitmapFrameDecode frm, IWICBitmapSource src, WicProcessingContext ctx)
		{
			Frame = frm;
			Source = src;
			Context = ctx;
		}
	}

	internal class WicFrameReader : WicTransform
	{
		public WicFrameReader(WicDecoder dec, WicProcessingContext ctx)
		{
			Frame = AddRef(dec.Decoder.GetFrame((uint)ctx.Settings.FrameIndex));
			Source = Frame;
			Context = ctx;

			if (ctx.ContainerFormat == Consts.GUID_ContainerFormatRaw && ctx.Settings.FrameIndex == 0)
				try { Source = AddRef(dec.Decoder.GetPreview()); } catch { }

			ctx.PixelFormat = Source.GetPixelFormat();
			Source.GetSize(out ctx.Width, out ctx.Height);

			var ptrans = Source as IWICPlanarBitmapSourceTransform;
			if (ptrans != null)
			{
				uint pw = ctx.Width, ph = ctx.Height;
				var pdesc = new WICBitmapPlaneDescription[2];
				var pfmts = new Guid[] { Consts.GUID_WICPixelFormat8bppY, Consts.GUID_WICPixelFormat16bppCbCr };
				ctx.SupportsPlanar = ptrans.DoesSupportTransform(ref pw, ref ph, WICBitmapTransformOptions.WICBitmapTransformRotate0, WICPlanarOptions.WICPlanarOptionsPreserveSubsampling, pfmts, pdesc, 2);
				ctx.IsSubsampled = pdesc[0].Width != pdesc[1].Width || pdesc[0].Height != pdesc[1].Height;
			}
		}
	}

	internal class WicMetadataReader : WicTransform
	{
		private static IWICColorContext getDefaultColorProfile(Guid pixelFormat)
		{
			var pfi = Wic.CreateComponentInfo(pixelFormat) as IWICPixelFormatInfo;
			var cc = pfi.GetColorContext();
			Marshal.ReleaseComObject(pfi);
			return cc;
		}

		private static readonly Lazy<IWICColorContext> cmykProfile = new Lazy<IWICColorContext>(() => getDefaultColorProfile(Consts.GUID_WICPixelFormat32bppCMYK));
		private static readonly Lazy<IWICColorContext> srgbProfile = new Lazy<IWICColorContext>(() => getDefaultColorProfile(Consts.GUID_WICPixelFormat24bppBGR));

		public WicMetadataReader(WicTransform prev, bool basicOnly = false) : base(prev)
		{
			var pfi = AddRef(Wic.CreateComponentInfo(Context.PixelFormat) as IWICPixelFormatInfo2);
			if (pfi.GetNumericRepresentation() == WICPixelFormatNumericRepresentation.WICPixelFormatNumericRepresentationIndexed)
			{
				var pal = AddRef(Wic.CreatePalette());
				Frame.CopyPalette(pal);

				Context.HasAlpha = pal.HasAlpha();
				Context.IsGreyscale = pal.IsGrayscale();
			}
			else
			{
				uint chans = pfi.GetChannelCount();
				bool trans = pfi.SupportsTransparency();
				Context.HasAlpha = trans;
				Context.IsGreyscale = chans == 1u;
				Context.IsCmyk = (chans == 4u && !trans) || (chans == 5u && trans);
			}

			var metareader = Frame.GetMetadataQueryReaderNoThrow();
			if (metareader != null)
			{
				AddRef(metareader);
				// Exif orientation
				if (metareader.HasMetadataName("System.Photo.Orientation"))
				{
					ushort orientation = 1;
					var ovar = new PropVariant();
					metareader.GetMetadataByName("System.Photo.Orientation", ovar);
					if (ovar.UnmanagedType == VarEnum.VT_UI2)
						orientation = (ushort)ovar.Value;

					var opt = WICBitmapTransformOptions.WICBitmapTransformRotate0;
					if (orientation == 3 || orientation == 4)
						opt = WICBitmapTransformOptions.WICBitmapTransformRotate180;
					else if (orientation == 6 || orientation == 7)
						opt = WICBitmapTransformOptions.WICBitmapTransformRotate90;
					else if (orientation == 5 || orientation == 8)
						opt = WICBitmapTransformOptions.WICBitmapTransformRotate270;

					if (orientation == 2 || orientation == 4 || orientation == 5 || orientation == 7)
						opt |= WICBitmapTransformOptions.WICBitmapTransformFlipHorizontal;

					Context.TransformOptions = opt;
				}

				if (basicOnly)
					return;

				// other requested properties
				var propdic = new Dictionary<string, PropVariant>();
				foreach (string prop in Context.Settings.MetadataNames ?? Enumerable.Empty<string>())
				{
					if (metareader.HasMetadataName(prop))
					{
						var pvar = new PropVariant();
						metareader.GetMetadataByName(prop, pvar);
						if (pvar.Value != null)
							propdic[prop] = pvar;
					}
				}

				Context.Metadata = propdic;
			}

			// ICC profiles
			//http://ninedegreesbelow.com/photography/embedded-color-space-information.html
			uint ccc = Frame.GetColorContextCount();
			var profiles = new IWICColorContext[ccc];
			var profile = (IWICColorContext)null;

			if (ccc > 0)
			{
				for (int i = 0; i < ccc; i++)
					profiles[i] = AddRef(Wic.CreateColorContext());

				Frame.GetColorContexts(ccc, profiles);
			}

			foreach (var cc in profiles)
			{
				var cct = cc.GetType();
				if (cct == WICColorContextType.WICColorContextProfile)
				{
					uint ccs = cc.GetProfileBytes(0, null);
					var ccb = new byte[ccs];
					cc.GetProfileBytes(ccs, ccb);

					// match only color profiles that match our intended use. if we have a standard sRGB profile, don't save it; we don't need to convert
					var cp = new ColorProfileInfo(ccb);
					if (cp.IsValid && ((cp.IsDisplayRgb && !cp.IsStandardSrgb) || (Context.IsCmyk && cp.IsCmyk) /* || (Context.IsGreyscale && cp.DataColorSpace == "GRAY") */))
					{
						profile = cc;
						break;
					}
				}
				else if (cct == WICColorContextType.WICColorContextExifColorSpace && cc.GetExifColorSpace() == ExifColorSpace.AdobeRGB)
				{
					profile = cc;
					break;
				}
			}

			Context.SourceColorContext = profile ?? (Context.IsCmyk ? cmykProfile.Value : null);
			Context.DestColorContext = srgbProfile.Value;
		}
	}

	internal class WicConditionalCache : WicTransform
	{
		public WicConditionalCache(WicTransform prev) : base(prev)
		{
			if (!Context.NeedsCache)
				return;

			var crop = Context.Settings.Crop;
			var bmp = AddRef(Wic.CreateBitmapFromSourceRect(Source, (uint)crop.X, (uint)crop.Y, (uint)crop.Width, (uint)crop.Height));

			Source = bmp;
			Source.GetSize(out Context.Width, out Context.Height);
			Context.Settings.Crop = new Rectangle(0, 0, (int)Context.Width, (int)Context.Height);
			Context.NeedsCache = false;
		}
	}

	internal class WicCmykConverter : WicTransform
	{
		public WicCmykConverter(WicTransform prev) : base(prev)
		{
			if (!Context.IsCmyk)
				return;

			// TODO 40bppcmyka unsupported -- WIC bug
			var trans = AddRef(Wic.CreateColorTransform());
			trans.Initialize(Source, Context.SourceColorContext, Context.DestColorContext, Context.HasAlpha ? Consts.GUID_WICPixelFormat32bppBGRA : Consts.GUID_WICPixelFormat24bppBGR);

			Source = trans;
			Context.SourceColorContext = null;
		}
	}

	internal class WicColorspaceConverter : WicTransform
	{
		public WicColorspaceConverter(WicTransform prev) : base(prev)
		{
			if (Context.SourceColorContext == null)
				return;

			var trans = AddRef(Wic.CreateColorTransform());
			trans.Initialize(Source, Context.SourceColorContext, Context.DestColorContext, Context.IsCmyk ? Context.HasAlpha ? Consts.GUID_WICPixelFormat32bppBGRA : Consts.GUID_WICPixelFormat24bppBGR : Context.PixelFormat);

			Source = trans;
		}
	}

	internal class WicPixelFormatConverter : WicTransform
	{
		public WicPixelFormatConverter(WicTransform prev) : base(prev)
		{
			var newFormat = Consts.GUID_WICPixelFormat24bppBGR;
			if (Context.HasAlpha)
				newFormat = Context.PixelFormat == Consts.GUID_WICPixelFormat32bppPBGRA ? Consts.GUID_WICPixelFormat32bppPBGRA : Consts.GUID_WICPixelFormat32bppBGRA;
			else if (Context.IsGreyscale)
				newFormat = Consts.GUID_WICPixelFormat8bppGray;
			else if (Context.IsCmyk && Context.SourceColorContext != null)
				newFormat = Consts.GUID_WICPixelFormat32bppCMYK;

			if (Context.PixelFormat == newFormat)
				return;

			var conv = AddRef(Wic.CreateFormatConverter());
			if (!conv.CanConvert(Context.PixelFormat, newFormat))
				throw new NotSupportedException("Can't convert to destination pixel format");

			conv.Initialize(Source, newFormat, WICBitmapDitherType.WICBitmapDitherTypeNone, null, 0.0, WICBitmapPaletteType.WICBitmapPaletteTypeCustom);
			Source = conv;
			Context.PixelFormat = Source.GetPixelFormat();
		}
	}

	internal class WicPaletizer : WicTransform
	{
		public WicPaletizer(WicTransform prev, uint colors) : base(prev)
		{
			var newFormat = Consts.GUID_WICPixelFormat8bppIndexed;

			if (!Context.Settings.IndexedColor || Context.PixelFormat == newFormat)
				return;

			var conv = AddRef(Wic.CreateFormatConverter());
			if (!conv.CanConvert(Context.PixelFormat, newFormat))
				throw new ArgumentException("Can't convert to destination pixel format");

			var bmp = AddRef(Wic.CreateBitmapFromSource(Source, WICBitmapCreateCacheOption.WICBitmapCacheOnDemand));

			var pal = AddRef(Wic.CreatePalette());
			pal.InitializeFromBitmap(bmp, colors, false);
			Context.DestPalette = pal;

			conv.Initialize(bmp, newFormat, WICBitmapDitherType.WICBitmapDitherTypeErrorDiffusion, pal, 1.0, WICBitmapPaletteType.WICBitmapPaletteTypeCustom);
			Source = conv;
			Context.PixelFormat = Source.GetPixelFormat();
		}
	}

	internal class WicExifRotator : WicTransform
	{
		public WicExifRotator(WicTransform prev) : base(prev)
		{
			if (Context.TransformOptions == WICBitmapTransformOptions.WICBitmapTransformRotate0)
				return;

			var rotator = AddRef(Wic.CreateBitmapFlipRotator());
			rotator.Initialize(Source, Context.TransformOptions);

			Source = rotator;
			Source.GetSize(out Context.Width, out Context.Height);

			if (Context.TransformOptions != WICBitmapTransformOptions.WICBitmapTransformFlipHorizontal)
				Context.NeedsCache = true;
		}
	}

	internal class WicCropper : WicTransform
	{
		public WicCropper(WicTransform prev) : base(prev)
		{
			var crop = Context.Settings.Crop;
			if (crop == Rectangle.FromLTRB(0, 0, (int)Context.Width, (int)Context.Height))
				return;

			var cropper = AddRef(Wic.CreateBitmapClipper());
			cropper.Initialize(Source, new WICRect() { X = crop.X, Y = crop.Y, Width = crop.Width, Height = crop.Height });

			Source = cropper;
			Source.GetSize(out Context.Width, out Context.Height);
		}
	}

	internal class WicScaler : WicTransform
	{
		public WicScaler(WicTransform prev, bool hybrid = false) : base(prev)
		{
			if (Context.Settings.Width == Context.Width && Context.Settings.Height == Context.Height)
				return;

			double rat = Context.Settings.HybridScaleRatio;
			if (hybrid && rat == 1d)
				return;

			if (Source is IWICBitmapSourceTransform)
			{
				// null crop to disable IWICBitmapSourceTransform scaling
				var clip = AddRef(Wic.CreateBitmapClipper());
				clip.Initialize(Source, new WICRect() { X = 0, Y = 0, Width = (int)Context.Width, Height = (int)Context.Height });

				Source = clip;
			}

			uint ow = hybrid ? (uint)Math.Ceiling(Context.Width / rat) : (uint)Context.Settings.Width;
			uint oh = hybrid ? (uint)Math.Ceiling(Context.Height / rat) : (uint)Context.Settings.Height;

			var scaler = AddRef(Wic.CreateBitmapScaler());
			scaler.Initialize(Source, ow, oh, Context.Settings.ScaleRatio < 1.1 ? WICBitmapInterpolationMode.WICBitmapInterpolationModeCubic : WICBitmapInterpolationMode.WICBitmapInterpolationModeFant);

			Source = scaler;
			Source.GetSize(out Context.Width, out Context.Height);

			if (hybrid)
				Context.Settings.Crop = new Rectangle(0, 0, (int)Context.Width, (int)Context.Height);
		}
	}

	internal class WicNativeScaler : WicTransform
	{
		public WicNativeScaler(WicTransform prev) : base(prev)
		{
			double rat = Context.Settings.HybridScaleRatio;
			if (rat == 1d)
				return;

			var trans = Source as IWICBitmapSourceTransform;
			if (trans == null)
				return;

			uint ow = Context.Width, oh = Context.Height;
			uint cw = (uint)Math.Ceiling(ow / rat), ch = (uint)Math.Ceiling(oh / rat);
			trans.GetClosestSize(ref cw, ref ch);

			if (cw == ow && ch == oh)
				return;

			double wrat = (double)ow / cw, hrat = (double)oh / ch;

			var crop = Context.Settings.Crop;
			Context.Settings.Crop = new Rectangle((int)Math.Floor(crop.X / wrat), (int)Math.Floor(crop.Y / hrat), (int)Math.Ceiling(crop.Width / wrat), (int)Math.Ceiling(crop.Height / hrat));

			var scaler = AddRef(Wic.CreateBitmapScaler());
			scaler.Initialize(Source, cw, ch, WICBitmapInterpolationMode.WICBitmapInterpolationModeFant);

			Source = scaler;
			Source.GetSize(out Context.Width, out Context.Height);
		}
	}

	internal class WicGammaExpand : WicTransform
	{
		public WicGammaExpand(WicTransform prev) : base(prev)
		{
			if (Context.Settings.BlendingMode != GammaMode.Linear)
				return;

			var fmt = Source.GetPixelFormat();
			var conv = (WicLinearFormatConverter)null;

			if (fmt == Consts.GUID_WICPixelFormat32bppBGRA)
				conv = new WicLinearFormatConverter(Source, Consts.GUID_WICPixelFormat64bppBGRA);
			else if (fmt == Consts.GUID_WICPixelFormat24bppBGR)
				conv = new WicLinearFormatConverter(Source, Consts.GUID_WICPixelFormat48bppBGR);
			else if (fmt == Consts.GUID_WICPixelFormat8bppGray || fmt == Consts.GUID_WICPixelFormat8bppY)
				conv = new WicLinearFormatConverter(Source, Consts.GUID_WICPixelFormat16bppGray);

			if (conv == null)
				return;

			Source = conv;
			Context.PixelFormat = Source.GetPixelFormat();
		}
	}

	internal class WicGammaCompress : WicTransform
	{
		public WicGammaCompress(WicTransform prev) : base(prev)
		{
			var fmt = Source.GetPixelFormat();
			var conv = (WicGammaFormatConverter)null;

			if (fmt == Consts.GUID_WICPixelFormat64bppBGRA)
				conv = new WicGammaFormatConverter(Source, Consts.GUID_WICPixelFormat32bppBGRA);
			else if (fmt == Consts.GUID_WICPixelFormat48bppBGR)
				conv = new WicGammaFormatConverter(Source, Consts.GUID_WICPixelFormat24bppBGR);
			else if (fmt == Consts.GUID_WICPixelFormat16bppGray)
				conv = new WicGammaFormatConverter(Source, Context.SupportsPlanar ? Consts.GUID_WICPixelFormat8bppY : Consts.GUID_WICPixelFormat8bppGray);

			if (conv == null)
				return;

			Source = conv;
			Context.PixelFormat = Source.GetPixelFormat();
		}
	}

	internal class WicHighQualityScaler : WicTransform
	{
		public WicHighQualityScaler(WicTransform prev) : base(prev)
		{
			uint width = (uint)Context.Settings.Width, height = (uint)Context.Settings.Height;
			var interpolatorx = width == Context.Width ? InterpolationSettings.NearestNeighbor : Context.Settings.Interpolation;
			var interpolatory = height == Context.Height ? InterpolationSettings.NearestNeighbor : Context.Settings.Interpolation;

			var mapx = KernelMap.MakeScaleMap(Context.Width, width, interpolatorx);
			var mapy = KernelMap.MakeScaleMap(Context.Height, height, interpolatory);

			if (Context.Settings.BlendingMode == GammaMode.Linear)
				Source = new WicConvolution16bpc(Source, mapx, mapy);
			else
				Source = new WicConvolution8bpc(Source, mapx, mapy);

			Source.GetSize(out Context.Width, out Context.Height);
		}
	}

	internal class WicUnsharpMask : WicTransform
	{
		public WicUnsharpMask(WicTransform prev) : base(prev)
		{
			var ss = Context.Settings.UnsharpMask;
			if (ss.Radius <= 0 || ss.Amount <= 0)
				return;

			var mapx = KernelMap.MakeBlurMap(Context.Width, ss.Radius);
			var mapy = KernelMap.MakeBlurMap(Context.Height, ss.Radius);

			Source = new WicUnsharpMask8bpc(Source, mapx, mapy, ss);
		}
	}

	internal class WicMatteTransform : WicTransform
	{
		public WicMatteTransform(WicTransform prev) : base(prev)
		{
			if (Context.Settings.MatteColor.IsEmpty || (Context.PixelFormat != Consts.GUID_WICPixelFormat32bppBGRA && Context.PixelFormat != Consts.GUID_WICPixelFormat64bppBGRA))
				return;

			var mat = new WicMatte(Source, Context.Settings.MatteColor);
			Source = mat;
		}
	}

}