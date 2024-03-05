// Copyright © Clinton Ingram and Contributors.  Licensed under the MIT License.

using System;

using PhotoSauce.MagicScaler;
using PhotoSauce.Interop.Libwebp;
using static PhotoSauce.Interop.Libwebp.Libwebp;

namespace PhotoSauce.NativeCodecs.Libwebp;

/// <summary>WebP decoder options.</summary>
/// <param name="FrameRange"><inheritdoc cref="IMultiFrameDecoderOptions.FrameRange" path="/summary/node()" /></param>
/// <param name="AllowPlanar"><inheritdoc cref="IPlanarDecoderOptions.AllowPlanar" path="/summary/node()" /></param>
/// <param name="UseBackgroundColor"><inheritdoc cref="IAnimationDecoderOptions.UseBackgroundColor" path="/summary/node()" /></param>
public readonly record struct WebpDecoderOptions(Range FrameRange, bool AllowPlanar, bool UseBackgroundColor = false) : IMultiFrameDecoderOptions, IPlanarDecoderOptions, IAnimationDecoderOptions
{
	/// <summary>Default WebP decoder options.</summary>
	public static WebpDecoderOptions Default => new(.., true);
}

/// <summary>Base interface for WebP encoder options.</summary>
public interface IWebpEncoderOptions : IEncoderOptions { }

/// <summary>Lossless WebP encoder options.</summary>
/// <param name="Level">Desired efficiency level between 0 (fastest, lowest compression) and 9 (slower, best compression).</param>
public readonly record struct WebpLosslessEncoderOptions(int Level = 6) : IWebpEncoderOptions
{
	/// <summary>Default lossless WebP encoder options.</summary>
	public static WebpLosslessEncoderOptions Default => new(6);
}

/// <summary>Lossy WebP encoder options.</summary>
/// <param name="Quality">The target quality value.</param>
public readonly record struct WebpLossyEncoderOptions(int Quality) : IWebpEncoderOptions, ILossyEncoderOptions
{
	/// <summary>Default lossy WebP encoder options.</summary>
	public static WebpLossyEncoderOptions Default => default;
}

/// <summary>Advanced WebP encoder options.  See <a href="https://developers.google.com/speed/webp/docs/api#advanced_encoding_api">WebP documentation</a> for details.</summary>
public class WebpAdvancedEncoderOptions : IWebpEncoderOptions
{
	internal WebPConfig Config;

	/// <summary>Create a new config for lossy encoding with suitable defaults.</summary>
	/// <param name="preset">Hint that describes the image type to be encoded.</param>
	/// <param name="quality">Target visual quality for the encoder [0..100].</param>
	/// <returns>A pre-initialized encoder config.</returns>
	public static unsafe WebpAdvancedEncoderOptions CreateLossy(WebpPreset preset, float quality)
	{
		WebPConfig conf;
		WebpResult.Check(WebPConfigPreset(&conf, (WebPPreset)preset, quality));

		return new(conf);
	}

	/// <summary>Create a new config for lossless encoding with suitable defaults.</summary>
	/// <param name="level">Target compression level for the encoder [0..9].</param>
	/// <returns>A pre-initialized encoder config.</returns>
	public static unsafe WebpAdvancedEncoderOptions CreateLossless(int level)
	{
		WebPConfig conf;
		WebpResult.Check(WebPConfigLosslessPreset(&conf, level));

		return new(conf);
	}

	private WebpAdvancedEncoderOptions(WebPConfig conf) => Config = conf;

	/// <summary>True for lossless encoding, false for lossy.</summary>
	public bool Lossless
	{
		get => Config.lossless != 0;
		set => Config.lossless = value ? 1 : 0;
	}

	/// <summary>Value between 0 and 100 determines the image quality (lossy) or compression level (lossless).</summary>
	/// <remarks>
	/// For lossy, 0 gives the smallest size and 100 the largest.<br />
	/// For lossless, this parameter is the amount of effort put into the compression: 0 is the fastest but gives larger files compared to the slowest, but best, 100.
	/// </remarks>
	public float Quality
	{
		get => Config.quality;
		set => Config.quality = value;
	}

	/// <summary>Quality/speed trade-off (0=fast, 6=slower-better).</summary>
	public int Method
	{
		get => Config.method;
		set => Config.method = value;
	}

	/// <summary>Hint for image type (lossless only).</summary>
	public WebpImageHint ImageHint
	{
		get => (WebpImageHint)Config.image_hint;
		set => Config.image_hint = (WebPImageHint)value;
	}

	/// <summary>If non-zero, set the desired target size in bytes. Takes precedence over <see cref="Quality" />.</summary>
	public int TargetSize
	{
		get => Config.target_size;
		set => Config.target_size = value;
	}

	/// <summary>If non-zero, specifies the minimal distortion to try to achieve. Takes precedence over <see cref="TargetSize" />.</summary>
	public float TargetPsnr
	{
		get => Config.target_PSNR;
		set => Config.target_PSNR = value;
	}

	/// <summary>maximum number of segments to use, in [1..4].</summary>
	public int Segments
	{
		get => Config.segments;
		set => Config.segments = value;
	}

	/// <summary>Spatial Noise Shaping. 0=off, 100=maximum.</summary>
	public int SnsStrength
	{
		get => Config.sns_strength;
		set => Config.sns_strength = value;
	}

	/// <summary>Range: [0 = off .. 100 = strongest].</summary>
	public int FilterStrength
	{
		get => Config.filter_strength;
		set => Config.filter_strength = value;
	}

	/// <summary>Range: [0 = off .. 7 = least sharp].</summary>
	public int FilterSharpness
	{
		get => Config.filter_sharpness;
		set => Config.filter_sharpness = value;
	}

	/// <summary>Filtering type: 0 = simple, 1 = strong (only used if <see cref="FilterStrength" /> > 0 or <see cref="AutoFilter" /> is true).</summary>
	public int FilterType
	{
		get => Config.filter_type;
		set => Config.filter_type = value;
	}

	/// <summary>True to automatically adjust filter's strength, otherwise false.</summary>
	public bool AutoFilter
	{
		get => Config.autofilter != 0;
		set => Config.autofilter = value ? 1 : 0;
	}

	/// <summary>True to use lossless compression on the alpha channel, false to leave alpha uncompressed.</summary>
	public bool AlphaCompression
	{
		get => Config.alpha_compression != 0;
		set => Config.alpha_compression = value ? 1 : 0;
	}

	/// <summary>Predictive filtering method for alpha plane. 0: none, 1: fast, 2: best. Default is 1.</summary>
	public int AlphaFiltering
	{
		get => Config.alpha_filtering;
		set => Config.alpha_filtering = value;
	}

	/// <summary>Between 0 (smallest size) and 100 (lossless). Default is 100.</summary>
	public int AlphaQuality
	{
		get => Config.alpha_quality;
		set => Config.alpha_quality = value;
	}

	/// <summary>Number of entropy-analysis passes (in [1..10]).</summary>
	public int Pass
	{
		get => Config.pass;
		set => Config.pass = value;
	}

	/// <summary>Preprocessing filter: 0=none, 1=segment-smooth, 2=pseudo-random dithering.</summary>
	public int Preprocessing
	{
		get => Config.preprocessing;
		set => Config.preprocessing = value;
	}

	/// <summary>Log2(number of token partitions) in [0..3]. Default is set to 0 for easier progressive decoding.</summary>
	public int Partitions
	{
		get => Config.partitions;
		set => Config.partitions = value;
	}

	/// <summary>Quality degradation allowed to fit the 512k limit on prediction modes coding (0: no degradation, 100: maximum possible degradation).</summary>
	public int PartitionLimit
	{
		get => Config.partition_limit;
		set => Config.partition_limit = value;
	}

	/// <summary>If true, compression parameters will be remapped to better match the expected output size from JPEG compression.</summary>
	/// <remarks> Generally, the output size will be similar but the degradation will be lower.</remarks>
	public bool EmulateJpegSize
	{
		get => Config.emulate_jpeg_size != 0;
		set => Config.emulate_jpeg_size = value ? 1 : 0;
	}

	/// <summary>If set, reduce memory usage (but increase CPU use).</summary>
	public bool LowMemory
	{
		get => Config.low_memory != 0;
		set => Config.low_memory = value ? 1 : 0;
	}

	/// <summary>Near lossless encoding [0 = max loss .. 100 = off (default)].</summary>
	public int NearLossless
	{
		get => Config.near_lossless;
		set => Config.near_lossless = value;
	}

	/// <summary>True to preserve the exact RGB values under transparent area, false to discard this invisible RGB information for better compression. The default is false.</summary>
	public bool Exact
	{
		get => Config.exact != 0;
		set => Config.exact = value ? 1 : 0;
	}

	/// <summary>If needed, use sharp (and slow) RGB->YUV conversion.</summary>
	public bool UseSharpYuv
	{
		get => Config.use_sharp_yuv != 0;
		set => Config.use_sharp_yuv = value ? 1 : 0;
	}

	/// <summary>Minimum permissible quality factor.</summary>
	public int QMin
	{
		get => Config.qmin;
		set => Config.qmin = value;
	}

	/// <summary>Maximum permissible quality factor.</summary>
	public int QMax
	{
		get => Config.qmax;
		set => Config.qmax = value;
	}

	/// <summary>Validate the current config with libwebp.</summary>
	/// <returns>True if the config is valid, otherwise false.</returns>
	public unsafe bool IsValid()
	{
		fixed (WebPConfig* ptr = &Config)
			return WebPValidateConfig(ptr) != 0;
	}
}

/// <summary>Defines encoder presets, tuned for different input types. For use with <see cref="WebpAdvancedEncoderOptions" />.</summary>
public enum WebpPreset
{
	/// <summary>Default encoder config.</summary>
	Default = WebPPreset.WEBP_PRESET_DEFAULT,
	/// <summary>Optimize for digital picture, like portraits with artificial light.</summary>
	Picture = WebPPreset.WEBP_PRESET_PICTURE,
	/// <summary>Optimize for photos, like landscapes with natural light.</summary>
	Photo = WebPPreset.WEBP_PRESET_PHOTO,
	/// <summary>Optimize for line drawings, with high-contrast details.</summary>
	Drawing = WebPPreset.WEBP_PRESET_DRAWING,
	/// <summary>Optimize for small-szied colorful images.</summary>
	Icon = WebPPreset.WEBP_PRESET_ICON,
	/// <summary>Optimize for images of text.</summary>
	Text = WebPPreset.WEBP_PRESET_TEXT
}

/// <summary>Defines image characteristics for use by the encoder.</summary>
public enum WebpImageHint
{
	/// <summary>Default image characteristics.</summary>
	Default = WebPImageHint.WEBP_HINT_DEFAULT,
	/// <summary>Optimize for digital picture, like portraits with artificial light.</summary>
	Picture = WebPImageHint.WEBP_HINT_PICTURE,
	/// <summary>Optimize for photos, like landscapes with natural light.</summary>
	Photo = WebPImageHint.WEBP_HINT_PHOTO,
	/// <summary>Optimize for discrete tone images with high-contrast details.</summary>
	Graph = WebPImageHint.WEBP_HINT_GRAPH
}