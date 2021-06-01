// Copyright © Clinton Ingram and Contributors.  Licensed under the MIT License.

namespace PhotoSauce.MagicScaler
{
	internal interface IEncoderConfig { }

	internal interface ILossyEncoderConfig : IEncoderConfig
	{
		int Quality { get; }
	}

	internal interface IPlanarEncoderConfig : IEncoderConfig
	{
		ChromaSubsampleMode Subsample { get; }
	}

	internal class JpegEncoderConfig : ILossyEncoderConfig, IPlanarEncoderConfig
	{
		public static readonly JpegEncoderConfig Default = new(75, ChromaSubsampleMode.Subsample420, false);

		public int Quality { get; }
		public ChromaSubsampleMode Subsample { get; }
		public bool SuppressApp0 { get; }

		public JpegEncoderConfig(int quality, ChromaSubsampleMode subsample, bool noApp0) =>
			(Quality, Subsample, SuppressApp0) = (quality, subsample, noApp0);
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

	internal class TiffEncoderConfig : IEncoderConfig
	{
		public static readonly TiffEncoderConfig Default = new(TiffCompressionMode.None);

		public TiffCompressionMode Compression { get; }

		public TiffEncoderConfig(TiffCompressionMode compression) => Compression = compression;
	}

	internal class PngEncoderConfig : IEncoderConfig
	{
		public static readonly PngEncoderConfig Default = new(false, PngFilterMode.Unspecified);

		public bool Interlace { get; }
		public PngFilterMode Filter { get; }

		public PngEncoderConfig(bool interlace, PngFilterMode filter) => (Interlace, Filter) = (interlace, filter);
	}
}
