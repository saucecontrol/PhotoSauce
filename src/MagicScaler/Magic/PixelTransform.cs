using System;
using System.Drawing;

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

	public abstract class PixelTransform : IPixelTransform
	{
		private protected PixelSource Source;

		public Guid Format => Source.Format.FormatGuid;

		public int Width => (int)Source.Width;

		public int Height => (int)Source.Height;

		public void CopyPixels(Rectangle sourceArea, long cbStride, long cbBufferSize, IntPtr pbBuffer) => Source.CopyPixels(sourceArea.ToWicRect(), (uint)cbStride, (uint)cbBufferSize, pbBuffer);

		void IPixelTransform.Init(IPixelSource source) => throw new NotImplementedException();
	}
}
