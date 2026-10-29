// Copyright © Clinton Ingram and Contributors
// SPDX-License-Identifier: MIT

using System;
using System.Linq;

using PhotoSauce.MagicScaler;

namespace PhotoSauce.ManagedCodecs.Ico;

internal static class IcoFactory
{
	public const string DisplayName = "PhotoSauce Managed ICO Decoder";
	public const string MimeType = "image/x-icon";

	public static readonly string[] MimeTypes = [ MimeType, "image/vnd.microsoft.icon" ];
	public static readonly string[] FileExtensions = [ ".ico", ".cur" ];
}

/// <inheritdoc cref="WindowsCodecExtensions" />
public static class CodecCollectionExtensions
{
	/// <inheritdoc cref="WindowsCodecExtensions.UseWicCodecs(CodecCollection, WicCodecPolicy)" />
	/// <param name="removeExisting">Remove any decoders already registered for ICO or CUR files, such as the Windows ICO decoder.</param>
	public static void UseIco(this CodecCollection codecs, bool removeExisting = true)
	{
		ThrowHelper.ThrowIfNull(codecs);

		if (removeExisting)
		{
			foreach (var codec in codecs.OfType<IImageDecoderInfo>().Where(static c => c.MimeTypes.Intersect(IcoFactory.MimeTypes, StringComparer.OrdinalIgnoreCase).Any() || c.FileExtensions.Intersect(IcoFactory.FileExtensions, StringComparer.OrdinalIgnoreCase).Any()).ToList())
				codecs.Remove(codec);
		}

		codecs.Add(new DecoderInfo(
			IcoFactory.DisplayName,
			IcoFactory.MimeTypes,
			IcoFactory.FileExtensions,
			[
				new(0, [ 0, 0, 1, 0 ], [ 0xff, 0xff, 0xff, 0xff ]),
				new(0, [ 0, 0, 2, 0 ], [ 0xff, 0xff, 0xff, 0xff ]),
			],
			null,
			IcoContainer.TryLoad
		));
	}
}
