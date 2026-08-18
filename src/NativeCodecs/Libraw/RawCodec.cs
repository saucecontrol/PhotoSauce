// Copyright © Clinton Ingram and Contributors
// SPDX-License-Identifier: MIT

using System;
using System.IO;
using System.Linq;
#if NETFRAMEWORK
using System.Runtime.InteropServices;
#endif

using PhotoSauce.MagicScaler;
using PhotoSauce.Interop.Libraw;
using static PhotoSauce.Interop.Libraw.Libraw;

namespace PhotoSauce.NativeCodecs.Libraw;

internal static unsafe class RawFactory
{
	public const string DisplayName = "libraw 0.22.1";
	public const string libraw = nameof(libraw);
	public const int libver = 0x001601;

	private static readonly Lazy<bool> dependencyValid = new(() => {
#if NETFRAMEWORK
		// netfx doesn't have RID-based native dependency resolution, so load the
		// architecture-specific dependency and wrapper before the first P/Invoke.
		if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
		{
			[DllImport("kernel32", ExactSpelling = true)]
			static extern IntPtr LoadLibraryW(ushort* lpLibFileName);

			string arch = typeof(RawFactory).Assembly.GetArchDirectory();
			foreach (string name in new[] { "raw_r", "psraw" })
			{
				fixed (char* plib = Path.Combine(arch, name))
					LoadLibraryW((ushort*)plib);
			}
		}
#endif

		int ver = PsRawVersion();
		if (ver != libver)
			throw new NotSupportedException($"Incorrect {libraw} version was loaded. Expected 0x{libver:x6}, found 0x{ver:x6}.");

		return true;
	});

	public static void* Create(void* data, nuint length, psraw_options* options, psraw_image_info* info, int* error) =>
		dependencyValid.Value ? PsRawCreate(data, length, options, info, error) : default;
}

/// <inheritdoc cref="WindowsCodecExtensions" />
public static class CodecCollectionExtensions
{
	private const string RawMimeType = "image/x-raw";
	private const string DngMimeType = "image/x-adobe-dng";

	/// <summary>Register the LibRaw camera RAW decoder.</summary>
	/// <param name="codecs">The codec collection to modify.</param>
	/// <param name="removeExisting">Remove any decoders already registered for a camera RAW MIME type.</param>
	/// <remarks>
	/// LibRaw is registered ahead of existing codecs because many RAW formats use a TIFF container signature.
	/// Files that LibRaw does not recognize are passed to the next matching decoder.
	/// </remarks>
	public static void UseLibraw(this CodecCollection codecs, bool removeExisting = true)
	{
		ThrowHelper.ThrowIfNull(codecs);

		if (removeExisting)
		{
			foreach (var codec in codecs.OfType<IImageDecoderInfo>().Where(c => c.MimeTypes.Any(m => m is RawMimeType or DngMimeType)).ToList())
				codecs.Remove(codec);
		}

		var decoder = new DecoderInfo(
			RawFactory.DisplayName,
			[ RawMimeType, DngMimeType ],
			[
				".3fr", ".ari", ".arw", ".bay", ".cap", ".cr2", ".cr3", ".crw", ".dcr", ".dcs", ".dng", ".drf",
				".eip", ".erf", ".fff", ".gpr", ".iiq", ".k25", ".kdc", ".mdc", ".mef", ".mos", ".mrw", ".nef",
				".nrw", ".orf", ".pef", ".ptx", ".pxn", ".raf", ".raw", ".rwl", ".rw2", ".rwz", ".sr2", ".srf",
				".srw", ".x3f"
			],
			[
				new(0, [ 0x49, 0x49, 0x2a, 0x00 ], [ 0xff, 0xff, 0xff, 0xff ]),
				new(0, [ 0x4d, 0x4d, 0x00, 0x2a ], [ 0xff, 0xff, 0xff, 0xff ]),
				new(0, [ 0x49, 0x49, 0x1a, 0x00 ], [ 0xff, 0xff, 0xff, 0xff ]),
				new(0, [ 0x49, 0x49, 0x49, 0x49 ], [ 0xff, 0xff, 0xff, 0xff ]),
				new(0, [ 0x4d, 0x4d, 0x4d, 0x4d ], [ 0xff, 0xff, 0xff, 0xff ]),
				new(0, [ 0x49, 0x49, 0x52, 0x4f ], [ 0xff, 0xff, 0xff, 0xff ]),
				new(0, [ 0x4d, 0x4d, 0x4f, 0x52 ], [ 0xff, 0xff, 0xff, 0xff ]),
				new(0, [ 0x49, 0x49, 0x55, 0x00 ], [ 0xff, 0xff, 0xff, 0xff ]),
				new(0, [ 0x00, (byte)'M', (byte)'R', (byte)'M' ], [ 0xff, 0xff, 0xff, 0xff ]),
				new(0, [ (byte)'F', (byte)'O', (byte)'V', (byte)'b' ], [ 0xff, 0xff, 0xff, 0xff ]),
				new(0, [ (byte)'P', (byte)'X', (byte)'N', 0x00 ], [ 0xff, 0xff, 0xff, 0xff ]),
				new(0, [ (byte)'P', (byte)'W', (byte)'A', (byte)'D' ], [ 0xff, 0xff, 0xff, 0xff ]),
				new(0, [ (byte)'N', (byte)'O', (byte)'K', (byte)'I', (byte)'A', (byte)'R', (byte)'A', (byte)'W' ], [ 0xff, 0xff, 0xff, 0xff, 0xff, 0xff, 0xff, 0xff ]),
				new(0, [ (byte)'D', (byte)'S', (byte)'C', (byte)'-', (byte)'I', (byte)'m', (byte)'a', (byte)'g', (byte)'e' ], [ 0xff, 0xff, 0xff, 0xff, 0xff, 0xff, 0xff, 0xff, 0xff ]),
				new(0, [ (byte)'F', (byte)'U', (byte)'J', (byte)'I', (byte)'F', (byte)'I', (byte)'L', (byte)'M', (byte)'C', (byte)'C', (byte)'D', (byte)'-', (byte)'R', (byte)'A', (byte)'W' ], [ 0xff, 0xff, 0xff, 0xff, 0xff, 0xff, 0xff, 0xff, 0xff, 0xff, 0xff, 0xff, 0xff, 0xff, 0xff ]),
				new(4, [ (byte)'f', (byte)'t', (byte)'y', (byte)'p', (byte)'c', (byte)'r', (byte)'x', (byte)' ' ], [ 0xff, 0xff, 0xff, 0xff, 0xff, 0xff, 0xff, 0xff ])
			],
			LibrawDecoderOptions.Default,
			RawContainer.TryLoad
		);

		// TIFF-based RAW files must be offered to LibRaw before a general TIFF decoder.
		var existing = codecs.ToArray();
		codecs.Clear();
		codecs.Add(decoder);
		foreach (var codec in existing)
			codecs.Add(codec);
	}
}
