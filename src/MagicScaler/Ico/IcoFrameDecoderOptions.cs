using PhotoSauce.MagicScaler;
using System;

namespace PhotoSauce.ManagedCodecs.Ico;

public readonly struct IcoFrameDecoderOptions(Range frameRange) : IMultiFrameDecoderOptions
{
	readonly Range IMultiFrameDecoderOptions.FrameRange => frameRange;
}