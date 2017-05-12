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

	internal class ConvolutionTransform<TPixel, TWeight> : PixelSource, IDisposable where TPixel : struct where TWeight : struct
	{
		protected bool BufferSource;
		protected int IntBpp;
		protected int IntStride;
		protected int IntStartLine;
		protected ArraySegment<byte> LineBuff;
		protected ArraySegment<byte> IntBuff;
		protected KernelMap<TWeight> XMap;
		protected KernelMap<TWeight> YMap;
		protected WICRect SourceRect;
		protected IConvolver Processor;

		public ConvolutionTransform(PixelSource source, KernelMap<TWeight> mapx, KernelMap<TWeight> mapy, bool bufferSource = false) : base(source)
		{
			if (Format.FormatGuid == Consts.GUID_WICPixelFormat32bppPBGRA)
				Processor = new Convolver4ChanByte();
			else if (Format.FormatGuid == Consts.GUID_WICPixelFormat32bppBGRA)
				Processor = new ConvolverBgraByte();
			else if (Format.FormatGuid == Consts.GUID_WICPixelFormat24bppBGR)
				Processor = new ConvolverBgrByte();
			else if (Format.FormatGuid == Consts.GUID_WICPixelFormat16bppCbCr)
				Processor = new Convolver2ChanByte();
			else if (Format.FormatGuid == Consts.GUID_WICPixelFormat8bppGray || Format.FormatGuid == Consts.GUID_WICPixelFormat8bppY)
				Processor = new Convolver1ChanByte();
			else if (Format == PixelFormat.Pbgra64BppLinearUQ15)
				Processor = new Convolver4ChanUQ15();
			else if (Format == PixelFormat.Bgra64BppLinearUQ15)
				Processor = new ConvolverBgraUQ15();
			else if (Format == PixelFormat.Bgr48BppLinearUQ15)
				Processor = new ConvolverBgrUQ15();
			else if (Format == PixelFormat.Grey16BppLinearUQ15 || Format == PixelFormat.Y16BppLinearUQ15)
				Processor = new Convolver1ChanUQ15();
			else if (Format == PixelFormat.Pbgra128BppLinearFloat || Format == PixelFormat.Pbgra128BppFloat)
				Processor = new Convolver4ChanFloat();
			else if (Format == PixelFormat.Bgr96BppLinearFloat || Format == PixelFormat.Bgr96BppFloat)
				Processor = new Convolver3ChanFloat();
			else if (Format == PixelFormat.CbCr64BppFloat)
				Processor = new Convolver2ChanFloat();
			else if (Format == PixelFormat.Grey32BppLinearFloat || Format.FormatGuid == Consts.GUID_WICPixelFormat32bppGrayFloat || Format == PixelFormat.Y32BppLinearFloat || Format == PixelFormat.Y32BppFloat)
				Processor = new Convolver1ChanFloat();
			else
				throw new NotSupportedException("Unsupported pixel format");

			BufferSource = bufferSource;
			SourceRect = new WICRect { Width = (int)Width, Height = 1 };
			Width = (uint)mapx.OutPixels;
			Height = (uint)mapy.OutPixels;
			XMap = mapx;
			YMap = mapy;

			IntBpp = Format.BitsPerPixel / 8 / Unsafe.SizeOf<TPixel>() * Unsafe.SizeOf<TWeight>();
			IntStride = mapy.Samples * IntBpp;
			IntStartLine = -mapy.Samples;

			int lineBuffLen = (bufferSource ? mapy.Samples : 1) * (int)BufferStride;
			int intBuffLen = mapx.OutPixels * IntStride;
			LineBuff = new ArraySegment<byte>(ArrayPool<byte>.Shared.Rent(lineBuffLen), 0, lineBuffLen);
			IntBuff = new ArraySegment<byte>(ArrayPool<byte>.Shared.Rent(intBuffLen), 0, intBuffLen);
		}

		unsafe protected override void CopyPixelsInternal(WICRect prc, uint cbStride, uint cbBufferSize, IntPtr pbBuffer)
		{
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
						Buffer.MemoryCopy(bstart + tc * BufferStride, bstart, LineBuff.Array.Length, tk * BufferStride);

					Buffer.MemoryCopy(tstart + tc * IntBpp, tstart, IntBuff.Array.Length, IntBuff.Count - tc * IntBpp);
				}

				for (int ty = tk; ty < smapy; ty++)
				{
					byte* bline = BufferSource ? bstart + ty * BufferStride : bstart;
					byte* tline = tstart + ty * IntBpp;

					SourceRect.Y = iy + ty;
					Timer.Stop();
					Source.CopyPixels(SourceRect, BufferStride, BufferStride, (IntPtr)bline);
					Timer.Start();

					Processor.ConvolveSourceLine(bline, tline, IntBuff.Count, mapxstart, XMap.Samples, YMap.Samples);
				}
			}
		}

		public virtual void Dispose()
		{
			ArrayPool<byte>.Shared.Return(LineBuff.Array ?? Array.Empty<byte>());
			ArrayPool<byte>.Shared.Return(IntBuff.Array ?? Array.Empty<byte>());
			LineBuff = IntBuff = default(ArraySegment<byte>);
		}

		public override string ToString() => Processor?.ToString() ?? base.ToString();
	}

	internal class UnsharpMaskTransform<TPixel, TWeight> : ConvolutionTransform<TPixel, TWeight> where TPixel : struct where TWeight : struct
	{
		private UnsharpMaskSettings sharpenSettings;
		private byte[] blurBuff;

		public UnsharpMaskTransform(PixelSource source, KernelMap<TWeight> mapx, KernelMap<TWeight> mapy, UnsharpMaskSettings ss) : base(source, mapx, mapy, true)
		{
			sharpenSettings = ss;
			blurBuff = ArrayPool<byte>.Shared.Rent((int)BufferStride);
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

				byte* bp = bstart + cy * BufferStride;
				Processor.SharpenLine(bp, blurstart, ostart, ox, ow, sharpenSettings.Amount, sharpenSettings.Threshold);
			}
		}

		public override void Dispose()
		{
			base.Dispose();

			ArrayPool<byte>.Shared.Return(blurBuff ?? Array.Empty<byte>());
			blurBuff = null;
		}

		public override string ToString() => $"{base.ToString()}: Sharpen";
	}
}
