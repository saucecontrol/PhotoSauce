using System;
using System.IO;

using PhotoSauce.MagicScaler.Interop;

namespace PhotoSauce.MagicScaler
{
	internal static class MagicPlanarImageProcessor
	{
		public static void ProcessImage(WicTransform prev, WicProcessingContext ctx, Stream ostm)
		{
			bool savePlanar = ctx.Settings.SaveFormat == FileFormat.Jpeg && ctx.SourceColorContext == null;

			using (var rot = new WicExifRotator(prev))
			using (var pln = new WicPlanarCache(rot))
			using (var ply = new WicPlanarSplitter(pln, WicPlane.Luma))
			using (var lll = new WicConvertToCustomPixelFormat(ply))
			using (var mmm = new WicHighQualityScaler(lll))
			using (var ggg = new WicConvertFromCustomPixelFormat(mmm))
			using (var sss = new WicUnsharpMask(ggg))

			using (var enc = new WicEncoder(ostm.AsIStream(), ctx))
			using (var plc = new WicPlanarSplitter(pln, WicPlane.Chroma))
			{
				if (savePlanar)
				{
					var subsample = ctx.Settings.JpegSubsampleMode;
					if (subsample == ChromaSubsampleMode.Subsample420)
						ctx.Settings.Height = (int)Math.Ceiling(ctx.Settings.Height / 2d);

					if (subsample == ChromaSubsampleMode.Subsample420 || subsample == ChromaSubsampleMode.Subsample422)
						ctx.Settings.Width = (int)Math.Ceiling(ctx.Settings.Width / 2d);

					using (var fff = new WicConvertToCustomPixelFormat(plc))
					using (var res = new WicHighQualityScaler(fff))
					using (var bbb = new WicConvertFromCustomPixelFormat(res))
					using (var pen = new WicPlanarEncoder(enc))
						pen.WriteSource(sss, bbb);
				}
				else
				{
					using (var fff = new WicConvertToCustomPixelFormat(plc))
					using (var res = new WicHighQualityScaler(fff))
					using (var bbb = new WicConvertFromCustomPixelFormat(res))
					using (var con = new WicPlanarConverter(sss, bbb))
					using (var pal = new WicPaletizer(con))
						enc.WriteSource(pal);
				}
			}
		}
	}
}