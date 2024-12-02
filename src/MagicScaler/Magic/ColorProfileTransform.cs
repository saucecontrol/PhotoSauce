// Copyright © Clinton Ingram and Contributors
// SPDX-License-Identifier: MIT

using System;
using System.Runtime.InteropServices;
using System.Diagnostics.CodeAnalysis;

using static PhotoSauce.Interop.Lcms.Lcms;

namespace PhotoSauce.MagicScaler.Transforms;

internal sealed unsafe class ColorProfileTransform(PixelSource source, PixelFormat dstfmt, void* hndsrc, void* hnddst, void* hndxform) : ChainedPixelSource(source)
{
	private readonly PixelFormat dstFormat = dstfmt;
	private void* hProfSrc = hndsrc, hProfDst = hnddst, hTransform = hndxform;

	public static bool HaveLcms => dependencyValid.Value;

	private static readonly Lazy<bool> dependencyValid = new(() => {
		if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows) && !AppConfig.EnableWindowsLcms)
			return false;

		try
		{
			if (cmsGetEncodedCMMversion() is >= 2090 and < 3000)
				return true;
		}
		catch { }

		return false;
	});

	public override PixelFormat Format => dstFormat;

	public static bool TryCreate(PixelSource src, PixelFormat dstfmt, ColorProfile srcProfile, ColorProfile dstProfile, [NotNullWhen(true)] out ColorProfileTransform? transform)
	{
		transform = null;
		if (!dependencyValid.Value)
			return false;

		fixed (byte* srcBytes = srcProfile.ProfileBytes, dstBytes = dstProfile.ProfileBytes)
		{
			void* hndsrc = cmsOpenProfileFromMem(srcBytes, (uint)srcProfile.ProfileBytes.Length);
			if (hndsrc is null && src.Format.ColorRepresentation is PixelColorRepresentation.Cmyk)
			{
				var cmyk = ColorProfile.CmykDefault.ProfileBytes;
				fixed (byte* pcmyk = cmyk)
					hndsrc = cmsOpenProfileFromMem(pcmyk, (uint)cmyk.Length);
			}

			void* hnddst = cmsOpenProfileFromMem(dstBytes, (uint)dstProfile.ProfileBytes.Length);
			if (hndsrc is null || hnddst is null)
				return false;

			void* hndxform = cmsCreateTransform(hndsrc, getLcmsPixelFormat(src.Format), hnddst, getLcmsPixelFormat(dstfmt), INTENT_PERCEPTUAL, cmsFLAGS_COPY_ALPHA);
			if (hndxform is null)
				return false;

			transform = new(src, dstfmt, hndsrc, hnddst, hndxform);
			return true;
		}
	}

	protected override unsafe void CopyPixelsInternal(in PixelArea prc, int cbStride, int cbBufferSize, byte* pbBuffer)
	{
		if (hProfSrc is null)
			throw new ObjectDisposedException(nameof(ColorProfileTransform));

		if (PrevSource.Format.BitsPerPixel > Format.BitsPerPixel)
			copyPixelsBuffered(prc, cbStride, pbBuffer);
		else
			copyPixelsDirect(prc, cbStride, pbBuffer);
	}

	private unsafe void copyPixelsBuffered(in PixelArea prc, int cbStride, byte* pbBuffer)
	{
		int buffStride = BufferStride;
		using var buff = BufferPool.RentLocal<byte>(buffStride);

		fixed (byte* bstart = buff.Span)
		{
			int cb = MathUtil.DivCeiling(prc.Width * PrevSource.Format.BitsPerPixel, 8);

			for (int y = 0; y < prc.Height; y++)
			{
				Profiler.PauseTiming();
				PrevSource.CopyPixels(prc.Slice(y, 1), buffStride, buffStride, bstart);
				Profiler.ResumeTiming();

				byte* op = pbBuffer + y * cbStride;
				cmsDoTransform(hTransform, bstart, op, (uint)prc.Width);
			}
		}
	}

	private unsafe void copyPixelsDirect(in PixelArea prc, int cbStride, byte* pbBuffer)
	{
		int cbi = MathUtil.DivCeiling(prc.Width * PrevSource.Format.BitsPerPixel, 8);
		int cbo = MathUtil.DivCeiling(prc.Width * Format.BitsPerPixel, 8);

		for (int y = 0; y < prc.Height; y++)
		{
			byte* op = pbBuffer + y * cbStride;
			byte* ip = op + cbo - cbi;

			Profiler.PauseTiming();
			PrevSource.CopyPixels(prc.Slice(y, 1), cbStride, cbi, ip);
			Profiler.ResumeTiming();

			cmsDoTransform(hTransform, ip, op, (uint)prc.Width);
		}
	}

	private static uint getLcmsPixelFormat(PixelFormat fmt)
	{
		if (fmt == PixelFormat.Bgr24)
			return TYPE_BGR_8;
		if (fmt == PixelFormat.Bgra32)
			return TYPE_BGRA_8;
		if (fmt == PixelFormat.Pbgra32)
			return TYPE_BGRA_8_PREMUL;
		if (fmt == PixelFormat.Cmyk32)
			return TYPE_CMYK_8;
		if (fmt.ColorRepresentation is PixelColorRepresentation.Cmyk && fmt.AlphaRepresentation is PixelAlphaRepresentation.Unassociated && fmt.BitsPerPixel is 40)
			return TYPE_CMYKA_8;
		if (fmt.ColorRepresentation is PixelColorRepresentation.Cmyk && fmt.BitsPerPixel is 64)
			return TYPE_CMYK_16;

		throw new NotSupportedException("Pixel format not supported.");
	}

	protected override void Dispose(bool disposing)
	{
		if (hProfSrc is null)
			return;

		cmsDeleteTransform(hTransform);
		_ = cmsCloseProfile(hProfDst);
		_ = cmsCloseProfile(hProfSrc);
		hTransform = hProfDst = hProfSrc = null;

		base.Dispose(disposing);
	}

	~ColorProfileTransform()
	{
		ThrowHelper.ThrowIfFinalizerExceptionsEnabled(nameof(ColorProfileTransform));

		Dispose(false);
	}

	public override string ToString() => nameof(ColorProfileTransform);
}
