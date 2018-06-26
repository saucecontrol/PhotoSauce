using System;
using System.Buffers;

using PhotoSauce.MagicScaler.Interop;

namespace PhotoSauce.MagicScaler
{
	internal enum WicPlane { Luma, Chroma }

	internal class WicPlanarCache : IDisposable
	{
		private double subsampleRatioX, subsampleRatioY;
		private uint scaledWidth, scaledHeight;
		private int strideY, strideC;
		private int buffHeightY, buffHeightC;
		private int startY = -1, loadedY = -1, nextY = 0;
		private int startC = -1, loadedC = -1, nextC = 0;
		private ArraySegment<byte> lineBuffY, lineBuffC;
		private IWICPlanarBitmapSourceTransform sourceTransform;
		private WICBitmapTransformOptions sourceTransformOptions;
		private PlanarPixelSource sourceY, sourceC;
		private WICRect scaledCrop;

		public WicPlanarCache(IWICPlanarBitmapSourceTransform source, WICBitmapPlaneDescription descY, WICBitmapPlaneDescription descC, WICRect crop, WICBitmapTransformOptions transformOptions, uint width, uint height, double ratio)
		{
			subsampleRatioX = Math.Ceiling((double)descY.Width / descC.Width);
			subsampleRatioY = Math.Ceiling((double)descY.Height / descC.Height);

			var scrop = new WICRect {
				X = (int)Math.Floor(crop.X / ratio),
				Y = (int)Math.Floor(crop.Y / ratio),
				Width = Math.Min((int)Math.Ceiling(crop.Width / ratio), (int)descY.Width),
				Height = Math.Min((int)Math.Ceiling(crop.Height / ratio), (int)descY.Height)
			};

			if (subsampleRatioX > 1d)
			{
				if (scrop.X % subsampleRatioX > double.Epsilon)
					scrop.X = (int)(scrop.X / subsampleRatioX) * (int)subsampleRatioX;
				if (scrop.Width % subsampleRatioX > double.Epsilon)
					scrop.Width = (int)Math.Min(Math.Ceiling(scrop.Width / subsampleRatioX) * (int)subsampleRatioX, descY.Width - scrop.X);

				descC.Width = Math.Min((uint)Math.Ceiling(scrop.Width / subsampleRatioX), descC.Width);
				descY.Width = (uint)Math.Min(descC.Width * subsampleRatioX, scrop.Width);
			}
			else
			{
				descC.Width = descY.Width = (uint)scrop.Width;
			}

			if (subsampleRatioY > 1d)
			{
				if (scrop.Y % subsampleRatioY > double.Epsilon)
					scrop.Y = (int)(scrop.Y / subsampleRatioY) * (int)subsampleRatioY;
				if (scrop.Height % subsampleRatioY > double.Epsilon)
					scrop.Height = (int)Math.Min(Math.Ceiling(scrop.Height / subsampleRatioY) * (int)subsampleRatioY, descY.Height - scrop.Y);

				descC.Height = Math.Min((uint)Math.Ceiling(scrop.Height / subsampleRatioY), descC.Height);
				descY.Height = (uint)Math.Min(descC.Height * subsampleRatioY, scrop.Height);
			}
			else
			{
				descC.Height = descY.Height = (uint)scrop.Height;
			}

			sourceTransform = source;
			sourceTransformOptions = transformOptions;
			scaledCrop = scrop;
			scaledWidth = width;
			scaledHeight = height;

			strideY = scrop.Width + 3 & ~3;
			strideC = (int)Math.Ceiling(scrop.Width / subsampleRatioX) * 2 + 3 & ~3;
			buffHeightY = Math.Min(scrop.Height, transformOptions.RequiresCache() ? scrop.Height : 16);
			buffHeightC = (int)Math.Ceiling(buffHeightY / subsampleRatioY);

			sourceY = new PlanarPixelSource(this, WicPlane.Luma, descY);
			sourceC = new PlanarPixelSource(this, WicPlane.Chroma, descC);
		}

		unsafe private void loadBuffer(WicPlane plane, WICRect prc)
		{
			if (lineBuffY.Array == null || nextY < buffHeightY / 4 || nextC < buffHeightC / 4)
			{
				if (lineBuffY.Array != null)
				{
					buffHeightY = Math.Min(buffHeightY * 2, (int)scaledHeight);
					buffHeightC = Math.Min(buffHeightC * 2, (int)Math.Ceiling(buffHeightY / subsampleRatioY));
				}

				var tbuffY = new ArraySegment<byte>(ArrayPool<byte>.Shared.Rent(buffHeightY * strideY), 0, buffHeightY * strideY);
				var tbuffC = new ArraySegment<byte>(ArrayPool<byte>.Shared.Rent(buffHeightC * strideC), 0, buffHeightC * strideC);

				if (lineBuffY.Array != null)
				{
					fixed (byte* ptbuffY = &tbuffY.Array[0], ptbuffC = &tbuffC.Array[0], pcbuffY = &lineBuffY.Array[0], pcbuffC = &lineBuffC.Array[0])
					{
						Buffer.MemoryCopy(pcbuffY, ptbuffY, tbuffY.Array.Length, lineBuffY.Count);
						Buffer.MemoryCopy(pcbuffC, ptbuffC, tbuffC.Array.Length, lineBuffC.Count);
					}

					ArrayPool<byte>.Shared.Return(lineBuffY.Array);
					ArrayPool<byte>.Shared.Return(lineBuffC.Array);
				}

				lineBuffY = tbuffY;
				lineBuffC = tbuffC;
			}

			fixed (byte* pBuffY = &lineBuffY.Array[0], pBuffC = &lineBuffC.Array[0])
			{
				int offsY = 0, offsC = 0;
				if (startY == -1)
				{
					startY = prc.Y;
					if (startY % subsampleRatioY > 0)
						startY = (int)(startY / subsampleRatioY) * (int)subsampleRatioY;

					startC = (int)(startY / subsampleRatioY);
				}
				else if (nextY < buffHeightY || nextC < buffHeightC)
				{
					int minY = (int)Math.Min(nextC * subsampleRatioY, nextY);
					int minC = (int)(minY / subsampleRatioY);
					minY = minC * (int)subsampleRatioY;

					Buffer.MemoryCopy(pBuffY + minY * strideY, pBuffY, lineBuffY.Array.Length, (buffHeightY - minY) * strideY);
					Buffer.MemoryCopy(pBuffC + minC * strideC, pBuffC, lineBuffC.Array.Length, (buffHeightC - minC) * strideC);

					offsY = loadedY - startY - minY;
					offsC = (int)(loadedY / subsampleRatioY) - startC - minC;

					startY += minY;
					startC += minC;

					nextY -= minY;
					nextC -= minC;
				}
				else
				{
					startY += buffHeightY;
					startC += buffHeightC;
				}

				var rect = new WICRect {
					X = scaledCrop.X,
					Y = Math.Max(0, Math.Max(scaledCrop.Y + startY + offsY, loadedY)),
					Width = scaledCrop.Width,
					Height = Math.Min(buffHeightY - offsY, scaledCrop.Height - (plane == WicPlane.Luma ? prc.Y : (int)Math.Ceiling(prc.Y * subsampleRatioY)))
				};

				var planes = new[] {
					new WICBitmapPlane { Format = sourceY.Format.FormatGuid, pbBuffer = (IntPtr)(pBuffY + offsY * strideY), cbStride = (uint)strideY, cbBufferSize = (uint)lineBuffY.Count },
					new WICBitmapPlane { Format = sourceC.Format.FormatGuid, pbBuffer = (IntPtr)(pBuffC + offsC * strideC), cbStride = (uint)strideC, cbBufferSize = (uint)lineBuffC.Count }
				};

				sourceTransform.CopyPixels(rect, scaledWidth, scaledHeight, sourceTransformOptions, WICPlanarOptions.WICPlanarOptionsDefault, planes, (uint)planes.Length);
				loadedY = rect.Y + rect.Height - scaledCrop.Y;
				loadedC = (int)Math.Ceiling(loadedY / subsampleRatioY);
			}
		}

		unsafe public void CopyPixels(WicPlane plane, WICRect prc, uint cbStride, uint cbBufferSize, IntPtr pbBuffer)
		{
			if (lineBuffY.Array == null || (plane == WicPlane.Luma && prc.Y + prc.Height > loadedY) || (plane == WicPlane.Chroma && prc.Y + prc.Height > loadedC))
				loadBuffer(plane, prc);

			switch (plane)
			{
				case WicPlane.Luma:
					fixed (byte* pBuffY = &lineBuffY.Array[0])
					for (int y = 0; y < prc.Height; y++)
						Buffer.MemoryCopy(pBuffY + (prc.Y - startY) * strideY + y * strideY + prc.X, (byte*)pbBuffer + y * cbStride, cbStride, prc.Width);
					nextY = prc.Y + prc.Height - startY;
					break;
				case WicPlane.Chroma:
					fixed (byte* pBuffC = &lineBuffC.Array[0])
					for (int y = 0; y < prc.Height; y++)
						Buffer.MemoryCopy(pBuffC + (prc.Y - startC) * strideC + y * strideC + prc.X * 2, (byte*)pbBuffer + y * cbStride, cbStride, prc.Width * 2);
					nextC = prc.Y + prc.Height - startC;
					break;
			}
		}

		public PixelSource GetPlane(WicPlane plane) => plane == WicPlane.Luma ? sourceY : sourceC;

		public void Dispose()
		{
			ArrayPool<byte>.Shared.Return(lineBuffY.Array ?? Array.Empty<byte>());
			ArrayPool<byte>.Shared.Return(lineBuffC.Array ?? Array.Empty<byte>());
			lineBuffY = lineBuffC = default;
		}
	}

	internal class PlanarPixelSource : PixelSource
	{
		private WicPlanarCache cacheSource;
		private WicPlane cachePlane;

		public PlanarPixelSource(WicPlanarCache cache, WicPlane plane, WICBitmapPlaneDescription planeDesc)
		{
			Width = planeDesc.Width;
			Height = planeDesc.Height;
			Format = PixelFormat.Cache[planeDesc.Format];

			cacheSource = cache;
			cachePlane = plane;
		}

		protected override void CopyPixelsInternal(WICRect prc, uint cbStride, uint cbBufferSize, IntPtr pbBuffer) => cacheSource.CopyPixels(cachePlane, prc, cbStride, cbBufferSize, pbBuffer);

		public override string ToString() => $"{base.ToString()}: {cachePlane}";
	}
}
