// Copyright © Clinton Ingram and Contributors
// SPDX-License-Identifier: MIT

using System;
using System.Linq;
using System.Runtime.InteropServices;

using PhotoSauce.MagicScaler;

namespace PhotoSauce.NativeCodecs.JxlRs;

internal static class JxlRsFactory
{
	public const string DisplayName = "jxl-rs 0.6.0";
	private const uint NativeVersion = 600;

	private static readonly Lazy<bool> dependencyValid = new(() => {
		uint version = JxlRsNative.jxlrs_version();
		if (version != NativeVersion)
			throw new NotSupportedException($"Incorrect jxl-rs native library version was loaded. Expected {NativeVersion}, found {version}.");

		return true;
	});

	public static void EnsureDependency() => _ = dependencyValid.Value;

	public static InvalidOperationException CreateDecoderException()
	{
		string? message = Marshal.PtrToStringAnsi(JxlRsNative.jxlrs_last_error());
		return new InvalidOperationException($"jxl-rs decoder failed{(string.IsNullOrWhiteSpace(message) ? "." : $": {message}")}");
	}
}

/// <summary>Registration helpers for the jxl-rs JPEG XL decoder.</summary>
public static class CodecCollectionExtensions
{
	/// <summary>Registers the jxl-rs JPEG XL decoder.</summary>
	/// <param name="codecs">The codec collection to modify.</param>
	/// <param name="removeExisting">Remove codecs already registered for JPEG XL.</param>
	public static void UseJxlRs(this CodecCollection codecs, bool removeExisting = true)
	{
		ThrowHelper.ThrowIfNull(codecs);

		if (removeExisting)
		{
			foreach (var codec in codecs.Where(c => c is DecoderInfo && c.MimeTypes.Any(m => m == ImageMimeTypes.Jxl)).ToList())
				codecs.Remove(codec);
		}

		codecs.Add(new DecoderInfo(
			JxlRsFactory.DisplayName,
			[ ImageMimeTypes.Jxl ],
			[ ImageFileExtensions.Jxl ],
			[
				new(0, [ 0xff, 0x0a ], [ 0xff, 0xff ]),
				new(0, [ 0x00, 0x00, 0x00, 0x0c, 0x4a, 0x58, 0x4c, 0x20, 0x0d, 0x0a, 0x87, 0x0a ], [ 0xff, 0xff, 0xff, 0xff, 0xff, 0xff, 0xff, 0xff, 0xff, 0xff, 0xff, 0xff ])
			],
			null,
			JxlRsContainer.TryLoad
		));
	}
}
