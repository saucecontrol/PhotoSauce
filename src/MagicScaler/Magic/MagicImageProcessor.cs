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

		private static void checkInStream(Stream imgStream)
		{
			if (imgStream == null) throw new ArgumentNullException(nameof(imgStream));
			if (!imgStream.CanSeek || !imgStream.CanRead) throw new ArgumentException("Input Stream must allow Seek and Read", nameof(imgStream));
			if (imgStream.Length <= 0 || imgStream.Position >= imgStream.Length) throw new ArgumentException("Input Stream is empty or positioned at its end", nameof(imgStream));
		}

		private static void checkOutStream(Stream outStream)
		{
			if (outStream == null) throw new ArgumentNullException(nameof(outStream));
			if (!outStream.CanSeek || !outStream.CanWrite) throw new ArgumentException("Output Stream must allow Seek and Write", nameof(outStream));
		}

		public static void ProcessImage(string imgPath, Stream outStream, ProcessImageSettings settings)
		{
			if (imgPath == null) throw new ArgumentNullException(nameof(imgPath));
			checkOutStream(outStream);

			using (var ctx = new WicProcessingContext(settings))
			using (var dec = new WicDecoder(imgPath, ctx))
				processImage(dec, ctx, outStream);
		}

		public static void ProcessImage(ArraySegment<byte> imgBuffer, Stream outStream, ProcessImageSettings settings)
		{
			if (imgBuffer == null) throw new ArgumentNullException(nameof(imgBuffer));
			checkOutStream(outStream);

			using (var ctx = new WicProcessingContext(settings))
			using (var dec = new WicDecoder(imgBuffer, ctx))
				processImage(dec, ctx, outStream);
		}

		public static void ProcessImage(Stream imgStream, Stream outStream, ProcessImageSettings settings)
		{
			checkInStream(imgStream);
			checkOutStream(outStream);

			using (var ctx = new WicProcessingContext(settings))
			using (var dec = new WicDecoder(imgStream, ctx))
				processImage(dec, ctx, outStream);
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
