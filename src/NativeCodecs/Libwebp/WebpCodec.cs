// Copyright © Clinton Ingram and Contributors.  Licensed under the MIT License.

using System;
using System.IO;
#if !NET5_0_OR_GREATER
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

			var libs = new[] { "webp", "webpdemux", "webpmux" };
			foreach (string name in libs)
			{
				string lib = Path.Combine(RuntimeInformation.ProcessArchitecture.ToString(), name);
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

		codecs.Add(new DecoderInfo(
			displayName,
			new[] { ImageMimeTypes.Webp },
			new[] { ImageFileExtensions.Webp },
			new ContainerPattern[] {
				new(0, new byte[] { (byte)'R', (byte)'I', (byte)'F', (byte)'F', 0, 0, 0, 0, (byte)'W', (byte)'E', (byte)'B', (byte)'P' }, new byte[] { 0xff, 0xff, 0xff, 0xff, 0, 0, 0, 0, 0xff, 0xff, 0xff, 0xff })
			},
			WebpDecoderOptions.Default,
			WebpContainer.TryLoad
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

	public static void Check(int res)
	{
		if (res == 0)
			throw new InvalidOperationException($"{WebpCodec.libwebp} returned an unexpected failure.");
	}

	public static bool Succeeded(int res) => res != 0;
}
