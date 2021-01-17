// Copyright © Clinton Ingram and Contributors.  Licensed under the MIT License.

using System;
using System.Runtime.InteropServices;

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

		public WicPixelSource(IWICBitmapSource* source, PipelineContext? ctx, string name, bool profile = true) : base()
		{
			WicSource = profile ? this.AsIWICBitmapSource(ctx!, true) : source;
			sourceName = name;
			upstreamSource = profile ? source : null;

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
			if (WicSource is null)
				return;

			if (upstreamSource is not null)
				upstreamSource->Release();
			else
				WicSource->Release();

			WicSource = null;
		}
	}

	internal static unsafe class WicPixelSourceExtensions
	{
		private sealed class PixelSourceAsIWICBitmapSource : IDisposable
		{
			private readonly SafeComCallable<IWICBitmapSourceImpl> ccw;

			public PixelSourceAsIWICBitmapSource(PixelSource src)
			{
				var gch = GCHandle.Alloc(src);
				var psi = new IWICBitmapSourceImpl(gch);
				ccw = new SafeComCallable<IWICBitmapSourceImpl>(psi);
			}

			public IWICBitmapSource* WicSource => (IWICBitmapSource*)ccw.DangerousGetHandle();

			public void Dispose() => ccw.Dispose();
		}

		public static WicPixelSource AsPixelSource(this ComPtr<IWICBitmapSource> source, PipelineContext? ctx, string name, bool profile = true) =>
			new(source, ctx, name, profile);

		public static IWICBitmapSource* AsIWICBitmapSource(this PixelSource source, PipelineContext ctx, bool forceWrap = false)
		{
			if (!forceWrap && source is WicPixelSource wsrc)
				return wsrc.WicSource;

			var wbs = new PixelSourceAsIWICBitmapSource(source);
			return ctx.AddDispose(wbs).WicSource;
		}
	}
}