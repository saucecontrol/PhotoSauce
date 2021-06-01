// Copyright © Clinton Ingram and Contributors.  Licensed under the MIT License.

using System.Linq;
using System.Collections.Generic;

using TerraFX.Interop;

namespace PhotoSauce.MagicScaler
{
	internal interface IMetadata
	{
		string Name { get; }
	}

	internal interface IMetadataSource
	{
		bool TryGetMetadata<T>(out T? metadata) where T : IMetadata;
	}

	internal sealed class NoopMetadataSource : IMetadataSource
	{
		public static readonly IMetadataSource Instance = new NoopMetadataSource();

		public bool TryGetMetadata<T>(out T? metadata) where T : IMetadata
		{
			metadata = default;
			return false;
		}
	}

	internal readonly unsafe struct WicFrameMetadataReader : IMetadata
	{
		public string Name => nameof(WicFrameMetadataReader);

		public readonly IWICMetadataQueryReader* Reader;
		public readonly IEnumerable<string> CopyNames;

		public WicFrameMetadataReader(IWICMetadataQueryReader* reader, IEnumerable<string> names)
		{
			Reader = reader;
			CopyNames = names;
		}
	}

	internal readonly struct BaseImageProperties : IMetadata
	{
		public string Name => nameof(BaseImageProperties);

		public readonly double DpiX;
		public readonly double DpiY;
		public readonly Orientation Orientation;
		public readonly ColorProfile? ColorProfile;

		public BaseImageProperties(double dpix, double dpiy, Orientation orientation, ColorProfile? profile) =>
			(DpiX, DpiY, Orientation, ColorProfile) = (dpix, dpiy, orientation, profile);
	}

	internal interface IMetadataTransform
	{
		void Init(IMetadataSource source);
	}

	internal sealed class MagicMetadataFilter : IMetadataSource
	{
		private readonly PipelineContext context;

		public MagicMetadataFilter(PipelineContext ctx) => context = ctx;

		public unsafe bool TryGetMetadata<T>(out T? metadata) where T : IMetadata
		{
			if (typeof(T) == typeof(BaseImageProperties))
			{
				var settings = context.Settings;

				double dpix = settings.DpiX > 0d ? settings.DpiX : context.ImageFrame.DpiX;
				double dpiy = settings.DpiY > 0d ? settings.DpiY : context.ImageFrame.DpiY;
				bool writeOrientation = settings.OrientationMode == OrientationMode.Preserve && context.ImageFrame.ExifOrientation != Orientation.Normal;
				bool writeColorProfile =
					settings.ColorProfileMode == ColorProfileMode.NormalizeAndEmbed ||
					settings.ColorProfileMode == ColorProfileMode.Preserve ||
					(settings.ColorProfileMode == ColorProfileMode.Normalize && context.DestColorProfile != ColorProfile.sRGB && context.DestColorProfile != ColorProfile.sGrey);

				metadata = (T)(object)(new BaseImageProperties(dpix, dpiy, writeOrientation ? context.ImageFrame.ExifOrientation : 0, writeColorProfile ? context.DestColorProfile : null));
				return true;
			}

			if (typeof(T) == typeof(WicFrameMetadataReader))
			{
				var settings = context.Settings;
				if (context.ImageFrame is WicImageFrame wicfrm && wicfrm.WicMetadataReader is not null && settings.MetadataNames != Enumerable.Empty<string>())
				{
					metadata = (T)(object)(new WicFrameMetadataReader(wicfrm.WicMetadataReader, settings.MetadataNames));
					return true;
				}

				metadata = default;
				return false;
			}

			if (context.ImageFrame is IMetadataSource frmsrc)
				return frmsrc.TryGetMetadata(out metadata);

			metadata = default;
			return false;
		}
	}

	/// <summary>Defines global/container metadata for a sequence of animated frames.</summary>
	internal readonly struct AnimationContainer : IMetadata
	{
		public string Name => nameof(AnimationContainer);

		/// <summary>The width of the animation's logical screen.  Values less than 1 imply the width is equal to the width of the first frame.</summary>
		public readonly int ScreenWidth;

		/// <summary>The height of the animation's logical screen.  Values less than 1 imply the height is equal to the height of the first frame.</summary>
		public readonly int ScreenHeight;

		/// <summary>The number of times to loop the animation.  Values less than 1 imply inifinte looping.</summary>
		public readonly int LoopCount;

		/// <summary>The background color to restore when a frame's disposal method is RestoreBackground, in ARGB order.</summary>
		public readonly int BackgroundColor;

		/// <summary>True if this animation requires a persistent screen buffer onto which frames are rendered, otherwise false.</summary>
		public readonly bool RequiresScreenBuffer;

		public AnimationContainer(int screenWidth, int screenHeight, int loopCount = 0, int bgColor = 0, bool screenBuffer = false) =>
			(ScreenWidth, ScreenHeight, LoopCount, BackgroundColor, RequiresScreenBuffer) = (screenWidth, screenHeight, loopCount, bgColor, screenBuffer);
	}

	/// <summary>Defines metadata for a single frame within an animated image sequence.</summary>
	internal readonly struct AnimationFrame : IMetadata
	{
		// Rather arbitrary default of NTSC film speed
		internal static AnimationFrame Default = new(0, 0, new Rational(1001, 24000), FrameDisposalMethod.Unspecified, false);

		public string Name => nameof(AnimationFrame);

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
}
