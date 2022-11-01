// Copyright © Clinton Ingram and Contributors.  Licensed under the MIT License.

using System;
using System.Linq;
#if NETFRAMEWORK
using System.IO;
using System.Runtime.InteropServices;
#endif

using PhotoSauce.MagicScaler;
using PhotoSauce.Interop.Giflib;
using static PhotoSauce.Interop.Giflib.Giflib;

namespace PhotoSauce.NativeCodecs.Giflib;

internal static unsafe class GifFactory
{
	public const string DisplayName = $"{giflib} 5.2.1";
	public const string giflib = nameof(giflib);

	enum CodecType { Decoder, Encoder }

	private static readonly Lazy<bool> dependencyValid = new(() => {
#if NETFRAMEWORK
		// netfx doesn't have RID-based native dependency resolution, so we include a .props
		// file that copies binaries for all supported architectures to the output folder,
		// then make a perfunctory attempt to load the right one before the first P/Invoke.
		if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
		{
			[DllImport("kernel32", ExactSpelling = true)]
			static extern IntPtr LoadLibraryW(ushort* lpLibFileName);

			string lib = Path.Combine(RuntimeInformation.ProcessArchitecture.ToString(), "gif");
			fixed (char* plib = lib)
				LoadLibraryW((ushort*)plib);
		}
#endif

		_ = GifErrorString(D_GIF_SUCCEEDED);

		return true;
	});

	public static GifFileType* CreateDecoder(IntPtr stmHandle, delegate* unmanaged[Cdecl]<GifFileType*, byte*, int, int> readFunc) =>
		createCodec(CodecType.Decoder, (void*)stmHandle, readFunc);

	public static GifFileType* CreateEncoder(IntPtr stmHandle, delegate* unmanaged[Cdecl]<GifFileType*, byte*, int, int> writeFunc) =>
		createCodec(CodecType.Encoder, (void*)stmHandle, writeFunc);

	private static GifFileType* createCodec(CodecType codec, void* stmHandle, delegate* unmanaged[Cdecl]<GifFileType*, byte*, int, int> ioFunc)
	{
		if (!dependencyValid.Value)
			return default;

		int err;
		var gif = codec is CodecType.Decoder ? DGifOpen(stmHandle, ioFunc, &err) : EGifOpen(stmHandle, ioFunc, &err);
		if (codec is CodecType.Encoder && err != E_GIF_SUCCEEDED)
			throw new InvalidOperationException($"{nameof(Giflib)} encoder failed. {new string(GifErrorString(err))}");

		return gif;
	}
}

/// <inheritdoc cref="WindowsCodecExtensions" />
public static class CodecCollectionExtensions
{
	/// <inheritdoc cref="WindowsCodecExtensions.UseWicCodecs(CodecCollection, WicCodecPolicy)" />
	/// <param name="removeExisting">Remove any codecs already registered that match <see cref="ImageMimeTypes.Gif" />.</param>
	public static void UseGiflib(this CodecCollection codecs, bool removeExisting = true)
	{
		ThrowHelper.ThrowIfNull(codecs);

		var gifMime = new[] { ImageMimeTypes.Gif };
		var gifExtension = new[] { ImageFileExtensions.Gif };

		if (removeExisting)
		{
			foreach (var codec in codecs.OfType<IImageDecoderInfo>().Where(c => c.MimeTypes.Any(m => m == ImageMimeTypes.Gif)).ToList())
				codecs.Remove(codec);
		}

		codecs.Add(new DecoderInfo(
			GifFactory.DisplayName,
			gifMime,
			gifExtension,
			new ContainerPattern[] {
				new(0, new byte[] { (byte)'G', (byte)'I', (byte)'F', (byte)'8', (byte)'7', (byte)'a' }, new byte[] { 0xff, 0xff, 0xff, 0xff, 0xff, 0xff }),
				new(0, new byte[] { (byte)'G', (byte)'I', (byte)'F', (byte)'8', (byte)'9', (byte)'a' }, new byte[] { 0xff, 0xff, 0xff, 0xff, 0xff, 0xff }),
			},
			null,
			GifContainer.TryLoad
		));
	}
}
