using System;
using System.Buffers;
using System.Diagnostics;
using System.Collections.Generic;
using System.Runtime.CompilerServices;

using PhotoSauce.Interop.Wic;

namespace PhotoSauce.MagicScaler.Transforms
{
	unsafe internal interface IConvolver
	{
		int Channels { get; }
		int MapChannels { get; }
		void ConvolveSourceLine(byte* istart, byte* tstart, int cb, byte* mapxstart, int smapx, int smapy);
		void WriteDestLine(byte* tstart, byte* ostart, int ox, int ow, byte* pmapy, int smapy);
		void SharpenLine(byte* cstart, byte* ystart, byte* bstart, byte* ostart, int ox, int ow, float amt, float thresh, bool gamma);
	}

	internal class ConvolutionTransform<TPixel, TWeight> : PixelSource, IDisposable where TPixel : unmanaged where TWeight : unmanaged
	{
		protected static readonly IReadOnlyDictionary<Guid, IConvolver> ProcessorMap = new Dictionary<Guid, IConvolver> {
			[Consts.GUID_WICPixelFormat32bppCMYK          ] = Convolver4ChanByte.Instance,
			[Consts.GUID_WICPixelFormat32bppPBGRA         ] = Convolver4ChanByte.Instance,
			[Consts.GUID_WICPixelFormat32bppBGRA          ] = ConvolverBgraByte.Instance,
			[Consts.GUID_WICPixelFormat24bppBGR           ] = ConvolverBgrByte.Instance,
			[Consts.GUID_WICPixelFormat8bppGray           ] = Convolver1ChanByte.Instance,
			[Consts.GUID_WICPixelFormat8bppY              ] = Convolver1ChanByte.Instance,
			[Consts.GUID_WICPixelFormat8bppCb             ] = Convolver1ChanByte.Instance,
			[Consts.GUID_WICPixelFormat8bppCr             ] = Convolver1ChanByte.Instance,
			[PixelFormat.Pbgra64BppLinearUQ15.FormatGuid  ] = Convolver4ChanUQ15.Instance,
			[PixelFormat.Bgr48BppLinearUQ15.FormatGuid    ] = ConvolverBgrUQ15.Instance,
			[PixelFormat.Grey16BppLinearUQ15.FormatGuid   ] = Convolver1ChanUQ15.Instance,
			[PixelFormat.Grey16BppUQ15.FormatGuid         ] = Convolver1ChanUQ15.Instance,
			[PixelFormat.Y16BppLinearUQ15.FormatGuid      ] = Convolver1ChanUQ15.Instance,
			[PixelFormat.Pbgra128BppLinearFloat.FormatGuid] = Convolver4ChanFloat.Instance,
			[PixelFormat.Pbgra128BppFloat.FormatGuid      ] = Convolver4ChanFloat.Instance,
			[PixelFormat.Bgrx128BppLinearFloat.FormatGuid ] = Convolver3XChanFloat.Instance,
			[PixelFormat.Bgrx128BppFloat.FormatGuid       ] = Convolver3XChanFloat.Instance,
			[PixelFormat.Bgr96BppLinearFloat.FormatGuid   ] = Convolver3ChanFloat.Instance,
			[PixelFormat.Bgr96BppFloat.FormatGuid         ] = Convolver3ChanFloat.Instance,
			[PixelFormat.Grey32BppLinearFloat.FormatGuid  ] = Convolver1ChanFloat.Instance,
			[PixelFormat.Grey32BppFloat.FormatGuid        ] = Convolver1ChanFloat.Instance,
			[PixelFormat.Y32BppLinearFloat.FormatGuid     ] = Convolver1ChanFloat.Instance,
			[PixelFormat.Y32BppFloat.FormatGuid           ] = Convolver1ChanFloat.Instance,
			[PixelFormat.Cb32BppFloat.FormatGuid          ] = Convolver1ChanFloat.Instance,
			[PixelFormat.Cr32BppFloat.FormatGuid          ] = Convolver1ChanFloat.Instance
		};

		protected readonly KernelMap<TWeight> XMap, YMap;
		protected readonly IConvolver XProcessor, YProcessor;
		protected readonly PixelBuffer IntBuff;
		protected readonly PixelBuffer? SrcBuff, WorkBuff;

		private readonly bool bufferSource;
		private readonly int inWidth;

		private byte[]? lineBuff;

		public static ConvolutionTransform<TPixel, TWeight> CreateResize(PixelSource src, int width, int height, InterpolationSettings interpolatorx, InterpolationSettings interpolatory, bool offsetX, bool offsetY)
		{
			var fmt = src.Format;
			var mx = KernelMap<TWeight>.CreateResample(src.Width, width, interpolatorx, fmt.ChannelCount, offsetX, typeof(TPixel) == typeof(float));
			var my = KernelMap<TWeight>.CreateResample(src.Height, height, interpolatory, fmt.ChannelCount == 3 ? 4 : fmt.ChannelCount, offsetY, typeof(TPixel) == typeof(float));

			return new ConvolutionTransform<TPixel, TWeight>(src, mx, my);
		}

		public static ConvolutionTransform<TPixel, TWeight> CreateBlur(PixelSource src, double radius)
		{
			var fmt = src.Format;
			var mx = KernelMap<TWeight>.CreateBlur(src.Width, radius, fmt.ChannelCount, typeof(TPixel) == typeof(float));
			var my = KernelMap<TWeight>.CreateBlur(src.Height, radius, fmt.ChannelCount == 3 ? 4 : fmt.ChannelCount, typeof(TPixel) == typeof(float));

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

			if (!ProcessorMap.TryGetValue(workfmt.FormatGuid, out XProcessor!))
				throw new NotSupportedException("Unsupported pixel format: " + workfmt.Name);

			if (workfmt == PixelFormat.Bgr96BppLinearFloat)
				Format = workfmt = PixelFormat.Bgrx128BppLinearFloat;
			else if (workfmt == PixelFormat.Bgr96BppFloat)
				Format = workfmt = PixelFormat.Bgrx128BppFloat;

			YProcessor = ProcessorMap[workfmt.FormatGuid];

			if (HWIntrinsics.IsSupported && (mapx.Samples & 3) == 0 && XProcessor is IVectorConvolver vcx)
				XProcessor = vcx.IntrinsicImpl;
			if (HWIntrinsics.IsSupported && (mapy.Samples & 3) == 0 && YProcessor is IVectorConvolver vcy)
				YProcessor = vcy.IntrinsicImpl;

			XMap = mapx;
			YMap = mapy;

			if (XMap.Channels != XProcessor.MapChannels || YMap.Channels != YProcessor.MapChannels)
				throw new NotSupportedException("Map and Processor channel counts don't match");

			inWidth = Width;
			Width = mapx.Pixels;
			Height = mapy.Pixels;

			int bpp = workfmt.BytesPerPixel / Unsafe.SizeOf<TPixel>() * Unsafe.SizeOf<TWeight>();
			IntBuff = new PixelBuffer(mapy.Samples, bpp, true, mapy.Samples * mapx.Pixels * bpp);

			if (bufferSource = lumaMode)
			{
				SrcBuff = new PixelBuffer(mapy.Samples, BufferStride, true);

				if (workfmt.IsBinaryCompatibleWith(infmt))
					WorkBuff = SrcBuff;
				else
					WorkBuff = new PixelBuffer(mapy.Samples, MathUtil.PowerOfTwoCeiling(inWidth * workfmt.BytesPerPixel, IntPtr.Size), true);
			}
			else
			{
				lineBuff = ArrayPool<byte>.Shared.Rent(BufferStride);
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
				var bspan = bufferSource ? SrcBuff!.PrepareLoad(ref flb, ref clb) : new Span<byte>(lineBuff, 0, BufferStride);

				int flw = first, clw = lines;
				var wspan = bufferSource && WorkBuff != SrcBuff ? WorkBuff!.PrepareLoad(ref flw, ref clw) : bspan;

				fixed (byte* bline = bspan, wline = wspan, tline = ispan)
				{
					byte* bp = bline, wp = wline, tp = tline;
					for (int ly = 0; ly < cli; ly++)
					{
						Profiler.PauseTiming();
						Source.CopyPixels(new PixelArea(0, fli + ly, inWidth, 1), BufferStride, BufferStride, (IntPtr)bp);
						Profiler.ResumeTiming();

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

			ArrayPool<byte>.Shared.Return(lineBuff ?? Array.Empty<byte>());
			lineBuff = null;
		}

		public override string? ToString() => $"{XProcessor.ToString()}: {Format.Name}";
	}

	internal class UnsharpMaskTransform<TPixel, TWeight> : ConvolutionTransform<TPixel, TWeight> where TPixel : unmanaged where TWeight : unmanaged
	{
		private readonly IConvolver processor;
		private readonly float amount, threshold;

		private byte[] blurBuff;

		public static UnsharpMaskTransform<TPixel, TWeight> CreateSharpen(PixelSource src, UnsharpMaskSettings sharp)
		{
			var mx = KernelMap<TWeight>.CreateBlur(src.Width, sharp.Radius, 1, typeof(TPixel) == typeof(float));
			var my = KernelMap<TWeight>.CreateBlur(src.Height, sharp.Radius, 1, typeof(TPixel) == typeof(float));

			return new UnsharpMaskTransform<TPixel, TWeight>(src, mx, my, sharp);
		}

		private UnsharpMaskTransform(PixelSource source, KernelMap<TWeight> mapx, KernelMap<TWeight> mapy, UnsharpMaskSettings ss) : base(source, mapx, mapy, true)
		{
			Debug.Assert(SrcBuff != null && WorkBuff != null);

			processor = ProcessorMap[Format.FormatGuid];
			if (HWIntrinsics.IsSupported && processor is IVectorConvolver vc)
				processor = vc.IntrinsicImpl;

			amount = ss.Amount * 0.01f;
			threshold = (float)ss.Threshold / byte.MaxValue;

			blurBuff = ArrayPool<byte>.Shared.Rent(WorkBuff.Stride);
		}

		unsafe protected override void ConvolveLine(byte* ostart, byte* pmapy, int smapy, int iy, int oy, int ox, int ow)
		{
			var bspan = SrcBuff!.PrepareRead(oy, 1);
			var wspan = WorkBuff != SrcBuff ? WorkBuff!.PrepareRead(oy, 1) : bspan;
			var tspan = IntBuff.PrepareRead(iy, smapy);

			fixed (byte* bstart = bspan, wstart = wspan, tstart = tspan, blurstart = &blurBuff[0])
			{
				YProcessor.WriteDestLine(tstart, blurstart, ox, ow, pmapy, smapy);
				processor.SharpenLine(bstart, wstart, blurstart, ostart, ox, ow, amount, threshold, Format.Encoding == PixelValueEncoding.Linear);
			}
		}

		public override void Dispose()
		{
			base.Dispose();

			ArrayPool<byte>.Shared.Return(blurBuff ?? Array.Empty<byte>());
			blurBuff = null!;
		}

		public override string ToString() => $"{processor.ToString()}: Sharpen";
	}
}
