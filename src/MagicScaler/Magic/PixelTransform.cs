using System;
using System.Drawing;
using System.ComponentModel;

namespace PhotoSauce.MagicScaler
{
	/// <summary>Provides a mechanism for defining a filter that transforms image pixels.</summary>
	public interface IPixelTransform : IPixelSource
	{
		/// <summary>Called once, before any pixels are passed through the filter.  The <paramref name="source" /> defines the input to the filter.</summary>
		/// <param name="source">The <see cref="IPixelSource" /> that provides input to the filter.</param>
		void Init(IPixelSource source);
	}

	internal interface IPixelTransformInternal : IPixelTransform
	{
		void Init(PipelineContext ctx);
	}

	/// <summary>Provides a minimal base implementation of <see cref="IPixelTransform" />, which simply passes calls through to the upstream source.</summary>
	/// <remarks>This class is intended for internal use only.</remarks>
	[EditorBrowsable(EditorBrowsableState.Never)]
	public abstract class PixelTransform : IPixelTransform
	{
		private protected PixelSource Source = NullPixelSource.Instance;

		/// <inheritdoc />
		public Guid Format => Source.Format.FormatGuid;

		/// <inheritdoc />
		public int Width => (int)Source.Width;

		/// <inheritdoc />
		public int Height => (int)Source.Height;

		/// <inheritdoc />
		unsafe public void CopyPixels(Rectangle sourceArea, int cbStride, Span<byte> buffer)
		{
			fixed (byte* pbBuffer = buffer)
				Source.CopyPixels(PixelArea.FromGdiRect(sourceArea), (uint)cbStride, (uint)buffer.Length, (IntPtr)pbBuffer);
		}

		void IPixelTransform.Init(IPixelSource source) => throw new NotImplementedException();
	}
}
