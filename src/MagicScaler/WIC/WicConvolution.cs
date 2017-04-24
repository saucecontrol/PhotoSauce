using System;
using System.Buffers;
using System.Runtime.CompilerServices;

using PhotoSauce.MagicScaler.Interop;

namespace PhotoSauce.MagicScaler
{
	unsafe internal interface IConvolver
	{
		void ConvolveSourceLine(byte* istart, byte* tstart, int cb, byte* mapxstart, int smapx, int smapy);
		void WriteDestLine(byte* tstart, byte* ostart, int ox, int ow, byte* pmapy, int smapy);
		void SharpenLine(byte* cstart, byte* bstart, byte* ostart, int ox, int ow, int amt, int thresh);
	}

	internal class WicConvolution<TPixel, TWeight> : WicBitmapSourceBase where TPixel : struct where TWeight : struct
	{
		protected bool BufferSource;
		protected int IntBpp;
		protected int IntStride;
		protected int IntStartLine;
		protected uint OutWidth;
		protected uint OutHeight;
		protected ArraySegment<byte> LineBuff;
		protected ArraySegment<byte> IntBuff;
		protected KernelMap<TWeight> XMap;
		protected KernelMap<TWeight> YMap;
		protected WICRect SourceRect;
		protected IConvolver Processor;

		public WicConvolution(IWICBitmapSource source, KernelMap<TWeight> mapx, KernelMap<TWeight> mapy, bool bufferSource = false) : base(source)
		{
			if (Format == Consts.GUID_WICPixelFormat32bppPBGRA)
				Processor = new ConvolverBgra8bpc();
			else if (Format == Consts.GUID_WICPixelFormat32bppBGRA)
				Processor = new ConvolverBgra8bpc();
			else if (Format == Consts.GUID_WICPixelFormat24bppBGR)
				Processor = new ConvolverBgr8bpc();
			else if (Format == Consts.GUID_WICPixelFormat16bppCbCr)
				Processor = new ConvolverCbCr8bpc();
			else if (Format == Consts.GUID_WICPixelFormat8bppGray)
				Processor = new ConvolverGrey8bpc();
			else if (Format == Consts.GUID_WICPixelFormat8bppY)
				Processor = new ConvolverGrey8bpc();
			else if (Format == Consts.GUID_WICPixelFormat64bppBGRA)
				Processor = new ConvolverBgra16bpc();
			else if (Format == Consts.GUID_WICPixelFormat48bppBGR)
				Processor = new ConvolverBgr16bpc();
			else if (Format == Consts.GUID_WICPixelFormat16bppGray)
				Processor = new ConvolverGrey16bpc();
			else
				throw new NotSupportedException("Unsupported pixel format");

			BufferSource = bufferSource;
			OutWidth = (uint)mapx.OutPixels;
			OutHeight = (uint)mapy.OutPixels;
			SourceRect = new WICRect { Width = (int)Width, Height = 1 };
			XMap = mapx;
			YMap = mapy;

			IntBpp = (int)Bpp / Unsafe.SizeOf<TPixel>() * Unsafe.SizeOf<TWeight>();
			IntStride = mapy.Samples * IntBpp;
			IntStartLine = -mapy.Samples;

			int lineBuffLen = (bufferSource ? mapy.Samples : 1) * (int)Stride;
			int intBuffLen = mapx.OutPixels * IntStride;
			LineBuff = new ArraySegment<byte>(ArrayPool<byte>.Shared.Rent(lineBuffLen), 0, lineBuffLen);
			IntBuff = new ArraySegment<byte>(ArrayPool<byte>.Shared.Rent(intBuffLen), 0, intBuffLen);
		}

		public override void GetSize(out uint puiWidth, out uint puiHeight)
		{
			puiWidth = OutWidth;
			puiHeight = OutHeight;
		}

		unsafe public override void CopyPixels(WICRect prc, uint cbStride, uint cbBufferSize, IntPtr pbBuffer)
		{
			if (prc.X < 0 || prc.Y < 0 || prc.X + prc.Width > OutWidth || prc.Y + prc.Height > OutHeight)
				throw new ArgumentOutOfRangeException(nameof(prc), "Requested rectangle does not fall within the image bounds");

			fixed (byte* bstart = LineBuff.Array, tstart = IntBuff.Array)
			fixed (byte* mapxstart = XMap.Map.Array, mapystart = YMap.Map.Array)
			{
				int oh = prc.Height, ow = prc.Width, ox = prc.X, oy = prc.Y;
				int smapy = YMap.Samples, chan = YMap.Channels;

				for (int y = 0; y < oh; y++)
				{
					int* pmapy = (int*)mapystart + ((oy + y) * (smapy * chan + 1));
					int iy = *pmapy++;
					LoadBuffer(bstart, tstart, mapxstart, iy);

					byte* op = (byte*)pbBuffer + y * cbStride;
					ConvolveLine(bstart, tstart, op, (byte*)pmapy, smapy, ox, oy + y, ow);
				}
			}
		}

		unsafe protected virtual void ConvolveLine(byte* bstart, byte* tstart, byte* ostart, byte* pmapy, int smapy, int ox, int oy, int ow)
		{
			Processor.WriteDestLine(tstart, ostart, ox, ow, pmapy, smapy);
		}

		unsafe protected void LoadBuffer(byte* bstart, byte* tstart, byte* mapxstart, int iy)
		{
			int smapy = YMap.Samples;

			if (iy < IntStartLine)
				IntStartLine = iy - smapy;

			int tc = Math.Min(iy - IntStartLine, smapy);
			if (tc > 0)
			{
				IntStartLine = iy;

				int tk = smapy - tc;
				if (tk > 0)
				{
					if (BufferSource)
						Buffer.MemoryCopy(bstart + tc * Stride, bstart, LineBuff.Array.Length, tk * Stride);

					Buffer.MemoryCopy(tstart + tc * IntBpp, tstart, IntBuff.Array.Length, IntBuff.Count - tc * IntBpp);
				}

				for (int ty = tk; ty < smapy; ty++)
				{
					byte* bline = BufferSource ? bstart + ty * Stride : bstart;
					byte* tline = tstart + ty * IntBpp;

					SourceRect.Y = iy + ty;
					Source.CopyPixels(SourceRect, Stride, Stride, (IntPtr)bline);

					Processor.ConvolveSourceLine(bline, tline, IntBuff.Count, mapxstart, XMap.Samples, YMap.Samples);
				}
			}
		}

		public override void Dispose()
		{
			base.Dispose();

			ArrayPool<byte>.Shared.Return(LineBuff.Array ?? Array.Empty<byte>());
			ArrayPool<byte>.Shared.Return(IntBuff.Array ?? Array.Empty<byte>());
			LineBuff = IntBuff = default(ArraySegment<byte>);
		}
	}

	internal class WicUnsharpMask<TPixel, TWeight> : WicConvolution<TPixel, TWeight> where TPixel : struct where TWeight : struct
	{
		private UnsharpMaskSettings sharpenSettings;
		private byte[] blurBuff;

		public WicUnsharpMask(IWICBitmapSource source, KernelMap<TWeight> mapx, KernelMap<TWeight> mapy, UnsharpMaskSettings ss) : base(source, mapx, mapy, true)
		{
			sharpenSettings = ss;
			blurBuff = ArrayPool<byte>.Shared.Rent((int)Stride);
		}

		unsafe protected override void ConvolveLine(byte* bstart, byte* tstart, byte* ostart, byte* pmapy, int smapy, int ox, int oy, int ow)
		{
			fixed (byte* blurstart = blurBuff)
			{
				Processor.WriteDestLine(tstart, blurstart, ox, ow, pmapy, smapy);

				int by = (int)Height - 1 - oy;
				int cy = smapy / 2;
				if (cy > oy)
					cy = oy;
				else if (cy > by)
					cy += cy - by;

				byte* bp = bstart + cy * Stride;
				Processor.SharpenLine(bp, blurstart, ostart, ox, ow, sharpenSettings.Amount, sharpenSettings.Threshold);
			}
		}

		public override void Dispose()
		{
			base.Dispose();

			ArrayPool<byte>.Shared.Return(blurBuff ?? Array.Empty<byte>());
			blurBuff = null;
		}
	}
}
