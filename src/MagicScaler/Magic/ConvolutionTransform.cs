// Copyright © Clinton Ingram and Contributors.  Licensed under the MIT License.

using System;
using System.Diagnostics;
using System.Collections.Generic;
using System.Runtime.CompilerServices;

namespace PhotoSauce.MagicScaler.Transforms
{
	unsafe internal interface IConvolver
	{
		int Channels { get; }
		int MapChannels { get; }
		void ConvolveSourceLine(byte* istart, byte* tstart, nint cb, byte* mapxstart, int smapx, int smapy);
		void WriteDestLine(byte* tstart, byte* ostart, int ox, int ow, byte* pmapy, int smapy);
		void SharpenLine(byte* cstart, byte* ystart, byte* bstart, byte* ostart, int ox, int ow, float amt, float thresh, bool gamma);
	}

	internal interface IVectorConvolver
	{
		IConvolver IntrinsicImpl { get; }
	}

	internal class ConvolutionTransform<TPixel, TWeight> : ChainedPixelSource, IDisposable where TPixel : unmanaged where TWeight : unmanaged
	{
		protected static readonly IReadOnlyDictionary<PixelFormat, IConvolver> ProcessorMap = new Dictionary<PixelFormat, IConvolver> {
			[PixelFormat.Cmyk32Bpp             ] = Convolver4ChanByte.Instance,
			[PixelFormat.Pbgra32Bpp            ] = Convolver4ChanByte.Instance,
			[PixelFormat.Bgra32Bpp             ] = ConvolverBgraByte.Instance,
			[PixelFormat.Bgr24Bpp              ] = ConvolverBgrByte.Instance,
			[PixelFormat.Grey8Bpp              ] = Convolver1ChanByte.Instance,
			[PixelFormat.Y8Bpp                 ] = Convolver1ChanByte.Instance,
			[PixelFormat.Cb8Bpp                ] = Convolver1ChanByte.Instance,
			[PixelFormat.Cr8Bpp                ] = Convolver1ChanByte.Instance,
			[PixelFormat.Pbgra64BppLinearUQ15  ] = Convolver4ChanUQ15.Instance,
			[PixelFormat.Bgr48BppLinearUQ15    ] = ConvolverBgrUQ15.Instance,
			[PixelFormat.Grey16BppLinearUQ15   ] = Convolver1ChanUQ15.Instance,
			[PixelFormat.Grey16BppUQ15         ] = Convolver1ChanUQ15.Instance,
			[PixelFormat.Y16BppLinearUQ15      ] = Convolver1ChanUQ15.Instance,
			[PixelFormat.Pbgra128BppLinearFloat] = Convolver4ChanVector.Instance,
			[PixelFormat.Pbgra128BppFloat      ] = Convolver4ChanVector.Instance,
			[PixelFormat.Bgrx128BppLinearFloat ] = Convolver4ChanVector.Instance,
			[PixelFormat.Bgrx128BppFloat       ] = Convolver4ChanVector.Instance,
			[PixelFormat.Bgr96BppLinearFloat   ] = Convolver3ChanVector.Instance,
			[PixelFormat.Bgr96BppFloat         ] = Convolver3ChanVector.Instance,
			[PixelFormat.Grey32BppLinearFloat  ] = Convolver1ChanVector.Instance,
			[PixelFormat.Grey32BppFloat        ] = Convolver1ChanVector.Instance,
			[PixelFormat.Y32BppLinearFloat     ] = Convolver1ChanVector.Instance,
			[PixelFormat.Y32BppFloat           ] = Convolver1ChanVector.Instance,
			[PixelFormat.Cb32BppFloat          ] = Convolver1ChanVector.Instance,
			[PixelFormat.Cr32BppFloat          ] = Convolver1ChanVector.Instance
		};

		protected readonly IConvolver XProcessor, YProcessor;
		protected readonly PixelBuffer IntBuff;
		protected readonly PixelBuffer? SrcBuff, WorkBuff;

		protected KernelMap<TWeight> XMap, YMap;

		private readonly bool bufferSource;

		private ArraySegment<byte> lineBuff;

		public override PixelFormat Format { get; }
		public override int Width { get; }
		public override int Height { get; }

		public static ConvolutionTransform<TPixel, TWeight> CreateResize(PixelSource src, int width, int height, InterpolationSettings interpolatorx, InterpolationSettings interpolatory, bool offsetX, bool offsetY)
		{
			var fmt = src.Format;
			var mx = KernelMap<TWeight>.CreateResample(src.Width, width, interpolatorx, fmt.ChannelCount, offsetX);
			var my = KernelMap<TWeight>.CreateResample(src.Height, height, interpolatory, fmt.ChannelCount == 3 ? 4 : fmt.ChannelCount, offsetY);

			return new ConvolutionTransform<TPixel, TWeight>(src, mx, my);
		}

		public static ConvolutionTransform<TPixel, TWeight> CreateBlur(PixelSource src, double radius)
		{
			var fmt = src.Format;
			var mx = KernelMap<TWeight>.CreateBlur(src.Width, radius, fmt.ChannelCount);
			var my = KernelMap<TWeight>.CreateBlur(src.Height, radius, fmt.ChannelCount == 3 ? 4 : fmt.ChannelCount);

			return new ConvolutionTransform<TPixel, TWeight>(src, mx, my);
		}

		protected ConvolutionTransform(PixelSource source, KernelMap<TWeight> mapx, KernelMap<TWeight> mapy, bool lumaMode = false) : base(source)
		{
			var infmt = source.Format;
			var workfmt = infmt;
			if (lumaMode)
			{
				if (infmt.ColorRepresentation != PixelColorRepresentation.Grey && infmt.ColorRepresentation != PixelColorRepresentation.Bgr)
					throw new NotSupportedException("Unsupported pixel format: " + infmt.Name);

				workfmt = infmt.NumericRepresentation == PixelNumericRepresentation.Float ? PixelFormat.Grey32BppFloat :
				          infmt.NumericRepresentation == PixelNumericRepresentation.Fixed ? PixelFormat.Grey16BppUQ15 :
				          infmt.NumericRepresentation == PixelNumericRepresentation.UnsignedInteger ? PixelFormat.Grey8Bpp :
				          throw new NotSupportedException("Unsupported pixel format: " + infmt.Name);
			}

			if (!ProcessorMap.TryGetValue(workfmt, out XProcessor!))
				throw new NotSupportedException("Unsupported pixel format: " + workfmt.Name);

			if (workfmt == PixelFormat.Bgr96BppLinearFloat)
				Format = workfmt = PixelFormat.Bgrx128BppLinearFloat;
			else if (workfmt == PixelFormat.Bgr96BppFloat)
				Format = workfmt = PixelFormat.Bgrx128BppFloat;
			else
				Format = infmt;

			YProcessor = ProcessorMap[workfmt];

			if (HWIntrinsics.IsSupported && (mapx.Samples * mapx.Channels & 3) == 0 && XProcessor is IVectorConvolver vcx)
				XProcessor = vcx.IntrinsicImpl;
			if (HWIntrinsics.IsSupported && (mapy.Samples * mapy.Channels & 3) == 0 && YProcessor is IVectorConvolver vcy)
				YProcessor = vcy.IntrinsicImpl;

			XMap = mapx;
			YMap = mapy;

			if (XMap.Channels != XProcessor.MapChannels || YMap.Channels != YProcessor.MapChannels)
				throw new NotSupportedException("Map and Processor channel counts don't match");

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
					WorkBuff = new PixelBuffer(mapy.Samples, MathUtil.PowerOfTwoCeiling(source.Width * workfmt.BytesPerPixel, IntPtr.Size), true);
			}
			else
			{
				lineBuff = BufferPool.Rent(BufferStride, true);
			}
		}

		unsafe protected override void CopyPixelsInternal(in PixelArea prc, int cbStride, int cbBufferSize, IntPtr pbBuffer)
		{
			if (XMap is null)
				throw new ObjectDisposedException(nameof(ConvolutionTransform<TPixel, TWeight>));

			fixed (byte* mapystart = YMap.Map)
			{
				int oh = prc.Height, ow = prc.Width, ox = prc.X, oy = prc.Y;
				int smapy = YMap.Samples, chan = YMap.Channels;

				for (int y = 0; y < oh; y++)
				{
					int* pmapy = (int*)mapystart + ((oy + y) * (typeof(TWeight) == typeof(float) ? 2 : (smapy * chan + 1)));
					int iy = *pmapy++;
					if (typeof(TWeight) == typeof(float))
						pmapy = (int*)(mapystart + *pmapy);

					if (!IntBuff.ContainsRange(iy, smapy))
						loadBuffer(iy, smapy);

					ConvolveLine((byte*)pbBuffer + y * cbStride, (byte*)pmapy, smapy, iy, oy + y, ox, ow);
				}
			}
		}

		protected override void Reset()
		{
			IntBuff.Reset();
			SrcBuff?.Reset();
			WorkBuff?.Reset();
		}

		unsafe private void loadBuffer(int first, int lines)
		{
			Debug.Assert((!bufferSource && lineBuff.Array is not null) || (WorkBuff is not null && SrcBuff is not null));

			fixed (byte* mapxstart = XMap.Map)
			{
				int fli = first, cli = lines;
				var ispan = IntBuff.PrepareLoad(ref fli, ref cli);

				int flb = first, clb = lines;
				var bspan = bufferSource ? SrcBuff!.PrepareLoad(ref flb, ref clb) : lineBuff.AsSpan();

				int flw = first, clw = lines;
				var wspan = bufferSource && WorkBuff != SrcBuff ? WorkBuff!.PrepareLoad(ref flw, ref clw) : bspan;

				fixed (byte* bline = bspan, wline = wspan, tline = ispan)
				{
					byte* bp = bline, wp = wline, tp = tline;
					for (int ly = 0; ly < cli; ly++)
					{
						Profiler.PauseTiming();
						PrevSource.CopyPixels(new PixelArea(0, fli + ly, PrevSource.Width, 1), bspan.Length, bspan.Length, (IntPtr)bp);
						Profiler.ResumeTiming();

						if (bp != wp)
							GreyConverter.ConvertLine(Format, bp, wp, SrcBuff!.Stride, WorkBuff!.Stride);

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
			if (XMap is null)
				return;

			XMap.Dispose();
			YMap.Dispose();
			XMap = null!;
			YMap = null!;

			IntBuff.Dispose();
			SrcBuff?.Dispose();
			WorkBuff?.Dispose();

			BufferPool.Return(lineBuff);
			lineBuff = default;
		}

		public override string? ToString() => $"{XProcessor}: {Format.Name}";
	}

	internal sealed class UnsharpMaskTransform<TPixel, TWeight> : ConvolutionTransform<TPixel, TWeight> where TPixel : unmanaged where TWeight : unmanaged
	{
		private readonly IConvolver processor;
		private readonly float amount, threshold;

		private ArraySegment<byte> blurBuff;

		public static UnsharpMaskTransform<TPixel, TWeight> CreateSharpen(PixelSource src, UnsharpMaskSettings sharp)
		{
			var mx = KernelMap<TWeight>.CreateBlur(src.Width, sharp.Radius, 1);
			var my = KernelMap<TWeight>.CreateBlur(src.Height, sharp.Radius, 1);

			return new UnsharpMaskTransform<TPixel, TWeight>(src, mx, my, sharp);
		}

		private UnsharpMaskTransform(PixelSource source, KernelMap<TWeight> mapx, KernelMap<TWeight> mapy, UnsharpMaskSettings ss) : base(source, mapx, mapy, true)
		{
			Debug.Assert(SrcBuff is not null && WorkBuff is not null);

			processor = ProcessorMap[Format];
			if (HWIntrinsics.IsSupported && processor is IVectorConvolver vc)
				processor = vc.IntrinsicImpl;

			amount = ss.Amount * 0.01f;
			threshold = (float)ss.Threshold / byte.MaxValue;

			blurBuff = BufferPool.Rent(WorkBuff.Stride, true);
		}

		unsafe protected override void ConvolveLine(byte* ostart, byte* pmapy, int smapy, int iy, int oy, int ox, int ow)
		{
			var bspan = SrcBuff!.PrepareRead(oy, 1);
			var wspan = WorkBuff != SrcBuff ? WorkBuff!.PrepareRead(oy, 1) : bspan;
			var tspan = IntBuff.PrepareRead(iy, smapy);

			fixed (byte* bstart = bspan, wstart = wspan, tstart = tspan, blurstart = blurBuff.AsSpan())
			{
				YProcessor.WriteDestLine(tstart, blurstart, ox, ow, pmapy, smapy);
				processor.SharpenLine(bstart, wstart, blurstart, ostart, ox, ow, amount, threshold, Format.Encoding == PixelValueEncoding.Linear);
			}
		}

		public override void Dispose()
		{
			base.Dispose();

			BufferPool.Return(blurBuff);
			blurBuff = default;
		}

		public override string ToString() => $"{processor}: Sharpen";
	}
}
