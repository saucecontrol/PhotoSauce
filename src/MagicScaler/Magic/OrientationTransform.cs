using System;
using System.Drawing;

using PhotoSauce.MagicScaler.Interop;

namespace PhotoSauce.MagicScaler
{
	public enum Orientation
	{
		Normal = 1,
		FlipHorizontal = 2,
		Rotate180 = 3,
		FlipVertical = 4,
		Transpose = 5,
		Rotate90 = 6,
		Transverse = 7,
		Rotate270 = 8
	}

	public sealed class OrientationTransform : IPixelTransformInternal
	{
		private Orientation orientation;
		private PixelSource source;

		public Guid Format => source.Format.FormatGuid;

		public int Width => (int)source.Width;

		public int Height => (int)source.Height;

		public OrientationTransform(Orientation orientation) => this.orientation = orientation;

		public void CopyPixels(Rectangle sourceArea, long cbStride, long cbBufferSize, IntPtr pbBuffer) => source.CopyPixels(sourceArea.ToWicRect(), (uint)cbStride, (uint)cbBufferSize, pbBuffer);

		void IPixelTransformInternal.Init(WicProcessingContext ctx)
		{
			var opt = orientation.ToWicTransformOptions();

			var rotator = ctx.AddRef(Wic.Factory.CreateBitmapFlipRotator());
			rotator.Initialize(ctx.Source.WicSource, opt);
			ctx.Source = rotator.AsPixelSource(nameof(IWICBitmapFlipRotator));

			if (opt.RequiresCache())
			{
				var bmp = ctx.AddRef(Wic.Factory.CreateBitmapFromSource(ctx.Source.WicSource, WICBitmapCreateCacheOption.WICBitmapCacheOnDemand));
				ctx.Source = bmp.AsPixelSource(nameof(IWICBitmap));
			}

			source = ctx.Source;
		}

		public void Init(IPixelSource source) => throw new NotImplementedException();
	}
}
