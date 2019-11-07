using System;
using System.Linq;
using System.Drawing;
using System.Diagnostics;
using System.ComponentModel;
using System.Collections.Generic;
using System.Text.RegularExpressions;

using Blake2Fast;
using PhotoSauce.MagicScaler.Interpolators;

namespace PhotoSauce.MagicScaler
{
	/// <summary>Defines the horizontal and vertical anchor positions for auto cropping.</summary>
	[Flags]
	public enum CropAnchor
	{
		/// <summary>Crop to the image center.</summary>
		Center = 0,
		/// <summary>Preserve the top edge of the image.</summary>
		Top = 1,
		/// <summary>Preserve the bottom edge of the image.</summary>
		Bottom = 2,
		/// <summary>Preserve the left edge of the image.</summary>
		Left = 4,
		/// <summary>Preserve the right edge of the image.</summary>
		Right = 8
	}

	/// <summary>Defines the modes for auto cropping and scaling.</summary>
	public enum CropScaleMode
	{
		/// <summary>Preserve the aspect ratio of the input image.  Crop if necessary to fit the output dimensions.</summary>
		Crop,
		/// <summary>Preserve the aspect ratio of the input image.  Reduce one of the output dimensions if necessary to preserve the ratio.</summary>
		Max,
		/// <summary>Stretch the image on one axis if necessary to fill the output dimensions.</summary>
		Stretch,
		/// <summary>Preserve the aspect ratio of the input image.  Fill any undefined pixels with the <see cref="ProcessImageSettings.MatteColor" />.</summary>
		Pad
	}

	/// <summary>Defines the modes that control speed vs. quality trade-offs for high-ratio scaling operations.</summary>
	public enum HybridScaleMode
	{
		/// <summary>Allow lower-quality downscaling to a size at least 3x the target size.  Use high-quality scaling to reach the final size.</summary>
		FavorQuality,
		/// <summary>Allow lower-quality downscaling to a size at least 2x the target size.  Use high-quality scaling to reach the final size.</summary>
		FavorSpeed,
		/// <summary>Allow lower-quality downscaling to the nearest power of 2 to the target size.  Use high-quality scaling to reach the final size.</summary>
		Turbo,
		/// <summary>Do not allow hybrid scaling.  Use the high-quality scaler exclusively.</summary>
		Off
	}

	/// <summary>Defines the modes that control <a href="http://blog.johnnovak.net/2016/09/21/what-every-coder-should-know-about-gamma/">gamma correction</a> in pixel blending.</summary>
	public enum GammaMode
	{
		/// <summary>Convert values to linear RGB before blending.</summary>
		Linear,
		/// <summary>Blend gamma-companded R'G'B' values directly.</summary>
		Companded,
		/// <summary>Same as <see cref="Companded" />.</summary>
		[Obsolete("Replaced by " + nameof(GammaMode) + "." + nameof(Companded), true), EditorBrowsable(EditorBrowsableState.Never)]
		sRGB = Companded
	}

	/// <summary>Defines the modes that control <a href="https://en.wikipedia.org/wiki/Chroma_subsampling">chroma subsampling</a> for output image formats that support it.</summary>
	public enum ChromaSubsampleMode
	{
		/// <summary>Configured subsampling automatically based on output format and quality settings.</summary>
		Default = 0,
		/// <summary>Use 4:2:0 Y'CbCr subsampling.</summary>
		Subsample420 = 1,
		/// <summary>Use 4:2:2 Y'CbCr subsampling.</summary>
		Subsample422 = 2,
		/// <summary>Do not use chroma subsampling (4:4:4).</summary>
		Subsample444 = 3
	}

	/// <summary>Defines known image container formats for auto-detection and output configuration.</summary>
	public enum FileFormat
	{
		/// <summary>Set output container format automatically based on input format and image contents.</summary>
		Auto,
		/// <summary>A JPEG container.</summary>
		Jpeg,
		/// <summary>A PNG container.</summary>
		Png,
		/// <summary>A PNG container using an 8-bit indexed pixel format.</summary>
		Png8,
		/// <summary>A GIF container.</summary>
		Gif,
		/// <summary>A BMP container.</summary>
		Bmp,
		/// <summary>A TIFF container.</summary>
		Tiff,
		/// <summary>An unrecognized but still decodable image container.</summary>
		Unknown = Auto
	}

	/// <summary>Defines the modes that control <a href="https://magnushoff.com/jpeg-orientation.html">Exif Orientation</a> correction.</summary>
	public enum OrientationMode
	{
		/// <summary>Correct the image orientation according to the Exif tag on load.  Save the output in normal orientation.</summary>
		Normalize,
		/// <summary>Preserve the orientation of the input image and tag the output image to reflect the orientation.  If the output format does not support orientation tagging, it will be discarded.</summary>
		Preserve,
		/// <summary>Ignore any orientation tag and treat the image as if its stored orientation is normal.  Do not tag the output image.</summary>
		Ignore = 0xff
	}

	/// <summary>Defines the modes that control <a href="https://en.wikipedia.org/wiki/ICC_profile">ICC Color Profile</a> handling.</summary>
	public enum ColorProfileMode
	{
		/// <summary>Convert the input image to the <a href="https://en.wikipedia.org/wiki/SRGB">sRGB color space</a> during processing.  Output an untagged sRGB image.</summary>
		Normalize,
		/// <summary>Convert the input image to the <a href="https://en.wikipedia.org/wiki/SRGB">sRGB color space</a> during processing.  Embed a compact sRGB profile in the output.</summary>
		NormalizeAndEmbed,
		/// <summary>Preserve the input image color space during processing.  Embed the ICC profile in the output image.  If the output format does not support embedded profiles, it will be discarded.</summary>
		Preserve,
		/// <summary>Ignore any embedded profiles and treat the image as <a href="https://en.wikipedia.org/wiki/SRGB">sRGB</a> data.  Do not tag the output image.</summary>
		Ignore = 0xff
	}

	/// <summary>Defines settings for an <a href="https://en.wikipedia.org/wiki/Unsharp_masking">Unsharp Masking</a> operation.</summary>
	public readonly struct UnsharpMaskSettings
	{
		/// <summary>No sharpening.</summary>
		public static readonly UnsharpMaskSettings None = default;

		/// <summary>The amount of sharpening.  This value represents a percentage of the difference between the blurred image and the original image.</summary>
		public int Amount { get; }
		/// <summary>The radius (sigma) of the gaussian blur used for the mask.  This value determines the size of details that are sharpened.</summary>
		public double Radius { get; }
		/// <summary>The minimum brightness change required for a pixel to be modified by the filter.</summary>
		public byte Threshold { get; }

		/// <summary>Constructs a new <see cref="UnsharpMaskSettings" /> instance with the specified values.</summary>
		/// <param name="amount">The amount of sharpening.</param>
		/// <param name="radius">The blur radius.</param>
		/// <param name="threshold">The minimum change required for a pixel to be filtered.</param>
		public UnsharpMaskSettings(int amount, double radius, byte threshold)
		{
			Amount = amount;
			Radius = radius;
			Threshold = threshold;
		}
	}

	/// <summary>Defines settings for resampling interpolation.</summary>
	public readonly struct InterpolationSettings
	{
		/// <summary>A predefined <see cref="PointInterpolator" />.</summary>
		public static readonly InterpolationSettings NearestNeighbor = new InterpolationSettings(new PointInterpolator());
		/// <summary>A predefined <see cref="BoxInterpolator" />.</summary>
		public static readonly InterpolationSettings Average = new InterpolationSettings(new BoxInterpolator());
		/// <summary>A predefined <see cref="LinearInterpolator" />.  Also known as Bilinear in some software.</summary>
		public static readonly InterpolationSettings Linear = new InterpolationSettings(new LinearInterpolator());
		/// <summary>A predefined Hermite (b=0, c=0) <see cref="CubicInterpolator" />.</summary>
		public static readonly InterpolationSettings Hermite = new InterpolationSettings(new CubicInterpolator(0d, 0d));
		/// <summary>A predefined <see cref="QuadraticInterpolator" /> with properties similar to a Catmull-Rom Cubic.</summary>
		public static readonly InterpolationSettings Quadratic = new InterpolationSettings(new QuadraticInterpolator());
		/// <summary>A predefined Mitchell-Netravali (b=1/3, c=1/3) Cubic interpolator.</summary>
		public static readonly InterpolationSettings Mitchell = new InterpolationSettings(new CubicInterpolator(1d/3d, 1d/3d));
		/// <summary>A predefined Catmull-Rom (b=0, c=1/2) <see cref="CubicInterpolator" />.</summary>
		public static readonly InterpolationSettings CatmullRom = new InterpolationSettings(new CubicInterpolator());
		/// <summary>A predefined Cardinal (b=0, c=1) <see cref="CubicInterpolator" />.  Also known as Bicubic in some software.</summary>
		public static readonly InterpolationSettings Cubic = new InterpolationSettings(new CubicInterpolator(0d, 1d));
		/// <summary>A predefined smoothing <see cref="CubicInterpolator" />.  Similar to Photoshop's "Bicubic Smoother".</summary>
		public static readonly InterpolationSettings CubicSmoother = new InterpolationSettings(new CubicInterpolator(0d, 0.625), 1.15);
		/// <summary>A predefined 3-lobed <see cref="LanczosInterpolator" />.</summary>
		public static readonly InterpolationSettings Lanczos = new InterpolationSettings(new LanczosInterpolator());
		/// <summary>A predefined <see cref="Spline36Interpolator" />.</summary>
		public static readonly InterpolationSettings Spline36 = new InterpolationSettings(new Spline36Interpolator());

		private readonly double blur;

		/// <summary>A blur value stretches or compresses the input window of an interpolation function.  This value represents a fraction of the normal window size, with 1.0 being normal.</summary>
		public double Blur => WeightingFunction is null ? default : WeightingFunction.Support * blur < 0.5 ? 1d : blur;

		/// <summary>An <see cref="IInterpolator" /> implementation that provides interpolated sample weights.</summary>
		public IInterpolator WeightingFunction { get; }

		/// <inheritdoc cref="InterpolationSettings(IInterpolator, double)" />
		public InterpolationSettings(IInterpolator weighting) : this(weighting, 1d) { }

		/// <summary>Constructs a new <see cref="InterpolationSettings" /> instance with the specified values.</summary>
		/// <param name="weighting">The weighting function implementation.</param>
		/// <param name="blur">The blur factor for the weighting function.</param>
		public InterpolationSettings(IInterpolator weighting, double blur)
		{
			if (blur < 0.5 || blur > 1.5) throw new ArgumentOutOfRangeException(nameof(blur), "Value must be between 0.5 and 1.5");

			WeightingFunction = weighting ?? throw new ArgumentNullException(nameof(weighting));
			this.blur = blur;
		}
	}

	/// <summary>Defines settings for a <see cref="MagicImageProcessor" /> pipeline operation.</summary>
	public sealed class ProcessImageSettings
	{
		private static readonly Lazy<Regex> cropExpression = new Lazy<Regex>(() => new Regex(@"^(\d+,){3}\d+$", RegexOptions.Compiled));
		private static readonly Lazy<Regex> anchorExpression = new Lazy<Regex>(() => new Regex(@"^(top|middle|bottom)?\-?(left|center|right)?$", RegexOptions.Compiled | RegexOptions.IgnoreCase));
		private static readonly Lazy<Regex> subsampleExpression = new Lazy<Regex>(() => new Regex(@"^4(20|22|44)$", RegexOptions.Compiled));
		private static readonly ProcessImageSettings empty = new ProcessImageSettings();

		private InterpolationSettings interpolation;
		private UnsharpMaskSettings unsharpMask;
		private int jpegQuality;
		private ChromaSubsampleMode jpegSubsampling;
		private ImageFileInfo? imageInfo;
		internal Rectangle InnerRect;
		internal Rectangle OuterRect;
		internal bool AutoCrop;

		/// <summary>The 0-based index of the image frame to process from within the container.</summary>
		public int FrameIndex { get; set; }
		/// <summary>The horizontal DPI of the output image.  This affects the image metadata only.</summary>
		public double DpiX { get; set; } = 96d;
		/// <summary>The vertical DPI of the output image.  This affects the image metadata only.</summary>
		public double DpiY { get; set; } = 96d;
		/// <summary>Determines whether automatic sharpening is applied during processing.</summary>
		public bool Sharpen { get; set; } = true;
		/// <summary>Determines how automatic scaling and cropping is performed.</summary>
		public CropScaleMode ResizeMode { get; set; }
		/// <summary>Defines the bounding rectangle to use from the input image.  Can be calculated automatically depending on <see cref="CropScaleMode" />.</summary>
		public Rectangle Crop { get; set; }
		/// <summary>Determines which part of the image is preserved when automatic cropping is performed.</summary>
		public CropAnchor Anchor { get; set; }
		/// <summary>Determines the container format of the output image.</summary>
		public FileFormat SaveFormat { get; set; }
		/// <summary>The background color to use when converting to a non-transparent format and the fill color for <see cref="CropScaleMode.Pad" /> mode.</summary>
		public Color MatteColor { get; set; }
		/// <summary>Determines whether Hybrid Scaling is allowed to be used to improve performance.</summary>
		public HybridScaleMode HybridMode { get; set; }
		/// <summary>Determines whether pixel blending is done using linear RGB or gamma-companded R'G'B'.</summary>
		public GammaMode BlendingMode { get; set; }
		/// <summary>Determines whether automatic orientation correction is performed.</summary>
		public OrientationMode OrientationMode { get; set; }
		/// <summary>Determines whether automatic colorspace conversion is performed.</summary>
		public ColorProfileMode ColorProfileMode { get; set; }
		/// <summary>A list of <a href="https://docs.microsoft.com/en-us/windows/desktop/wic/photo-metadata-policies">Windows Photo Metadata Policy</a> names.  Any values matching the included policies will be copied to the output image if supported.</summary>
		public IEnumerable<string> MetadataNames { get; set; } = Enumerable.Empty<string>();

		internal bool Normalized => imageInfo != null;

		internal bool IsEmpty =>
			OuterRect       == empty.OuterRect       &&
			jpegQuality     == empty.jpegQuality     &&
			jpegSubsampling == empty.jpegSubsampling &&
			FrameIndex      == empty.FrameIndex      &&
			DpiX            == empty.DpiX            &&
			DpiY            == empty.DpiY            &&
			Crop            == empty.Crop            &&
			SaveFormat      == empty.SaveFormat      &&
			MatteColor      == empty.MatteColor      &&
			unsharpMask.Equals(empty.unsharpMask)
		;

		/// <summary>The width of the output image in pixels.</summary>
		public int Width
		{
			get => OuterRect.Width;
			set
			{
				if (value < 0) throw new ArgumentOutOfRangeException(nameof(Width), "Value must be >= 0");
				OuterRect.Width = value;
			}
		}

		/// <summary>The height of the output image in pixels.</summary>
		public int Height
		{
			get => OuterRect.Height;
			set
			{
				if (value < 0) throw new ArgumentOutOfRangeException(nameof(Height), "Value must be >= 0");
				OuterRect.Height = value;
			}
		}

		/// <summary>True if the output format requires indexed color, otherwise false.</summary>
		public bool IndexedColor => SaveFormat == FileFormat.Png8 || SaveFormat == FileFormat.Gif;

		/// <summary>The calculated ratio of the input image size to output size.</summary>
		public double ScaleRatio => Math.Max(InnerRect.Width > 0 ? (double)Crop.Width / InnerRect.Width : 0d, InnerRect.Height > 0 ? (double)Crop.Height / InnerRect.Height : 0d);

		/// <summary>The calculated ratio for the low-quality portion of a hybrid scaling operation.</summary>
		public int HybridScaleRatio
		{
			get
			{
				if (HybridMode == HybridScaleMode.Off)
					return 1;

				double sr = ScaleRatio / (HybridMode == HybridScaleMode.FavorQuality ? 3d : HybridMode == HybridScaleMode.FavorSpeed ? 2d : 1d);

				return (int)Math.Max(Math.Pow(2d, Math.Floor(Math.Log(sr, 2d))), 1d);
			}
		}

		/// <summary>The quality setting to use for JPEG output.</summary>
		public int JpegQuality
		{
			get
			{
				if (jpegQuality > 0)
					return jpegQuality;

				int res = Math.Max(Width, Height);
				return res <= 160 ? 95 : res <= 320 ? 93 : res <= 480 ? 91 : res <= 640 ? 89 : res <= 1280 ? 87 : res <= 1920 ? 85 : 83;
			}
			set
			{
				if (value < 0 || value > 100)
					throw new ArgumentOutOfRangeException(nameof(JpegQuality), "Value must be between 0 and 100");

				jpegQuality = value;
			}
		}

		/// <summary>Determines what type of chroma subsampling is used for the output image.</summary>
		public ChromaSubsampleMode JpegSubsampleMode
		{
			get
			{
				if (jpegSubsampling != ChromaSubsampleMode.Default)
					return jpegSubsampling;

				return JpegQuality >= 95 ? ChromaSubsampleMode.Subsample444 : JpegQuality >= 90 ? ChromaSubsampleMode.Subsample422 : ChromaSubsampleMode.Subsample420;
			}
			set => jpegSubsampling = value;
		}

		/// <summary>Determines how resampling interpolation is performed.</summary>
		public InterpolationSettings Interpolation
		{
			get
			{
				if (interpolation.Blur > 0d)
					return interpolation;

				var interpolator = InterpolationSettings.Spline36;
				double rat = ScaleRatio / HybridScaleRatio;

				if (rat < 0.5)
					interpolator = InterpolationSettings.Lanczos;
				else if (rat > 16.0)
					interpolator = InterpolationSettings.Quadratic;
				else if (rat > 4.0)
					interpolator = InterpolationSettings.CatmullRom;

				return interpolator;
			}
			set => interpolation = value;
		}

		/// <summary>Settings for automatic sharpening.</summary>
		public UnsharpMaskSettings UnsharpMask
		{
			get
			{
				if (unsharpMask.Amount > 0)
					return unsharpMask;

				if (!Sharpen || ScaleRatio == 1.0)
					return UnsharpMaskSettings.None;

				if (ScaleRatio < 0.5)
					return new UnsharpMaskSettings(40, 1.5, 0);
				else if (ScaleRatio < 1.0)
					return new UnsharpMaskSettings(30, 1.0, 0);
				else if (ScaleRatio < 2.0)
					return new UnsharpMaskSettings(25, 0.75, 4);
				else if (ScaleRatio < 4.0)
					return new UnsharpMaskSettings(75, 0.5, 2);
				else if (ScaleRatio < 6.0)
					return new UnsharpMaskSettings(50, 0.75, 2);
				else if (ScaleRatio < 8.0)
					return new UnsharpMaskSettings(100, 0.6, 1);
				else if (ScaleRatio < 10.0)
					return new UnsharpMaskSettings(125, 0.5, 0);
				else
					return new UnsharpMaskSettings(150, 0.5, 0);
			}
			set => unsharpMask = value;
		}

		/// <summary>Create a new <see cref="ProcessImageSettings" /> instance based on name/value pairs in a dictionary.</summary>
		/// <param name="dic">The dictionary containing the name/value pairs.</param>
		/// <returns>A new settings instance.</returns>
		public static ProcessImageSettings FromDictionary(IDictionary<string, string?> dic)
		{
			if (dic == null) throw new ArgumentNullException(nameof(dic));
			if (dic.Count == 0) return new ProcessImageSettings();

			var s = new ProcessImageSettings {
				FrameIndex = Math.Max(int.TryParse(dic.GetValueOrDefault("frame") ?? dic.GetValueOrDefault("page"), out int f) ? f : 0, 0),
				Width = Math.Max(int.TryParse(dic.GetValueOrDefault("width") ?? dic.GetValueOrDefault("w"), out int w) ? w : 0, 0),
				Height = Math.Max(int.TryParse(dic.GetValueOrDefault("height") ?? dic.GetValueOrDefault("h"), out int h) ? h : 0, 0),
				JpegQuality = Math.Max(int.TryParse(dic.GetValueOrDefault("quality") ?? dic.GetValueOrDefault("q"), out int q) ? q : 0, 0)
			};

			s.Sharpen = bool.TryParse(dic.GetValueOrDefault("sharpen"), out bool bs) ? bs : s.Sharpen;
			s.ResizeMode = Enum.TryParse(dic.GetValueOrDefault("mode"), true, out CropScaleMode mode) ? mode : s.ResizeMode;
			s.BlendingMode = Enum.TryParse(dic.GetValueOrDefault("gamma"), true, out GammaMode bm) ? bm : s.BlendingMode;
			s.HybridMode = Enum.TryParse(dic.GetValueOrDefault("hybrid"), true, out HybridScaleMode hyb) ? hyb : s.HybridMode;
			s.SaveFormat = Enum.TryParse(dic.GetValueOrDefault("format")?.ToLower().Replace("jpg", "jpeg"), true, out FileFormat fmt) ? fmt : s.SaveFormat;

			if (cropExpression.Value.IsMatch(dic.GetValueOrDefault("crop") ?? string.Empty))
			{
				var ps = dic["crop"]!.Split(',');
				s.Crop = new Rectangle(int.Parse(ps[0]), int.Parse(ps[1]), int.Parse(ps[2]), int.Parse(ps[3]));
			}

			foreach (var group in anchorExpression.Value.Match(dic.GetValueOrDefault("anchor") ?? string.Empty).Groups.Cast<Group>())
			{
				if (Enum.TryParse(group.Value, true, out CropAnchor anchor))
					s.Anchor |= anchor;
			}

			foreach (var cap in subsampleExpression.Value.Match(dic.GetValueOrDefault("subsample") ?? string.Empty).Captures.Cast<Capture>())
				s.JpegSubsampleMode = Enum.TryParse(string.Concat("Subsample", cap.Value), true, out ChromaSubsampleMode csub) ? csub : s.JpegSubsampleMode;

			string? colorName = dic.GetValueOrDefault("bgcolor") ?? dic.GetValueOrDefault("bg");
			if (!string.IsNullOrWhiteSpace(colorName) && ColorParser.TryParse(colorName, out var color))
				s.MatteColor = color;

			string? filter = dic.GetValueOrDefault("filter")?.ToLower();
			switch (filter)
			{
				case "point":
				case "nearestneighbor":
					s.Interpolation = InterpolationSettings.NearestNeighbor;
					break;
				case "box":
				case "average":
					s.Interpolation = InterpolationSettings.Average;
					break;
				case "linear":
				case "bilinear":
					s.Interpolation = InterpolationSettings.Linear;
					break;
				case "quadratic":
					s.Interpolation = InterpolationSettings.Quadratic;
					break;
				case "catrom":
				case "catmullrom":
					s.Interpolation = InterpolationSettings.CatmullRom;
					break;
				case "cubic":
				case "bicubic":
					s.Interpolation = InterpolationSettings.Cubic;
					break;
				case "lanczos":
				case "lanczos3":
					s.Interpolation = InterpolationSettings.Lanczos;
					break;
				case "spline36":
					s.Interpolation = InterpolationSettings.Spline36;
					break;
			}

			return s;
		}

		/// <summary>Create a new <see cref="ProcessImageSettings" /> instance with settings calculated for a specific input image.</summary>
		/// <param name="settings">The input settings.</param>
		/// <param name="imageInfo">The input image for which the new settings should be calculated.</param>
		/// <returns>The calculated settings for the input image.</returns>
		public static ProcessImageSettings Calculate(ProcessImageSettings settings, ImageFileInfo imageInfo)
		{
			var clone = settings.Clone();
			clone.NormalizeFrom(imageInfo);
			clone.imageInfo = default;

			return clone;
		}

		internal void Fixup(int inWidth, int inHeight, bool swapDimensions = false)
		{
			int imgWidth = swapDimensions ? inHeight : inWidth;
			int imgHeight = swapDimensions ? inWidth : inHeight;

			var whole = new Rectangle(0, 0, imgWidth, imgHeight);
			AutoCrop = Crop.IsEmpty || Crop != Rectangle.Intersect(whole, Crop);

			if (Width == 0 || Height == 0)
				ResizeMode = CropScaleMode.Crop;

			if (Width == 0 && Height == 0)
			{
				Crop = AutoCrop ? whole : Crop;
				OuterRect = InnerRect = new Rectangle(0, 0, Crop.Width, Crop.Height);
				return;
			}

			if (!AutoCrop || ResizeMode != CropScaleMode.Crop)
				Anchor = CropAnchor.Center;

			int wwin = imgWidth, hwin = imgHeight;
			int width = Width, height = Height;
			double wrat = width > 0 ? (double)wwin / width : (double)hwin / height;
			double hrat = height > 0 ? (double)hwin / height : wrat;

			if (AutoCrop)
			{
				if (ResizeMode == CropScaleMode.Crop)
				{
					double rat = Math.Min(wrat, hrat);
					hwin = height > 0 ? MathUtil.Clamp((int)Math.Ceiling(rat * height), 1, imgHeight) : hwin;
					wwin = width > 0 ? MathUtil.Clamp((int)Math.Ceiling(rat * width), 1, imgWidth) : wwin;

					int left = Anchor.HasFlag(CropAnchor.Left) ? 0 : Anchor.HasFlag(CropAnchor.Right) ? (imgWidth - wwin) : ((imgWidth - wwin) / 2);
					int top = Anchor.HasFlag(CropAnchor.Top) ? 0 : Anchor.HasFlag(CropAnchor.Bottom) ? (imgHeight - hwin) : ((imgHeight - hwin) / 2);

					Crop = new Rectangle(left, top, wwin, hwin);

					width = width > 0 ? width : Math.Max((int)Math.Round(imgWidth / wrat), 1);
					height = height > 0 ? height : Math.Max((int)Math.Round(imgHeight / hrat), 1);
				}
				else
				{
					Crop = whole;
				}
			}

			wrat = width > 0 ? (double)Crop.Width / width : (double)Crop.Height / height;
			hrat = height > 0 ? (double)Crop.Height / height : wrat;

			if (ResizeMode == CropScaleMode.Max || ResizeMode == CropScaleMode.Pad)
			{
				double rat = Math.Max(wrat, hrat);
				int dim = Math.Max(width, height);

				width = MathUtil.Clamp((int)Math.Round(Crop.Width / rat), 1, dim);
				height = MathUtil.Clamp((int)Math.Round(Crop.Height / rat), 1, dim);
			}

			InnerRect.Width = width > 0 ? width : Math.Max((int)Math.Round(Crop.Width / wrat), 1);
			InnerRect.Height = height > 0 ? height : Math.Max((int)Math.Round(Crop.Height / hrat), 1);

			if (ResizeMode == CropScaleMode.Crop || ResizeMode == CropScaleMode.Max)
				OuterRect = InnerRect;

			if (ResizeMode == CropScaleMode.Pad)
			{
				InnerRect.X = (OuterRect.Width - InnerRect.Width) / 2;
				InnerRect.Y = (OuterRect.Height - InnerRect.Height) / 2;
			}
		}

		internal void SetSaveFormat(FileFormat containerType, bool frameHasAlpha)
		{
			if (containerType == FileFormat.Gif) // Restrict to animated only?
				SaveFormat = FileFormat.Gif;
			else if (containerType == FileFormat.Png || ((frameHasAlpha || InnerRect != OuterRect) && MatteColor.A < byte.MaxValue))
				SaveFormat = FileFormat.Png;
			else
				SaveFormat = FileFormat.Jpeg;
		}

		internal void NormalizeFrom(ImageFileInfo img)
		{
			if (FrameIndex >= img.Frames.Count || img.Frames.Count + FrameIndex < 0) throw new InvalidOperationException($"Invalid {nameof(FrameIndex)}");

			if (FrameIndex < 0)
				FrameIndex = img.Frames.Count + FrameIndex;

			var frame = img.Frames[FrameIndex];

			Fixup(frame.Width, frame.Height, OrientationMode != OrientationMode.Normalize && frame.ExifOrientation.SwapsDimensions());

			if (SaveFormat == FileFormat.Auto)
				SetSaveFormat(img.ContainerType, frame.HasAlpha);

			if (SaveFormat != FileFormat.Jpeg)
			{
				JpegSubsampleMode = ChromaSubsampleMode.Default;
				JpegQuality = 0;
			}

			if (!frame.HasAlpha && InnerRect == OuterRect)
				MatteColor = Color.Empty;

			if (!Sharpen)
				UnsharpMask = UnsharpMaskSettings.None;

			imageInfo = img;
		}

		internal string GetCacheHash()
		{
			Debug.Assert(Normalized, "Hash is only valid for normalized settings.");

			var hash = Blake2b.CreateIncrementalHasher(CacheHash.DigestLength);
			hash.Update(imageInfo?.FileSize ?? 0L);
			hash.Update(imageInfo?.FileDate.Ticks ?? 0L);
			hash.Update(FrameIndex);
			hash.Update(Width);
			hash.Update(Height);
			hash.Update(Crop.X);
			hash.Update(Crop.Y);
			hash.Update(Crop.Width);
			hash.Update(Crop.Height);
			hash.Update(MatteColor.ToArgb());
			hash.Update(Anchor);
			hash.Update(SaveFormat);
			hash.Update(BlendingMode);
			hash.Update(ResizeMode);
			hash.Update(OrientationMode);
			hash.Update(ColorProfileMode);
			hash.Update(JpegSubsampleMode);
			hash.Update(JpegQuality);
			hash.Update(HybridScaleRatio);
			hash.Update(Interpolation.WeightingFunction.ToString().AsSpan());
			hash.Update(Interpolation.Blur);
			hash.Update(UnsharpMask.Amount);
			hash.Update(UnsharpMask.Radius);
			hash.Update(UnsharpMask.Threshold);
			foreach (string m in MetadataNames ?? Enumerable.Empty<string>())
				hash.Update(m.AsSpan());

			Span<byte> hbuff = stackalloc byte[hash.DigestLength];
			hash.TryFinish(hbuff, out _);

			return CacheHash.Encode(hbuff);
		}

		internal ProcessImageSettings Clone() => (ProcessImageSettings)MemberwiseClone();
	}
}