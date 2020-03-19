using System;
using System.Runtime.CompilerServices;

using PhotoSauce.Interop.Wic;

namespace PhotoSauce.MagicScaler
{
	internal sealed class WicPixelSource : PixelSource
	{
		private readonly string sourceName;
		private readonly IWICBitmapSource? upstreamSource;

		public IWICBitmapSource WicSource { get; }

		public override PixelFormat Format { get; }
		public override int Width { get; }
		public override int Height { get; }

		public WicPixelSource(IWICBitmapSource source, string name, bool profile = true) : base()
		{
			WicSource = profile ? this.AsIWICBitmapSource(true) : source;
			sourceName = name;
			upstreamSource = profile ? source : null;

			source.GetSize(out uint width, out uint height);
			Format = PixelFormat.FromGuid(source.GetPixelFormat());
			Width = (int)width;
			Height = (int)height;
		}

		protected override void CopyPixelsInternal(in PixelArea prc, int cbStride, int cbBufferSize, IntPtr pbBuffer) =>
			(upstreamSource ?? WicSource).CopyPixels(prc.ToWicRect(), (uint)cbStride, (uint)cbBufferSize, pbBuffer);

		public override string ToString() => upstreamSource is null ? $"{sourceName} (nonprofiling)" : sourceName;
	}

	internal static class WicPixelSourceExtensions
	{
		private sealed class PixelSourceAsIWICBitmapSource : IWICBitmapSource
		{
			private readonly PixelSource source;

			public PixelSourceAsIWICBitmapSource(PixelSource src) => source = src;

			public void GetSize(out uint puiWidth, out uint puiHeight)
			{
				puiWidth = (uint)source.Width;
				puiHeight = (uint)source.Height;
			}

			public Guid GetPixelFormat() => source.Format.FormatGuid;

			public void GetResolution(out double pDpiX, out double pDpiY) => pDpiX = pDpiY = 96d;

			public void CopyPalette(IWICPalette pIPalette) => throw new NotImplementedException();

			public void CopyPixels(in WICRect prc, uint cbStride, uint cbBufferSize, IntPtr pbBuffer) =>
				source.CopyPixels(Unsafe.AreSame(ref WICRect.Null, ref Unsafe.AsRef(prc)) ? source.Area : prc.ToPixelArea(), (int)cbStride, (int)cbBufferSize, pbBuffer);
		}

		public static PixelSource AsPixelSource(this IWICBitmapSource source, string name, bool profile = true) =>
			new WicPixelSource(source, name, profile);
		public static IWICBitmapSource AsIWICBitmapSource(this PixelSource source, bool forceWrap = false) =>
			!forceWrap && source is WicPixelSource wsrc ? wsrc.WicSource : new PixelSourceAsIWICBitmapSource(source);
	}
}