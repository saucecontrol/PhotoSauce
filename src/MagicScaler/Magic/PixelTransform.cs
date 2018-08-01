using System;
using System.Drawing;
using System.Runtime.InteropServices;

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

		unsafe public void CopyPixels(Rectangle sourceArea, int cbStride, Span<byte> buffer)
		{
			fixed (byte* pbBuffer = &MemoryMarshal.GetReference(buffer))
				Source.CopyPixels(sourceArea.ToWicRect(), (uint)cbStride, (uint)buffer.Length, (IntPtr)pbBuffer);
		}

		void IPixelTransform.Init(IPixelSource source) => throw new NotImplementedException();
	}
}
