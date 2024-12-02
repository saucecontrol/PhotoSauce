// Copyright © Clinton Ingram and Contributors
// SPDX-License-Identifier: MIT

using System;
using System.Numerics;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.InteropServices;
using System.Runtime.CompilerServices;

using PhotoSauce.MagicScaler;
using PhotoSauce.Interop.Libjpeg;
using static PhotoSauce.Interop.Libjpeg.Libjpeg;

namespace PhotoSauce.NativeCodecs.Libjpeg;

internal sealed unsafe class JpegPlanarCache : IYccImageFrame, IMetadataSource, ICroppedDecoder, IScaledDecoder
{
	private const int mcu = DCTSIZE;

	private enum JpegPlane { Y, Cb, Cr }

	public readonly JpegFrame Frame;

	private int ratioX, ratioY;
	private PixelBuffer<BufferType.Caching>? buffY, buffCb, buffCr;

	public JpegPlanarCache(JpegFrame frm)
	{
		var handle = frm.Container.GetHandle();
		ratioX = handle->comp_info[0].h_samp_factor;
		ratioY = handle->comp_info[0].v_samp_factor;

		Frame = frm;
		PixelSource = new PlanarCachePixelSource(this, JpegPlane.Y);
		PixelSourceCb = new PlanarCachePixelSource(this, JpegPlane.Cb);
		PixelSourceCr = new PlanarCachePixelSource(this, JpegPlane.Cr);
	}

	public ChromaPosition ChromaPosition => ChromaPosition.Center;
	public Matrix4x4 RgbYccMatrix => YccMatrix.Rec601;
	public bool IsFullRange => true;

	public IPixelSource PixelSource { get; }
	public IPixelSource PixelSourceCb { get; }
	public IPixelSource PixelSourceCr { get; }

	private void copyPixels(JpegPlane plane, in PixelArea prc, int cbStride, int cbBufferSize, byte* pbBuffer)
	{
		Debug.Assert(cbStride >= prc.Width);
		Debug.Assert(cbBufferSize >= (prc.Height - 1) * cbStride + prc.Width);

		ensureBuffer();
		var buff = plane switch {
			JpegPlane.Cb => buffCb,
			JpegPlane.Cr => buffCr,
			_            => buffY
		};

		ref readonly var outCrop = ref Frame.OutCrop;
		int offsY = plane == JpegPlane.Y ? outCrop.Y : outCrop.Y / ratioY;

		for (int y = 0; y < prc.Height; y++)
		{
			int line = offsY + prc.Y + y;
			if (!buff.ContainsLine(line))
				loadBuffer(plane, line);

			int offsX = plane == JpegPlane.Y ? outCrop.X : outCrop.X / ratioX;
			var lspan = buff.PrepareRead(line, 1).Slice(offsX + prc.X, prc.Width);
			Unsafe.CopyBlockUnaligned(ref *(pbBuffer + y * cbStride), ref MemoryMarshal.GetReference(lspan), (uint)lspan.Length);
		}
	}

	[MemberNotNull(nameof(buffY), nameof(buffCb), nameof(buffCr))]
	private void ensureBuffer()
	{
		if (buffY is not null && buffCb is not null && buffCr is not null)
			return;

		ref readonly var scaledCrop = ref Frame.ScaledCrop;
		int chromaWidth = MathUtil.DivCeiling(scaledCrop.Width, ratioX);

		int strideY = MathUtil.PowerOfTwoCeiling(scaledCrop.Width + 32, HWIntrinsics.VectorCount<byte>());
		int strideC = MathUtil.PowerOfTwoCeiling(chromaWidth + 16, HWIntrinsics.VectorCount<byte>());
		int buffLines = Math.Min(mcu * 2, scaledCrop.Height);

		buffY = new PixelBuffer<BufferType.Caching>(buffLines, strideY);
		buffCb = new PixelBuffer<BufferType.Caching>(MathUtil.DivCeiling(buffLines, ratioY), strideC);
		buffCr = new PixelBuffer<BufferType.Caching>(MathUtil.DivCeiling(buffLines, ratioY), strideC);
	}

	private void loadBuffer(JpegPlane plane, int line)
	{
		const int slice = mcu * 2;

		var container = Frame.Container;
		var handle = container.GetHandle();
		int prcY = MathUtil.PowerOfTwoFloor(plane == JpegPlane.Y ? line : line * ratioY, ratioY);
		int minslice = handle->max_v_samp_factor * handle->min_DCT_scaled_size;

		if (prcY < (int)handle->output_scanline)
			Frame.ResetDecoder();

		if (handle->global_state == DSTATE_READY)
			Frame.StartDecoder();

		if (prcY != (int)handle->output_scanline)
		{
			skipTo(handle, prcY);
			prcY = (int)handle->output_scanline;
		}

		int lineC = prcY / ratioY;

		// If the required height is less than 1 MCU row, we may be at the bottom of the image.  In that case,
		// pad buffer by 1 line because libjpeg might fill our last row with garbage from the block remainder.
		ref readonly var outCrop = ref Frame.OutCrop;
		int heightY = Math.Min(slice, outCrop.Y + outCrop.Height - prcY + 1);
		int heightC = MathUtil.DivCeiling(heightY, ratioY);

		var spanY = buffY!.PrepareLoad(prcY, heightY);
		var spanCb = buffCb!.PrepareLoad(lineC, heightC);
		var spanCr = buffCr!.PrepareLoad(lineC, heightC);

		fixed (byte* py = spanY, pb = spanCb, pr = spanCr)
		{
			byte** pyr = stackalloc byte*[slice], pbr = stackalloc byte*[slice], prr = stackalloc byte*[slice];
			byte*** planes = stackalloc[] { pyr, pbr, prr };

			for (int i = 0; i < slice; i++)
			{
				pyr[i] = i < heightY ? py + (buffY.Stride * i) : pyr[i - 1];
				pbr[i] = i < heightC ? pb + (buffCb.Stride * i) : pbr[i - 1];
				prr[i] = i < heightC ? pr + (buffCr.Stride * i) : prr[i - 1];
			}

			for (int i = 0; i < slice / minslice; i++)
			{
				uint lines;
				container.CheckResult(JpegReadRawData(handle, planes, (uint)minslice, &lines));

				planes[0] += minslice;
				planes[1] += handle->comp_info[1].DCT_scaled_size;
				planes[2] += handle->comp_info[2].DCT_scaled_size;
			}
		}
	}

	// TODO libjpeg doesn't support skipping in planar output mode; we read and throw away
	private void skipTo(jpeg_decompress_struct* handle, int line)
	{
		int minslice = handle->max_v_samp_factor * handle->min_DCT_scaled_size;

		using var buff = BufferPool.RentLocal<byte>(buffY!.Stride);
		fixed (byte* pb = buff)
		{
			byte** pyr = stackalloc byte*[minslice], pbr = stackalloc byte*[minslice], prr = stackalloc byte*[minslice];
			byte*** planes = stackalloc[] { pyr, pbr, prr };

			for (int i = 0; i < minslice; i++)
			{
				pyr[i] = pb;
				pbr[i] = pb;
				prr[i] = pb;
			}

			while (line >= (int)handle->output_scanline + minslice)
			{
				uint lines;
				Frame.Container.CheckResult(JpegReadRawData(handle, planes, (uint)minslice, &lines));
			}
		}
	}

	public bool TryGetMetadata<T>([NotNullWhen(true)] out T? metadata) where T : IMetadata => Frame.TryGetMetadata(out metadata);

	public void SetDecodeCrop(PixelArea crop) => Frame.SetDecodeCrop(crop);

	public (int width, int height) SetDecodeScale(int ratio)
	{
		var (cw, ch) = Frame.SetDecodeScale(ratio);

		var handle = Frame.Container.GetHandle();
		ratioX = (int)((handle->comp_info[0].downsampled_width + 1 & ~1) / handle->comp_info[1].downsampled_width);
		ratioY = (int)((handle->comp_info[0].downsampled_height + 1 & ~1) / handle->comp_info[1].downsampled_height);

		return (cw, ch);
	}

	public override string ToString() => nameof(JpegPlanarCache);

	public void Dispose()
	{
		Frame.Dispose();

		buffY?.Dispose();
		buffCb?.Dispose();
		buffCr?.Dispose();
	}

	private sealed class PlanarCachePixelSource : PixelSource, IFramePixelSource
	{
		private readonly JpegPlanarCache cacheSource;
		private readonly JpegPlane cachePlane;

		public override PixelFormat Format => cachePlane switch {
			JpegPlane.Cb => PixelFormat.Cb8,
			JpegPlane.Cr => PixelFormat.Cr8,
			_            => PixelFormat.Y8
		};

		public override int Width => cachePlane == JpegPlane.Y ? cacheSource.Frame.OutCrop.Width : MathUtil.DivCeiling(cacheSource.Frame.OutCrop.Width, cacheSource.ratioX);
		public override int Height => cachePlane == JpegPlane.Y ? cacheSource.Frame.OutCrop.Height : MathUtil.DivCeiling(cacheSource.Frame.OutCrop.Height, cacheSource.ratioY);
		public IImageFrame Frame => cacheSource;

		public PlanarCachePixelSource(JpegPlanarCache cache, JpegPlane plane) => (cacheSource, cachePlane) = (cache, plane);

		protected override void CopyPixelsInternal(in PixelArea prc, int cbStride, int cbBufferSize, byte* pbBuffer) =>
			cacheSource.copyPixels(cachePlane, prc, cbStride, cbBufferSize, pbBuffer);

		public override string ToString() => nameof(PlanarCachePixelSource);
	}
}
