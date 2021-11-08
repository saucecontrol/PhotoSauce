// Copyright © Clinton Ingram and Contributors.  Licensed under the MIT License.

namespace PhotoSauce.MagicScaler
{
	internal interface ILossyEncoderOptions : IEncoderOptions
	{
		int Quality { get; }
	}

	internal interface IPlanarEncoderOptions : IEncoderOptions
	{
		ChromaSubsampleMode Subsample { get; }
	}

	internal readonly record struct JpegEncoderOptions(int Quality, ChromaSubsampleMode Subsample, bool SuppressApp0) : ILossyEncoderOptions, IPlanarEncoderOptions
	{
		public static JpegEncoderOptions Default => new(75, ChromaSubsampleMode.Subsample420, false);
	}

	internal readonly record struct PngEncoderOptions(bool Interlace, PngFilterMode Filter) : IEncoderOptions
	{
		public static PngEncoderOptions Default => new(false, PngFilterMode.Unspecified);
	}

	internal readonly record struct TiffEncoderOptions(TiffCompressionMode Compression) : IEncoderOptions
	{
		public static TiffEncoderOptions Default => new(TiffCompressionMode.None);
	}

	internal interface IPlanarDecoderOptions : IDecoderOptions
	{
		bool AllowPlanar { get; }
	}

	internal interface INativeScalingDecoderOptions : IDecoderOptions
	{
		bool AllowNativeScaling { get; }
	}

	internal interface IMultiFrameDecoderOptions : IDecoderOptions
	{
		bool ReadAllFrames { get; }
	}

	internal readonly record struct JpegDecoderOptions(bool AllowPlanar, bool AllowNativeScaling) : IPlanarDecoderOptions, INativeScalingDecoderOptions
	{
		public static JpegDecoderOptions Default => new(true, true);
	}

	internal readonly record struct GifDecoderOptions(bool ReadAllFrames) : IMultiFrameDecoderOptions
	{
		public static GifDecoderOptions Default => new(true);
	}

	internal readonly record struct TiffDecoderOptions(bool ReadAllFrames) : IMultiFrameDecoderOptions
	{
		public static TiffDecoderOptions Default => new(true);
	}

	internal enum PngFilterMode
	{
		Unspecified = 0,
		None = 1,
		Sub = 2,
		Up = 3,
		Average = 4,
		Paeth = 5,
		Adaptive = 6
	}

	internal enum TiffCompressionMode
	{
		Unspecified = 0,
		None = 1,
		CCITT3 = 2,
		CCITT4 = 3,
		LZW = 4,
		RLE = 5,
		ZIP = 6,
		LZWHDifferencing = 7
	}
}
