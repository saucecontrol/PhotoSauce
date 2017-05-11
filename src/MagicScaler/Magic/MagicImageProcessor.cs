using System;
using System.IO;
using System.Numerics;

using PhotoSauce.MagicScaler.Interop;

namespace PhotoSauce.MagicScaler
{
	public static class MagicImageProcessor
	{
		public static bool EnablePlanarPipeline { get; set; } = true;
		public static bool EnableSimd { get; set; } = Vector.IsHardwareAccelerated;

		public static void ProcessImage(string imgPath, Stream ostm, ProcessImageSettings s)
		{
			if (imgPath == null) throw new ArgumentNullException(nameof(imgPath));
			if (ostm == null) throw new ArgumentNullException(nameof(ostm));
			if (!ostm.CanSeek || !ostm.CanWrite) throw new ArgumentException("Output Stream must allow Seek and Write", nameof(ostm));

			using (var ctx = new WicProcessingContext(s))
			using (var dec = new WicDecoder(imgPath, ctx))
				processImage(dec, ctx, ostm);
		}

		public static void ProcessImage(byte[] imgBuffer, Stream ostm, ProcessImageSettings s)
		{
			if (imgBuffer == null) throw new ArgumentNullException(nameof(imgBuffer));
			if (ostm == null) throw new ArgumentNullException(nameof(ostm));
			if (!ostm.CanSeek || !ostm.CanWrite) throw new ArgumentException("Output Stream must allow Seek and Write", nameof(ostm));

			using (var ctx = new WicProcessingContext(s))
			using (var dec = new WicDecoder(imgBuffer, ctx))
				processImage(dec, ctx, ostm);
		}

		public static void ProcessImage(Stream istm, Stream ostm, ProcessImageSettings s)
		{
			if (istm == null) throw new ArgumentNullException(nameof(istm));
			if (ostm == null) throw new ArgumentNullException(nameof(ostm));
			if (!istm.CanSeek || !istm.CanRead)  throw new ArgumentException("Input Stream must allow Seek and Read", nameof(istm));
			if (!ostm.CanSeek || !ostm.CanWrite) throw new ArgumentException("Output Stream must allow Seek and Write", nameof(ostm));
			if (istm.Length <= 0 || istm.Position >= istm.Length) throw new ArgumentException("Input Stream is empty or positioned at its end", nameof(istm));

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

				if (EnablePlanarPipeline && ctx.SupportsPlanar)
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
				using (var lll = new WicConvertToCustomPixelFormat(res))
				using (var mmm = new WicHighQualityScaler(lll))
				using (var mat = new WicMatteTransform(mmm))
				using (var ggg = new WicConvertFromCustomPixelFormat(mat))
				using (var csc = new WicColorspaceConverter(ggg))
				using (var sss = new WicUnsharpMask(csc))
				using (var dit = new WicPaletizer(sss))
				using (var enc = new WicEncoder(ostm.AsIStream(), ctx))
					enc.WriteSource(dit);
			}
		}
	}
}
