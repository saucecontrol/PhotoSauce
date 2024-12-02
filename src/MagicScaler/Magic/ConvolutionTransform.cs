// Copyright © Clinton Ingram and Contributors
// SPDX-License-Identifier: MIT

using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;

using PhotoSauce.MagicScaler.Converters;

namespace PhotoSauce.MagicScaler.Transforms;

internal interface IConvolver
{
	int Channels { get; }
	int MapChannels { get; }
	unsafe void ConvolveSourceLine(byte* istart, byte* tstart, nint cb, byte* mapxstart, int smapx, int smapy);
	unsafe void WriteDestLine(byte* tstart, byte* ostart, int ox, int ow, byte* pmapy, int smapy);
	unsafe void SharpenLine(byte* cstart, byte* ystart, byte* bstart, byte* ostart, int ox, int ow, float amt, float thresh, bool gamma);
}

internal interface IVectorConvolver
{
	IConvolver IntrinsicImpl { get; }
}

internal class ConvolutionTransform<TPixel, TWeight, TConv> : ChainedPixelSource where TPixel : unmanaged where TWeight : unmanaged where TConv : struct, ConvolutionType
{
	protected static readonly Dictionary<PixelFormat, IConvolver> ProcessorMap = new() {
		[PixelFormat.Cmyk32             ] = Convolver4ChanByte.Instance,
		[PixelFormat.Pbgra32            ] = Convolver4ChanByte.Instance,
		[PixelFormat.Bgrx32             ] = Convolver4ChanByte.Instance,
		[PixelFormat.Pbgra64UQ15Linear  ] = Convolver4ChanUQ15.Instance,
		[PixelFormat.Bgra32             ] = ConvolverBgraByte.Instance,
		[PixelFormat.Bgr24              ] = ConvolverBgrByte.Instance,
		[PixelFormat.Bgr48UQ15Linear    ] = ConvolverBgrUQ15.Instance,
		[PixelFormat.Grey8              ] = Convolver1ChanByte.Instance,
		[PixelFormat.Grey16UQ15         ] = Convolver1ChanUQ15.Instance,
		[PixelFormat.Grey16UQ15Linear   ] = Convolver1ChanUQ15.Instance,
		[PixelFormat.Y8                 ] = Convolver1ChanByte.Instance,
		[PixelFormat.Y8Video            ] = Convolver1ChanByte.Instance,
		[PixelFormat.Y16UQ15Linear      ] = Convolver1ChanUQ15.Instance,
		[PixelFormat.Cb8                ] = Convolver1ChanByte.Instance,
		[PixelFormat.Cb8Video           ] = Convolver1ChanByte.Instance,
		[PixelFormat.Cr8                ] = Convolver1ChanByte.Instance,
		[PixelFormat.Cr8Video           ] = Convolver1ChanByte.Instance,
		[PixelFormat.Pbgra128Float      ] = Convolver4ChanVector.Instance,
		[PixelFormat.Pbgra128FloatLinear] = Convolver4ChanVector.Instance,
		[PixelFormat.Bgrx128Float       ] = Convolver4ChanVector.Instance,
		[PixelFormat.Bgrx128FloatLinear ] = Convolver4ChanVector.Instance,
		[PixelFormat.Bgr96Float         ] = Convolver3ChanVector.Instance,
		[PixelFormat.Bgr96FloatLinear   ] = Convolver3ChanVector.Instance,
		[PixelFormat.Grey32Float        ] = Convolver1ChanVector.Instance,
		[PixelFormat.Grey32FloatLinear  ] = Convolver1ChanVector.Instance,
		[PixelFormat.Y32Float           ] = Convolver1ChanVector.Instance,
		[PixelFormat.Y32FloatLinear     ] = Convolver1ChanVector.Instance,
		[PixelFormat.Cb32Float          ] = Convolver1ChanVector.Instance,
		[PixelFormat.Cr32Float          ] = Convolver1ChanVector.Instance
	};

	protected readonly IConvolver XProcessor, YProcessor;
	protected readonly PixelBuffer<BufferType.Windowed> IntBuff;

	protected KernelMap<TWeight> XMap, YMap;
	protected TConv ConvBuff;

	public override PixelFormat Format { get; }
	public override int Width { get; }
	public override int Height { get; }

	protected ConvolutionTransform(PixelSource source, KernelMap<TWeight> mapx, KernelMap<TWeight> mapy) : base(source)
	{
		var infmt = source.Format;
		var workfmt = infmt;
		if (typeof(TConv) == typeof(ConvolutionType.Buffered))
		{
			if (infmt.ColorRepresentation != PixelColorRepresentation.Grey && infmt.ColorRepresentation != PixelColorRepresentation.Bgr)
				throw new NotSupportedException("Unsupported pixel format: " + infmt.Name);

			workfmt = infmt.NumericRepresentation == PixelNumericRepresentation.Float ? PixelFormat.Grey32Float :
			          infmt.NumericRepresentation == PixelNumericRepresentation.Fixed ? PixelFormat.Grey16UQ15 :
			          infmt.NumericRepresentation == PixelNumericRepresentation.UnsignedInteger ? PixelFormat.Grey8 :
			          throw new NotSupportedException("Unsupported pixel format: " + infmt.Name);
		}

		if (!ProcessorMap.TryGetValue(workfmt, out XProcessor!))
			throw new NotSupportedException("Unsupported pixel format: " + workfmt.Name);

		if (workfmt == PixelFormat.Bgr96FloatLinear)
			Format = workfmt = PixelFormat.Bgrx128FloatLinear;
		else if (workfmt == PixelFormat.Bgr96Float)
			Format = workfmt = PixelFormat.Bgrx128Float;
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
		IntBuff = new PixelBuffer<BufferType.Windowed>(mapy.Samples, bpp, new(mapy.Samples * mapx.Pixels * bpp));

		if (typeof(TConv) == typeof(ConvolutionType.Buffered))
		{
			var srcBuff = new PixelBuffer<BufferType.Sliding>(mapy.Samples, BufferStride);
			var wrkBuff = default(PixelBuffer<BufferType.Sliding>);
			var converter = default(IConversionProcessor);

			if (!workfmt.IsBinaryCompatibleWith(infmt))
			{
				wrkBuff = new PixelBuffer<BufferType.Sliding>(mapy.Samples, MathUtil.PowerOfTwoCeiling(source.Width * workfmt.BytesPerPixel, IntPtr.Size));
				converter = GreyConverter.GetProcessor(infmt);
			}

			ConvBuff = (TConv)(object)(new ConvolutionType.Buffered(srcBuff, wrkBuff, converter));
		}
		else
		{
			ConvBuff = (TConv)(object)(new ConvolutionType.Direct(BufferPool.RentAligned<byte>(BufferStride)));
		}
	}

	protected override unsafe void CopyPixelsInternal(in PixelArea prc, int cbStride, int cbBufferSize, byte* pbBuffer)
	{
		if (XMap is null)
			ThrowHelper.ThrowObjectDisposed(nameof(ConvolutionTransform<TPixel, TWeight, TConv>));

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

				ConvolveLine(pbBuffer + y * cbStride, (byte*)pmapy, smapy, iy, oy + y, ox, ow);
			}
		}
	}

	protected override void Reset()
	{
		IntBuff.Reset();

		if (typeof(TConv) == typeof(ConvolutionType.Buffered))
		{
			var cbuf = (ConvolutionType.Buffered)(object)ConvBuff;
			cbuf.SrcBuff.Reset();
			cbuf.WorkBuff?.Reset();
		}
	}

	private unsafe void loadBuffer(int first, int lines)
	{
		int fli = first, cli = lines;
		var ispan = IntBuff.PrepareLoad(ref fli, ref cli);

		Span<byte> bspan, wspan;
		int bstride;
		if (typeof(TConv) == typeof(ConvolutionType.Buffered))
		{
			var cbuf = (ConvolutionType.Buffered)(object)ConvBuff;
			bspan = cbuf.SrcBuff.PrepareLoad(first, lines);
			bstride = cbuf.SrcBuff.Stride;
			if (cbuf.WorkBuff is not null)
				wspan = cbuf.WorkBuff.PrepareLoad(first, lines);
			else
				wspan = bspan;
		}
		else
		{
			var cbuf = (ConvolutionType.Direct)(object)ConvBuff;
			bspan = cbuf.LineBuff.Span;
			bstride = cbuf.LineBuff.Length;
			wspan = bspan;
		}

		var area = PrevSource.Area.Slice(fli);
		fixed (byte* bline = bspan, wline = wspan, tline = ispan, mapxstart = XMap.Map)
		{
			byte* bp = bline, wp = wline, tp = tline;
			for (int ly = 0; ly < cli; ly++)
			{
				Profiler.PauseTiming();
				PrevSource.CopyPixels(area.Slice(ly, 1), bstride, bstride, bp);
				Profiler.ResumeTiming();

				if (typeof(TConv) == typeof(ConvolutionType.Buffered) && bp != wp)
				{
					var cbuf = (ConvolutionType.Buffered)(object)ConvBuff;
					cbuf.Converter!.ConvertLine(bp, wp, cbuf.SrcBuff.Stride);
				}

				XProcessor.ConvolveSourceLine(wp, tp, ispan.Length - ly * IntBuff.Stride, mapxstart, XMap.Samples, lines);

				tp += IntBuff.Stride;

				if (typeof(TConv) == typeof(ConvolutionType.Buffered))
				{
					var cbuf = (ConvolutionType.Buffered)(object)ConvBuff;
					wp += (cbuf.WorkBuff ?? cbuf.SrcBuff).Stride;
					bp += cbuf.SrcBuff.Stride;
				}
			}
		}
	}

	protected virtual unsafe void ConvolveLine(byte* ostart, byte* pmapy, int smapy, int iy, int oy, int ox, int ow)
	{
		fixed (byte* tstart = IntBuff.PrepareRead(iy, smapy))
			YProcessor.WriteDestLine(tstart, ostart, ox, ow, pmapy, smapy);
	}

	protected override void Dispose(bool disposing)
	{
		if (disposing)
		{
			if (XMap is null)
				return;

			XMap.Dispose();
			YMap.Dispose();
			XMap = null!;
			YMap = null!;

			IntBuff.Dispose();

			if (typeof(TConv) == typeof(ConvolutionType.Buffered))
			{
				var cbuf = (ConvolutionType.Buffered)(object)ConvBuff;
				cbuf.SrcBuff.Dispose();
				cbuf.WorkBuff?.Dispose();
			}
			else
			{
				var cbuf = (ConvolutionType.Direct)(object)ConvBuff;
				cbuf.LineBuff.Dispose();
				ConvBuff = default;
			}
		}

		base.Dispose(disposing);
	}

	public override string? ToString() => $"{XProcessor}: {Format.Name}";
}

internal sealed class DirectConvolutionTransform<TPixel, TWeight>(PixelSource source, KernelMap<TWeight> mapx, KernelMap<TWeight> mapy)
	: ConvolutionTransform<TPixel, TWeight, ConvolutionType.Direct>(source, mapx, mapy) where TPixel : unmanaged where TWeight : unmanaged { }

internal sealed class UnsharpMaskTransform<TPixel, TWeight> : ConvolutionTransform<TPixel, TWeight, ConvolutionType.Buffered> where TPixel : unmanaged where TWeight : unmanaged
{
	private readonly IConvolver processor;
	private readonly float amount, threshold;

	private RentedBuffer<byte> blurBuff;

	public UnsharpMaskTransform(PixelSource source, KernelMap<TWeight> mapx, KernelMap<TWeight> mapy, UnsharpMaskSettings ss) : base(source, mapx, mapy)
	{
		processor = ProcessorMap[Format];
		if (HWIntrinsics.IsSupported && processor is IVectorConvolver vc)
			processor = vc.IntrinsicImpl;

		amount = ss.Amount * 0.01f;
		threshold = (float)ss.Threshold / byte.MaxValue;

		blurBuff = BufferPool.RentAligned<byte>(ConvBuff.WorkBuff?.Stride ?? ConvBuff.SrcBuff.Stride);
	}

	protected override unsafe void ConvolveLine(byte* ostart, byte* pmapy, int smapy, int iy, int oy, int ox, int ow)
	{
		var bspan = ConvBuff.SrcBuff.PrepareRead(oy, 1);
		var wspan = ConvBuff.WorkBuff is not null ? ConvBuff.WorkBuff.PrepareRead(oy, 1) : bspan;
		var tspan = IntBuff.PrepareRead(iy, smapy);

		fixed (byte* bstart = bspan, wstart = wspan, tstart = tspan, blurstart = blurBuff)
		{
			YProcessor.WriteDestLine(tstart, blurstart, ox, ow, pmapy, smapy);
			processor.SharpenLine(bstart, wstart, blurstart, ostart, ox, ow, amount, threshold, Format.Encoding == PixelValueEncoding.Linear);
		}
	}

	protected override void Dispose(bool disposing)
	{
		if (disposing)
		{
			blurBuff.Dispose();
			blurBuff = default;
		}

		base.Dispose(disposing);
	}

	public override string ToString() => $"{processor}: Sharpen";
}

internal static class ConvolutionTransform
{
	private static PixelSource createResample<TPixel, TWeight>(PixelSource src, int width, int height, InterpolationSettings interpolatorx, InterpolationSettings interpolatory, double offsetX, double offsetY) where TPixel : unmanaged where TWeight : unmanaged
	{
		var fmt = src.Format;
		var mx = KernelMap<TWeight>.CreateResample(src.Width, width, interpolatorx, fmt.ChannelCount, offsetX);
		var my = KernelMap<TWeight>.CreateResample(src.Height, height, interpolatory, fmt.ChannelCount == 3 ? 4 : fmt.ChannelCount, offsetY);

		return new DirectConvolutionTransform<TPixel, TWeight>(src, mx, my);
	}

	private static PixelSource createBlur<TPixel, TWeight>(PixelSource src, double radius) where TPixel : unmanaged where TWeight : unmanaged
	{
		var fmt = src.Format;
		var mx = KernelMap<TWeight>.CreateBlur(src.Width, radius, fmt.ChannelCount);
		var my = KernelMap<TWeight>.CreateBlur(src.Height, radius, fmt.ChannelCount == 3 ? 4 : fmt.ChannelCount);

		return new DirectConvolutionTransform<TPixel, TWeight>(src, mx, my);
	}

	private static PixelSource createSharpen<TPixel, TWeight>(PixelSource src, UnsharpMaskSettings sharp) where TPixel : unmanaged where TWeight : unmanaged
	{
		var mx = KernelMap<TWeight>.CreateBlur(src.Width, sharp.Radius, 1);
		var my = KernelMap<TWeight>.CreateBlur(src.Height, sharp.Radius, 1);

		return new UnsharpMaskTransform<TPixel, TWeight>(src, mx, my, sharp);
	}

	public static PixelSource CreateResample(PixelSource src, int width, int height, InterpolationSettings interpolatorx, InterpolationSettings interpolatory, double offsetX = 0, double offsetY = 0) =>
		src.Format.NumericRepresentation switch {
			PixelNumericRepresentation.Float =>	createResample<float, float>(src, width, height, interpolatorx, interpolatory, offsetX, offsetY),
			PixelNumericRepresentation.Fixed =>	createResample<ushort, int>(src, width, height, interpolatorx, interpolatory, offsetX, offsetY),
			_                                => createResample<byte, int>(src, width, height, interpolatorx, interpolatory, offsetX, offsetY)
		};

	public static PixelSource CreateBlur(PixelSource src, double radius) =>
		src.Format.NumericRepresentation switch {
			PixelNumericRepresentation.Float => createBlur<float, float>(src, radius),
			PixelNumericRepresentation.Fixed => createBlur<ushort, int>(src, radius),
			_                                => createBlur<byte, int>(src, radius)
		};

	public static PixelSource CreateSharpen(PixelSource src, UnsharpMaskSettings ss) =>
		src.Format.NumericRepresentation switch {
			PixelNumericRepresentation.Float => createSharpen<float, float>(src, ss),
			PixelNumericRepresentation.Fixed => createSharpen<ushort, int>(src, ss),
			_                                => createSharpen<byte, int>(src, ss)
		};
}

internal interface ConvolutionType
{
	public readonly struct Direct(RentedBuffer<byte> buff) : ConvolutionType
	{
		public readonly RentedBuffer<byte> LineBuff = buff;
	}

	public readonly struct Buffered(PixelBuffer<BufferType.Sliding> src, PixelBuffer<BufferType.Sliding>? work, IConversionProcessor? conv) : ConvolutionType
	{
		public readonly PixelBuffer<BufferType.Sliding> SrcBuff = src;
		public readonly PixelBuffer<BufferType.Sliding>? WorkBuff = work;
		public readonly IConversionProcessor? Converter = conv;
	}
}
