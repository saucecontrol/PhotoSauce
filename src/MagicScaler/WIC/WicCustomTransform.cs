using PhotoSauce.MagicScaler.Interop;

namespace PhotoSauce.MagicScaler
{

	internal class WicGammaExpand : WicTransform
	{
		public WicGammaExpand(WicTransform prev) : base(prev)
		{
			if (Context.Settings.BlendingMode != GammaMode.Linear)
				return;

			var fmt = Source.GetPixelFormat();
			var conv = (WicFormatConverterBase)null;

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
			var conv = (WicFormatConverterBase)null;

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
		private KernelMap<int> mapx;
		private KernelMap<int> mapy;
		private WicBitmapSourceBase source;

		public WicHighQualityScaler(WicTransform prev) : base(prev)
		{
			uint width = (uint)Context.Settings.Width, height = (uint)Context.Settings.Height;
			var interpolatorx = width == Context.Width ? InterpolationSettings.NearestNeighbor : Context.Settings.Interpolation;
			var interpolatory = height == Context.Height ? InterpolationSettings.NearestNeighbor : Context.Settings.Interpolation;

			var fmt = Source.GetPixelFormat();
			mapx = KernelMap<int>.MakeScaleMap(Context.Width, width, 1u, Context.HasAlpha, interpolatorx);
			mapy = KernelMap<int>.MakeScaleMap(Context.Height, height, 1u, Context.HasAlpha, interpolatory);

			if (Context.Settings.BlendingMode == GammaMode.Linear && fmt != Consts.GUID_WICPixelFormat16bppCbCr)
				source = new WicConvolution<ushort, int>(Source, mapx, mapy);
			else
				source = new WicConvolution<byte, int>(Source, mapx, mapy);

			Source = source;
			Source.GetSize(out Context.Width, out Context.Height);
		}

		public override void Dispose()
		{
			base.Dispose();
			mapx?.Dispose();
			mapy?.Dispose();
			source?.Dispose();
		}
	}

	internal class WicUnsharpMask : WicTransform
	{
		private KernelMap<int> mapx;
		private KernelMap<int> mapy;

		public WicUnsharpMask(WicTransform prev) : base(prev)
		{
			var ss = Context.Settings.UnsharpMask;
			if (ss.Radius <= 0d || ss.Amount <= 0)
				return;

			mapx = KernelMap<int>.MakeBlurMap(Context.Width, ss.Radius, 1u, Context.HasAlpha);
			mapy = KernelMap<int>.MakeBlurMap(Context.Height, ss.Radius, 1u, Context.HasAlpha);

			Source = new WicUnsharpMask<byte, int>(Source, mapx, mapy, ss);
		}

		public override void Dispose()
		{
			base.Dispose();
			mapx?.Dispose();
			mapy?.Dispose();
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
