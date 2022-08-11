// Copyright © Clinton Ingram and Contributors.  Licensed under the MIT License.

using System;
using System.Linq;
#if NETFRAMEWORK
using System.IO;
using System.Runtime.InteropServices;
#endif

using PhotoSauce.MagicScaler;
using PhotoSauce.Interop.Libjxl;
using static PhotoSauce.Interop.Libjxl.Libjxl;

namespace PhotoSauce.NativeCodecs.Libjxl;

internal static unsafe class JxlFactory
{
	public const string DisplayName = $"{libjxl} 0.6.1";
	public const string libjxl = nameof(libjxl);
	public const uint libver = 6001;

	private static readonly Lazy<bool> dependencyValid = new(() => {
#if NETFRAMEWORK
		// netfx doesn't have RID-based native dependency resolution, so we include a .props
		// file that copies binaries for all supported architectures to the output folder,
		// then make a perfunctory attempt to load the right one before the first P/Invoke.
		if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
		{
			[DllImport("kernel32", ExactSpelling = true)]
			static extern IntPtr LoadLibraryW(ushort* lpLibFileName);

			string lib = Path.Combine(RuntimeInformation.ProcessArchitecture.ToString(), "jxl");
			fixed (char* plib = lib)
				LoadLibraryW((ushort*)plib);
		}
#endif

		uint ver = JxlDecoderVersion();
		if (ver != libver || (ver = JxlEncoderVersion()) != libver)
			throw new NotSupportedException($"Incorrect {libjxl} version was loaded.  Expected {libver}, found {ver}.");

		return true;
	});

	public static IntPtr CreateDecoder() => dependencyValid.Value ? JxlDecoderCreate(null) : default;

	public static IntPtr CreateEncoder() => dependencyValid.Value ? JxlEncoderCreate(null) : default;
}

/// <inheritdoc cref="WindowsCodecExtensions" />
public static class CodecCollectionExtensions
{
	/// <inheritdoc cref="WindowsCodecExtensions.UseWicCodecs(CodecCollection, WicCodecPolicy)" />
	/// <param name="removeExisting">Remove any codecs already registered that match <see cref="ImageMimeTypes.Jxl" />.</param>
	public static void UseLibjxl(this CodecCollection codecs, bool removeExisting = true)
	{
		ThrowHelper.ThrowIfNull(codecs);

		var jxlMime = new[] { ImageMimeTypes.Jxl };
		var jxlExtension = new[] { ImageFileExtensions.Jxl };

		if (removeExisting)
		{
			foreach (var codec in codecs.Where(c => c.MimeTypes.Any(m => m == ImageMimeTypes.Jxl)).ToList())
				codecs.Remove(codec);
		}

		codecs.Add(new DecoderInfo(
			JxlFactory.DisplayName,
			jxlMime,
			jxlExtension,
			new ContainerPattern[] {
				new(0, new byte[] { 0xff, 0x0a }, new byte[] { 0xff, 0xff }),
				new(0, new byte[] { 0x00, 0x00, 0x00, 0x0c, 0x4a, 0x58, 0x4c, 0x20, 0x0d, 0x0a, 0x87, 0x0a }, new byte[] { 0xff, 0xff, 0xff, 0xff, 0xff, 0xff, 0xff, 0xff, 0xff, 0xff, 0xff, 0xff })
			},
			null,
			JxlContainer.TryLoad
		));
		codecs.Add(new EncoderInfo(
			JxlFactory.DisplayName,
			jxlMime,
			jxlExtension,
			new[] { PixelFormat.Grey8.FormatGuid, PixelFormat.Rgb24.FormatGuid, PixelFormat.Rgba32.FormatGuid },
			JxlLossyEncoderOptions.Default,
			JxlEncoder.Create,
			false,
			false,
			false
		));
	}
}

internal static class JxlError
{
	public static void Check(JxlDecoderStatus status)
	{
		if (status == JxlDecoderStatus.JXL_DEC_ERROR)
			throw new InvalidOperationException($"{nameof(Libjxl)} decoder failed.");
	}

	public static void Check(JxlEncoderStatus status)
	{
		if (status == JxlEncoderStatus.JXL_ENC_ERROR || status == JxlEncoderStatus.JXL_ENC_NOT_SUPPORTED)
			throw new InvalidOperationException($"{nameof(Libjxl)} encoder failed.");
	}
}
