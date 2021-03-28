// Copyright © Clinton Ingram and Contributors.  Licensed under the MIT License.

using System;

using TerraFX.Interop;

using PhotoSauce.Interop.Wic;

namespace PhotoSauce.MagicScaler
{
	internal sealed unsafe class WicPixelSource : PixelSource, IDisposable
	{
		private readonly string sourceName;
		private readonly IWICBitmapSource* upstreamSource;

		public IWICBitmapSource* WicSource { get; private set; }

		public override PixelFormat Format { get; }
		public override int Width { get; }
		public override int Height { get; }

		public WicPixelSource(IWICBitmapSource* source, string name, bool profile = true) : base()
		{
			sourceName = name;
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

		protected override void CopyPixelsInternal(in PixelArea prc, int cbStride, int cbBufferSize, IntPtr pbBuffer)
		{
			var rect = prc.ToWicRect();
			var src = upstreamSource is not null ? upstreamSource : WicSource;
			src->CopyPixels(&rect, (uint)cbStride, (uint)cbBufferSize, (byte*)pbBuffer);
		}

		public override string ToString() => upstreamSource is null ? $"{sourceName} (nonprofiling)" : sourceName;

		public void Dispose()
		{
			dispose(true);
			GC.SuppressFinalize(this);
		}

		private void dispose(bool disposing)
		{
			if (WicSource is null)
				return;

			if (upstreamSource is not null)
				upstreamSource->Release();

			WicSource->Release();
			WicSource = null;
		}

		~WicPixelSource() => dispose(false);
	}

	internal static unsafe class WicPixelSourceExtensions
	{
		public static WicPixelSource AsPixelSource(this ComPtr<IWICBitmapSource> source, string name, bool profile = true) =>
			new(source, name, profile);

		public static IWICBitmapSource* AsIWICBitmapSource(this PixelSource source, bool forceWrap = false)
		{
			if (!forceWrap && source is WicPixelSource wsrc)
				return wsrc.WicSource;

			return IWICBitmapSourceImpl.Wrap(source);
		}
	}
}