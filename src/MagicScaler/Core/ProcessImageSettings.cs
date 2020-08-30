using System;
using System.Linq;
using System.Drawing;
using System.Collections.Generic;
using System.Text.RegularExpressions;

using Blake2Fast;
using PhotoSauce.MagicScaler.Interpolators;

namespace PhotoSauce.MagicScaler
{
	/// <summary>Defines settings for an <a href="https://en.wikipedia.org/wiki/Unsharp_masking">Unsharp Masking</a> operation.</summary>
	/// <remarks>These settings are designed to function similarly to the Unsharp Mask settings in Photoshop.</remarks>
	public readonly struct UnsharpMaskSettings
	{
		/// <summary>No sharpening.</summary>
		public static readonly UnsharpMaskSettings None;

		/// <summary>The amount of sharpening.  This value represents a percentage of the difference between the blurred image and the original image.</summary>
		/// <value>Typical values are between <c>25</c> and <c>200</c>.</value>
		public int Amount { get; }
		/// <summary>The radius (sigma) of the gaussian blur used for the mask.  This value determines the size of details that are sharpened.</summary>
		/// <value>Typical values are between <c>0.3</c> and <c>3.0</c>. Larger radius values can have significant performance cost.</value>
		public double Radius { get; }
		/// <summary>The minimum brightness change required for a pixel to be modified by the filter.</summary>
		/// <remarks>When using larger <see cref="Radius"/> or <see cref="Amount"/> values, a larger <see cref="Threshold"/> value can ensure lines are sharpened while textures are not.</remarks>
		/// <value>Typical values are between <c>0</c> and <c>10</c>.</value>
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
		public static readonly InterpolationSettings NearestNeighbor = new (new PointInterpolator());
		/// <summary>A predefined <see cref="BoxInterpolator" />.</summary>
		public static readonly InterpolationSettings Average = new (new BoxInterpolator());
		/// <summary>A predefined <see cref="LinearInterpolator" />.  Also known as Bilinear in some software.</summary>
		public static readonly InterpolationSettings Linear = new (new LinearInterpolator());
		/// <summary>A predefined Hermite (b=0, c=0) <see cref="CubicInterpolator" />.</summary>
		public static readonly InterpolationSettings Hermite = new (new CubicInterpolator(0d, 0d));
		/// <summary>A predefined <see cref="QuadraticInterpolator" /> with properties similar to a Catmull-Rom Cubic.</summary>
		public static readonly InterpolationSettings Quadratic = new (new QuadraticInterpolator());
		/// <summary>A predefined Mitchell-Netravali (b=1/3, c=1/3) Cubic interpolator.</summary>
		public static readonly InterpolationSettings Mitchell = new (new CubicInterpolator(1d/3d, 1d/3d));
		/// <summary>A predefined Catmull-Rom (b=0, c=1/2) <see cref="CubicInterpolator" />.</summary>
		public static readonly InterpolationSettings CatmullRom = new (new CubicInterpolator());
		/// <summary>A predefined Cardinal (b=0, c=1) <see cref="CubicInterpolator" />.  Also known as Bicubic in some software.</summary>
		public static readonly InterpolationSettings Cubic = new (new CubicInterpolator(0d, 1d));
		/// <summary>A predefined smoothing <see cref="CubicInterpolator" />.  Similar to Photoshop's "Bicubic Smoother".</summary>
		public static readonly InterpolationSettings CubicSmoother = new (new CubicInterpolator(0d, 0.625), 1.15);
		/// <summary>A predefined 3-lobed <see cref="LanczosInterpolator" />.</summary>
		public static readonly InterpolationSettings Lanczos = new (new LanczosInterpolator());
		/// <summary>A predefined <see cref="Spline36Interpolator" />.</summary>
		public static readonly InterpolationSettings Spline36 = new (new Spline36Interpolator());

		private readonly double blur;

		/// <summary>A blur value stretches or compresses the input window of an interpolation function.  This value represents a fraction of the normal window size, with <c>1.0</c> being normal.</summary>
		/// <value>Supported values: <c>0.5</c> to <c>1.5</c>.  Values less than <c>1.0</c> can cause unpleasant artifacts.</value>
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
		private static readonly Lazy<Regex> cropExpression = new (() => new Regex(@"^(\d+,){2}-?\d+,-?\d+$", RegexOptions.Compiled));
		private static readonly Lazy<Regex> cropBasisExpression = new (() => new Regex(@"^\d+,\d+$", RegexOptions.Compiled));
		private static readonly Lazy<Regex> anchorExpression = new (() => new Regex(@"^(top|middle|bottom)?\-?(left|center|right)?$", RegexOptions.Compiled | RegexOptions.IgnoreCase));
		private static readonly Lazy<Regex> subsampleExpression = new (() => new Regex(@"^4(20|22|44)$", RegexOptions.Compiled));
		private static readonly ProcessImageSettings empty = new ();

		private InterpolationSettings interpolation;
		private UnsharpMaskSettings unsharpMask;
		private int jpegQuality;
		private ChromaSubsampleMode jpegSubsampling;
		private ImageFileInfo? imageInfo;
		internal Size InnerSize;
		internal Size OuterSize;
		internal bool AutoCrop;

		internal bool IndexedColor => SaveFormat == FileFormat.Png8 || SaveFormat == FileFormat.Gif;

		internal bool IsNormalized => imageInfo is not null;

		internal bool IsEmpty =>
			OuterSize       == empty.OuterSize       &&
			jpegQuality     == empty.jpegQuality     &&
			jpegSubsampling == empty.jpegSubsampling &&
			FrameIndex      == empty.FrameIndex      &&
			DpiX            == empty.DpiX            &&
			DpiY            == empty.DpiY            &&
			Crop            == empty.Crop            &&
			CropBasis       == empty.CropBasis       &&
			SaveFormat      == empty.SaveFormat      &&
			MatteColor      == empty.MatteColor      &&
			unsharpMask.Equals(empty.unsharpMask)
		;

		internal Rectangle InnerRect => new Rectangle {
			X = (OuterSize.Width - InnerSize.Width) / 2,
			Y = (OuterSize.Height - InnerSize.Height) / 2,
			Width = InnerSize.Width,
			Height = InnerSize.Height
		};

		internal double ScaleRatio =>
			Math.Min(InnerSize.Width > 0 ? (double)Crop.Width / InnerSize.Width : 0d, InnerSize.Height > 0 ? (double)Crop.Height / InnerSize.Height : 0d);

		/// <summary>The 0-based index of the image frame to process from within the container.</summary>
		/// <value>Default value: <c>0</c></value>
		public int FrameIndex { get; set; }
		/// <summary>The horizontal DPI of the output image.  A value of <c>0</c> will preserve the DPI of the input image.</summary>
		/// <remarks>This affects the image metadata only.  Not all image formats support a DPI setting and most applications will ignore it.</remarks>
		/// <value>Default value: <c>96</c></value>
		public double DpiX { get; set; } = 96d;
		/// <summary>The vertical DPI of the output image.  A value of <c>0</c> will preserve the DPI of the input image.</summary>
		/// <remarks>This affects the image metadata only.  Not all image formats support a DPI setting and most applications will ignore it.</remarks>
		/// <value>Default value: <c>96</c></value>
		public double DpiY { get; set; } = 96d;
		/// <summary>Determines whether automatic sharpening is applied during processing.  The sharpening settings are controlled by the <see cref="UnsharpMask"/> property.</summary>
		/// <value>Default value: <c>true</c></value>
		public bool Sharpen { get; set; } = true;
		/// <summary>Determines how automatic scaling and cropping is performed.</summary>
		/// <remarks>Auto-cropping is performed only if a <see cref="Crop" /> value is not explicitly set.</remarks>
		/// <value>Default value: <see cref="CropScaleMode.Crop" /></value>
		public CropScaleMode ResizeMode { get; set; }
		/// <summary>Defines the bounding rectangle to use from the input image.  Can be calculated automatically depending on <see cref="CropScaleMode" />.</summary>
		/// <include file='Docs/Remarks.xml' path='doc/member[@name="Crop"]/*'/>
		/// <value>Default value: <see cref="Rectangle.Empty" /></value>
		public Rectangle Crop { get; set; }
		/// <summary>Defines the dimensions on which the <see cref="Crop" /> rectangle is based.  If this value is empty, <see cref="Crop" /> values are based on the actual input image dimensions.</summary>
		/// <value>Default value: <see cref="Size.Empty" /></value>
		public Size CropBasis { get; set; }
		/// <summary>Determines which part of the image is preserved when automatic cropping is performed.</summary>
		/// <include file='Docs/Remarks.xml' path='doc/member[@name="Anchor"]/*'/>
		/// <value>Default value: <see cref="CropAnchor.Center" /></value>
		public CropAnchor Anchor { get; set; }
		/// <summary>Determines the container format of the output image. A value of <see cref="FileFormat.Auto" /> will choose the output codec based on the input image type.</summary>
		/// <value>Default value: <see cref="FileFormat.Auto" /></value>
		public FileFormat SaveFormat { get; set; }
		/// <summary>The background color to use when converting to a non-transparent format and the fill color for <see cref="CropScaleMode.Pad" /> mode.</summary>
		/// <include file='Docs/Remarks.xml' path='doc/member[@name="MatteColor"]/*'/>
		/// <value>Default value: <see cref="Color.Empty" /></value>
		public Color MatteColor { get; set; }
		/// <summary>Determines whether Hybrid Scaling is allowed to be used to improve performance.</summary>
		/// <include file='Docs/Remarks.xml' path='doc/member[@name="HybridMode"]/*'/>
		/// <value>Default value: <see cref="HybridScaleMode.FavorQuality" /></value>
		public HybridScaleMode HybridMode { get; set; }
		/// <summary>Determines whether pixel blending is done using linear RGB or gamma-companded R'G'B'.</summary>
		/// <remarks>Linear processing will yield better quality in almost all cases but with a performance cost.</remarks>
		/// <value>Default value: <see cref="GammaMode.Linear" /></value>
		public GammaMode BlendingMode { get; set; }
		/// <summary>Determines whether automatic orientation correction is performed.</summary>
		/// <value>Default value: <see cref="OrientationMode.Normalize" /></value>
		public OrientationMode OrientationMode { get; set; }
		/// <summary>Determines whether automatic colorspace conversion is performed.</summary>
		/// <value>Default value: <see cref="ColorProfileMode.Normalize" /></value>
		public ColorProfileMode ColorProfileMode { get; set; }
		/// <summary>A list of metadata policy names or explicit metadata paths to be copied from the input image to the output image.</summary>
		/// <include file='Docs/Remarks.xml' path='doc/member[@name="MetadataNames"]/*'/>
		/// <value>Default value: <see cref="Enumerable.Empty" /></value>
		public IEnumerable<string> MetadataNames { get; set; } = Enumerable.Empty<string>();

		/// <summary>The width of the output image in pixels.  If auto-cropping is enabled, a value of <c>0</c> will set the width automatically based on the output height.</summary>
		/// <remarks>If <see cref="Width"/> and <see cref="Height"/> are both set to <c>0</c>, no resizing will be performed but a crop may still be applied.</remarks>
		/// <value>Default value: <c>0</c></value>
		public int Width
		{
			get => OuterSize.Width;
			set
			{
				if (value < 0) throw new ArgumentOutOfRangeException(nameof(Width), "Value must be >= 0");
				OuterSize.Width = value;
			}
		}

		/// <summary>The height of the output image in pixels.  If auto-cropping is enabled, a value of <c>0</c> will set the height automatically based on the output width.</summary>
		/// <remarks>If <see cref="Width"/> and <see cref="Height"/> are both set to <c>0</c>, no resizing will be performed but a crop may still be applied.</remarks>
		/// <value>Default value: <c>0</c></value>
		public int Height
		{
			get => OuterSize.Height;
			set
			{
				if (value < 0) throw new ArgumentOutOfRangeException(nameof(Height), "Value must be >= 0");
				OuterSize.Height = value;
			}
		}

		/// <summary>The calculated ratio for the lower-quality portion of a hybrid scaling operation.</summary>
		/// <value>Calculated based on <see cref="HybridScaleMode" /> and the ratio of input image size to output image size</value>
		public int HybridScaleRatio
		{
			get
			{
				if (HybridMode == HybridScaleMode.Off)
					return 1;

				double sr = ScaleRatio / (HybridMode == HybridScaleMode.FavorQuality ? 3d : HybridMode == HybridScaleMode.FavorSpeed ? 2d : 1d);

				return (int)Math.Pow(2d, Math.Floor(Math.Log(sr, 2d))).Clamp(1d, 32d);
			}
		}

		/// <summary>The quality setting to use for JPEG output.</summary>
		/// <remarks>If this value is set to <c>0</c>, the quality level will be set automatically according to the output image dimensions. Typically, this value should be <c>80</c> or greater if set explicitly.</remarks>
		/// <value>Default value: calculated based on output image size</value>
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
		/// <remarks>If this value is set to <see cref="ChromaSubsampleMode.Default"/>, the chroma subsampling will be set automatically based on the <see cref="JpegQuality"/> setting.</remarks>
		/// <value>Default value: calculated based on <see cref="JpegQuality"/></value>
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
		/// <remarks>If this value is unset, the algorithm will be chosen automatically to maximize image quality and performance based on the ratio of input image size to output image size.</remarks>
		/// <value>Default value: calculated based on resize ratio</value>
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

		/// <summary>Settings for automatic post-resize sharpening.</summary>
		/// <remarks>If this value is unset, the settings will be chosen automatically to maximize image quality based on the ratio of input image size to output image size.</remarks>
		/// <value>Default value: calculated based on resize ratio</value>
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

			if (cropBasisExpression.Value.IsMatch(dic.GetValueOrDefault("cropbasis") ?? string.Empty))
			{
				var ps = dic["cropbasis"]!.Split(',');
				s.CropBasis = new Size(int.Parse(ps[0]), int.Parse(ps[1]));
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

			if (!Crop.IsEmpty && !CropBasis.IsEmpty)
			{
				var crop = Crop;
				if (CropBasis.Width > 0)
				{
					double xrat = (double)imgWidth / CropBasis.Width;
					crop.X = (int)Math.Floor(Crop.Left * xrat);
					crop.Width = Math.Min((int)Math.Ceiling(Crop.Width * xrat), imgWidth - crop.X);
				}
				if (CropBasis.Height > 0)
				{
					double yrat = (double)imgHeight / CropBasis.Height;
					crop.Y = (int)Math.Floor(Crop.Top * yrat);
					crop.Height = Math.Min((int)Math.Ceiling(Crop.Height * yrat), imgHeight - crop.Y);
				}

				Crop = crop;
				CropBasis = new Size(imgWidth, imgHeight);
			}

			if (!Crop.IsEmpty && (Crop.Width <= 0 || Crop.Height <= 0))
			{
				var crop = Crop;
				if (crop.Width <= 0)
				{
					int rem = Math.Max(1, imgWidth - crop.X);
					crop.Width = crop.Width == 0 ? rem : (imgWidth - crop.X + crop.Width).Clamp(1, rem);
				}
				if (crop.Height <= 0)
				{
					int rem = Math.Max(1, imgHeight - crop.Y);
					crop.Height = crop.Height == 0 ? rem : (imgHeight - crop.Y + crop.Height).Clamp(1, rem);
				}

				Crop = crop;
			}

			var whole = new Rectangle(0, 0, imgWidth, imgHeight);
			AutoCrop = Crop.IsEmpty || Crop != Rectangle.Intersect(whole, Crop);

			if ((Width == 0 || Height == 0) && (ResizeMode == CropScaleMode.Pad || ResizeMode == CropScaleMode.Stretch))
				ResizeMode = CropScaleMode.Crop;

			if (OuterSize.IsEmpty)
			{
				Crop = AutoCrop ? whole : Crop;
				OuterSize = InnerSize = new Size(Crop.Width, Crop.Height);
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

			if (width == 0 && (ResizeMode == CropScaleMode.Contain || ResizeMode == CropScaleMode.Max))
				width = int.MaxValue;
			if (height == 0 && (ResizeMode == CropScaleMode.Contain || ResizeMode == CropScaleMode.Max))
				height = int.MaxValue;

			wrat = width > 0 ? (double)Crop.Width / width : (double)Crop.Height / height;
			hrat = height > 0 ? (double)Crop.Height / height : wrat;

			if (ResizeMode == CropScaleMode.Contain || ResizeMode == CropScaleMode.Max || ResizeMode == CropScaleMode.Pad)
			{
				int dim = Math.Max(width, height);

				double rat = Math.Max(wrat, hrat);
				if (ResizeMode == CropScaleMode.Max)
					rat = Math.Max(rat, 1d);

				width = MathUtil.Clamp((int)Math.Round(Crop.Width / rat), 1, dim);
				height = MathUtil.Clamp((int)Math.Round(Crop.Height / rat), 1, dim);
			}

			InnerSize.Width = width > 0 ? width : Math.Max((int)Math.Round(Crop.Width / wrat), 1);
			InnerSize.Height = height > 0 ? height : Math.Max((int)Math.Round(Crop.Height / hrat), 1);

			if (ResizeMode == CropScaleMode.Crop || ResizeMode == CropScaleMode.Contain || ResizeMode == CropScaleMode.Max)
				OuterSize = InnerSize;
		}

		internal void SetSaveFormat(FileFormat containerType, bool frameHasAlpha)
		{
			if (containerType == FileFormat.Gif) // Restrict to animated only?
				SaveFormat = FileFormat.Gif;
			else if (containerType == FileFormat.Png || ((frameHasAlpha || InnerSize != OuterSize) && MatteColor.A < byte.MaxValue))
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

			if (!frame.HasAlpha && InnerSize == OuterSize)
				MatteColor = Color.Empty;

			if (!Sharpen)
				UnsharpMask = UnsharpMaskSettings.None;

			if (ColorProfileMode <= ColorProfileMode.NormalizeAndEmbed && (SaveFormat == FileFormat.Bmp || SaveFormat == FileFormat.Gif))
				ColorProfileMode = ColorProfileMode.ConvertToSrgb;

			imageInfo = img;
		}

		internal string GetCacheHash()
		{
			if (imageInfo is null) throw new InvalidOperationException("Hash is only valid for normalized settings.");
			if (!(Interpolation.WeightingFunction is IUniquelyIdentifiable uif)) throw new InvalidOperationException("Hash is only valid for internal interpolators.");

			var hash = Blake2b.CreateIncrementalHasher(CacheHash.DigestLength);
			hash.Update(imageInfo.FileSize);
			hash.Update(imageInfo.FileDate.Ticks);
			hash.Update(FrameIndex);
			hash.Update(Crop);
			hash.Update(InnerSize);
			hash.Update(OuterSize);
			hash.Update(MatteColor.ToArgb());
			hash.Update(BlendingMode);
			hash.Update(OrientationMode);
			hash.Update(ColorProfileMode);
			hash.Update(HybridScaleRatio);
			hash.Update(uif.UniqueID);
			hash.Update(Interpolation.Blur);
			hash.Update(UnsharpMask);
			hash.Update(SaveFormat);

			if (SaveFormat == FileFormat.Jpeg)
			{
				hash.Update(JpegSubsampleMode);
				hash.Update(JpegQuality);
			}

			foreach (string m in MetadataNames ?? Enumerable.Empty<string>())
				hash.Update(m.AsSpan());

			var hbuff = (Span<byte>)stackalloc byte[hash.DigestLength];
			hash.Finish(hbuff);

			return CacheHash.Encode(hbuff);
		}

		internal ProcessImageSettings Clone() => (ProcessImageSettings)MemberwiseClone();
	}
}