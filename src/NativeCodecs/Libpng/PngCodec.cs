// Copyright © Clinton Ingram and Contributors
// SPDX-License-Identifier: MIT

using System;
using System.Linq;
#if NETFRAMEWORK
using System.IO;
using System.Runtime.InteropServices;
#endif

using PhotoSauce.MagicScaler;
using PhotoSauce.Interop.Libpng;
using static PhotoSauce.Interop.Libpng.Libpng;

namespace PhotoSauce.NativeCodecs.Libpng;

internal static unsafe class PngFactory
{
	public const string DisplayName = $"{pspng} (libpng) {PNG_LIBPNG_VER_STRING}";
	public const string pspng = nameof(pspng);
	public const uint libver = PNG_LIBPNG_VER;

	private static readonly Lazy<bool> dependencyValid = new(() => {
#if NETFRAMEWORK
		// netfx doesn't have RID-based native dependency resolution, so we include a .props
		// file that copies binaries for all supported architectures to the output folder,
		// then make a perfunctory attempt to load the right one before the first P/Invoke.
		if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
		{
			[DllImport("kernel32", ExactSpelling = true)]
			static extern IntPtr LoadLibraryW(ushort* lpLibFileName);

			fixed (char* plib = Path.Combine(typeof(PngFactory).Assembly.GetArchDirectory(), pspng))
				LoadLibraryW((ushort*)plib);
		}
#endif

		uint ver = PngVersion();
		if (ver != libver)
			throw new NotSupportedException($"Incorrect {pspng} version was loaded.  Expected {libver}, found {ver}.");

		return true;
	});

	public static ps_png_struct* CreateDecoder() => dependencyValid.Value ? PngCreateRead() : default;

	public static ps_png_struct* CreateEncoder() => dependencyValid.Value ? PngCreateWrite() : default;
}

/// <inheritdoc cref="WindowsCodecExtensions" />
public static class CodecCollectionExtensions
{
	internal const string EnableApngEncodeName = $"{nameof(PhotoSauce)}.{nameof(NativeCodecs)}.{nameof(Libpng)}.{nameof(EnableApngEncode)}";

	internal static readonly bool EnableApngEncode = AppContext.TryGetSwitch(EnableApngEncodeName, out bool val) && val;

	/// <inheritdoc cref="WindowsCodecExtensions.UseWicCodecs(CodecCollection, WicCodecPolicy)" />
	/// <param name="removeExisting">Remove any codecs already registered that match <see cref="ImageMimeTypes.Png" />.</param>
	public static void UseLibpng(this CodecCollection codecs, bool removeExisting = true)
	{
		ThrowHelper.ThrowIfNull(codecs);

		string[] pngMime = [ ImageMimeTypes.Png ];
		string[] pngExtension = [ ImageFileExtensions.Png ];

		if (removeExisting)
		{
			foreach (var codec in codecs.Where(c => c.MimeTypes.Any(m => m == ImageMimeTypes.Png)).ToList())
				codecs.Remove(codec);
		}

		codecs.Add(new DecoderInfo(
			PngFactory.DisplayName,
			pngMime,
			pngExtension,
			[
				new(0, [ 0x89, 0x50, 0x4e, 0x47, 0x0d, 0x0a, 0x1a, 0x0a ], [ 0xff, 0xff, 0xff, 0xff, 0xff, 0xff, 0xff, 0xff ]),
			],
			null,
			PngContainer.TryLoad
		));
		codecs.Add(new EncoderInfo(
			PngFactory.DisplayName,
			pngMime,
			pngExtension,
			[ PixelFormat.Grey8.FormatGuid, PixelFormat.Rgb24.FormatGuid, PixelFormat.Rgba32.FormatGuid, PixelFormat.Indexed8.FormatGuid ],
			PngEncoderOptions.Default,
			PngEncoder.Create,
			EnableApngEncode,
			EnableApngEncode,
			true
		));
	}
}
