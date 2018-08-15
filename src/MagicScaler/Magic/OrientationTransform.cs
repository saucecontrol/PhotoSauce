using PhotoSauce.MagicScaler.Interop;

namespace PhotoSauce.MagicScaler
{
	/// <summary>Represents orientation correction to be applied to an image.  The values in this enumeration match the values defined in the Exif specification.</summary>
	public enum Orientation
	{
		/// <summary>No orientation correction is required.</summary>
		Normal = 1,
		/// <summary>The image should be flipped along the X axis prior to display.</summary>
		FlipHorizontal = 2,
		/// <summary>The image should be rotated 180 degrees prior to display.</summary>
		Rotate180 = 3,
		/// <summary>The image should be flipped along the Y axis prior to display.</summary>
		FlipVertical = 4,
		/// <summary>The image should be flipped diagonally (top left to bottom right) prior to display.</summary>
		Transpose = 5,
		/// <summary>The image should be rotated 90 degrees prior to display.</summary>
		Rotate90 = 6,
		/// <summary>The image should be flipped diagonally (bottom left to top right) prior to display.</summary>
		Transverse = 7,
		/// <summary>The image should be rotated 270 degrees prior to display.</summary>
		Rotate270 = 8
	}

	/// <summary>Transforms an image by changing its column/row order according to an <see cref="Orientation" /> value.</summary>
	public sealed class OrientationTransform : PixelTransform, IPixelTransformInternal
	{
		private readonly Orientation orientation;

		/// <summary>Creates a new transform with the specified <paramref name="orientation" /> value.</summary>
		/// <param name="orientation">The <see cref="Orientation" /> correction to apply to the image.</param>
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
