using System;
using PhotoSauce.MagicScaler.Interop;

namespace PhotoSauce.MagicScaler
{
	internal class WicPlanarCacheSource
	{
		private byte[] lineBuffY = null;
		private byte[] lineBuffC = null;
		private IWICPlanarBitmapSourceTransform sourceTransform;
		private WICBitmapTransformOptions sourceTransformOptions;
		private WICBitmapPlaneDescription planeDescriptionY;
		private WICBitmapPlaneDescription planeDescriptionC;
		private WICRect scaledCrop;
		private WicPlanarSource sourceY;
		private WicPlanarSource sourceC;
		private double subsampleRatioX;
		private double subsampleRatioY;
		private uint scaledWidth;
		private uint scaledHeight;
		private uint strideY;
		private uint strideC;
		private uint buffHeightY;
		private uint buffHeightC;
		private int startY = -1, posY = 0;
		private int startC = -1, posC = 0;

		public WicPlanarCacheSource(IWICPlanarBitmapSourceTransform source, WICBitmapPlaneDescription descY, WICBitmapPlaneDescription descC, WICRect crop, WICBitmapTransformOptions transformOptions, uint width, uint height, double ratio)
		{
			// TODO fractional ratio support?
			subsampleRatioX = Math.Ceiling((double)descY.Width / descC.Width);
			subsampleRatioY = Math.Ceiling((double)descY.Height / descC.Height);

			var scrop = new WICRect();
			scrop.X = (int)Math.Floor(crop.X / ratio);
			scrop.Y = (int)Math.Floor(crop.Y / ratio);
			scrop.Width = Math.Min((int)Math.Ceiling(crop.Width / ratio), (int)descY.Width);
			scrop.Height = Math.Min((int)Math.Ceiling(crop.Height / ratio), (int)descY.Height);

			if (subsampleRatioX > 1d)
			{
				descC.Width = Math.Min((uint)Math.Ceiling(scrop.Width / subsampleRatioX), descC.Width);
				descY.Width = (uint)Math.Min(descC.Width * subsampleRatioX, scrop.Width);

				if (scrop.X % subsampleRatioX > 0)
					scrop.X = (int)(scrop.X / subsampleRatioX) * (int)subsampleRatioX;
			}
			else
			{
				descC.Width = descY.Width = (uint)scrop.Width;
			}

			if (subsampleRatioY > 1d)
			{
				descC.Height = Math.Min((uint)Math.Ceiling(scrop.Height / subsampleRatioY), descC.Height);
				descY.Height = (uint)Math.Min(descC.Height * subsampleRatioY, scrop.Height);

				if (scrop.Y % subsampleRatioY > 0)
					scrop.Y = (int)(scrop.Y / subsampleRatioY) * (int)subsampleRatioY;
			}
			else
			{
				descC.Height = descY.Height = (uint)scrop.Height;
			}

			sourceTransform = source;
			sourceTransformOptions = transformOptions;
			planeDescriptionY = descY;
			planeDescriptionC = descC;
			scaledCrop = scrop;
			scaledWidth = width;
			scaledHeight = height;

			strideY = (uint)scrop.Width + 3u & ~3u;
			buffHeightY = 16u;

			strideC = (uint)(Math.Ceiling(scrop.Width / subsampleRatioX)) * 2u + 3u & ~3u;
			buffHeightC = (uint)(buffHeightY / subsampleRatioY);

			sourceY = new WicPlanarSource(this, WicPlane.Luma, descY);
			sourceC = new WicPlanarSource(this, WicPlane.Chroma, descC);
		}

		unsafe private void loadBuffer(byte* pBuffY, byte* pBuffC, WICRect rect)
		{
			var planes = new WICBitmapPlane[2];
			planes[0].Format = sourceY.GetPixelFormat();
			planes[1].Format = sourceC.GetPixelFormat();
			planes[0].cbStride = strideY;
			planes[1].cbStride = strideC;
			planes[0].cbBufferSize = (uint)lineBuffY.Length;
			planes[1].cbBufferSize = (uint)lineBuffC.Length;
			planes[0].pbBuffer = (IntPtr)pBuffY;
			planes[1].pbBuffer = (IntPtr)pBuffC;

			sourceTransform.CopyPixels(rect, scaledWidth, scaledHeight, sourceTransformOptions, WICPlanarOptions.WICPlanarOptionsPreserveSubsampling, planes, 2);
		}

		unsafe public void CopyPixels(WicPlane plane, WICRect prc, uint cbStride, uint cbBufferSize, IntPtr pbBuffer)
		{
			var load = (lineBuffY == null || (plane == WicPlane.Luma && prc.Y + prc.Height > startY + buffHeightY) || (plane == WicPlane.Chroma && prc.Y + prc.Height > startC + buffHeightC));

			if (load && (lineBuffY == null || posY < buffHeightY / 4 || posC < buffHeightC / 4))
			{
				if (lineBuffY != null)
				{
					buffHeightY *= 2;
					buffHeightC *= 2;
				}

				var tbuffY = new byte[strideY * buffHeightY];
				var tbuffC = new byte[strideC * buffHeightC];

				if (lineBuffY != null)
				{
					fixed (byte* ptbuffY = tbuffY, ptbuffC = tbuffC, pcbuffY = lineBuffY, pcbuffC = lineBuffC)
					{
						Buffer.MemoryCopy(pcbuffY, ptbuffY, tbuffY.Length, lineBuffY.Length);
						Buffer.MemoryCopy(pcbuffC, ptbuffC, tbuffC.Length, lineBuffC.Length);

						var rect = new WICRect { X = scaledCrop.X, Y = scaledCrop.Y + startY + posY, Width = scaledCrop.Width, Height = Math.Min((int)buffHeightY - posY, scaledCrop.Height - prc.Y) };
						loadBuffer(ptbuffY + (buffHeightY / 2) * strideY, ptbuffC + (buffHeightC / 2) * strideC, rect);
					}

					load = false;
				}

				lineBuffY = tbuffY;
				lineBuffC = tbuffC;
			}

			fixed (byte* pBuffY = lineBuffY, pBuffC = lineBuffC)
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

						Buffer.MemoryCopy(pBuffY + posMin * strideY, pBuffY, lineBuffY.Length, (buffHeightY - posMin) * strideY);
						Buffer.MemoryCopy(pBuffC + posMins * strideC, pBuffC, lineBuffC.Length, (buffHeightC - posMins) * strideC);

						startY += posMin;
						startC += posMins;

						posY -= posMin;
						posC -= posMins;

						offsY = (int)buffHeightY - posMin;
						offsC = (int)buffHeightC - posMins;
					}
					else
					{
						startY += (int)buffHeightY;
						startC += (int)buffHeightC;
						posY = posC = 0;
					}

					var rect = new WICRect { X = scaledCrop.X, Y = scaledCrop.Y + startY + posY, Width = scaledCrop.Width, Height = Math.Min((int)buffHeightY - posY, scaledCrop.Height - prc.Y) };
					loadBuffer(pBuffY + offsY * strideY, pBuffC + offsC * strideC, rect);
				}

				if (plane == WicPlane.Luma)
				{
					Buffer.MemoryCopy(pBuffY + (posY * strideY), (void*)pbBuffer, cbBufferSize, cbBufferSize);
					posY += prc.Height;
				}
				else
				{
					Buffer.MemoryCopy(pBuffC + (posC * strideC), (void*)pbBuffer, cbBufferSize, cbBufferSize);
					posC += prc.Height;
				}
			}
		}

		public IWICBitmapSource GetPlane(WicPlane plane)
		{
			return plane == WicPlane.Luma ? sourceY : sourceC;
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
			Format = planeDesc.Format;

			cacheSource = cache;
			cachePlane = plane;
		}

		public override void CopyPixels(WICRect prc, uint cbStride, uint cbBufferSize, IntPtr pbBuffer)
		{
			cacheSource.CopyPixels(cachePlane, prc, cbStride, cbBufferSize, pbBuffer);
		}

		public override void GetResolution(out double pDpiX, out double pDpiY)
		{
			pDpiX = pDpiY = 96d;
		}

		public override void CopyPalette(IWICPalette pIPalette)
		{
			throw new NotImplementedException();
		}
	}
}
