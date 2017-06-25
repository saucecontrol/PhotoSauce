namespace PhotoSauce.MagicScaler
{
	public interface IPixelTransform : IPixelSource
	{
		void Init(IPixelSource source);
	}

	internal interface IPixelTransformInternal : IPixelTransform
	{
		void Init(WicProcessingContext ctx);
	}
}
