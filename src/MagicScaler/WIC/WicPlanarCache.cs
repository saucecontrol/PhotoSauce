using System;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Runtime.CompilerServices;

using PhotoSauce.MagicScaler.Interop;

namespace PhotoSauce.MagicScaler
{
	internal enum WicPlane { Luma, Chroma }

	internal class WicPlanarCache : IDisposable
	{
		private readonly uint scaledWidth, scaledHeight;
		private readonly int subsampleRatioX, subsampleRatioY;
		private readonly int strideY, strideC;
		private readonly int buffHeight;

		private readonly IWICPlanarBitmapSourceTransform sourceTransform;
		private readonly WICBitmapTransformOptions sourceTransformOptions;
		private readonly PlanarPixelSource sourceY, sourceC;
		private readonly PixelBuffer buffY, buffC;
		private readonly Rectangle scaledCrop;
		private readonly WICRect sourceRect;
		private readonly WICBitmapPlane[] sourcePlanes;

		public WicPlanarCache(IWICPlanarBitmapSourceTransform source, WICBitmapPlaneDescription descY, WICBitmapPlaneDescription descC, in Rectangle crop, WICBitmapTransformOptions transformOptions, uint width, uint height, int scaleRatio)
		{
			// IWICPlanarBitmapSourceTransform only supports 4:2:0, 4:2:2, and 4:4:4 subsampling, so ratios will always be 1 or 2
			subsampleRatioX = (int)((descY.Width + 1u) / descC.Width);
			subsampleRatioY = (int)((descY.Height + 1u) / descC.Height);

			var scrop = new Rectangle {
				X = MathUtil.PowerOf2Floor(crop.X / scaleRatio, subsampleRatioX),
				Y = MathUtil.PowerOf2Floor(crop.Y / scaleRatio, subsampleRatioY),
			};
			scrop.Width = Math.Min(MathUtil.PowerOf2Ceiling(MathUtil.DivCeiling(crop.Width, scaleRatio), subsampleRatioX), (int)descY.Width - scrop.X);
			scrop.Height = Math.Min(MathUtil.PowerOf2Ceiling(MathUtil.DivCeiling(crop.Height, scaleRatio), subsampleRatioY), (int)descY.Height - scrop.Y);

			descC.Width = Math.Min((uint)MathUtil.DivCeiling(scrop.Width, subsampleRatioX), descC.Width);
			descC.Height = Math.Min((uint)MathUtil.DivCeiling(scrop.Height, subsampleRatioY), descC.Height);

			descY.Width = Math.Min(descC.Width * (uint)subsampleRatioX, (uint)scrop.Width);
			descY.Height = Math.Min(descC.Height * (uint)subsampleRatioY, (uint)scrop.Height);

			sourceTransform = source;
			sourceTransformOptions = transformOptions;
			scaledCrop = scrop;
			scaledWidth = width;
			scaledHeight = height;

			strideY = MathUtil.PowerOf2Ceiling((int)descY.Width, IntPtr.Size);
			strideC = MathUtil.PowerOf2Ceiling((int)descC.Width * 2, IntPtr.Size);

			buffHeight = Math.Min(scrop.Height, transformOptions.RequiresCache() ? (int)descY.Height : 16 /* 8 / scaleRatio * subsampleRatioY */);
			buffY = new PixelBuffer(buffHeight, strideY);
			buffC = new PixelBuffer(MathUtil.DivCeiling(buffHeight, subsampleRatioY), strideC);

			sourceY = new PlanarPixelSource(this, WicPlane.Luma, descY);
			sourceC = new PlanarPixelSource(this, WicPlane.Chroma, descC);
			sourceRect = new WICRect { X = scaledCrop.X, Width = scaledCrop.Width };
			sourcePlanes = new[] {
				new WICBitmapPlane { Format = sourceY.Format.FormatGuid, cbStride = (uint)strideY },
				new WICBitmapPlane { Format = sourceC.Format.FormatGuid, cbStride = (uint)strideC }
			};
		}

		unsafe public void CopyPixels(WicPlane plane, in Rectangle prc, uint cbStride, uint cbBufferSize, IntPtr pbBuffer)
		{
			var buff = plane == WicPlane.Luma ? buffY : buffC;
			int bpp = plane == WicPlane.Luma ? 1 : 2;

			for (int y = 0; y < prc.Height; y++)
			{
				int line = prc.Y + y;
				if (!buff.ContainsLine(line))
					loadBuffer(plane, line);

				var lspan = buff.PrepareRead(line, 1).Slice(prc.X * bpp, prc.Width * bpp);
				Unsafe.CopyBlockUnaligned(ref Unsafe.AsRef<byte>((byte*)pbBuffer  + y * cbStride), ref MemoryMarshal.GetReference(lspan), (uint)lspan.Length);
			}
		}

		public PixelSource GetPlane(WicPlane plane) => plane == WicPlane.Luma ? sourceY : sourceC;

		unsafe private void loadBuffer(WicPlane plane, int line)
		{
			int prcY = MathUtil.PowerOf2Floor(plane == WicPlane.Luma ? line : line * subsampleRatioY, subsampleRatioY);

			sourceRect.Y = scaledCrop.Y + prcY;
			sourceRect.Height = Math.Min(buffHeight, scaledCrop.Height - prcY);

			int lineC = sourceRect.Y / subsampleRatioY;
			int heightC = MathUtil.DivCeiling(sourceRect.Height, subsampleRatioY);

			var spanY = buffY.PrepareLoad(ref sourceRect.Y, ref sourceRect.Height);
			var spanC = buffC.PrepareLoad(ref lineC, ref heightC);

			fixed (byte* pBuffY = spanY, pBuffC = spanC)
			{
				sourcePlanes[0].pbBuffer = (IntPtr)pBuffY;
				sourcePlanes[1].pbBuffer = (IntPtr)pBuffC;
				sourcePlanes[0].cbBufferSize = (uint)spanY.Length;
				sourcePlanes[1].cbBufferSize = (uint)spanC.Length;

				sourceTransform.CopyPixels(sourceRect, scaledWidth, scaledHeight, sourceTransformOptions, WICPlanarOptions.WICPlanarOptionsDefault, sourcePlanes, (uint)sourcePlanes.Length);
			}
		}

		public void Dispose()
		{
			buffY.Dispose();
			buffC.Dispose();
		}

		private class PlanarPixelSource : PixelSource
		{
			private readonly WicPlanarCache cacheSource;
			private readonly WicPlane cachePlane;

			public PlanarPixelSource(WicPlanarCache cache, WicPlane plane, WICBitmapPlaneDescription planeDesc)
			{
				Width = planeDesc.Width;
				Height = planeDesc.Height;
				Format = PixelFormat.Cache[planeDesc.Format];
				WicSource = this.AsIWICBitmapSource();

				cacheSource = cache;
				cachePlane = plane;
			}

			protected override void CopyPixelsInternal(in Rectangle prc, uint cbStride, uint cbBufferSize, IntPtr pbBuffer) =>
				cacheSource.CopyPixels(cachePlane, prc, cbStride, cbBufferSize, pbBuffer);

			public override string ToString() => $"{base.ToString()}: {cachePlane}";
		}
	}
}
