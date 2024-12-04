// Copyright © Clinton Ingram and Contributors
// SPDX-License-Identifier: MIT

using PhotoSauce.MagicScaler.Converters;

namespace PhotoSauce.MagicScaler.Transforms;

internal sealed class InvertTransform(PixelSource source) : ChainedPixelSource(source)
{
	protected override unsafe void CopyPixelsInternal(in PixelArea prc, int cbStride, int cbBufferSize, byte* pbBuffer)
	{
		Profiler.PauseTiming();
		PrevSource.CopyPixels(prc, cbStride, cbBufferSize, pbBuffer);
		Profiler.ResumeTiming();

		nint cb = prc.Width * PrevSource.Format.BytesPerPixel;
		for (int y = 0; y < prc.Height; y++)
		{
			InvertConverter.InvertLine(pbBuffer, cb);
			pbBuffer += cbStride;
		}
	}

	public override string ToString() => nameof(InvertTransform);
}
