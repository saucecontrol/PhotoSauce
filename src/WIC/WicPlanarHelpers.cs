using System;

using PhotoSauce.MagicScaler.Interop;

namespace PhotoSauce.MagicScaler
{
	internal enum WicPlane { Luma, Chroma }

	internal abstract class WicPlanarTransform : WicBase
	{
		public WicProcessingContext Context { get; protected set; }
		public IWICBitmapFrameDecode Frame { get; protected set; }
		public IWICBitmapSource SourceY { get; protected set; }
		public IWICBitmapSource SourceCbCr { get; protected set; }

		protected WicPlanarTransform(WicTransform prev)
		{
			Context = prev.Context;
			Frame = prev.Frame;
		}
	}

	internal class WicPlanarCache : WicPlanarTransform
	{
		public WicPlanarCache(WicTransform prev) : base(prev)
		{
			if (!(prev.Source is IWICPlanarBitmapSourceTransform)) throw new NotSupportedException("Transform chain doesn't support planar mode.  Only JPEG Decoder, Rotator, Scaler, and ColorSpaceConverter are allowed");
			var trans = (IWICPlanarBitmapSourceTransform)prev.Source;

			double rat = Context.Settings.HybridScaleRatio.Clamp(1d, 8d);
			Context.Width = (uint)Math.Ceiling(Context.Width / rat);
			Context.Height = (uint)Math.Ceiling(Context.Height / rat);

			var prog = Frame as IWICProgressiveLevelControl;
			if (prog != null) // TODO needs work
			{
				uint levels = prog.GetLevelCount();
				uint level = (uint)Math.Ceiling(levels / rat) + (Context.Settings.HybridMode == HybridScaleMode.FavorQuality || Context.Settings.HybridMode == HybridScaleMode.Off ? (uint)Math.Ceiling(levels / 8d) : 0u);
				prog.SetCurrentLevel(Math.Min(level, levels - (Context.Settings.ScaleRatio >= 2d && levels > 7u ? 2u : 1u)));
			}

			var fmts = new Guid[] { Consts.GUID_WICPixelFormat8bppY, Consts.GUID_WICPixelFormat16bppCbCr };
			var desc = new WICBitmapPlaneDescription[2];

			if (!trans.DoesSupportTransform(ref Context.Width, ref Context.Height, Context.TransformOptions, WICPlanarOptions.WICPlanarOptionsPreserveSubsampling, fmts, desc, 2))
				throw new NotSupportedException("Requested planar transform not supported");

			var crop = new WICRect() { X = Context.Settings.Crop.X, Y = Context.Settings.Crop.Y, Width = Context.Settings.Crop.Width, Height = Context.Settings.Crop.Height };
			var source = new WicPlanarCacheSource(trans, desc[0], desc[1], crop, Context.TransformOptions, Context.Width, Context.Height, rat);

			SourceY = source.GetPlane(WicPlane.Luma);
			SourceCbCr = source.GetPlane(WicPlane.Chroma);
		}
	}

	internal class WicPlanarSplitter : WicTransform
	{
		public WicPlanarSplitter(WicPlanarTransform prev, WicPlane plane)
		{
			Context = prev.Context;
			Frame = prev.Frame;
			Source = plane == WicPlane.Luma ? prev.SourceY : prev.SourceCbCr;

			Source.GetSize(out Context.Width, out Context.Height);
		}
	}

	internal class WicPlanarConverter : WicTransform
	{
		public WicPlanarConverter(WicTransform prevY, WicTransform prevCbCr) : base(prevY)
		{
			var cfmt = Consts.GUID_WICPixelFormat24bppBGR;
			var conv = AddRef(Wic.CreateFormatConverter());
			var pconv = conv as IWICPlanarFormatConverter;
			pconv.Initialize(new IWICBitmapSource[] { prevY.Source, prevCbCr.Source }, 2, cfmt, WICBitmapDitherType.WICBitmapDitherTypeNone, null, 0.0, WICBitmapPaletteType.WICBitmapPaletteTypeCustom);
			Source = pconv;

			if (Context.SourceColorContext != null)
			{
				var trans = AddRef(Wic.CreateColorTransform());
				trans.Initialize(Source, Context.SourceColorContext, Context.DestColorContext, cfmt);
				Source = trans;
			}
		}
	}

	internal class WicPlanarEncoder : WicBase
	{
		public IWICBitmapEncoder Encoder { get; private set; }
		public IWICBitmapFrameEncode Frame { get; private set; }
		public IWICPlanarBitmapFrameEncode PlanarFrame { get; private set; }

		public WicPlanarEncoder(WicEncoder enc)
		{
			Encoder = enc.Encoder;
			Frame = enc.Frame;
			PlanarFrame = enc.Frame as IWICPlanarBitmapFrameEncode;
		}

		public void WriteSource(WicTransform prevY, WicTransform prevCbCr)
		{
			var oformat = Consts.GUID_WICPixelFormat24bppBGR;
			Frame.SetPixelFormat(ref oformat);

			PlanarFrame.WriteSource(new IWICBitmapSource[] { prevY.Source, prevCbCr.Source }, 2, null);

			Frame.Commit();
			Encoder.Commit();
		}
	}
}