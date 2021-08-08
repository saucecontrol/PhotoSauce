// Copyright © Clinton Ingram and Contributors.  Licensed under the MIT License.

using System;

using TerraFX.Interop;

using PhotoSauce.Interop.Wic;

namespace PhotoSauce.MagicScaler
{
	internal sealed unsafe class WicPixelSource : PixelSource
	{
		private readonly string sourceName;
		private readonly IWICBitmapSource* upstreamSource;
		private readonly PixelSource? upstreamManaged;

		public IWICBitmapSource* WicSource { get; private set; }

		public override PixelFormat Format { get; }
		public override int Width { get; }
		public override int Height { get; }

		public WicPixelSource(PixelSource? managed, IWICBitmapSource* source, string name, bool profile = true) : base()
		{
			sourceName = name;
			upstreamManaged = managed;
			upstreamSource = profile ? source : null;
			WicSource = profile ? new ComPtr<IWICBitmapSource>(this.AsIWICBitmapSource(true)) : source;

			uint width, height;
			HRESULT.Check(source->GetSize(&width, &height));

			var fmt = default(Guid);
			HRESULT.Check(source->GetPixelFormat(&fmt));

			Format = PixelFormat.FromGuid(fmt);
			Width = (int)width;
			Height = (int)height;
		}

		public void CopyPalette(IWICPalette* ppal)
		{
			var src = upstreamSource is not null ? upstreamSource : WicSource;
			HRESULT.Check(src->CopyPalette(ppal));
		}

		protected override void CopyPixelsInternal(in PixelArea prc, int cbStride, int cbBufferSize, IntPtr pbBuffer)
		{
			var rect = (WICRect)prc;
			var src = upstreamSource is not null ? upstreamSource : WicSource;
			HRESULT.Check(src->CopyPixels(&rect, (uint)cbStride, (uint)cbBufferSize, (byte*)pbBuffer));
		}

		public override string ToString() => upstreamSource is null ? $"{sourceName} (nonprofiling)" : sourceName;

		protected override void Dispose(bool disposing)
		{
			if (disposing)
				upstreamManaged?.Dispose();

			if (WicSource is null)
				return;

			if (upstreamSource is not null)
				upstreamSource->Release();

			WicSource->Release();
			WicSource = null;

			base.Dispose(disposing);
		}

		~WicPixelSource() => Dispose(false);
	}

	internal static unsafe class WicPixelSourceExtensions
	{
		public static WicPixelSource AsPixelSource(this ComPtr<IWICBitmapSource> source, string name, bool profile = true) =>
			new(null, source, name, profile);

		public static WicPixelSource AsPixelSource(this ComPtr<IWICBitmapSource> source, PixelSource? managed, string name, bool profile = true) =>
			new(managed, source, name, profile);

		public static IWICBitmapSource* AsIWICBitmapSource(this PixelSource source, bool forceWrap = false)
		{
			if (!forceWrap && source is WicPixelSource wsrc)
				return wsrc.WicSource;

			return IWICBitmapSourceImpl.Wrap(source);
		}
	}
}