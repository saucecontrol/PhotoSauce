// Copyright © Clinton Ingram and Contributors.  Licensed under the MIT License.

using System;
using System.IO;
using System.Linq;
using System.Numerics;
#if NETFRAMEWORK
using System.Runtime.InteropServices;
#endif

using PhotoSauce.MagicScaler;
using PhotoSauce.Interop.Libwebp;
using static PhotoSauce.Interop.Libwebp.Libwebp;
using static PhotoSauce.Interop.Libwebp.Libwebpmux;
using static PhotoSauce.Interop.Libwebp.Libwebpdemux;

namespace PhotoSauce.NativeCodecs.Libwebp;

internal static unsafe class WebpFactory
{
	public const string DisplayName = $"{libwebp} 1.3.0";
	public const string libwebp = nameof(libwebp);
	public const uint libver = 0x00010300;

	private static readonly Lazy<bool> dependencyValid = new(() => {
#if NETFRAMEWORK
		// netfx doesn't have RID-based native dependency resolution, so we include a .props
		// file that copies binaries for all supported architectures to the output folder,
		// then make a perfunctory attempt to load the right set before the first P/Invoke.
		if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
		{
			[DllImport("kernel32", ExactSpelling = true)]
			static extern IntPtr LoadLibraryW(ushort* lpLibFileName);

			string arch = typeof(WebpFactory).Assembly.GetArchDirectory();
			foreach (string name in new[] { "webp", "webpdemux", "webpmux" })
			{
				fixed (char* plib = Path.Combine(arch, name))
					LoadLibraryW((ushort*)plib);
			}
		}
#endif

		uint ver = (uint)WebPGetDecoderVersion();
		if (ver < libver)
			throw new NotSupportedException($"Incorrect {libwebp} version was loaded.  Expected at least 0x{libver:x6}, found 0x{ver:x6}.");

		ver = (uint)WebPGetMuxVersion();
		if (ver < libver)
			throw new NotSupportedException($"Incorrect {libwebp}mux version was loaded.  Expected at least 0x{libver:x6}, found 0x{ver:x6}.");

		ver = (uint)WebPGetDemuxVersion();
		if (ver < libver)
			throw new NotSupportedException($"Incorrect {libwebp}demux version was loaded.  Expected at least 0x{libver:x6}, found 0x{ver:x6}.");

		return true;
	});

	public static void* NativeAlloc(nuint size) => dependencyValid.Value ? WebPMalloc(size) : default;

	public static void* CreateDemuxerPartial(WebPData* data, WebPDemuxState* state) => dependencyValid.Value ? WebPDemuxPartial(data, state) : default;

	public static void* CreateDemuxer(WebPData* data) => dependencyValid.Value ? WebPDemux(data) : default;

	public static void* CreateMuxer() => dependencyValid.Value ? WebPMuxNew() : default;
}

/// <inheritdoc cref="WindowsCodecExtensions" />
public static class WebpCodec
{
	/// <inheritdoc cref="WindowsCodecExtensions.UseWicCodecs(CodecCollection, WicCodecPolicy)" />
	/// <param name="removeExisting">Remove any codecs already registered that match <see cref="ImageMimeTypes.Webp" />.</param>
	public static void UseLibwebp(this CodecCollection codecs, bool removeExisting = true)
	{
		ThrowHelper.ThrowIfNull(codecs);

		var webpMime = new[] { ImageMimeTypes.Webp };
		var webpExtension = new[] { ImageFileExtensions.Webp };

		if (removeExisting)
		{
			foreach (var codec in codecs.Where(c => c.MimeTypes.Any(m => m == ImageMimeTypes.Webp)).ToList())
				codecs.Remove(codec);
		}

		codecs.Add(new DecoderInfo(
			WebpFactory.DisplayName,
			webpMime,
			webpExtension,
			new ContainerPattern[] {
				new(0, new byte[] { (byte)'R', (byte)'I', (byte)'F', (byte)'F', 0, 0, 0, 0, (byte)'W', (byte)'E', (byte)'B', (byte)'P' }, new byte[] { 0xff, 0xff, 0xff, 0xff, 0, 0, 0, 0, 0xff, 0xff, 0xff, 0xff })
			},
			WebpDecoderOptions.Default,
			WebpContainer.TryLoad
		));
		codecs.Add(new PlanarEncoderInfo(
			WebpFactory.DisplayName,
			webpMime,
			webpExtension,
			new[] { PixelFormat.Bgra32.FormatGuid, PixelFormat.Y8Video.FormatGuid, PixelFormat.Cb8Video.FormatGuid, PixelFormat.Cr8Video.FormatGuid, PixelFormat.Grey8.FormatGuid },
			WebpLossyEncoderOptions.Default,
			WebpEncoder.Create,
			true,
			true,
			true,
			new[] { ChromaSubsampleMode.Subsample420 },
			ChromaPosition.Bottom,
			WebpConstants.YccMatrix,
			false
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
			throw new InvalidOperationException($"{WebpFactory.libwebp} returned an unexpected failure.");
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