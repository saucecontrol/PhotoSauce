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

	public sealed class OrientationTransform : PixelTransform, IPixelTransformInternal
	{
		private readonly Orientation orientation;

		public OrientationTransform(Orientation orientation) => this.orientation = orientation;

		void IPixelTransformInternal.Init(WicProcessingContext ctx)
		{
			var rotator = ctx.AddRef(Wic.Factory.CreateBitmapFlipRotator());
			rotator.Initialize(ctx.Source.WicSource, orientation.ToWicTransformOptions());
			ctx.Source = rotator.AsPixelSource(nameof(IWICBitmapFlipRotator));

			if (orientation.RequiresCache())
			{
				var bmp = ctx.AddRef(Wic.Factory.CreateBitmapFromSource(ctx.Source.WicSource, WICBitmapCreateCacheOption.WICBitmapCacheOnDemand));
				ctx.Source = bmp.AsPixelSource(nameof(IWICBitmap));
			}

			Source = ctx.Source;
		}
	}
}
