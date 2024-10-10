// Copyright © Clinton Ingram and Contributors.  Licensed under the MIT License.

using System;
using System.Runtime.CompilerServices;

using TerraFX.Interop.Windows;

using PhotoSauce.Interop.Wic;

namespace PhotoSauce.MagicScaler;

internal sealed unsafe class WicFramePixelSource : PixelSource, IFramePixelSource
{
	private readonly WicImageFrame frame;
	private int width, height;

	public IImageFrame Frame => frame;
	public override PixelFormat Format { get; }
	public override int Width => width;
	public override int Height => height;

	public WicFramePixelSource(WicImageFrame frm)
	{
		var fmt = default(Guid);
		HRESULT.Check(frm.WicSource->GetPixelFormat(&fmt));

		frame = frm;
		Format = PixelFormat.FromGuid(fmt);
		UpdateSize();
	}

	protected override unsafe void CopyPixelsInternal(in PixelArea prc, int cbStride, int cbBufferSize, byte* pbBuffer)
	{
		// Some codecs (e.g. WebP) may error if the stride is larger than the buffer.
		cbStride = Math.Min(cbStride, cbBufferSize);

		var rect = (WICRect)prc;
		HRESULT.Check(frame.WicSource->CopyPixels(&rect, (uint)cbStride, (uint)cbBufferSize, pbBuffer));
	}

	public void UpdateSize()
	{
		uint w, h;
		HRESULT.Check(frame.WicSource->GetSize(&w, &h));

		(width, height) = ((int)w, (int)h);
	}

	public override string ToString() => nameof(IWICBitmapFrameDecode);
}

internal sealed unsafe class WicPixelSource : PixelSource
{
	private readonly string sourceName;
	private readonly IWICBitmapSource* upstreamSource;
	private readonly PixelSource? upstreamManaged;

	public IWICBitmapSource* WicSource { get; private set; }

	public override PixelFormat Format { get; }
	public override int Width { get; }
	public override int Height { get; }

	public WicPixelSource(PixelSource? managed, IWICBitmapSource* source, string name) : base()
	{
		bool profile = StatsManager.ProfilingEnabled;

		sourceName = name;
		upstreamManaged = managed;
		upstreamSource = profile ? source : null;
		WicSource = profile ? new ComPtr<IWICBitmapSource>(this.AsIWICBitmapSource(true)).Detach() : source;

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

	protected override void CopyPixelsInternal(in PixelArea prc, int cbStride, int cbBufferSize, byte* pbBuffer)
	{
		var rect = (WICRect)prc;
		var src = upstreamSource is not null ? upstreamSource : WicSource;
		HRESULT.Check(src->CopyPixels(&rect, (uint)cbStride, (uint)cbBufferSize, pbBuffer));
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

	~WicPixelSource()
	{
		ThrowHelper.ThrowIfFinalizerExceptionsEnabled(nameof(WicPixelSource));

		Dispose(false);
	}
}

internal static unsafe class WicPixelSourceExtensions
{
	public static WicPixelSource AsPixelSource(this ref IWICBitmapSource source, PixelSource? managed, string name) =>
		new(managed, (IWICBitmapSource*)Unsafe.AsPointer(ref source), name);

	public static IWICBitmapSource* AsIWICBitmapSource(this PixelSource source, bool forceWrap = false)
	{
		if (!forceWrap && source is WicPixelSource wsrc)
			return wsrc.WicSource;

		return IWICBitmapSourceImpl.Wrap(source);
	}
}