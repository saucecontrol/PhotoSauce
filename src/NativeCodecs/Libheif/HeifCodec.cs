// Copyright © Clinton Ingram and Contributors.  Licensed under the MIT License.

using System;
#if NETFRAMEWORK
using System.IO;
using System.Runtime.InteropServices;
#endif

using PhotoSauce.MagicScaler;
using PhotoSauce.Interop.Libheif;
using static PhotoSauce.Interop.Libheif.Libheif;

namespace PhotoSauce.NativeCodecs.Libheif;

internal static unsafe class HeifFactory
{
	public const string DisplayName = $"{libheif} 1.12.0";
	public const string libheif = nameof(libheif);
	public const uint libver = 0x010c0000;

	private static readonly Lazy<bool> dependencyValid = new(() => {
#if NETFRAMEWORK
		// netfx doesn't have RID-based native dependency resolution, so we include a .props
		// file that copies binaries for all supported architectures to the output folder,
		// then make a perfunctory attempt to load the right one before the first P/Invoke.
		if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
		{
			[DllImport("kernel32", ExactSpelling = true)]
			static extern IntPtr LoadLibraryW(ushort* lpLibFileName);

			string lib = Path.Combine(RuntimeInformation.ProcessArchitecture.ToString(), "heif");
			fixed (char* plib = lib)
				LoadLibraryW((ushort*)plib);
		}
#endif

		uint ver = heif_get_version_number();
		if (ver != libver)
			throw new NotSupportedException($"Incorrect {libheif} version was loaded.  Expected 0x{libver:x8}, found 0x{ver:x8}.");

		return true;
	});

	public static IntPtr CreateContext() => dependencyValid.Value ? heif_context_alloc() : default;
}

/// <inheritdoc cref="WindowsCodecExtensions" />
public static class CodecCollectionExtensions
{
	/// <inheritdoc cref="WindowsCodecExtensions.UseWicCodecs(CodecCollection, WicCodecPolicy)" />
	public static void UseLibheif(this CodecCollection codecs)
	{
		Guard.NotNull(codecs);

		codecs.Add(new DecoderInfo(
			HeifFactory.DisplayName,
			new[] { ImageMimeTypes.Heic },
			new[] { ImageFileExtensions.Heic },
			new ContainerPattern[] {
				new(0, new byte[] { 0, 0, 0, 0, (byte)'f', (byte)'t', (byte)'y', (byte)'p', (byte)'h', (byte)'e', (byte)'i', (byte)'c' }, new byte[] { 0xff, 0xff, 0xff, 0x00, 0xff, 0xff, 0xff, 0xff, 0xff, 0xff, 0xff, 0xff }),
				new(0, new byte[] { 0, 0, 0, 0, (byte)'f', (byte)'t', (byte)'y', (byte)'p', (byte)'m', (byte)'i', (byte)'f', (byte)'1' }, new byte[] { 0xff, 0xff, 0xff, 0x00, 0xff, 0xff, 0xff, 0xff, 0xff, 0xff, 0xff, 0xff })
			},
			null,
			HeifContainer.TryLoad
		));
	}
}

internal static unsafe class HeifResult
{
	public static void Check(heif_error err)
	{
		if (err.code != heif_error_code.heif_error_Ok)
			throw new InvalidOperationException(new string(err.message));
	}

	public static bool Succeeded(heif_error err) => err.code == heif_error_code.heif_error_Ok;
}
