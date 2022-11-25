// Copyright © Clinton Ingram and Contributors.  Licensed under the MIT License.

using System;
using System.Linq;
#if NETFRAMEWORK
using System.IO;
using System.Runtime.InteropServices;
#endif

using PhotoSauce.MagicScaler;
using PhotoSauce.Interop.Libjpeg;
using static PhotoSauce.Interop.Libjpeg.Libjpeg;

namespace PhotoSauce.NativeCodecs.Libjpeg;

internal static unsafe class JpegFactory
{
	public const string DisplayName = $"{psjpeg} (libjpeg-turbo) 2.1.4";
	public const string psjpeg = nameof(psjpeg);
	public const uint libver = 2001004;

	private static readonly Lazy<bool> dependencyValid = new(() => {
#if NETFRAMEWORK
		// netfx doesn't have RID-based native dependency resolution, so we include a .props
		// file that copies binaries for all supported architectures to the output folder,
		// then make a perfunctory attempt to load the right one before the first P/Invoke.
		if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
		{
			[DllImport("kernel32", ExactSpelling = true)]
			static extern IntPtr LoadLibraryW(ushort* lpLibFileName);

			fixed (char* plib = Path.Combine(typeof(JpegFactory).Assembly.GetArchDirectory(), psjpeg))
				LoadLibraryW((ushort*)plib);
		}
#endif

		int ver = JpegVersion();
		if (ver != libver)
			throw new NotSupportedException($"Incorrect {psjpeg} version was loaded.  Expected {libver}, found {ver}.");

		return true;
	});

	public static jpeg_decompress_struct* CreateDecoder() => dependencyValid.Value ? JpegCreateDecompress() : default;

	public static jpeg_compress_struct* CreateEncoder() => dependencyValid.Value ? JpegCreateCompress() : default;
}

/// <inheritdoc cref="WindowsCodecExtensions" />
public static class CodecCollectionExtensions
{
	/// <inheritdoc cref="WindowsCodecExtensions.UseWicCodecs(CodecCollection, WicCodecPolicy)" />
	/// <param name="removeExisting">Remove any codecs already registered that match <see cref="ImageMimeTypes.Jpeg" />.</param>
	public static void UseLibjpeg(this CodecCollection codecs, bool removeExisting = true)
	{
		ThrowHelper.ThrowIfNull(codecs);

		var jpegMime = new[] { ImageMimeTypes.Jpeg };
		var jpegExtension = new[] { ImageFileExtensions.Jpeg, ".jpg" };

		if (removeExisting)
		{
			foreach (var codec in codecs.Where(c => c.MimeTypes.Any(m => m == ImageMimeTypes.Jpeg)).ToList())
				codecs.Remove(codec);
		}

		codecs.Add(new DecoderInfo(
			JpegFactory.DisplayName,
			jpegMime,
			jpegExtension,
			new ContainerPattern[] {
				new(0, new byte[] { 0xff, 0xd8 }, new byte[] { 0xff, 0xff }),
			},
			null,
			JpegContainer.TryLoad
		));
		codecs.Add(new PlanarEncoderInfo(
			JpegFactory.DisplayName,
			jpegMime,
			jpegExtension,
			new[] { PixelFormat.Grey8.FormatGuid, PixelFormat.Bgr24.FormatGuid, PixelFormat.Y8.FormatGuid, PixelFormat.Cb8.FormatGuid, PixelFormat.Cr8.FormatGuid },
			JpegEncoderOptions.Default,
			JpegEncoder.Create,
			false,
			false,
			true,
			new[] { ChromaSubsampleMode.Subsample420, ChromaSubsampleMode.Subsample422, ChromaSubsampleMode.Subsample444 },
			ChromaPosition.Center,
			YccMatrix.Rec601,
			false
		));
	}
}
