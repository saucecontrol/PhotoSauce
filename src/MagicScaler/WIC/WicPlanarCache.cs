using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Runtime.CompilerServices;

using PhotoSauce.MagicScaler.Interop;

namespace PhotoSauce.MagicScaler
{
	internal enum WicPlane { Y, CbCr }

	internal class WicPlanarCache : IDisposable
	{
		private readonly uint scaledWidth, scaledHeight;
		private readonly int subsampleRatioX, subsampleRatioY;
		private readonly int strideY, strideC;
		private readonly int buffHeight;

		private readonly IWICPlanarBitmapSourceTransform sourceTransform;
		private readonly WICBitmapTransformOptions sourceTransformOptions;
		private readonly PlanarPixelSource sourceY, sourceCbCr;
		private readonly PixelBuffer buffY, buffCbCr;
		private readonly WICRect scaledCrop;
		private readonly WICBitmapPlane[] sourcePlanes;

		public WicPlanarCache(IWICPlanarBitmapSourceTransform source, WICBitmapPlaneDescription[] desc, WICBitmapTransformOptions transformOptions, uint width, uint height, in PixelArea crop)
		{
			var descY = desc[0];
			var descC = desc[1];

			// IWICPlanarBitmapSourceTransform only supports 4:2:0, 4:4:0, 4:2:2, and 4:4:4 subsampling, so ratios will always be 1 or 2
			subsampleRatioX = (int)((descY.Width + 1u) / descC.Width);
			subsampleRatioY = (int)((descY.Height + 1u) / descC.Height);

			var scrop = new WICRect {
				X = MathUtil.PowerOfTwoFloor(crop.X, subsampleRatioX),
				Y = MathUtil.PowerOfTwoFloor(crop.Y, subsampleRatioY),
			};
			scrop.Width = Math.Min(MathUtil.PowerOfTwoCeiling(crop.Width, subsampleRatioX), (int)descY.Width - scrop.X);
			scrop.Height = Math.Min(MathUtil.PowerOfTwoCeiling(crop.Height, subsampleRatioY), (int)descY.Height - scrop.Y);

			descC.Width = Math.Min((uint)MathUtil.DivCeiling(scrop.Width, subsampleRatioX), descC.Width);
			descC.Height = Math.Min((uint)MathUtil.DivCeiling(scrop.Height, subsampleRatioY), descC.Height);

			descY.Width = Math.Min(descC.Width * (uint)subsampleRatioX, (uint)scrop.Width);
			descY.Height = Math.Min(descC.Height * (uint)subsampleRatioY, (uint)scrop.Height);

			sourceTransform = source;
			sourceTransformOptions = transformOptions;
			scaledCrop = scrop;
			scaledWidth = width;
			scaledHeight = height;

			strideY = MathUtil.PowerOfTwoCeiling((int)descY.Width, IntPtr.Size);
			strideC = MathUtil.PowerOfTwoCeiling((int)descC.Width * 2, IntPtr.Size);

			buffHeight = Math.Min(scrop.Height, transformOptions.RequiresCache() ? (int)descY.Height : 16);
			buffY = new PixelBuffer(buffHeight, strideY);
			buffCbCr = new PixelBuffer(MathUtil.DivCeiling(buffHeight, subsampleRatioY), strideC);

			sourceY = new PlanarPixelSource(this, WicPlane.Y, descY);
			sourceCbCr = new PlanarPixelSource(this, WicPlane.CbCr, descC);

			sourcePlanes = new[] {
				new WICBitmapPlane { Format = sourceY.Format.FormatGuid, cbStride = (uint)strideY },
				new WICBitmapPlane { Format = sourceCbCr.Format.FormatGuid, cbStride = (uint)strideC }
			};
		}

		unsafe public void CopyPixels(WicPlane plane, in PixelArea prc, uint cbStride, uint cbBufferSize, IntPtr pbBuffer)
		{
			var buff = plane == WicPlane.Y ? buffY : buffCbCr;
			int bpp = plane == WicPlane.Y ? 1 : 2;
			int cbLine = prc.Width * bpp;
			int cbOffs = prc.X * bpp;

			Debug.Assert(cbStride >= cbLine);
			Debug.Assert(cbBufferSize >= (prc.Height - 1) * cbStride + cbLine);

			for (int y = 0; y < prc.Height; y++)
			{
				int line = prc.Y + y;
				if (!buff.ContainsLine(line))
					loadBuffer(plane, line);

				var lspan = buff.PrepareRead(line, 1).Slice(cbOffs, cbLine);
				Unsafe.CopyBlockUnaligned(ref Unsafe.AsRef<byte>((byte*)pbBuffer + y * cbStride), ref MemoryMarshal.GetReference(lspan), (uint)lspan.Length);
			}
		}

		public PixelSource GetPlane(WicPlane plane) => plane == WicPlane.Y ? sourceY : sourceCbCr;

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
			int lineC = lineY / subsampleRatioY;
			int heightY = sourceRect.Height;
			int heightC = MathUtil.DivCeiling(heightY, subsampleRatioY);

			var spanY = buffY.PrepareLoad(ref lineY, ref heightY);
			var spanC = buffCbCr.PrepareLoad(ref lineC, ref heightC);

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
			buffCbCr.Dispose();
		}

		private class PlanarPixelSource : PixelSource
		{
			private readonly WicPlanarCache cacheSource;
			private readonly WicPlane cachePlane;

			public PlanarPixelSource(WicPlanarCache cache, WicPlane plane, WICBitmapPlaneDescription planeDesc)
			{
				Width = planeDesc.Width;
				Height = planeDesc.Height;
				Format = PixelFormat.FromGuid(planeDesc.Format);
				WicSource = this.AsIWICBitmapSource();

				cacheSource = cache;
				cachePlane = plane;
			}

			protected override void CopyPixelsInternal(in PixelArea prc, uint cbStride, uint cbBufferSize, IntPtr pbBuffer) =>
				cacheSource.CopyPixels(cachePlane, prc, cbStride, cbBufferSize, pbBuffer);

			public override string ToString() => $"{base.ToString()}: {cachePlane}";
		}
	}
}
