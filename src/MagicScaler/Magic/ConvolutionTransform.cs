using System;
using System.Buffers;
using System.Diagnostics;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Runtime.CompilerServices;

using PhotoSauce.Interop.Wic;

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
			[Consts.GUID_WICPixelFormat8bppCb             ] = new Convolver1ChanByte(),
			[Consts.GUID_WICPixelFormat8bppCr             ] = new Convolver1ChanByte(),
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
			[PixelFormat.Y32BppFloat.FormatGuid           ] = new Convolver1ChanFloat(),
			[PixelFormat.Cb32BppFloat.FormatGuid          ] = new Convolver1ChanFloat(),
			[PixelFormat.Cr32BppFloat.FormatGuid          ] = new Convolver1ChanFloat()
		});

		protected readonly KernelMap<TWeight> XMap, YMap;
		protected readonly IConvolver XProcessor, YProcessor;
		protected readonly PixelBuffer IntBuff;
		protected readonly PixelBuffer? SrcBuff, WorkBuff;

		private readonly IMemoryOwner<byte>? lineBuff;
		private readonly bool bufferSource;
		private readonly int inWidth;

		public static ConvolutionTransform<TPixel, TWeight> CreateResize(PixelSource src, int width, int height, InterpolationSettings interpolatorx, InterpolationSettings interpolatory)
		{
			var fmt = src.Format;
			var mx = KernelMap<TWeight>.MakeScaleMap(src.Width, width, interpolatorx, fmt.ChannelCount, typeof(TPixel) == typeof(float));
			var my = KernelMap<TWeight>.MakeScaleMap(src.Height, height, interpolatory, fmt.ChannelCount == 3 ? 4 : fmt.ChannelCount, typeof(TPixel) == typeof(float));

			return new ConvolutionTransform<TPixel, TWeight>(src, mx, my);
		}

		public static ConvolutionTransform<TPixel, TWeight> CreateBlur(PixelSource src, double radius)
		{
			var fmt = src.Format;
			var mx = KernelMap<TWeight>.MakeBlurMap(src.Width, radius, fmt.ChannelCount, typeof(TPixel) == typeof(float));
			var my = KernelMap<TWeight>.MakeBlurMap(src.Height, radius, fmt.ChannelCount == 3 ? 4 : fmt.ChannelCount, typeof(TPixel) == typeof(float));

			return new ConvolutionTransform<TPixel, TWeight>(src, mx, my);
		}

		protected ConvolutionTransform(PixelSource source, KernelMap<TWeight> mapx, KernelMap<TWeight> mapy, bool lumaMode = false) : base(source)
		{
			var infmt = Format;
			var workfmt = infmt;
			if (lumaMode)
			{
				if (infmt.ColorRepresentation != PixelColorRepresentation.Grey && infmt.ColorRepresentation != PixelColorRepresentation.Bgr)
					throw new NotSupportedException("Unsupported pixel format: " + infmt.Name);

				workfmt = infmt.NumericRepresentation == PixelNumericRepresentation.Float ? PixelFormat.Grey32BppFloat :
				          infmt.NumericRepresentation == PixelNumericRepresentation.Fixed ? PixelFormat.Grey16BppUQ15 :
				          infmt.NumericRepresentation == PixelNumericRepresentation.UnsignedInteger ? PixelFormat.FromGuid(Consts.GUID_WICPixelFormat8bppGray) :
				          throw new NotSupportedException("Unsupported pixel format: " + infmt.Name);
			}

			if (!ProcessorMap.TryGetValue(workfmt.FormatGuid, out XProcessor))
				throw new NotSupportedException("Unsupported pixel format: " + workfmt.Name);

			if (workfmt == PixelFormat.Bgr96BppLinearFloat)
				Format = workfmt = PixelFormat.Bgrx128BppLinearFloat;
			else if (workfmt == PixelFormat.Bgr96BppFloat)
				Format = workfmt = PixelFormat.Bgrx128BppFloat;

			YProcessor = ProcessorMap[workfmt.FormatGuid];
			XMap = mapx;
			YMap = mapy;

			if (XMap.Channels != XProcessor.MapChannels || YMap.Channels != YProcessor.MapChannels)
				throw new NotSupportedException("Map and Processor channel counts don't match");

			inWidth = Width;
			Width = mapx.OutPixels;
			Height = mapy.OutPixels;

			int bpp = workfmt.BitsPerPixel / 8 / Unsafe.SizeOf<TPixel>() * Unsafe.SizeOf<TWeight>();
			IntBuff = new PixelBuffer(mapy.Samples, bpp, true, mapy.Samples * mapx.OutPixels * bpp);

			if (bufferSource = lumaMode)
			{
				SrcBuff = new PixelBuffer(mapy.Samples, BufferStride, true);

				if (workfmt.IsBinaryCompatibleWith(infmt))
					WorkBuff = SrcBuff;
				else
					WorkBuff = new PixelBuffer(mapy.Samples, MathUtil.PowerOfTwoCeiling(workfmt.BitsPerPixel / 8 * inWidth, IntPtr.Size), true);
			}
			else
			{
				lineBuff = MemoryPool<byte>.Shared.Rent(BufferStride);
			}
		}

		unsafe protected override void CopyPixelsInternal(in PixelArea prc, int cbStride, int cbBufferSize, IntPtr pbBuffer)
		{
			fixed (byte* mapystart = YMap.Map)
			{
				int oh = prc.Height, ow = prc.Width, ox = prc.X, oy = prc.Y;
				int smapy = YMap.Samples, chan = YMap.Channels;

				for (int y = 0; y < oh; y++)
				{
					int* pmapy = (int*)mapystart + ((oy + y) * (smapy * chan + 1));
					int iy = *pmapy++;

					if (!IntBuff.ContainsRange(iy, smapy))
						loadBuffer(iy, smapy);

					ConvolveLine((byte*)pbBuffer + y * cbStride, (byte*)pmapy, smapy, iy, oy + y, ox, ow);
				}
			}
		}

		unsafe private void loadBuffer(int first, int lines)
		{
			Debug.Assert((!bufferSource && lineBuff != null) || (WorkBuff != null && SrcBuff != null));

			fixed (byte* mapxstart = XMap.Map)
			{
				int fli = first, cli = lines;
				var ispan = IntBuff.PrepareLoad(ref fli, ref cli);

				int flb = first, clb = lines;
				var bspan = bufferSource ? SrcBuff!.PrepareLoad(ref flb, ref clb) : lineBuff!.Memory.Span;

				int flw = first, clw = lines;
				var wspan = bufferSource && WorkBuff != SrcBuff ? WorkBuff!.PrepareLoad(ref flw, ref clw) : bspan;

				fixed (byte* bline = bspan, wline = wspan, tline = ispan)
				{
					byte* bp = bline, wp = wline, tp = tline;
					for (int ly = 0; ly < cli; ly++)
					{
						Timer.Stop();
						Source.CopyPixels(new PixelArea(0, fli + ly, inWidth, 1), BufferStride, BufferStride, (IntPtr)bp);
						Timer.Start();

						if (bp != wp)
							GreyConverter.ConvertLine(Format.FormatGuid, bp, wp, SrcBuff!.Stride, WorkBuff!.Stride);

						XProcessor.ConvolveSourceLine(wp, tp, ispan.Length - ly * IntBuff.Stride, mapxstart, XMap.Samples, lines);

						tp += IntBuff.Stride;

						if (bufferSource)
						{
							wp += WorkBuff!.Stride;
							bp += SrcBuff!.Stride;
						}
					}
				}
			}
		}

		unsafe protected virtual void ConvolveLine(byte* ostart, byte* pmapy, int smapy, int iy, int oy, int ox, int ow)
		{
			fixed (byte* tstart = IntBuff.PrepareRead(iy, smapy))
				YProcessor.WriteDestLine(tstart, ostart, ox, ow, pmapy, smapy);
		}

		public virtual void Dispose()
		{
			XMap.Dispose();
			YMap.Dispose();
			IntBuff.Dispose();
			SrcBuff?.Dispose();
			WorkBuff?.Dispose();
			lineBuff?.Dispose();
		}

		public override string? ToString() => $"{XProcessor.ToString()}: {Format.Name}";
	}

	internal class UnsharpMaskTransform<TPixel, TWeight> : ConvolutionTransform<TPixel, TWeight> where TPixel : unmanaged where TWeight : unmanaged
	{
		private readonly UnsharpMaskSettings sharpenSettings;
		private readonly IConvolver processor;

		private readonly IMemoryOwner<byte> blurBuff;

		public static UnsharpMaskTransform<TPixel, TWeight> CreateSharpen(PixelSource src, UnsharpMaskSettings sharp)
		{
			var mx = KernelMap<TWeight>.MakeBlurMap(src.Width, sharp.Radius, 1, typeof(TPixel) == typeof(float));
			var my = KernelMap<TWeight>.MakeBlurMap(src.Height, sharp.Radius, 1, typeof(TPixel) == typeof(float));

			return new UnsharpMaskTransform<TPixel, TWeight>(src, mx, my, sharp);
		}

		private UnsharpMaskTransform(PixelSource source, KernelMap<TWeight> mapx, KernelMap<TWeight> mapy, UnsharpMaskSettings ss) : base(source, mapx, mapy, true)
		{
			Debug.Assert(SrcBuff != null && WorkBuff != null);

			sharpenSettings = ss;
			processor = ProcessorMap[Format.FormatGuid];
			blurBuff = MemoryPool<byte>.Shared.Rent(WorkBuff.Stride);
		}

		unsafe protected override void ConvolveLine(byte* ostart, byte* pmapy, int smapy, int iy, int oy, int ox, int ow)
		{
			var bspan = SrcBuff!.PrepareRead(oy, 1);
			var wspan = WorkBuff != SrcBuff ? WorkBuff!.PrepareRead(oy, 1) : bspan;
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

		public override string ToString() => $"{processor.ToString()}: Sharpen";
	}
}
