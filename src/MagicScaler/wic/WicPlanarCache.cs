using System;
using System.Buffers;

using PhotoSauce.MagicScaler.Interop;

namespace PhotoSauce.MagicScaler
{
	internal class WicPlanarCacheSource : IDisposable
	{
		private double subsampleRatioX, subsampleRatioY;
		private uint scaledWidth, scaledHeight;
		private int strideY, strideC;
		private int buffHeightY, buffHeightC;
		private int startY = -1, posY = 0;
		private int startC = -1, posC = 0;
		private ArraySegment<byte> lineBuffY, lineBuffC;
		private IWICPlanarBitmapSourceTransform sourceTransform;
		private WICBitmapTransformOptions sourceTransformOptions;
		private WicPlanarSource sourceY, sourceC;
		private WICRect scaledCrop;

		public WicPlanarCacheSource(IWICPlanarBitmapSourceTransform source, WICBitmapPlaneDescription descY, WICBitmapPlaneDescription descC, WICRect crop, WICBitmapTransformOptions transformOptions, uint width, uint height, double ratio)
		{
			// TODO fractional ratio support?
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
				if (scrop.X % subsampleRatioX > 0d)
					scrop.X = (int)(scrop.X / subsampleRatioX) * (int)subsampleRatioX;
				if (scrop.Width % subsampleRatioX > 0d)
					scrop.Width = (int)Math.Min(Math.Ceiling(scrop.Width / subsampleRatioX) * (int)subsampleRatioX, descY.Width);

				descC.Width = Math.Min((uint)Math.Ceiling(scrop.Width / subsampleRatioX), descC.Width);
				descY.Width = (uint)Math.Min(descC.Width * subsampleRatioX, scrop.Width);
			}
			else
			{
				descC.Width = descY.Width = (uint)scrop.Width;
			}

			if (subsampleRatioY > 1d)
			{
				if (scrop.Y % subsampleRatioY > 0d)
					scrop.Y = (int)(scrop.Y / subsampleRatioY) * (int)subsampleRatioY;
				if (scrop.Height % subsampleRatioY > 0d)
					scrop.Height = (int)Math.Min(Math.Ceiling(scrop.Height / subsampleRatioY) * (int)subsampleRatioY, descY.Height);

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
			buffHeightY = Math.Min(scrop.Height, transformOptions.RequiresCache() ? scrop.Height : 16);

			strideC = (int)Math.Ceiling(scrop.Width / subsampleRatioX) * 2 + 3 & ~3;
			buffHeightC = (int)Math.Ceiling(buffHeightY / subsampleRatioY);

			sourceY = new WicPlanarSource(this, WicPlane.Luma, descY);
			sourceC = new WicPlanarSource(this, WicPlane.Chroma, descC);
		}

		unsafe private void loadBuffer(byte* pBuffY, byte* pBuffC, WICRect rect)
		{
			var planes = new WICBitmapPlane[2];
			planes[0].Format = sourceY.GetPixelFormat();
			planes[1].Format = sourceC.GetPixelFormat();
			planes[0].cbStride = (uint)strideY;
			planes[1].cbStride = (uint)strideC;
			planes[0].cbBufferSize = (uint)lineBuffY.Count;
			planes[1].cbBufferSize = (uint)lineBuffC.Count;
			planes[0].pbBuffer = (IntPtr)pBuffY;
			planes[1].pbBuffer = (IntPtr)pBuffC;

			sourceTransform.CopyPixels(rect, scaledWidth, scaledHeight, sourceTransformOptions, WICPlanarOptions.WICPlanarOptionsDefault, planes, 2);
		}

		unsafe public void CopyPixels(WicPlane plane, WICRect prc, uint cbStride, uint cbBufferSize, IntPtr pbBuffer)
		{
			if (prc.X < 0 || prc.Y < 0 || prc.X + prc.Width > scaledWidth || prc.Y + prc.Height > scaledHeight)
				throw new ArgumentOutOfRangeException(nameof(prc), "Requested rectangle does not fall within the image bounds");

			bool load = (lineBuffY.Array == null || (plane == WicPlane.Luma && prc.Y + prc.Height > startY + buffHeightY) || (plane == WicPlane.Chroma && prc.Y + prc.Height > startC + buffHeightC));
			if (load && (lineBuffY.Array == null || posY < buffHeightY / 4 || posC < buffHeightC / 4))
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
					fixed (byte* ptbuffY = tbuffY.Array, ptbuffC = tbuffC.Array, pcbuffY = lineBuffY.Array, pcbuffC = lineBuffC.Array)
					{
						Buffer.MemoryCopy(pcbuffY, ptbuffY, tbuffY.Array.Length, lineBuffY.Count);
						Buffer.MemoryCopy(pcbuffC, ptbuffC, tbuffC.Array.Length, lineBuffC.Count);

						var rect = new WICRect { X = scaledCrop.X, Y = scaledCrop.Y + startY + posY, Width = scaledCrop.Width, Height = Math.Min(buffHeightY - posY, scaledCrop.Height - (plane == WicPlane.Luma ? prc.Y : (int)Math.Ceiling(prc.Y * subsampleRatioY))) };
						loadBuffer(ptbuffY + lineBuffY.Count, ptbuffC + lineBuffC.Count, rect);
					}

					load = false;

					ArrayPool<byte>.Shared.Return(lineBuffY.Array);
					ArrayPool<byte>.Shared.Return(lineBuffC.Array);
				}

				lineBuffY = tbuffY;
				lineBuffC = tbuffC;
			}

			fixed (byte* pBuffY = lineBuffY.Array, pBuffC = lineBuffC.Array)
			{
				if (load)
				{
					int offsY = 0, offsC = 0;
					if (startY == -1)
					{
						startY = prc.Y;
						if (startY % subsampleRatioY > 0)
							startY = (int)(startY / subsampleRatioY) * (int)subsampleRatioY;

						startC = (int)(startY / subsampleRatioY);
						posY = prc.Y - startY;
						posC = (int)(posY / subsampleRatioY);
					}
					else if (posY < buffHeightY || posC < buffHeightC)
					{
						int posMin = (int)Math.Min(posC * subsampleRatioY, posY);
						int posMins = (int)(posMin / subsampleRatioY);
						posMin =  posMins * (int)subsampleRatioY;

						Buffer.MemoryCopy(pBuffY + posMin * strideY, pBuffY, lineBuffY.Array.Length, (buffHeightY - posMin) * strideY);
						Buffer.MemoryCopy(pBuffC + posMins * strideC, pBuffC, lineBuffC.Array.Length, (buffHeightC - posMins) * strideC);

						startY += posMin;
						startC += posMins;

						posY -= posMin;
						posC -= posMins;

						offsY = buffHeightY - posMin;
						offsC = buffHeightC - posMins;
					}
					else
					{
						startY += buffHeightY;
						startC += buffHeightC;
						posY = posC = 0;
					}

					var rect = new WICRect { X = scaledCrop.X, Y = scaledCrop.Y + startY + offsY, Width = scaledCrop.Width, Height = Math.Min(buffHeightY - offsY, scaledCrop.Height - (plane == WicPlane.Luma ? prc.Y : (int)Math.Ceiling(prc.Y * subsampleRatioY))) };
					loadBuffer(pBuffY + offsY * strideY, pBuffC + offsC * strideC, rect);
				}

				if (plane == WicPlane.Luma)
				{
					for (int y = 0; y < prc.Height; y++)
						Buffer.MemoryCopy(pBuffY + posY * strideY + y * strideY, (byte*)pbBuffer + y * cbStride, cbBufferSize, cbStride);
					posY += prc.Height;
				}
				else
				{
					for (int y = 0; y < prc.Height; y++)
						Buffer.MemoryCopy(pBuffC + posC * strideC + y * strideC, (byte*)pbBuffer + y * cbStride, cbBufferSize, cbStride);
					posC += prc.Height;
				}
			}
		}

		public void GetResolution(out double pDpiX, out double pDpiY) => ((IWICBitmapSource)sourceTransform).GetResolution(out pDpiX, out pDpiY);

		public IWICBitmapSource GetPlane(WicPlane plane) => plane == WicPlane.Luma ? sourceY : sourceC;

		public void Dispose()
		{
			ArrayPool<byte>.Shared.Return(lineBuffY.Array ?? Array.Empty<byte>());
			ArrayPool<byte>.Shared.Return(lineBuffC.Array ?? Array.Empty<byte>());
			lineBuffY = lineBuffC = default(ArraySegment<byte>);
		}
	}

	internal class WicPlanarSource : WicBitmapSourceBase
	{
		private WicPlanarCacheSource cacheSource;
		private WicPlane cachePlane;

		public WicPlanarSource(WicPlanarCacheSource cache, WicPlane plane, WICBitmapPlaneDescription planeDesc)
		{
			Width = planeDesc.Width;
			Height = planeDesc.Height;
			Format = PixelFormat.Cache[planeDesc.Format];

			cacheSource = cache;
			cachePlane = plane;
		}

		public override void CopyPixels(WICRect prc, uint cbStride, uint cbBufferSize, IntPtr pbBuffer) => cacheSource.CopyPixels(cachePlane, prc, cbStride, cbBufferSize, pbBuffer);

		public override void GetResolution(out double pDpiX, out double pDpiY) => cacheSource.GetResolution(out pDpiX, out pDpiY);

		public override void CopyPalette(IWICPalette pIPalette) => throw new NotImplementedException();
	}
}
