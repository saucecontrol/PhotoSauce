// Copyright © Clinton Ingram and Contributors.  Licensed under the MIT License.

using System;
using System.Numerics;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.InteropServices;
using System.Runtime.CompilerServices;

using TerraFX.Interop.Windows;
using static TerraFX.Interop.Windows.GUID;

namespace PhotoSauce.MagicScaler;

internal sealed unsafe class WicPlanarCache : IYccImageFrame, IMetadataSource, ICroppedDecoder, IScaledDecoder
{
	private const int mcuLines = 16;

	public static readonly Guid[] PlanarPixelFormats = [ GUID_WICPixelFormat8bppY, GUID_WICPixelFormat8bppCb, GUID_WICPixelFormat8bppCr ];

	private enum WicPlane { Y, Cb, Cr }

	public readonly WicPlanarFrame Frame;

	private uint scaledWidth, scaledHeight;
	private int ratioX, ratioY;

	private PixelBuffer<BufferType.Caching>? buffY, buffCb, buffCr;
	private PixelArea scaledCrop, outCrop;

	public WicPlanarCache(WicPlanarFrame frm, ReadOnlySpan<WICBitmapPlaneDescription> desc)
	{
		Debug.Assert(desc.Length == 3 && desc[2].Width == desc[1].Width && desc[2].Height == desc[1].Height);

		scaledWidth = desc[0].Width;
		scaledHeight = desc[0].Height;
		Frame = frm;

		// IWICPlanarBitmapSourceTransform only supports subsampling ratios 1:1 or 2:1
		ratioX = (int)((scaledWidth + 1u & ~1u) / desc[1].Width);
		ratioY = (int)((scaledHeight + 1u & ~1u) / desc[1].Height);
		scaledCrop = PixelArea.FromSize((int)scaledWidth, (int)scaledHeight);
		outCrop = scaledCrop;

		PixelSource = new PlanarCachePixelSource(this, WicPlane.Y);
		PixelSourceCb = new PlanarCachePixelSource(this, WicPlane.Cb);
		PixelSourceCr = new PlanarCachePixelSource(this, WicPlane.Cr);
	}

	public ChromaPosition ChromaPosition => ChromaPosition.Center;
	public Matrix4x4 RgbYccMatrix => YccMatrix.Rec601;
	public bool IsFullRange => true;

	public IPixelSource PixelSource { get; }
	public IPixelSource PixelSourceCb { get; }
	public IPixelSource PixelSourceCr { get; }

	private void copyPixels(WicPlane plane, in PixelArea prc, int cbStride, int cbBufferSize, byte* pbBuffer)
	{
		Debug.Assert(cbStride >= prc.Width);
		Debug.Assert(cbBufferSize >= (prc.Height - 1) * cbStride + prc.Width);

		ensureBuffer();
		var buff = plane switch {
			WicPlane.Cb => buffCb,
			WicPlane.Cr => buffCr,
			_           => buffY
		};

		int offsX = plane == WicPlane.Y ? outCrop.X : 0;
		int offsY = plane == WicPlane.Y ? outCrop.Y : 0;

		for (int y = 0; y < prc.Height; y++)
		{
			int line = offsY + prc.Y + y;
			if (!buff.ContainsLine(line))
				loadBuffer(plane, line);

			var lspan = buff.PrepareRead(line, 1).Slice(offsX + prc.X, prc.Width);
			Unsafe.CopyBlockUnaligned(ref *(pbBuffer + y * cbStride), ref MemoryMarshal.GetReference(lspan), (uint)lspan.Length);
		}
	}

	[MemberNotNull(nameof(buffY), nameof(buffCb), nameof(buffCr))]
	private void ensureBuffer()
	{
		if (buffY is not null && buffCb is not null && buffCr is not null)
			return;

		uint chromaWidth = (uint)MathUtil.DivCeiling(scaledCrop.Width, ratioX);

		int strideY = MathUtil.PowerOfTwoCeiling((int)scaledWidth, IntPtr.Size);
		int strideC = MathUtil.PowerOfTwoCeiling((int)chromaWidth, IntPtr.Size);
		int buffLines = Math.Min(mcuLines, scaledCrop.Height);

		buffY = new PixelBuffer<BufferType.Caching>(buffLines, strideY);
		buffCb = new PixelBuffer<BufferType.Caching>(MathUtil.DivCeiling(buffLines, ratioY), strideC);
		buffCr = new PixelBuffer<BufferType.Caching>(MathUtil.DivCeiling(buffLines, ratioY), strideC);
	}

	private void loadBuffer(WicPlane plane, int line)
	{
		int prcY = MathUtil.PowerOfTwoFloor(plane == WicPlane.Y ? line : line * ratioY, ratioY);

		var sourceRect = new WICRect(
			scaledCrop.X,
			scaledCrop.Y + prcY,
			scaledCrop.Width,
			Math.Min(mcuLines, scaledCrop.Height - prcY)
		);

		int lineC = prcY / ratioY;
		int heightC = MathUtil.DivCeiling(sourceRect.Height, ratioY);

		var spanY = buffY!.PrepareLoad(prcY, sourceRect.Height);
		var spanCb = buffCb!.PrepareLoad(lineC, heightC);
		var spanCr = buffCr!.PrepareLoad(lineC, heightC);

		fixed (byte* pBuffY = spanY, pBuffCb = spanCb, pBuffCr = spanCr)
		{
			var formats = PlanarPixelFormats;
			var sourcePlanes = stackalloc[] {
				new WICBitmapPlane { Format = formats[0], pbBuffer = pBuffY, cbStride = (uint)buffY.Stride, cbBufferSize = (uint)spanY.Length },
				new WICBitmapPlane { Format = formats[1], pbBuffer = pBuffCb, cbStride = (uint)buffCb.Stride, cbBufferSize = (uint)spanCb.Length },
				new WICBitmapPlane { Format = formats[2], pbBuffer = pBuffCr, cbStride = (uint)buffCr.Stride, cbBufferSize = (uint)spanCr.Length }
			};

			HRESULT.Check(Frame.WicPlanarTransform->CopyPixels(&sourceRect, scaledWidth, scaledHeight, WICBitmapTransformOptions.WICBitmapTransformRotate0, WICPlanarOptions.WICPlanarOptionsDefault, sourcePlanes, (uint)formats.Length));
		}
	}

	public bool TryGetMetadata<T>([NotNullWhen(true)] out T? metadata) where T : IMetadata => Frame.TryGetMetadata(out metadata);

	public void SetDecodeCrop(PixelArea crop)
	{
		if (buffY is not null)
			throw new InvalidOperationException("Crop cannot be changed after decode has started.");

		var newcrop = scaledCrop.Intersect(crop);
		scaledCrop = newcrop.SnapTo(ratioX, ratioY, scaledCrop.Width, scaledCrop.Height);
		outCrop = newcrop.RelativeTo(scaledCrop);
	}

	public (int width, int height) SetDecodeScale(int ratio)
	{
		if (buffY is not null)
			throw new InvalidOperationException("Scale cannot be changed after decode has started.");

		uint ow = scaledWidth, oh = scaledHeight;
		uint cw = (uint)MathUtil.DivCeiling((int)ow, ratio), ch = (uint)MathUtil.DivCeiling((int)oh, ratio);

		var formats = PlanarPixelFormats;
		var desc = stackalloc WICBitmapPlaneDescription[formats.Length];
		fixed (Guid* pfmt = formats)
		{
			BOOL bval;
			HRESULT.Check(Frame.WicPlanarTransform->DoesSupportTransform(&cw, &ch, WICBitmapTransformOptions.WICBitmapTransformRotate0, WICPlanarOptions.WICPlanarOptionsDefault, pfmt, desc, (uint)formats.Length, &bval));
			if (!bval)
				throw new NotSupportedException("Requested planar transform not supported.");

			if (desc[0].Width != cw || desc[0].Height != ch)
				throw new NotSupportedException("Luma plane description does not match transform size.");
		}

		(scaledWidth, scaledHeight) = (cw, ch);

		ratioX = (int)((cw + 1u & ~1u) / desc[1].Width);
		ratioY = (int)((ch + 1u & ~1u) / desc[1].Height);

		var newcrop = scaledCrop.ProportionalScale((int)ow, (int)oh, (int)cw, (int)ch);
		scaledCrop = newcrop.SnapTo(ratioX, ratioY, (int)cw, (int)ch);
		outCrop = newcrop.RelativeTo(scaledCrop);

		return (outCrop.Width, outCrop.Height);
	}

	public override string ToString() => nameof(WicPlanarCache);

	public void Dispose()
	{
		Frame.Dispose();

		buffY?.Dispose();
		buffCb?.Dispose();
		buffCr?.Dispose();
	}

	private sealed class PlanarCachePixelSource : PixelSource, IFramePixelSource
	{
		private readonly WicPlanarCache cacheSource;
		private readonly WicPlane cachePlane;

		public override PixelFormat Format => cachePlane switch {
			WicPlane.Cb => PixelFormat.Cb8,
			WicPlane.Cr => PixelFormat.Cr8,
			_           => PixelFormat.Y8
		};

		public override int Width => cachePlane == WicPlane.Y ? cacheSource.outCrop.Width : MathUtil.DivCeiling(cacheSource.outCrop.Width, cacheSource.ratioX);
		public override int Height => cachePlane == WicPlane.Y ? cacheSource.outCrop.Height : MathUtil.DivCeiling(cacheSource.outCrop.Height, cacheSource.ratioY);
		public IImageFrame Frame => cacheSource;

		public PlanarCachePixelSource(WicPlanarCache cache, WicPlane plane) => (cacheSource, cachePlane) = (cache, plane);

		protected override void CopyPixelsInternal(in PixelArea prc, int cbStride, int cbBufferSize, byte* pbBuffer) =>
			cacheSource.copyPixels(cachePlane, prc, cbStride, cbBufferSize, pbBuffer);

		public override string ToString() => nameof(PlanarCachePixelSource);
	}
}
