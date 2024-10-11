using PhotoSauce.MagicScaler;
using System;

namespace PhotoSauce.ManagedCodecs.Ico;

public static class IcoCodecCollectionExtensions
{
	public static void UseIcoManagedDecoder(this CodecCollection codecs)
	{
		if (codecs == null)
		{
			throw new ArgumentNullException(nameof(codecs));
		}

		codecs.Add(new DecoderInfo(
			IcoContainer.DecoderDisplayName,
			IcoContainer.MimeTypes,
			IcoContainer.Extensions,
			[new ContainerPattern(0, [0, 0, 1, 0], [0xFF, 0xFF, 0xFF, 0xFF])],
			null,
			IcoContainer.TryLoad));
	}
}