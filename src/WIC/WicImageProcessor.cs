using System.IO;

using PhotoSauce.MagicScaler.Interop;

namespace PhotoSauce.MagicScaler
{
	public static class WicImageProcessor
	{
		public static void ProcessImage(string imgPath, Stream ostm, ProcessImageSettings s)
		{
			using (var ctx = new WicProcessingContext(s))
			using (var dec = new WicDecoder(imgPath, ctx))
				processImage(dec, ctx, ostm);
		}

		public static void ProcessImage(byte[] imgBuffer, Stream ostm, ProcessImageSettings s)
		{
			using (var ctx = new WicProcessingContext(s))
			using (var dec = new WicDecoder(imgBuffer, ctx))
				processImage(dec, ctx, ostm);
		}

		public static void ProcessImage(Stream istm, Stream ostm, ProcessImageSettings s)
		{
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

				ctx.Settings.HybridMode = HybridScaleMode.Turbo;
				//ctx.NeedsCache = true;

				using (var qsc = new WicNativeScaler(met))
				using (var rot = new WicExifRotator(qsc))
				using (var cac = new WicConditionalCache(rot))
				using (var crp = new WicCropper(cac))
				using (var pix = new WicPixelFormatConverter(crp))
				using (var res = new WicScaler(pix))
				using (var csc = new WicColorspaceConverter(res))
				using (var mat = new WicMatteTransform(csc))
				using (var pal = new WicPaletizer(mat, 256))
				using (var enc = new WicEncoder(ostm.AsIStream(), ctx))
					enc.WriteSource(pal);
			}
		}
	}
}