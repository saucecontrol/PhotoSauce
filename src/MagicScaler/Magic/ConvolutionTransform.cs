using System;
using System.Buffers;
using System.Collections.Generic;
using System.Collections.ObjectModel;
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
		protected static readonly ReadOnlyDictionary<Guid, IConvolver> ProcessorMap = new ReadOnlyDictionary<Guid, IConvolver>(new Dictionary<Guid, IConvolver> {
			[Consts.GUID_WICPixelFormat32bppPBGRA         ] = new Convolver4ChanByte(),
			[Consts.GUID_WICPixelFormat32bppBGRA          ] = new ConvolverBgraByte(),
			[Consts.GUID_WICPixelFormat24bppBGR           ] = new ConvolverBgrByte(),
			[Consts.GUID_WICPixelFormat16bppCbCr          ] = new Convolver2ChanByte(),
			[Consts.GUID_WICPixelFormat8bppGray           ] = new Convolver1ChanByte(),
			[Consts.GUID_WICPixelFormat8bppY              ] = new Convolver1ChanByte(),
			[PixelFormat.Pbgra64BppLinearUQ15.FormatGuid  ] = new Convolver4ChanUQ15(),
			[PixelFormat.Bgra64BppLinearUQ15.FormatGuid   ] = new ConvolverBgraUQ15(),
			[PixelFormat.Bgr48BppLinearUQ15.FormatGuid    ] = new ConvolverBgrUQ15(),
			[PixelFormat.Grey16BppLinearUQ15.FormatGuid   ] = new Convolver1ChanUQ15(),
			[PixelFormat.Y16BppLinearUQ15.FormatGuid      ] = new Convolver1ChanUQ15(),
			[PixelFormat.Pbgra128BppLinearFloat.FormatGuid] = new Convolver4ChanFloat(),
			[PixelFormat.Pbgra128BppFloat.FormatGuid      ] = new Convolver4ChanFloat(),
			[PixelFormat.Bgrx128BppLinearFloat.FormatGuid ] = new Convolver3XChanFloat(),
			[PixelFormat.Bgrx128BppFloat.FormatGuid       ] = new Convolver3XChanFloat(),
			[PixelFormat.Bgr96BppLinearFloat.FormatGuid   ] = new Convolver3ChanFloat(),
			[PixelFormat.Bgr96BppFloat.FormatGuid         ] = new Convolver3ChanFloat(),
			[PixelFormat.CbCr64BppFloat.FormatGuid        ] = new Convolver2ChanFloat(),
			[PixelFormat.Grey32BppLinearFloat.FormatGuid  ] = new Convolver1ChanFloat(),
			[Consts.GUID_WICPixelFormat32bppGrayFloat     ] = new Convolver1ChanFloat(),
			[PixelFormat.Y32BppLinearFloat.FormatGuid     ] = new Convolver1ChanFloat(),
			[PixelFormat.Y32BppFloat.FormatGuid           ] = new Convolver1ChanFloat()
		});

		protected bool BufferSource;
		protected int IntBpp;
		protected int IntStride;
		protected int IntStartLine;
		protected WICRect SourceRect;
		protected ArraySegment<byte> LineBuff, IntBuff;
		protected KernelMap<TWeight> XMap, YMap;
		protected IConvolver XProcessor, YProcessor;

		public ConvolutionTransform(PixelSource source, KernelMap<TWeight> mapx, KernelMap<TWeight> mapy, bool bufferSource = false) : base(source)
		{

			if (!ProcessorMap.TryGetValue(Format.FormatGuid, out XProcessor))
				throw new NotSupportedException("Unsupported pixel format");

			if (Format == PixelFormat.Bgr96BppLinearFloat)
				Format = PixelFormat.Bgrx128BppLinearFloat;
			else if (Format == PixelFormat.Bgr96BppFloat)
				Format = PixelFormat.Bgrx128BppFloat;

			YProcessor = ProcessorMap[Format.FormatGuid];
			XMap = mapx;
			YMap = mapy;

			BufferSource = bufferSource;
			SourceRect = new WICRect { Width = (int)Width, Height = 1 };
			IntStartLine = -mapy.Samples;

			IntBpp = Format.BitsPerPixel / 8 / Unsafe.SizeOf<TPixel>() * Unsafe.SizeOf<TWeight>();
			IntStride = mapy.Samples * IntBpp;

			int lineBuffLen = (bufferSource ? mapy.Samples : 1) * (int)BufferStride;
			int intBuffLen = mapx.OutPixels * IntStride;
			LineBuff = new ArraySegment<byte>(ArrayPool<byte>.Shared.Rent(lineBuffLen), 0, lineBuffLen);
			IntBuff = new ArraySegment<byte>(ArrayPool<byte>.Shared.Rent(intBuffLen), 0, intBuffLen);

			Width = (uint)mapx.OutPixels;
			Height = (uint)mapy.OutPixels;
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
			YProcessor.WriteDestLine(tstart, ostart, ox, ow, pmapy, smapy);
		}

		unsafe protected void LoadBuffer(byte* bstart, byte* tstart, byte* mapxstart, int iy)
		{
			int smapy = YMap.Samples;

			if (iy < IntStartLine)
				IntStartLine = iy - smapy;

			int tc = Math.Min(iy - IntStartLine, smapy);
			if (tc <= 0)
				return;

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

				SourceRect.Y = iy + ty;
				Timer.Stop();
				Source.CopyPixels(SourceRect, BufferStride, BufferStride, (IntPtr)bline);
				Timer.Start();

				byte* tline = tstart + ty * IntBpp;
				XProcessor.ConvolveSourceLine(bline, tline, IntBuff.Count, mapxstart, XMap.Samples, smapy);
			}
		}

		public virtual void Dispose()
		{
			ArrayPool<byte>.Shared.Return(LineBuff.Array ?? Array.Empty<byte>());
			ArrayPool<byte>.Shared.Return(IntBuff.Array ?? Array.Empty<byte>());
			LineBuff = IntBuff = default(ArraySegment<byte>);
		}

		public override string ToString() => XProcessor?.ToString() ?? base.ToString();
	}

	internal class UnsharpMaskTransform<TPixel, TWeight> : ConvolutionTransform<TPixel, TWeight> where TPixel : struct where TWeight : struct
	{
		private UnsharpMaskSettings sharpenSettings;
		private ArraySegment<byte> blurBuff;

		public UnsharpMaskTransform(PixelSource source, KernelMap<TWeight> mapx, KernelMap<TWeight> mapy, UnsharpMaskSettings ss) : base(source, mapx, mapy, true)
		{
			sharpenSettings = ss;
			blurBuff = new ArraySegment<byte>(ArrayPool<byte>.Shared.Rent((int)BufferStride), 0, (int)BufferStride);
		}

		unsafe protected override void ConvolveLine(byte* bstart, byte* tstart, byte* ostart, byte* pmapy, int smapy, int ox, int oy, int ow)
		{
			fixed (byte* blurstart = blurBuff.Array)
			{
				int cy = oy - IntStartLine;
				byte* bp = bstart + cy * BufferStride;

				YProcessor.WriteDestLine(tstart, blurstart, ox, ow, pmapy, smapy);
				YProcessor.SharpenLine(bp, blurstart, ostart, ox, ow, sharpenSettings.Amount, sharpenSettings.Threshold);
			}
		}

		public override void Dispose()
		{
			base.Dispose();

			ArrayPool<byte>.Shared.Return(blurBuff.Array ?? Array.Empty<byte>());
			blurBuff = default(ArraySegment<byte>);
		}

		public override string ToString() => $"{base.ToString()}: Sharpen";
	}
}
