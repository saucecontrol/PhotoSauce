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
		int Channels { get; }
		int MapChannels { get; }
		void ConvolveSourceLine(byte* istart, byte* tstart, int cb, byte* mapxstart, int smapx, int smapy);
		void WriteDestLine(byte* tstart, byte* ostart, int ox, int ow, byte* pmapy, int smapy);
		void SharpenLine(byte* cstart, byte* ystart, byte* bstart, byte* ostart, int ox, int ow, int amt, int thresh, bool gamma);
	}

	internal class ConvolutionTransform<TPixel, TWeight> : PixelSource, IDisposable where TPixel : unmanaged where TWeight : unmanaged
	{
		protected static readonly ReadOnlyDictionary<Guid, IConvolver> ProcessorMap = new ReadOnlyDictionary<Guid, IConvolver>(new Dictionary<Guid, IConvolver> {
			[Consts.GUID_WICPixelFormat32bppCMYK          ] = new Convolver4ChanByte(),
			[Consts.GUID_WICPixelFormat32bppPBGRA         ] = new Convolver4ChanByte(),
			[Consts.GUID_WICPixelFormat32bppBGRA          ] = new ConvolverBgraByte(),
			[Consts.GUID_WICPixelFormat24bppBGR           ] = new ConvolverBgrByte(),
			[Consts.GUID_WICPixelFormat16bppCbCr          ] = new Convolver2ChanByte(),
			[Consts.GUID_WICPixelFormat8bppGray           ] = new Convolver1ChanByte(),
			[Consts.GUID_WICPixelFormat8bppY              ] = new Convolver1ChanByte(),
			[PixelFormat.Pbgra64BppLinearUQ15.FormatGuid  ] = new Convolver4ChanUQ15(),
			[PixelFormat.Bgr48BppLinearUQ15.FormatGuid    ] = new ConvolverBgrUQ15(),
			[PixelFormat.Grey16BppLinearUQ15.FormatGuid   ] = new Convolver1ChanUQ15(),
			[PixelFormat.Grey16BppUQ15.FormatGuid         ] = new Convolver1ChanUQ15(),
			[PixelFormat.Y16BppLinearUQ15.FormatGuid      ] = new Convolver1ChanUQ15(),
			[PixelFormat.Pbgra128BppLinearFloat.FormatGuid] = new Convolver4ChanFloat(),
			[PixelFormat.Pbgra128BppFloat.FormatGuid      ] = new Convolver4ChanFloat(),
			[PixelFormat.Bgrx128BppLinearFloat.FormatGuid ] = new Convolver3XChanFloat(),
			[PixelFormat.Bgrx128BppFloat.FormatGuid       ] = new Convolver3XChanFloat(),
			[PixelFormat.Bgr96BppLinearFloat.FormatGuid   ] = new Convolver3ChanFloat(),
			[PixelFormat.Bgr96BppFloat.FormatGuid         ] = new Convolver3ChanFloat(),
			[PixelFormat.CbCr64BppFloat.FormatGuid        ] = new Convolver2ChanFloat(),
			[PixelFormat.Grey32BppLinearFloat.FormatGuid  ] = new Convolver1ChanFloat(),
			[PixelFormat.Grey32BppFloat.FormatGuid        ] = new Convolver1ChanFloat(),
			[PixelFormat.Y32BppLinearFloat.FormatGuid     ] = new Convolver1ChanFloat(),
			[PixelFormat.Y32BppFloat.FormatGuid           ] = new Convolver1ChanFloat()
		});

		protected readonly KernelMap<TWeight> XMap, YMap;
		protected readonly IConvolver XProcessor, YProcessor;

		protected bool BufferSource;
		protected int IntBpp;
		protected int IntStride, WorkStride;
		protected int IntStartLine;
		protected WICRect SourceRect;
		protected ArraySegment<byte> LineBuff, WorkBuff, IntBuff;

		public ConvolutionTransform(PixelSource source, KernelMap<TWeight> mapx, KernelMap<TWeight> mapy, bool lumaMode = false) : base(source)
		{
			var fmt = Format;
			if (lumaMode)
			{
				if (fmt.ColorRepresentation == PixelColorRepresentation.Bgr || fmt.ColorRepresentation == PixelColorRepresentation.Grey)
					fmt = fmt.NumericRepresentation == PixelNumericRepresentation.Float ? PixelFormat.Grey32BppFloat :
					      fmt.NumericRepresentation == PixelNumericRepresentation.Fixed ? PixelFormat.Grey16BppUQ15 :
					      fmt.NumericRepresentation == PixelNumericRepresentation.UnsignedInteger ? PixelFormat.Cache[Consts.GUID_WICPixelFormat8bppGray] :
					      throw new NotSupportedException("Unsupported pixel format");
				else
					throw new NotSupportedException("Unsupported pixel format");
			}

			if (!ProcessorMap.TryGetValue(fmt.FormatGuid, out XProcessor))
				throw new NotSupportedException("Unsupported pixel format");

			if (fmt == PixelFormat.Bgr96BppLinearFloat)
				Format = fmt = PixelFormat.Bgrx128BppLinearFloat;
			else if (fmt == PixelFormat.Bgr96BppFloat)
				Format = fmt = PixelFormat.Bgrx128BppFloat;

			YProcessor = ProcessorMap[fmt.FormatGuid];
			XMap = mapx;
			YMap = mapy;

			if (XMap.Channels != XProcessor.MapChannels || YMap.Channels != YProcessor.MapChannels)
				throw new NotSupportedException("Map and Processor channel counts don't match");

			BufferSource = lumaMode;
			SourceRect = new WICRect { Width = (int)Width, Height = 1 };
			IntStartLine = -mapy.Samples;

			IntBpp = fmt.BitsPerPixel / 8 / Unsafe.SizeOf<TPixel>() * Unsafe.SizeOf<TWeight>();
			IntStride = mapy.Samples * IntBpp;
			WorkStride = fmt.BitsPerPixel / 8 * (int)Width + (IntPtr.Size - 1) & ~(IntPtr.Size - 1);

			int lineBuffLen = (BufferSource ? mapy.Samples : 1) * (int)BufferStride;
			int intBuffLen = mapx.OutPixels * IntStride;
			int workBuffLen = mapy.Samples * WorkStride;
			LineBuff = new ArraySegment<byte>(ArrayPool<byte>.Shared.Rent(lineBuffLen), 0, lineBuffLen).Zero();
			IntBuff = new ArraySegment<byte>(ArrayPool<byte>.Shared.Rent(intBuffLen), 0, intBuffLen).Zero();
			WorkBuff = lumaMode && !Format.IsBinaryCompatibleWith(fmt) ? new ArraySegment<byte>(ArrayPool<byte>.Shared.Rent(workBuffLen), 0, workBuffLen).Zero() : LineBuff;

			Width = (uint)mapx.OutPixels;
			Height = (uint)mapy.OutPixels;
		}

		unsafe protected override void CopyPixelsInternal(WICRect prc, uint cbStride, uint cbBufferSize, IntPtr pbBuffer)
		{
			fixed (byte* bstart = &LineBuff.Array[0], wstart = &WorkBuff.Array[0], tstart = &IntBuff.Array[0])
			fixed (byte* mapxstart = &XMap.Map.Array[0], mapystart = &YMap.Map.Array[0])
			{
				int oh = prc.Height, ow = prc.Width, ox = prc.X, oy = prc.Y;
				int smapy = YMap.Samples, chan = YMap.Channels;

				for (int y = 0; y < oh; y++)
				{
					int* pmapy = (int*)mapystart + ((oy + y) * (smapy * chan + 1));
					int iy = *pmapy++;
					LoadBuffer(bstart, wstart, tstart, mapxstart, iy);

					byte* op = (byte*)pbBuffer + y * cbStride;
					ConvolveLine(bstart, wstart, tstart, op, (byte*)pmapy, smapy, ox, oy + y, ow);
				}
			}
		}

		unsafe protected virtual void ConvolveLine(byte* bstart, byte* wstart, byte* tstart, byte* ostart, byte* pmapy, int smapy, int ox, int oy, int ow) =>
			YProcessor.WriteDestLine(tstart, ostart, ox, ow, pmapy, smapy);

		unsafe protected void LoadBuffer(byte* bstart, byte* wstart, byte* tstart, byte* mapxstart, int iy)
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
				{
					Buffer.MemoryCopy(bstart + tc * BufferStride, bstart, LineBuff.Array.Length, tk * BufferStride);
					if (WorkBuff.Array != LineBuff.Array)
						Buffer.MemoryCopy(wstart + tc * WorkStride, wstart, WorkBuff.Array.Length, tk * WorkStride);
				}

				Buffer.MemoryCopy(tstart + tc * IntBpp, tstart, IntBuff.Array.Length, IntBuff.Count - tc * IntBpp);
			}

			for (int ty = tk; ty < smapy; ty++)
			{
				byte* bline = BufferSource ? bstart + ty * BufferStride : bstart;
				byte* wline = BufferSource ? wstart + ty * WorkStride : bstart;

				SourceRect.Y = iy + ty;
				Timer.Stop();
				Source.CopyPixels(SourceRect, BufferStride, BufferStride, (IntPtr)bline);
				Timer.Start();

				if (BufferSource)
				{
					if (Format == PixelFormat.Grey32BppLinearFloat || Format == PixelFormat.Y32BppLinearFloat)
						GreyConverter.ConvertGreyLinearToGreyFloat(bline, wline, (int)BufferStride);
					else if (Format == PixelFormat.Grey16BppLinearUQ15 || Format == PixelFormat.Y16BppLinearUQ15)
						GreyConverter.ConvertGreyLinearToGreyUQ15(bline, wline, (int)BufferStride);
					else if (Format.FormatGuid == Consts.GUID_WICPixelFormat24bppBGR)
						GreyConverter.ConvertBgrToGreyByte(bline, wline, (int)BufferStride);
					else if (Format == PixelFormat.Bgr48BppLinearUQ15)
						GreyConverter.ConvertBgrToGreyUQ15(bline, wline, (int)BufferStride);
					else if (Format.FormatGuid == Consts.GUID_WICPixelFormat32bppBGR || Format.FormatGuid == Consts.GUID_WICPixelFormat32bppBGRA || Format.FormatGuid == Consts.GUID_WICPixelFormat32bppPBGRA)
						GreyConverter.ConvertBgrxToGreyByte(bline, wline, (int)BufferStride);
					else if (Format == PixelFormat.Pbgra64BppLinearUQ15)
						GreyConverter.ConvertBgrxToGreyUQ15(bline, wline, (int)BufferStride);
					else if (Format == PixelFormat.Bgr96BppFloat)
						GreyConverter.ConvertBgrToGreyFloat(bline, wline, (int)BufferStride, false);
					else if (Format == PixelFormat.Bgrx128BppFloat || Format == PixelFormat.Pbgra128BppFloat)
						GreyConverter.ConvertBgrxToGreyFloat(bline, wline, (int)BufferStride, false);
					else if (Format == PixelFormat.Bgr96BppLinearFloat)
					{
						GreyConverter.ConvertBgrToGreyFloat(bline, wline, (int)BufferStride, true);
						GreyConverter.ConvertGreyLinearToGreyFloat(wline, wline, WorkStride);
					}
					else if (Format == PixelFormat.Bgrx128BppLinearFloat || Format == PixelFormat.Pbgra128BppLinearFloat)
					{
						GreyConverter.ConvertBgrxToGreyFloat(bline, wline, (int)BufferStride, true);
						GreyConverter.ConvertGreyLinearToGreyFloat(wline, wline, WorkStride);
					}
				}

				byte* tline = tstart + ty * IntBpp;
				XProcessor.ConvolveSourceLine(wline, tline, IntBuff.Count, mapxstart, XMap.Samples, smapy);
			}
		}

		public virtual void Dispose()
		{
			ArrayPool<byte>.Shared.Return(LineBuff.Array ?? Array.Empty<byte>());
			ArrayPool<byte>.Shared.Return(IntBuff.Array ?? Array.Empty<byte>());
			if (WorkBuff.Array != LineBuff.Array)
				ArrayPool<byte>.Shared.Return(WorkBuff.Array ?? Array.Empty<byte>());

			LineBuff = WorkBuff = IntBuff = default;
		}

		public override string ToString() => XProcessor?.ToString() ?? base.ToString();
	}

	internal class UnsharpMaskTransform<TPixel, TWeight> : ConvolutionTransform<TPixel, TWeight> where TPixel : unmanaged where TWeight : unmanaged
	{
		private UnsharpMaskSettings sharpenSettings;
		private ArraySegment<byte> blurBuff;
		private IConvolver processor;

		public UnsharpMaskTransform(PixelSource source, KernelMap<TWeight> mapx, KernelMap<TWeight> mapy, UnsharpMaskSettings ss) : base(source, mapx, mapy, true)
		{
			sharpenSettings = ss;
			processor = ProcessorMap[Format.FormatGuid];
			blurBuff = new ArraySegment<byte>(ArrayPool<byte>.Shared.Rent(WorkStride), 0, WorkStride);
		}

		unsafe protected override void ConvolveLine(byte* bstart, byte* wstart, byte* tstart, byte* ostart, byte* pmapy, int smapy, int ox, int oy, int ow)
		{
			fixed (byte* blurstart = &blurBuff.Array[0])
			{
				int cy = oy - IntStartLine;
				byte* bp = bstart + cy * BufferStride;
				byte* wp = wstart + cy * WorkStride;

				YProcessor.WriteDestLine(tstart, blurstart, ox, ow, pmapy, smapy);
				processor.SharpenLine(bp, wp, blurstart, ostart, ox, ow, sharpenSettings.Amount, sharpenSettings.Threshold, Format.Colorspace == PixelColorspace.LinearRgb);
			}
		}

		public override void Dispose()
		{
			base.Dispose();

			ArrayPool<byte>.Shared.Return(blurBuff.Array ?? Array.Empty<byte>());
			blurBuff = default;
		}

		public override string ToString() => $"{processor?.ToString() ?? base.ToString()}: Sharpen";
	}
}
