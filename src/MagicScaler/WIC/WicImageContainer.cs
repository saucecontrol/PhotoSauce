using PhotoSauce.Interop.Wic;

namespace PhotoSauce.MagicScaler
{
	internal class WicImageContainer : IImageContainer
	{
		public IWICBitmapDecoder WicDecoder { get; }
		public FileFormat ContainerFormat { get; }
		public int FrameCount { get; }

		public bool IsRawContainer {
			get {
				if (ContainerFormat != FileFormat.Unknown)
					return false;

				var guid = WicDecoder.GetContainerFormat();
				return guid == Consts.GUID_ContainerFormatRaw || guid == Consts.GUID_ContainerFormatRaw2 || guid == Consts.GUID_ContainerFormatAdng;
			}
		}

		public IImageFrame GetFrame(int index) => new WicImageFrame(this, (uint)index);

		public WicImageContainer(IWICBitmapDecoder dec, WicPipelineContext ctx)
		{
			WicDecoder = ctx.AddRef(dec);

			ContainerFormat = WicImageDecoder.FormatMap.GetValueOrDefault(dec.GetContainerFormat(), FileFormat.Unknown);
			FrameCount = (int)dec.GetFrameCount();
		}
	}
}