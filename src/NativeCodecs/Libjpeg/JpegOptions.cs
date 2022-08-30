// Copyright © Clinton Ingram and Contributors.  Licensed under the MIT License.

using PhotoSauce.MagicScaler;

namespace PhotoSauce.NativeCodecs.Libjpeg;

/// <summary>Encoder options for optimized JPEG.  When this options type is used, entropy coding will derive code tables optimized for the source image rather than using the reference JPEG tables.</summary>
/// <remarks>Optimized entopy coding produces smaller file sizes at the cost of increased memory and CPU use.</remarks>
/// <param name="Quality"><inheritdoc cref="JpegEncoderOptions.Quality" path="/summary/node()" /></param>
/// <param name="Subsample"><inheritdoc cref="JpegEncoderOptions.Subsample" path="/summary/node()" /></param>
/// <param name="SuppressApp0"><inheritdoc cref="JpegEncoderOptions.SuppressApp0" path="/summary/node()" /></param>
/// <param name="Progressive">The desired progressive scan encoding behavior.</param>
public readonly record struct JpegOptimizedEncoderOptions(int Quality, ChromaSubsampleMode Subsample, JpegProgressiveMode Progressive, bool SuppressApp0 = false) : ILossyEncoderOptions, IPlanarEncoderOptions
{
	/// <summary>Default optimized JPEG encoder options.</summary>
	public static JpegOptimizedEncoderOptions Default => default;
}

/// <summary>Describes options for progressive (interlaced) JPEG encoding.</summary>
public enum JpegProgressiveMode
{
	/// <summary>Use baseline (non-progressive) encoding.</summary>
	/// <remarks>This option gives the fastest encode and decode times at the cost of some compression.</remarks>
	None,
	/// <summary>Use an abbreviated progressive scan script.</summary>
	/// <remarks>This option is designed to improve encode and decode speed over full progressive, while eliminating the <a href="https://cloudinary.com/blog/progressive_jpegs_and_green_martians">"green martian"</a> effect typical of progressive JPEG.</remarks>
	Semi,
	/// <summary>Use the default JPEG progressive scan script.</summary>
	/// <remarks>This option typically produces the smallest file sizes, at the cost of increased encode and decode times.</remarks>
	Full
}
