// Copyright © Clinton Ingram and Contributors.  Licensed under the MIT License.

using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Runtime.CompilerServices;

using TerraFX.Interop;

using PhotoSauce.Interop.Wic;

namespace PhotoSauce.MagicScaler
{
	internal unsafe class WicPlanarCache : IDisposable
	{
		private enum WicPlane { Y, Cb, Cr }

		private readonly uint scaledWidth, scaledHeight;
		private readonly int subsampleRatioX, subsampleRatioY;
		private readonly int strideY, strideC;
		private readonly int buffHeight;

		private readonly WICBitmapTransformOptions sourceTransformOptions;
		private readonly PlanarCachePixelSource sourceY, sourceCb, sourceCr;
		private readonly PixelBuffer buffY, buffCb, buffCr;
		private readonly WICRect scaledCrop;

		private IWICPlanarBitmapSourceTransform* sourceTransform;

		public WicPlanarCache(IWICPlanarBitmapSourceTransform* source, ReadOnlySpan<WICBitmapPlaneDescription> desc, WICBitmapTransformOptions transformOptions, uint width, uint height, in PixelArea crop)
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
		}

		public PixelSource SourceY => sourceY;
		public PixelSource SourceCb => sourceCb;
		public PixelSource SourceCr => sourceCr;

		private void copyPixels(WicPlane plane, in PixelArea prc, int cbStride, int cbBufferSize, IntPtr pbBuffer)
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
				Unsafe.CopyBlockUnaligned(ref *((byte*)pbBuffer + y * cbStride), ref MemoryMarshal.GetReference(lspan), (uint)lspan.Length);
			}
		}

		private void loadBuffer(WicPlane plane, int line)
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
				var formats = WicTransforms.PlanarPixelFormats;
				var sourcePlanes = stackalloc[] {
					new WICBitmapPlane { Format = formats[0], pbBuffer = pBuffY, cbStride = (uint)strideY, cbBufferSize = (uint)spanY.Length },
					new WICBitmapPlane { Format = formats[1], pbBuffer = pBuffCb, cbStride = (uint)strideC, cbBufferSize = (uint)spanCb.Length },
					new WICBitmapPlane { Format = formats[2], pbBuffer = pBuffCr, cbStride = (uint)strideC, cbBufferSize = (uint)spanCr.Length }
				};

				HRESULT.Check(sourceTransform->CopyPixels(&sourceRect, scaledWidth, scaledHeight, sourceTransformOptions, WICPlanarOptions.WICPlanarOptionsDefault, sourcePlanes, (uint)WicTransforms.PlanarPixelFormats.Length));
			}
		}

		public void Dispose()
		{
			if (sourceTransform is null)
				return;

			buffY.Dispose();
			buffCb.Dispose();
			buffCr.Dispose();

			sourceTransform->Release();
			sourceTransform = null;
		}

		private sealed class PlanarCachePixelSource : PixelSource
		{
			private readonly WicPlanarCache cacheSource;
			private readonly WicPlane cachePlane;

			public override PixelFormat Format { get; }
			public override int Width { get; }
			public override int Height { get; }

			public PlanarCachePixelSource(WicPlanarCache cache, WicPlane plane, WICBitmapPlaneDescription planeDesc) : base()
			{
				Format = PixelFormat.FromGuid(planeDesc.Format);
				Width = (int)planeDesc.Width;
				Height = (int)planeDesc.Height;

				cacheSource = cache;
				cachePlane = plane;
			}

			protected override void CopyPixelsInternal(in PixelArea prc, int cbStride, int cbBufferSize, IntPtr pbBuffer) =>
				cacheSource.copyPixels(cachePlane, prc, cbStride, cbBufferSize, pbBuffer);

			public override string ToString() => $"{nameof(PlanarCachePixelSource)}: {cachePlane}";
		}
	}
}
