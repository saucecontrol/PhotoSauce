using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Runtime.CompilerServices;

using PhotoSauce.Interop.Wic;

namespace PhotoSauce.MagicScaler
{
	internal class WicPlanarCache : IDisposable
	{
		private enum WicPlane { Y, Cb, Cr }

		private readonly uint scaledWidth, scaledHeight;
		private readonly int subsampleRatioX, subsampleRatioY;
		private readonly int strideY, strideC;
		private readonly int buffHeight;

		private readonly IWICPlanarBitmapSourceTransform sourceTransform;
		private readonly WICBitmapTransformOptions sourceTransformOptions;
		private readonly PlanarCachePixelSource sourceY, sourceCb, sourceCr;
		private readonly PixelBuffer buffY, buffCb, buffCr;
		private readonly WICRect scaledCrop;
		private readonly WICBitmapPlane[] sourcePlanes;

		public WicPlanarCache(IWICPlanarBitmapSourceTransform source, ReadOnlySpan<WICBitmapPlaneDescription> desc, WICBitmapTransformOptions transformOptions, uint width, uint height, in PixelArea crop)
		{
			var descY = desc[0];
			var descCb = desc[1];
			var descCr = desc[2];

			// IWICPlanarBitmapSourceTransform only supports 4:2:0, 4:4:0, 4:2:2, and 4:4:4 subsampling, so ratios will always be 1 or 2
			subsampleRatioX = (int)((descY.Width + 1u) / descCb.Width);
			subsampleRatioY = (int)((descY.Height + 1u) / descCb.Height);

			var scrop = new WICRect {
				X = MathUtil.PowerOfTwoFloor(crop.X, subsampleRatioX),
				Y = MathUtil.PowerOfTwoFloor(crop.Y, subsampleRatioY),
			};
			scrop.Width = Math.Min(MathUtil.PowerOfTwoCeiling(crop.Width, subsampleRatioX), (int)descY.Width - scrop.X);
			scrop.Height = Math.Min(MathUtil.PowerOfTwoCeiling(crop.Height, subsampleRatioY), (int)descY.Height - scrop.Y);

			descCb.Width = Math.Min((uint)MathUtil.DivCeiling(scrop.Width, subsampleRatioX), descCb.Width);
			descCb.Height = Math.Min((uint)MathUtil.DivCeiling(scrop.Height, subsampleRatioY), descCb.Height);

			descY.Width = Math.Min(descCb.Width * (uint)subsampleRatioX, (uint)scrop.Width);
			descY.Height = Math.Min(descCb.Height * (uint)subsampleRatioY, (uint)scrop.Height);

			descCr.Width = descCb.Width;
			descCr.Height = descCb.Height;

			sourceTransform = source;
			sourceTransformOptions = transformOptions;
			scaledCrop = scrop;
			scaledWidth = width;
			scaledHeight = height;

			strideY = MathUtil.PowerOfTwoCeiling((int)descY.Width, IntPtr.Size);
			strideC = MathUtil.PowerOfTwoCeiling((int)descCb.Width, IntPtr.Size);

			buffHeight = Math.Min(scrop.Height, transformOptions.RequiresCache() ? (int)descY.Height : 16);
			buffY = new PixelBuffer(buffHeight, strideY);
			buffCb = new PixelBuffer(MathUtil.DivCeiling(buffHeight, subsampleRatioY), strideC);
			buffCr = new PixelBuffer(MathUtil.DivCeiling(buffHeight, subsampleRatioY), strideC);

			sourceY = new PlanarCachePixelSource(this, WicPlane.Y, descY);
			sourceCb = new PlanarCachePixelSource(this, WicPlane.Cb, descCb);
			sourceCr = new PlanarCachePixelSource(this, WicPlane.Cr, descCr);

			sourcePlanes = new[] {
				new WICBitmapPlane { Format = descY.Format, cbStride = (uint)strideY },
				new WICBitmapPlane { Format = descCb.Format, cbStride = (uint)strideC },
				new WICBitmapPlane { Format = descCr.Format, cbStride = (uint)strideC }
			};
		}

		public PixelSource SourceY => sourceY;
		public PixelSource SourceCb => sourceCb;
		public PixelSource SourceCr => sourceCr;

		unsafe private void copyPixels(WicPlane plane, in PixelArea prc, int cbStride, int cbBufferSize, IntPtr pbBuffer)
		{
			Debug.Assert(cbStride >= prc.Width);
			Debug.Assert(cbBufferSize >= (prc.Height - 1) * cbStride + prc.Width);

			var buff = plane switch {
				WicPlane.Cb => buffCb,
				WicPlane.Cr => buffCr,
				_ => buffY
			};

			for (int y = 0; y < prc.Height; y++)
			{
				int line = prc.Y + y;
				if (!buff.ContainsLine(line))
					loadBuffer(plane, line);

				var lspan = buff.PrepareRead(line, 1).Slice(prc.X, prc.Width);
				Unsafe.CopyBlockUnaligned(ref Unsafe.AsRef<byte>((byte*)pbBuffer + y * cbStride), ref MemoryMarshal.GetReference(lspan), (uint)lspan.Length);
			}
		}

		unsafe private void loadBuffer(WicPlane plane, int line)
		{
			int prcY = MathUtil.PowerOfTwoFloor(plane == WicPlane.Y ? line : line * subsampleRatioY, subsampleRatioY);

			var sourceRect = new WICRect {
				X = scaledCrop.X,
				Y = scaledCrop.Y + prcY,
				Width = scaledCrop.Width,
				Height = Math.Min(buffHeight, scaledCrop.Height - prcY)
			};

			int lineY = prcY;
			int lineCb = lineY / subsampleRatioY, lineCr = lineCb;
			int heightY = sourceRect.Height;
			int heightCb = MathUtil.DivCeiling(heightY, subsampleRatioY), heightCr = heightCb;

			var spanY = buffY.PrepareLoad(ref lineY, ref heightY);
			var spanCb = buffCb.PrepareLoad(ref lineCb, ref heightCb);
			var spanCr = buffCr.PrepareLoad(ref lineCr, ref heightCr);

			fixed (byte* pBuffY = spanY, pBuffCb = spanCb, pBuffCr = spanCr)
			{
				sourcePlanes[0].pbBuffer = (IntPtr)pBuffY;
				sourcePlanes[1].pbBuffer = (IntPtr)pBuffCb;
				sourcePlanes[2].pbBuffer = (IntPtr)pBuffCr;
				sourcePlanes[0].cbBufferSize = (uint)spanY.Length;
				sourcePlanes[1].cbBufferSize = (uint)spanCb.Length;
				sourcePlanes[2].cbBufferSize = (uint)spanCr.Length;

				sourceTransform.CopyPixels(sourceRect, scaledWidth, scaledHeight, sourceTransformOptions, WICPlanarOptions.WICPlanarOptionsDefault, sourcePlanes, (uint)sourcePlanes.Length);
			}
		}

		public void Dispose()
		{
			buffY.Dispose();
			buffCb.Dispose();
			buffCr.Dispose();
		}

		private class PlanarCachePixelSource : PixelSource
		{
			private readonly WicPlanarCache cacheSource;
			private readonly WicPlane cachePlane;

			public PlanarCachePixelSource(WicPlanarCache cache, WicPlane plane, WICBitmapPlaneDescription planeDesc)
			{
				Width = (int)planeDesc.Width;
				Height = (int)planeDesc.Height;
				Format = PixelFormat.FromGuid(planeDesc.Format);

				cacheSource = cache;
				cachePlane = plane;
			}

			protected override void CopyPixelsInternal(in PixelArea prc, int cbStride, int cbBufferSize, IntPtr pbBuffer) =>
				cacheSource.copyPixels(cachePlane, prc, cbStride, cbBufferSize, pbBuffer);

			public override string ToString() => $"{base.ToString()}: {cachePlane}";
		}
	}
}
