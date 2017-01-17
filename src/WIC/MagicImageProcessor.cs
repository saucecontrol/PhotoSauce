using System;
using System.IO;
using System.Diagnostics.Contracts;

using PhotoSauce.MagicScaler.Interop;

namespace PhotoSauce.MagicScaler
{
	public static class MagicImageProcessor
	{
		public static void ProcessImage(string imgPath, Stream ostm, ProcessImageSettings s)
		{
			Contract.Requires<ArgumentNullException>(imgPath != null, nameof(imgPath));
			Contract.Requires<ArgumentNullException>(ostm != null, nameof(ostm));
			Contract.Requires<ArgumentException>(ostm.CanSeek && ostm.CanWrite, "Output Stream must allow Seek and Write");

			using (var ctx = new WicProcessingContext(s))
			using (var dec = new WicDecoder(imgPath, ctx))
				processImage(dec, ctx, ostm);
		}

		public static void ProcessImage(byte[] imgBuffer, Stream ostm, ProcessImageSettings s)
		{
			Contract.Requires<ArgumentNullException>(imgBuffer != null, nameof(imgBuffer));
			Contract.Requires<ArgumentNullException>(ostm != null, nameof(ostm));
			Contract.Requires<ArgumentException>(ostm.CanSeek && ostm.CanWrite, "Output Stream must allow Seek and Write");

			using (var ctx = new WicProcessingContext(s))
			using (var dec = new WicDecoder(imgBuffer, ctx))
				processImage(dec, ctx, ostm);
		}

		public static void ProcessImage(Stream istm, Stream ostm, ProcessImageSettings s)
		{
			Contract.Requires<ArgumentNullException>(istm != null, nameof(istm));
			Contract.Requires<ArgumentNullException>(ostm != null, nameof(ostm));
			Contract.Requires<ArgumentException>(istm.CanSeek && istm.CanRead, "Input Stream must allow Seek and Read");
			Contract.Requires<ArgumentException>(ostm.CanSeek && ostm.CanWrite, "Output Stream must allow Seek and Write");
			Contract.Assume(istm.Position < istm.Length, "Input Stream Position is at the end.  Did you forget to Seek?");

			using (var ctx = new WicProcessingContext(s))
			using (var dec = new WicDecoder(istm, ctx))
				processImage(dec, ctx, ostm);
		}

		private static void processImage(WicDecoder dec, WicProcessingContext ctx, Stream ostm)
		{
			using (var frm = new WicFrameReader(dec, ctx))
			using (var met = new WicMetadataReader(frm))
			{
				if (!ctx.Settings.Normalized)
					ctx.Settings.Fixup((int)ctx.Width, (int)ctx.Height, ctx.IsRotated90);

				bool mod1 = (!ctx.IsSubsampled || ctx.Settings.HybridScaleRatio > 1d || (ctx.Settings.Crop.Width % 2 == 0 && ctx.Settings.Crop.Height % 2 == 0) || (ctx.Settings.Crop.Width == ctx.Width && ctx.Settings.Crop.Height == ctx.Height));
				bool planar = ctx.SupportsPlanar && mod1;
				if (planar && ctx.Settings.HybridMode != HybridScaleMode.Off)
				{
					MagicPlanarImageProcessor.ProcessImage(met, ctx, ostm);
					return;
				}

				using (var qsc = new WicNativeScaler(met))
				using (var rot = new WicExifRotator(qsc))
				using (var cac = new WicConditionalCache(rot))
				using (var crp = new WicCropper(cac))
				using (var pix = new WicPixelFormatConverter(crp))
				using (var cmy = new WicCmykConverter(pix))
				using (var res = new WicScaler(cmy, true))
				using (var lll = new WicGammaExpand(res))
				using (var mmm = new WicHighQualityScaler(lll))
				using (var mat = new WicMatteTransform(mmm))
				using (var ggg = new WicGammaCompress(mat))
				using (var csc = new WicColorspaceConverter(ggg))
				using (var sss = new WicUnsharpMask(csc))
				using (var dit = new WicPaletizer(sss, 256))
				using (var enc = new WicEncoder(ostm.AsIStream(), ctx))
					enc.WriteSource(dit);
			}
		}
	}
}
