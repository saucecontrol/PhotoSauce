// Copyright © Clinton Ingram and Contributors.  Licensed under the MIT License.

using System;
using System.IO;
using System.Numerics;
#if NETFRAMEWORK
using System.Runtime.InteropServices;
#endif

using PhotoSauce.MagicScaler;
using PhotoSauce.Interop.Libwebp;
using static PhotoSauce.Interop.Libwebp.Libwebp;

namespace PhotoSauce.NativeCodecs.Libwebp;

/// <inheritdoc cref="WindowsCodecExtensions" />
public static unsafe class WebpCodec
{
	internal const string libwebp = nameof(libwebp);
	private const string displayName = $"{libwebp} 1.2.2";
	private const uint libver = 0x00010202;

	private static readonly Lazy<bool> dependencyValid = new(() => {
#if NETFRAMEWORK
		// netfx doesn't have RID-based native dependency resolution, so we include a .props
		// file that copies binaries for all supported architectures to the output folder,
		// then make a perfunctory attempt to load the right set before the first P/Invoke.
		if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
		{
			[DllImport("kernel32", ExactSpelling = true)]
			static extern IntPtr LoadLibraryW(ushort* lpLibFileName);

			string arch = RuntimeInformation.ProcessArchitecture.ToString();
			foreach (string name in new[] { "webp", "webpdemux", "webpmux" })
			{
				string lib = Path.Combine(arch, name);
				fixed (char* plib = lib)
					LoadLibraryW((ushort*)plib);
			}
		}
#endif

		uint ver = (uint)WebPGetDecoderVersion();
		if (ver != libver)
			throw new NotSupportedException($"Incorrect {libwebp} version was loaded.  Expected 0x{libver:x8}, found 0x{ver:x8}.");

		return true;
	});

	/// <inheritdoc cref="WindowsCodecExtensions.UseWicCodecs(CodecCollection, WicCodecPolicy)" />
	public static void UseLibwebp(this CodecCollection codecs)
	{
		Guard.NotNull(codecs);

		if (!dependencyValid.Value)
			return;

		var webpMime = new[] { ImageMimeTypes.Webp };
		var webpExtension = new[] { ImageFileExtensions.Webp };

		codecs.Add(new DecoderInfo(
			displayName,
			webpMime,
			webpExtension,
			new ContainerPattern[] {
				new(0, new byte[] { (byte)'R', (byte)'I', (byte)'F', (byte)'F', 0, 0, 0, 0, (byte)'W', (byte)'E', (byte)'B', (byte)'P' }, new byte[] { 0xff, 0xff, 0xff, 0xff, 0, 0, 0, 0, 0xff, 0xff, 0xff, 0xff })
			},
			WebpDecoderOptions.Default,
			WebpContainer.TryLoad
		));
		codecs.Add(new EncoderInfo(
			displayName,
			webpMime,
			webpExtension,
			new[] { PixelFormat.Grey8.FormatGuid, PixelFormat.Rgb24.FormatGuid, PixelFormat.Rgba32.FormatGuid },
			WebpLossyEncoderOptions.Default,
			WebpEncoder.Create,
			false,
			false,
			true
		));
	}
}

internal static class WebpResult
{
	public static void Check(VP8StatusCode status)
	{
		if (status != VP8StatusCode.VP8_STATUS_OK)
			throw new InvalidDataException(status.ToString());
	}

	public static void Check(WebPEncodingError error)
	{
		if (error != WebPEncodingError.VP8_ENC_OK)
			throw new InvalidOperationException(error.ToString());
	}

	public static void Check(WebPMuxError error)
	{
		if (error != WebPMuxError.WEBP_MUX_OK)
			throw new InvalidOperationException(error.ToString());
	}

	public static void Check(int res)
	{
		if (res == 0)
			throw new InvalidOperationException($"{WebpCodec.libwebp} returned an unexpected failure.");
	}

	public static bool Succeeded(int res) => res != 0;
}

internal static class WebpConstants
{
	public const uint IccpTag = 'I' | 'C' << 8 | 'C' << 16 | 'P' << 24;
	public const uint ExifTag = 'E' | 'X' << 8 | 'I' << 16 | 'F' << 24;

	// WebP uses a non-standard matrix close to Rec.601 but rounded strangely
	// https://chromium.googlesource.com/webm/libwebp/+/refs/tags/v1.2.2/src/dsp/yuv.h
	private static readonly Matrix4x4 yccMatrix = new() {
		M11 =  0.2992f,
		M21 =  0.5874f,
		M31 =  0.1141f,
		M12 = -0.1688f,
		M22 = -0.3315f,
		M32 =  0.5003f,
		M13 =  0.5003f,
		M23 = -0.4189f,
		M33 = -0.0814f,
		M44 = 1
	};

	public static ref readonly Matrix4x4 YccMatrix => ref yccMatrix;
}