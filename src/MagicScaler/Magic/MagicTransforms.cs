using System;

using PhotoSauce.MagicScaler.Interop;

namespace PhotoSauce.MagicScaler
{

	internal class WicConvertToCustomPixelFormat : WicTransform
	{
		private WicCustomPixelFormatConverter conv;

		public WicConvertToCustomPixelFormat(WicTransform prev) : base(prev)
		{
			var ifmt = Source.GetPixelFormat();
			var ofmt = ifmt;
			bool linear = Context.Settings.BlendingMode == GammaMode.Linear;

			if (MagicImageProcessor.EnableSimd)
			{
				if (ifmt == Consts.GUID_WICPixelFormat8bppGray)
					ofmt = linear ? PixelFormat.Grey32BppLinearFloat.FormatGuid : Consts.GUID_WICPixelFormat32bppGrayFloat;
				else if (ifmt == Consts.GUID_WICPixelFormat8bppY)
					ofmt = linear ? PixelFormat.Y32BppLinearFloat.FormatGuid : PixelFormat.Y32BppFloat.FormatGuid;
				else if (ifmt == Consts.GUID_WICPixelFormat24bppBGR)
					ofmt = linear ? PixelFormat.Bgr96BppLinearFloat.FormatGuid : PixelFormat.Bgr96BppFloat.FormatGuid;
				else if (ifmt == Consts.GUID_WICPixelFormat32bppBGRA || ifmt == Consts.GUID_WICPixelFormat32bppPBGRA)
					ofmt = linear ? PixelFormat.Pbgra128BppLinearFloat.FormatGuid : PixelFormat.Pbgra128BppFloat.FormatGuid;
				else if (ifmt == Consts.GUID_WICPixelFormat16bppCbCr)
					ofmt = PixelFormat.CbCr64BppFloat.FormatGuid;
			}
			else if (linear)
			{
				if (ifmt == Consts.GUID_WICPixelFormat8bppGray)
					ofmt = PixelFormat.Grey16BppLinearUQ15.FormatGuid;
				else if (ifmt == Consts.GUID_WICPixelFormat8bppY)
					ofmt = PixelFormat.Y16BppLinearUQ15.FormatGuid;
				else if (ifmt == Consts.GUID_WICPixelFormat24bppBGR)
					ofmt = PixelFormat.Bgr48BppLinearUQ15.FormatGuid;
				else if (ifmt == Consts.GUID_WICPixelFormat32bppBGRA)
					ofmt = PixelFormat.Bgra64BppLinearUQ15.FormatGuid;
				else if (ifmt == Consts.GUID_WICPixelFormat32bppPBGRA)
					ofmt = PixelFormat.Pbgra64BppLinearUQ15.FormatGuid;
			}

			if (ofmt == ifmt)
				return;

			Source = conv = new WicCustomPixelFormatConverter(Source, ofmt);
			Context.PixelFormat = PixelFormat.Cache[Source.GetPixelFormat()];
		}

		public override void Dispose()
		{
			base.Dispose();
			conv?.Dispose();
		}
	}

	internal class WicConvertFromCustomPixelFormat : WicTransform
	{
		private WicCustomPixelFormatConverter conv;

		public WicConvertFromCustomPixelFormat(WicTransform prev) : base(prev)
		{
			var ifmt = Source.GetPixelFormat();
			var ofmt = ifmt;

			if (ifmt == Consts.GUID_WICPixelFormat32bppGrayFloat || ifmt == PixelFormat.Grey32BppLinearFloat.FormatGuid || ifmt == PixelFormat.Grey16BppLinearUQ15.FormatGuid)
				ofmt = Consts.GUID_WICPixelFormat8bppGray;
			else if (ifmt == PixelFormat.Y32BppFloat.FormatGuid || ifmt == PixelFormat.Y32BppLinearFloat.FormatGuid || ifmt == PixelFormat.Y16BppLinearUQ15.FormatGuid)
				ofmt = Consts.GUID_WICPixelFormat8bppY;
			else if (ifmt == PixelFormat.Bgr96BppFloat.FormatGuid || ifmt == PixelFormat.Bgr96BppLinearFloat.FormatGuid || ifmt == PixelFormat.Bgr48BppLinearUQ15.FormatGuid)
				ofmt = Consts.GUID_WICPixelFormat24bppBGR;
			else if (ifmt == PixelFormat.Pbgra128BppFloat.FormatGuid || ifmt == PixelFormat.Pbgra128BppLinearFloat.FormatGuid || ifmt == PixelFormat.Bgra64BppLinearUQ15.FormatGuid || ifmt == PixelFormat.Pbgra64BppLinearUQ15.FormatGuid)
				ofmt = Consts.GUID_WICPixelFormat32bppBGRA;
			else if (ifmt == PixelFormat.CbCr64BppFloat.FormatGuid)
				ofmt = Consts.GUID_WICPixelFormat16bppCbCr;

			if (ofmt == ifmt)
				return;

			Source = conv = new WicCustomPixelFormatConverter(Source, ofmt);
			Context.PixelFormat = PixelFormat.Cache[Source.GetPixelFormat()];
		}

		public override void Dispose()
		{
			base.Dispose();
			conv?.Dispose();
		}
	}

	internal class WicHighQualityScaler : WicTransform
	{
		private IDisposable mapx;
		private IDisposable mapy;
		private WicBitmapSourceBase source;

		public WicHighQualityScaler(WicTransform prev) : base(prev)
		{
			uint width = (uint)Context.Settings.Width, height = (uint)Context.Settings.Height;
			var interpolatorx = width == Context.Width ? InterpolationSettings.NearestNeighbor : Context.Settings.Interpolation;
			var interpolatory = height == Context.Height ? InterpolationSettings.NearestNeighbor : Context.Settings.Interpolation;

			var fmt = PixelFormat.Cache[Source.GetPixelFormat()];
			if (fmt.NumericRepresentation == PixelNumericRepresentation.Float)
			{
				var mx = KernelMap<float>.MakeScaleMap(Context.Width, width, fmt.ColorChannelCount, fmt.AlphaRepresentation != PixelAlphaRepresentation.None, true, interpolatorx);
				var my = KernelMap<float>.MakeScaleMap(Context.Height, height, fmt.ColorChannelCount, fmt.AlphaRepresentation != PixelAlphaRepresentation.None, true, interpolatory);

				source = new WicConvolution<float, float>(Source, mx, my);

				mapx = mx;
				mapy = my;
			}
			else
			{
				var mx = KernelMap<int>.MakeScaleMap(Context.Width, width, 1, fmt.AlphaRepresentation == PixelAlphaRepresentation.Unassociated, false, interpolatorx);
				var my = KernelMap<int>.MakeScaleMap(Context.Height, height, 1, fmt.AlphaRepresentation == PixelAlphaRepresentation.Unassociated, false, interpolatory);

				if (fmt.NumericRepresentation == PixelNumericRepresentation.Fixed)
					source = new WicConvolution<ushort, int>(Source, mx, my);
				else
					source = new WicConvolution<byte, int>(Source, mx, my);

				mapx = mx;
				mapy = my;
			}

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
		private WicBitmapSourceBase source;

		public WicUnsharpMask(WicTransform prev) : base(prev)
		{
			var ss = Context.Settings.UnsharpMask;
			if (ss.Radius <= 0d || ss.Amount <= 0)
				return;

			var fmt = PixelFormat.Cache[Source.GetPixelFormat()];
			mapx = KernelMap<int>.MakeBlurMap(Context.Width, ss.Radius, 1u, fmt.AlphaRepresentation != PixelAlphaRepresentation.None);
			mapy = KernelMap<int>.MakeBlurMap(Context.Height, ss.Radius, 1u, fmt.AlphaRepresentation != PixelAlphaRepresentation.None);

			Source = source = new WicUnsharpMask<byte, int>(Source, mapx, mapy, ss);
		}

		public override void Dispose()
		{
			base.Dispose();
			mapx?.Dispose();
			mapy?.Dispose();
			source?.Dispose();
		}
	}

	internal class WicMatteTransform : WicTransform
	{
		private WicCustomPixelFormatConverter conv;

		public WicMatteTransform(WicTransform prev) : base(prev)
		{
			if (Context.PixelFormat == PixelFormat.Pbgra128BppFloat)
			{
				Source = conv = new WicCustomPixelFormatConverter(Source, Consts.GUID_WICPixelFormat32bppBGRA);
				Context.PixelFormat = PixelFormat.Cache[Source.GetPixelFormat()];
			}

			if (Context.Settings.MatteColor.IsEmpty || Context.PixelFormat.ColorRepresentation != PixelColorRepresentation.Bgr || Context.PixelFormat.AlphaRepresentation == PixelAlphaRepresentation.None)
				return;

			Source = new Matte(Source, Context.Settings.MatteColor);
		}

		public override void Dispose()
		{
			base.Dispose();
			conv?.Dispose();
		}
	}
}
