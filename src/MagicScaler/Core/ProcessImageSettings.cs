using System;
using System.IO;
using System.Linq;
using System.Drawing;
using System.Diagnostics;
using System.Text;
using System.Text.RegularExpressions;
using System.Collections.Generic;

#if DRAWING_SHIM
using System.Drawing.Temp;
#endif

using PhotoSauce.MagicScaler.Interpolators;

namespace PhotoSauce.MagicScaler
{
	[Flags]
	public enum CropAnchor { Center = 0, Top = 1, Bottom = 2, Left = 4, Right = 8 }
	public enum CropScaleMode { Crop, Max, Stretch }
	public enum HybridScaleMode { FavorQuality, FavorSpeed, Turbo, Off }
	public enum GammaMode { Linear, sRGB }
	public enum ChromaSubsampleMode { Default = 0, Subsample420 = 1, Subsample422 = 2, Subsample444 = 3 }
	public enum FileFormat { Auto, Jpeg, Png, Png8, Gif, Bmp, Tiff, Unknown = Auto }

	public readonly struct UnsharpMaskSettings
	{
		public static readonly UnsharpMaskSettings None = default;

		public int Amount { get; }
		public double Radius { get; }
		public byte Threshold { get; }

		public UnsharpMaskSettings(int amount, double radius, byte threshold)
		{
			Amount = amount;
			Radius = radius;
			Threshold = threshold;
		}
	}

	public readonly struct InterpolationSettings
	{
		public static readonly InterpolationSettings NearestNeighbor = new InterpolationSettings(new PointInterpolator());
		public static readonly InterpolationSettings Average = new InterpolationSettings(new BoxInterpolator());
		public static readonly InterpolationSettings Linear = new InterpolationSettings(new LinearInterpolator());
		public static readonly InterpolationSettings Hermite = new InterpolationSettings(new CubicInterpolator(0d, 0d));
		public static readonly InterpolationSettings Quadratic = new InterpolationSettings(new QuadraticInterpolator());
		public static readonly InterpolationSettings Mitchell = new InterpolationSettings(new CubicInterpolator(1d/3d, 1d/3d));
		public static readonly InterpolationSettings CatmullRom = new InterpolationSettings(new CubicInterpolator());
		public static readonly InterpolationSettings Cubic = new InterpolationSettings(new CubicInterpolator(0d, 1d));
		public static readonly InterpolationSettings CubicSmoother = new InterpolationSettings(new CubicInterpolator(0d, 0.625), 1.15);
		public static readonly InterpolationSettings Lanczos = new InterpolationSettings(new LanczosInterpolator());
		public static readonly InterpolationSettings Spline36 = new InterpolationSettings(new Spline36Interpolator());

		private readonly double blur;
		public double Blur => WeightingFunction == null ? default : WeightingFunction.Support * blur < 0.5 ? 1d : blur;

		public IInterpolator WeightingFunction { get; }

		public InterpolationSettings(IInterpolator weighting) : this(weighting, 1d) { }

		public InterpolationSettings(IInterpolator weighting, double blur)
		{
			if (blur < 0.5 || blur > 1.5) throw new ArgumentOutOfRangeException(nameof(blur), "Value must be between 0.5 and 1.5");

			WeightingFunction = weighting ?? throw new ArgumentNullException(nameof(weighting));
			this.blur = blur;
		}
	}

	public sealed class ProcessImageSettings
	{
		private static readonly Regex cropExpression = new Regex(@"^(\d+,){3}\d+$", RegexOptions.Compiled);
		private static readonly Regex anchorExpression = new Regex(@"^(top|middle|bottom)?\-?(left|center|right)?$", RegexOptions.Compiled | RegexOptions.IgnoreCase);
		private static readonly Regex subsampleExpression = new Regex(@"^4(20|22|44)$", RegexOptions.Compiled);
		private static readonly Regex hexColorExpression = new Regex(@"^[0-9A-Fa-f]{6}$", RegexOptions.Compiled);
		private static readonly ProcessImageSettings empty = new ProcessImageSettings();

		private int width;
		private int height;
		private InterpolationSettings interpolation;
		private UnsharpMaskSettings unsharpMask;
		private int jpegQuality;
		private ChromaSubsampleMode jpegSubsampling;
		private ImageFileInfo imageInfo;

		public int FrameIndex { get; set; }
		public double DpiX { get; set; } = 96d;
		public double DpiY { get; set; } = 96d;
		public bool Sharpen { get; set; } = true;
		public CropScaleMode ResizeMode { get; set; }
		public Rectangle Crop { get; set; }
		public CropAnchor Anchor { get; set; }
		public FileFormat SaveFormat { get; set; }
		public Color MatteColor { get; set; }
		public HybridScaleMode HybridMode { get; set; }
		public GammaMode BlendingMode { get; set; }
		public IEnumerable<string> MetadataNames { get; set; }

		internal bool Normalized => imageInfo != null;

		internal bool IsEmpty =>
			width           == empty.width           &&
			height          == empty.height          &&
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

		public int Width
		{
			get => width;
			set
			{
				if (value < 0) throw new ArgumentOutOfRangeException(nameof(Width), "Value must be >= 0");
				width = value;
			}
		}

		public int Height
		{
			get => height;
			set
			{
				if (value < 0) throw new ArgumentOutOfRangeException(nameof(Height), "Value must be >= 0");
				height = value;
			}
		}

		public bool IndexedColor => SaveFormat == FileFormat.Png8 || SaveFormat == FileFormat.Gif;

		public double ScaleRatio => Math.Max(Width > 0 ? (double)Crop.Width / Width : 0d, Height > 0 ? (double)Crop.Height / Height : 0d);

		public double HybridScaleRatio
		{
			get
			{
				if (HybridMode == HybridScaleMode.Off || ScaleRatio < 2d)
					return 1d;

				double sr = ScaleRatio / (HybridMode == HybridScaleMode.FavorQuality ? 3d : HybridMode == HybridScaleMode.FavorSpeed ? 2d : 1d);

				return Math.Pow(2d, Math.Floor(Math.Log(sr, 2d)));
			}
		}

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

		public ChromaSubsampleMode JpegSubsampleMode
		{
			get
			{
				if (jpegSubsampling != ChromaSubsampleMode.Default)
					return jpegSubsampling;

				return JpegQuality >= 95 ? ChromaSubsampleMode.Subsample444 : JpegQuality >= 91 ? ChromaSubsampleMode.Subsample422 : ChromaSubsampleMode.Subsample420;
			}
			set
			{
				jpegSubsampling = value;
			}
		}

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
			set
			{
				interpolation = value;
			}
		}

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
			set
			{
				unsharpMask = value;
			}
		}

		public static ProcessImageSettings FromDictionary(IDictionary<string, string> dic)
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

			if (cropExpression.IsMatch(dic.GetValueOrDefault("crop") ?? string.Empty))
			{
				string[] ps = dic["crop"].Split(',');
				s.Crop = new Rectangle(int.Parse(ps[0]), int.Parse(ps[1]), int.Parse(ps[2]), int.Parse(ps[3]));
			}

			foreach (var group in anchorExpression.Match(dic.GetValueOrDefault("anchor") ?? string.Empty).Groups.Cast<Group>())
			{
				if (Enum.TryParse(group.Value, true, out CropAnchor anchor))
					s.Anchor |= anchor;
			}

			foreach (var cap in subsampleExpression.Match(dic.GetValueOrDefault("subsample") ?? string.Empty).Captures.Cast<Capture>())
				s.JpegSubsampleMode = Enum.TryParse(string.Concat("Subsample", cap.Value), true, out ChromaSubsampleMode csub) ? csub : s.JpegSubsampleMode;

			string color = dic.GetValueOrDefault("bgcolor") ?? dic.GetValueOrDefault("bg");
			if (color != null)
			{
				if (hexColorExpression.IsMatch(color))
					color = string.Concat('#', color);

				try { s.MatteColor = (Color)(new ColorConverter()).ConvertFromString(color); } catch { }
			}

			string filter = dic.GetValueOrDefault("filter")?.ToLower();
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

		internal void Fixup(int inWidth, int inHeight, bool swapDimensions = false)
		{
			int imgWidth = swapDimensions ? inHeight : inWidth;
			int imgHeight = swapDimensions ? inWidth : inHeight;

			var whole = new Rectangle(0, 0, imgWidth, imgHeight);
			bool needsCrop = Crop.IsEmpty || !Crop.IntersectsWith(whole);

			if (Width == 0 || Height == 0)
				ResizeMode = CropScaleMode.Crop;

			if (Width == 0 && Height == 0)
			{
				Crop = needsCrop ? whole : Crop;
				Crop.Intersect(whole);
				Width = Crop.Width;
				Height = Crop.Height;
				return;
			}

			if (!needsCrop || ResizeMode != CropScaleMode.Crop)
				Anchor = CropAnchor.Center;

			int wwin = imgWidth, hwin = imgHeight;
			double wrat = Width > 0 ? (double)wwin / Width : (double)hwin / Height;
			double hrat = Height > 0 ? (double)hwin / Height : wrat;

			if (needsCrop)
			{
				if (ResizeMode == CropScaleMode.Crop)
				{
					double rat = Math.Min(wrat, hrat);
					hwin = Height > 0 ? MathUtil.Clamp((int)Math.Ceiling(rat * Height), 1, imgHeight) : hwin;
					wwin = Width > 0 ? MathUtil.Clamp((int)Math.Ceiling(rat * Width), 1, imgWidth) : wwin;

					int left = Anchor.HasFlag(CropAnchor.Left) ? 0 : Anchor.HasFlag(CropAnchor.Right) ? (imgWidth - wwin) : ((imgWidth - wwin) / 2);
					int top = Anchor.HasFlag(CropAnchor.Top) ? 0 : Anchor.HasFlag(CropAnchor.Bottom) ? (imgHeight - hwin) : ((imgHeight - hwin) / 2);

					Crop = new Rectangle(left, top, wwin, hwin);

					Width = Width > 0 ? Width : Math.Max((int)Math.Round(imgWidth / wrat), 1);
					Height = Height > 0 ? Height : Math.Max((int)Math.Round(imgHeight / hrat), 1);
				}
				else
				{
					Crop = whole;

					if (ResizeMode == CropScaleMode.Max)
					{
						double rat = Math.Max(wrat, hrat);
						int dim = Math.Max(Width, Height);

						Width = MathUtil.Clamp((int)Math.Round(imgWidth / rat), 1, dim);
						Height = MathUtil.Clamp((int)Math.Round(imgHeight / rat), 1, dim);
					}
				}
			}

			Crop.Intersect(whole);

			wrat = Width > 0 ? (double)Crop.Width / Width : (double)Crop.Height / Height;
			hrat = Height > 0 ? (double)Crop.Height / Height : wrat;

			Width = Width > 0 ? Width : Math.Max((int)Math.Round(Crop.Width / wrat), 1);
			Height = Height > 0 ? Height : Math.Max((int)Math.Round(Crop.Height / hrat), 1);
		}

		internal void SetSaveFormat(FileFormat containerType, bool frameHasAlpha)
		{
			if (containerType == FileFormat.Gif) // Restrict to animated only?
				SaveFormat = FileFormat.Gif;
			else if (containerType == FileFormat.Png || (frameHasAlpha && MatteColor.A < byte.MaxValue))
				SaveFormat = FileFormat.Png;
			else
				SaveFormat = FileFormat.Jpeg;
		}

		internal void NormalizeFrom(ImageFileInfo img)
		{
			if (FrameIndex >= img.Frames.Length || img.Frames.Length + FrameIndex < 0) throw new InvalidOperationException($"Invalid {nameof(FrameIndex)}");

			if (FrameIndex < 0)
				FrameIndex = img.Frames.Length + FrameIndex;

			var frame = img.Frames[FrameIndex];

			Fixup(frame.Width, frame.Height);

			if (SaveFormat == FileFormat.Auto)
				SetSaveFormat(img.ContainerType, frame.HasAlpha);

			if (SaveFormat != FileFormat.Jpeg)
			{
				JpegSubsampleMode = ChromaSubsampleMode.Default;
				JpegQuality = 0;
			}

			if (!frame.HasAlpha)
				MatteColor = Color.Empty;

			if (!Sharpen)
				UnsharpMask = UnsharpMaskSettings.None;

			imageInfo = img;
		}

		internal string GetCacheHash()
		{
			Debug.Assert(Normalized, "Hash is only valid for normalized settings.");

			using (var bw = new BinaryWriter(new MemoryStream(256), Encoding.UTF8))
			{
				bw.Write(imageInfo?.FileSize ?? 0L);
				bw.Write(imageInfo?.FileDate.Ticks ?? 0L);
				bw.Write(FrameIndex);
				bw.Write(Width);
				bw.Write(Height);
				bw.Write((int)SaveFormat);
				bw.Write((int)BlendingMode);
				bw.Write((int)ResizeMode);
				bw.Write((int)Anchor);
				bw.Write(Crop.X);
				bw.Write(Crop.Y);
				bw.Write(Crop.Width);
				bw.Write(Crop.Height);
				bw.Write(MatteColor.A);
				bw.Write(MatteColor.R);
				bw.Write(MatteColor.G);
				bw.Write(MatteColor.B);
				bw.Write(HybridScaleRatio);
				bw.Write(Interpolation.WeightingFunction.ToString());
				bw.Write(Interpolation.Blur);
				bw.Write(UnsharpMask.Amount);
				bw.Write(UnsharpMask.Radius);
				bw.Write(UnsharpMask.Threshold);
				bw.Write(JpegQuality);
				bw.Write((int)JpegSubsampleMode);
				foreach (string m in MetadataNames ?? Enumerable.Empty<string>())
					bw.Write(m);

				((MemoryStream)bw.BaseStream).TryGetBuffer(out var buff);
				return CacheHash.Create(buff);
			}
		}

		internal ProcessImageSettings Clone() => (ProcessImageSettings)MemberwiseClone();
	}
}