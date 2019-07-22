using System;
using System.Buffers;
using System.Drawing;
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
		protected readonly PixelBuffer IntBuff, SrcBuff, WorkBuff;

		private readonly IMemoryOwner<byte> lineBuff;
		private readonly bool bufferSource;
		private readonly int inWidth;

		public ConvolutionTransform(PixelSource source, KernelMap<TWeight> mapx, KernelMap<TWeight> mapy, bool lumaMode = false) : base(source)
		{
			var infmt = Format;
			var workfmt = infmt;
			if (lumaMode)
			{
				if (infmt.ColorRepresentation != PixelColorRepresentation.Grey && infmt.ColorRepresentation != PixelColorRepresentation.Bgr)
					throw new NotSupportedException("Unsupported pixel format");

				workfmt = infmt.NumericRepresentation == PixelNumericRepresentation.Float ? PixelFormat.Grey32BppFloat :
				          infmt.NumericRepresentation == PixelNumericRepresentation.Fixed ? PixelFormat.Grey16BppUQ15 :
				          infmt.NumericRepresentation == PixelNumericRepresentation.UnsignedInteger ? PixelFormat.Cache[Consts.GUID_WICPixelFormat8bppGray] :
				          throw new NotSupportedException("Unsupported pixel format");
			}

			if (!ProcessorMap.TryGetValue(workfmt.FormatGuid, out XProcessor))
				throw new NotSupportedException("Unsupported pixel format");

			if (workfmt == PixelFormat.Bgr96BppLinearFloat)
				Format = workfmt = PixelFormat.Bgrx128BppLinearFloat;
			else if (workfmt == PixelFormat.Bgr96BppFloat)
				Format = workfmt = PixelFormat.Bgrx128BppFloat;

			YProcessor = ProcessorMap[workfmt.FormatGuid];
			XMap = mapx;
			YMap = mapy;

			if (XMap.Channels != XProcessor.MapChannels || YMap.Channels != YProcessor.MapChannels)
				throw new NotSupportedException("Map and Processor channel counts don't match");

			inWidth = (int)Width;
			Width = (uint)mapx.OutPixels;
			Height = (uint)mapy.OutPixels;

			int bpp = workfmt.BitsPerPixel / 8 / Unsafe.SizeOf<TPixel>() * Unsafe.SizeOf<TWeight>();
			IntBuff = new PixelBuffer(mapy.Samples, bpp, mapy.Samples * mapx.OutPixels * bpp);

			if (bufferSource = lumaMode)
			{
				SrcBuff = new PixelBuffer(mapy.Samples, (int)BufferStride);

				if (workfmt.IsBinaryCompatibleWith(infmt))
					WorkBuff = SrcBuff;
				else
					WorkBuff = new PixelBuffer(mapy.Samples, MathUtil.PowerOf2Ceiling(workfmt.BitsPerPixel / 8 * inWidth, IntPtr.Size));
			}
			else
			{
				lineBuff = MemoryPool<byte>.Shared.Rent((int)BufferStride);
			}
		}

		unsafe protected override void CopyPixelsInternal(in Rectangle prc, uint cbStride, uint cbBufferSize, IntPtr pbBuffer)
		{
			fixed (byte* mapxstart = &XMap.Map.Array[0], mapystart = &YMap.Map.Array[0])
			{
				int oh = prc.Height, ow = prc.Width, ox = prc.X, oy = prc.Y;
				int smapy = YMap.Samples, chan = YMap.Channels;

				for (int y = 0; y < oh; y++)
				{
					int* pmapy = (int*)mapystart + ((oy + y) * (smapy * chan + 1));
					int iy = *pmapy++;

					int first = iy;
					int lines = smapy;
					var ispan = IntBuff.PrepareLoad(ref first, ref lines, true);

					if (lines > 0)
					{
						for (int ly = 0; ly < lines; ly++)
						{
							int lf = first + ly;
							int ll = 1;
							var bspan = bufferSource ? SrcBuff.PrepareLoad(ref lf, ref ll, true) : lineBuff.Memory.Span;
							var wspan = bufferSource && WorkBuff != SrcBuff ? WorkBuff.PrepareLoad(ref lf, ref ll, true) : bspan;
							var tspan = ispan.Slice(ly * IntBuff.Stride);

							fixed (byte* bline = bspan, wline = wspan, tline = tspan)
							{
								Timer.Stop();
								Source.CopyPixels(new Rectangle(0, first + ly, inWidth, 1), BufferStride, BufferStride, (IntPtr)bline);
								Timer.Start();

								if (bline != wline)
									convertToWorking(bline, wline, SrcBuff.Stride, WorkBuff.Stride);

								XProcessor.ConvolveSourceLine(wline, tline, tspan.Length, mapxstart, XMap.Samples, smapy);
							}
						}
					}

					ConvolveLine((byte*)pbBuffer + y * cbStride, (byte*)pmapy, smapy, iy, oy, ox, ow);
				}
			}
		}

		unsafe protected virtual void ConvolveLine(byte* ostart, byte* pmapy, int smapy, int iy, int oy, int ox, int ow)
		{
			fixed (byte* tstart = IntBuff.PrepareRead(iy, smapy))
				YProcessor.WriteDestLine(tstart, ostart, ox, ow, pmapy, smapy);
		}

		unsafe private void convertToWorking(byte* bline, byte* wline, int bstride, int wstride)
		{
			if (Format == PixelFormat.Grey32BppLinearFloat || Format == PixelFormat.Y32BppLinearFloat)
				GreyConverter.ConvertGreyLinearToGreyFloat(bline, wline, bstride);
			else if (Format == PixelFormat.Grey16BppLinearUQ15 || Format == PixelFormat.Y16BppLinearUQ15)
				GreyConverter.ConvertGreyLinearToGreyUQ15(bline, wline, bstride);
			else if (Format.FormatGuid == Consts.GUID_WICPixelFormat24bppBGR)
				GreyConverter.ConvertBgrToGreyByte(bline, wline, bstride);
			else if (Format == PixelFormat.Bgr48BppLinearUQ15)
				GreyConverter.ConvertBgrToGreyUQ15(bline, wline, bstride);
			else if (Format.FormatGuid == Consts.GUID_WICPixelFormat32bppBGR || Format.FormatGuid == Consts.GUID_WICPixelFormat32bppBGRA || Format.FormatGuid == Consts.GUID_WICPixelFormat32bppPBGRA)
				GreyConverter.ConvertBgrxToGreyByte(bline, wline, bstride);
			else if (Format == PixelFormat.Pbgra64BppLinearUQ15)
				GreyConverter.ConvertBgrxToGreyUQ15(bline, wline, bstride);
			else if (Format == PixelFormat.Bgr96BppFloat)
				GreyConverter.ConvertBgrToGreyFloat(bline, wline, bstride, false);
			else if (Format == PixelFormat.Bgrx128BppFloat || Format == PixelFormat.Pbgra128BppFloat)
				GreyConverter.ConvertBgrxToGreyFloat(bline, wline, bstride, false);
			else if (Format == PixelFormat.Bgr96BppLinearFloat)
			{
				GreyConverter.ConvertBgrToGreyFloat(bline, wline, bstride, true);
				GreyConverter.ConvertGreyLinearToGreyFloat(wline, wline, wstride);
			}
			else if (Format == PixelFormat.Bgrx128BppLinearFloat || Format == PixelFormat.Pbgra128BppLinearFloat)
			{
				GreyConverter.ConvertBgrxToGreyFloat(bline, wline, bstride, true);
				GreyConverter.ConvertGreyLinearToGreyFloat(wline, wline, wstride);
			}
		}

		public virtual void Dispose()
		{
			IntBuff.Dispose();
			SrcBuff?.Dispose();
			WorkBuff?.Dispose();
			lineBuff?.Dispose();
		}

		public override string ToString() => XProcessor?.ToString() ?? base.ToString();
	}

	internal class UnsharpMaskTransform<TPixel, TWeight> : ConvolutionTransform<TPixel, TWeight> where TPixel : unmanaged where TWeight : unmanaged
	{
		private readonly UnsharpMaskSettings sharpenSettings;
		private readonly IConvolver processor;

		private readonly IMemoryOwner<byte> blurBuff;

		public UnsharpMaskTransform(PixelSource source, KernelMap<TWeight> mapx, KernelMap<TWeight> mapy, UnsharpMaskSettings ss) : base(source, mapx, mapy, true)
		{
			sharpenSettings = ss;
			processor = ProcessorMap[Format.FormatGuid];
			blurBuff = MemoryPool<byte>.Shared.Rent(WorkBuff.Stride);
		}

		unsafe protected override void ConvolveLine(byte* ostart, byte* pmapy, int smapy, int iy,int oy, int ox, int ow)
		{
			var bspan = SrcBuff.PrepareRead(oy, 1);
			var wspan = WorkBuff != SrcBuff ? WorkBuff.PrepareRead(oy, 1) : bspan;
			var tspan = IntBuff.PrepareRead(iy, smapy);

			fixed (byte* bstart = bspan, wstart = wspan, tstart = tspan, blurstart = blurBuff.Memory.Span)
			{
				YProcessor.WriteDestLine(tstart, blurstart, ox, ow, pmapy, smapy);
				processor.SharpenLine(bstart, wstart, blurstart, ostart, ox, ow, sharpenSettings.Amount, sharpenSettings.Threshold, Format.Colorspace == PixelColorspace.LinearRgb);
			}
		}

		public override void Dispose()
		{
			base.Dispose();
			blurBuff.Dispose();
		}

		public override string ToString() => $"{processor?.ToString() ?? base.ToString()}: Sharpen";
	}
}
