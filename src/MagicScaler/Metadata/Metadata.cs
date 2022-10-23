// Copyright © Clinton Ingram and Contributors.  Licensed under the MIT License.

using System;
using System.Linq;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;

using TerraFX.Interop.Windows;

namespace PhotoSauce.MagicScaler;

internal sealed class NoopMetadataSource : IMetadataSource
{
	public static readonly IMetadataSource Instance = new NoopMetadataSource();

	public bool TryGetMetadata<T>([NotNullWhen(true)] out T? metadata) where T : IMetadata
	{
		metadata = default;
		return false;
	}
}

internal readonly unsafe struct WicFrameMetadataReader : IMetadata
{
	public readonly IWICMetadataQueryReader* Reader;
	public readonly IEnumerable<string> CopyNames;

	public WicFrameMetadataReader(IWICMetadataQueryReader* reader, IEnumerable<string> names)
	{
		Reader = reader;
		CopyNames = names;
	}
}

internal interface IIccProfileSource : IMetadata
{
	int ProfileLength { get; }
	void CopyProfile(Span<byte> dest);
}

internal interface IExifSource : IMetadata
{
	int ExifLength { get; }
	void CopyExif(Span<byte> dest);
}

internal readonly record struct ColorProfileMetadata(ColorProfile Profile) : IMetadata
{
	public byte[] Embed => (Profile as CurveProfile)?.CompactProfile ?? Profile.ProfileBytes;
}

internal readonly record struct OrientationMetadata(Orientation Orientation) : IMetadata
{
	public static OrientationMetadata FromExif(IExifSource exifsrc)
	{
		using var buff = BufferPool.RentLocal<byte>(exifsrc.ExifLength);
		exifsrc.CopyExif(buff.Span);

		var orient = Orientation.Normal;
		var rdr = ExifReader.Create(buff.Span);
		while (rdr.MoveNext())
		{
			ref readonly var tag = ref rdr.Current;
			if (tag.ID == ExifTags.Tiff.Orientation)
				orient = ((Orientation)rdr.CoerceValue<int>(tag)).Clamp();
		}

		return new OrientationMetadata(orient);
	}
}

internal readonly record struct ResolutionMetadata(Rational ResolutionX, Rational ResolutionY, ResolutionUnit Units) : IMetadata
{
	public static readonly ResolutionMetadata Default = new(new(96, 1), new(96, 1), ResolutionUnit.Inch);
	public static readonly ResolutionMetadata ExifDefault = new(new(72, 1), new(72, 1), ResolutionUnit.Inch);

	public static ResolutionMetadata FromExif(IExifSource exifsrc)
	{
		using var buff = BufferPool.RentLocal<byte>(exifsrc.ExifLength);
		exifsrc.CopyExif(buff.Span);

		var res = ExifDefault;
		var rdr = ExifReader.Create(buff.Span);
		while (rdr.MoveNext())
		{
			ref readonly var tag = ref rdr.Current;
			if (tag.ID == ExifTags.Tiff.ResolutionX && tag.Type == ExifType.Rational)
				res = res with { ResolutionX = rdr.GetValue<Rational>(tag) };
			else if (tag.ID == ExifTags.Tiff.ResolutionY && tag.Type == ExifType.Rational)
				res = res with { ResolutionY = rdr.GetValue<Rational>(tag) };
			else if (tag.ID == ExifTags.Tiff.ResolutionUnit)
				res = res with { Units = rdr.CoerceValue<int>(tag) switch { 3 => ResolutionUnit.Centimeter, _ => ResolutionUnit.Inch } };
		}

		return res;
	}

	public bool IsValid => ResolutionX.Denominator != 0 && ResolutionY.Denominator != 0;

	public ResolutionMetadata ToDpi() => Units switch {
		ResolutionUnit.Inch       => this,
		ResolutionUnit.Centimeter => new(((double)ResolutionX *  2.54).ToRational(), ((double)ResolutionY *  2.54).ToRational(), ResolutionUnit.Inch),
		ResolutionUnit.Meter      => new(((double)ResolutionX / 39.37).ToRational(), ((double)ResolutionY / 39.37).ToRational(), ResolutionUnit.Inch),
		_                         => new(((double)ResolutionX * 96.0 ).ToRational(), ((double)ResolutionY * 96.0 ).ToRational(), ResolutionUnit.Inch)
	};

	public ResolutionMetadata ToDpm() => Units switch {
		ResolutionUnit.Inch       => new(((double)ResolutionX *   39.37).ToRational(), ((double)ResolutionY *   39.37).ToRational(), ResolutionUnit.Meter),
		ResolutionUnit.Centimeter => new(((double)ResolutionX *  100.0 ).ToRational(), ((double)ResolutionY *  100.0 ).ToRational(), ResolutionUnit.Meter),
		ResolutionUnit.Meter      => this,
		_                         => new(((double)ResolutionX * 3779.53).ToRational(), ((double)ResolutionY * 3779.53).ToRational(), ResolutionUnit.Meter)
	};
}

internal interface IMetadataTransform
{
	void Init(IMetadataSource source);
}

internal sealed class MagicMetadataFilter : IMetadataSource
{
	private readonly PipelineContext context;

	public MagicMetadataFilter(PipelineContext ctx) => context = ctx;

	public unsafe bool TryGetMetadata<T>([NotNullWhen(true)] out T? metadata) where T : IMetadata
	{
		var source = context.ImageFrame as IMetadataSource ?? NoopMetadataSource.Instance;
		var settings = context.Settings;

		if (typeof(T) == typeof(ResolutionMetadata))
		{
			var res = new ResolutionMetadata(settings.DpiX.ToRational(), settings.DpiY.ToRational(), ResolutionUnit.Inch);
			if (settings.DpiX == default || settings.DpiY == default)
			{
				if (!source.TryGetMetadata<ResolutionMetadata>(out var sourceRes) && source.TryGetMetadata<IExifSource>(out var exifsrc))
					sourceRes = ResolutionMetadata.FromExif(exifsrc);

				res = sourceRes.IsValid ? sourceRes : ResolutionMetadata.Default;
				if (settings.DpiX != default)
					res = res.ToDpi() with { ResolutionX = settings.DpiX.ToRational() };
				if (settings.DpiY != default)
					res = res.ToDpi() with { ResolutionY = settings.DpiY.ToRational() };
			}

			metadata = (T)(object)res;
			return true;
		}

		if (typeof(T) == typeof(OrientationMetadata))
		{
			if (settings.OrientationMode == OrientationMode.Preserve)
			{
				var orient = context.ImageFrame.GetOrientation();
				if (orient != Orientation.Normal)
				{
					metadata = (T)(object)(new OrientationMetadata(orient));
					return true;
				}
			}

			metadata = default;
			return false;
		}

		if (typeof(T) == typeof(ColorProfileMetadata))
		{
			if (settings.ColorProfileMode is ColorProfileMode.NormalizeAndEmbed or ColorProfileMode.Preserve || (settings.ColorProfileMode is ColorProfileMode.Normalize && context.DestColorProfile != ColorProfile.sRGB && context.DestColorProfile != ColorProfile.sGrey))
			{
				metadata = (T)(object)(new ColorProfileMetadata(context.DestColorProfile!));
				return true;
			}

			metadata = default;
			return false;
		}

		if (typeof(T) == typeof(WicFrameMetadataReader))
		{
			var frame = source is WicPlanarCache pframe ? pframe.Frame : source as WicImageFrame;
			if (frame is not null && frame.WicMetadataReader is not null && settings.MetadataNames.Any())
			{
				metadata = (T)(object)(new WicFrameMetadataReader(frame.WicMetadataReader, settings.MetadataNames));
				return true;
			}

			metadata = default;
			return false;
		}

		if (typeof(T) == typeof(AnimationContainer))
		{
			if (context.ImageContainer is not IMetadataSource cmsrc || !cmsrc.TryGetMetadata<AnimationContainer>(out var anicnt))
				anicnt = default;

			metadata = (T)(object)anicnt;
			return true;
		}

		return source.TryGetMetadata(out metadata);
	}
}

internal static class MetadataExtensions
{
	public static Orientation GetOrientation(this IImageFrame frame)
	{
		if (frame is IMetadataSource meta)
		{
			if (meta.TryGetMetadata<OrientationMetadata>(out var orient))
				return orient.Orientation;
			else if (meta.TryGetMetadata<IExifSource>(out var exif))
				return OrientationMetadata.FromExif(exif).Orientation;
		}

		return Orientation.Normal;
	}
}

/// <summary>A <a href="https://en.wikipedia.org/wiki/Rational_number">rational number</a>, as defined by an integer <paramref name="Numerator" /> and <paramref name="Denominator" />.</summary>
/// <param name="Numerator">The numerator of the rational number.</param>
/// <param name="Denominator">The denominator of the rational number.</param>
internal readonly record struct Rational(uint Numerator, uint Denominator)
{
	public Rational NormalizeTo(uint newDenominator) => Denominator == newDenominator ? this : new((uint)((double)this * newDenominator), newDenominator);

	public override string ToString() => $"{Numerator}/{Denominator}";

	public static implicit operator Rational((uint n, uint d) f) => new(f.n, f.d);
	public static explicit operator double(Rational r) => r.Denominator is 0 ? double.NaN : (double)r.Numerator / r.Denominator;
}

/// <summary>A signed <a href="https://en.wikipedia.org/wiki/Rational_number">rational number</a>, as defined by an integer <paramref name="Numerator" /> and <paramref name="Denominator" />.</summary>
/// <param name="Numerator">The numerator of the rational number.</param>
/// <param name="Denominator">The denominator of the rational number.</param>
internal readonly record struct SRational(int Numerator, int Denominator)
{
	public override string ToString() => $"{Numerator}/{Denominator}";

	public static implicit operator SRational((int n, int d) f) => new(f.n, f.d);
	public static explicit operator double(SRational r) => r.Denominator is 0 ? double.NaN : (double)r.Numerator / r.Denominator;
}

/// <summary>Defines global/container metadata for a sequence of animated frames.</summary>
internal readonly struct AnimationContainer : IMetadata
{
	/// <summary>The width of the animation's logical screen.  Values less than 1 imply the width is equal to the width of the first frame.</summary>
	public readonly int ScreenWidth;

	/// <summary>The height of the animation's logical screen.  Values less than 1 imply the height is equal to the height of the first frame.</summary>
	public readonly int ScreenHeight;

	/// <summary>The total number of frames in the animation, disregarding any <see cref="IMultiFrameDecoderOptions.FrameRange" /> decoder option.</summary>
	/// <remarks>Coalescing the animation may require frames that precede the <see cref="IMultiFrameDecoderOptions.FrameRange" />.  The decoder must allow this.</remarks>
	public readonly int FrameCount;

	/// <summary>The number of times to loop the animation.  Values less than 1 imply inifinte looping.</summary>
	public readonly int LoopCount;

	/// <summary>The background color to restore when a frame's disposal method is RestoreBackground, in ARGB order.</summary>
	public readonly int BackgroundColor;

	/// <summary>The pixel aspect ratio of the animation.</summary>
	/// <remarks>This property is used only for GIF.  Valid range is 0.25 to 4.21875, where 0 or 1 means square pixels.</remarks>
	public readonly float PixelAspectRatio;

	/// <summary>True if this animation requires a persistent screen buffer onto which frames are rendered, otherwise false.</summary>
	public readonly bool RequiresScreenBuffer;

	public AnimationContainer(int screenWidth, int screenHeight, int frameCount, int loopCount = 0, int bgColor = 0, float pixelAspect = 1f, bool screenBuffer = false) =>
		(ScreenWidth, ScreenHeight, FrameCount, LoopCount, BackgroundColor, PixelAspectRatio, RequiresScreenBuffer) = (screenWidth, screenHeight, frameCount, loopCount, bgColor, pixelAspect, screenBuffer);
}

/// <summary>Defines metadata for a single frame within an animated image sequence.</summary>
internal readonly struct AnimationFrame : IMetadata
{
	// Rather arbitrary default of NTSC film speed
	internal static AnimationFrame Default = new(default, default, new Rational(1001, 24000), default, default);

	/// <summary>The horizontal offset of the frame's content, relative to the logical screen.</summary>
	public readonly int OffsetLeft;

	/// <summary>The vertical offset of the frame's content, relative to the logical screen.</summary>
	public readonly int OffsetTop;

	/// <summary>The amount of time, in seconds, the frame should be displayed.</summary>
	/// <remarks>For animated GIF output, the denominator will be normalized to <c>100</c>.</remarks>
	public readonly Rational Duration;

	/// <summary>The disposition of the frame.</summary>
	public readonly FrameDisposalMethod Disposal;

	/// <summary>True to indicate the frame contains transparent pixels, otherwise false.</summary>
	public readonly bool HasAlpha;

	public AnimationFrame(int offsetLeft, int offsetTop, Rational duration, FrameDisposalMethod disposal, bool alpha) =>
		(OffsetLeft, OffsetTop, Duration, Disposal, HasAlpha) = (offsetLeft, offsetTop, duration, disposal, alpha);
}
