// Copyright © Clinton Ingram and Contributors.  Licensed under the MIT License.

using System;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Reflection;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Runtime.CompilerServices;
using System.Diagnostics.CodeAnalysis;

#if HWINTRINSICS
using System.Runtime.Intrinsics;
using System.Runtime.Intrinsics.X86;
#endif

namespace PhotoSauce.MagicScaler;

/// <summary>Well-known <a href="https://en.wikipedia.org/wiki/Media_type">MIME types</a> for image formats.</summary>
public static class ImageMimeTypes
{
#pragma warning disable CS1591 // Missing XML comments
	public const string Avif = "image/avif";
	public const string Bmp  = "image/bmp";
	public const string Dds  = "image/vnd.ms-dds";
	public const string Gif  = "image/gif";
	public const string Heic = "image/heic";
	public const string Jpeg = "image/jpeg";
	public const string Jxl  = "image/jxl";
	public const string Jxr  = "image/vnd.ms-photo"; // actual IANA assignment is image/jxr, but WIC reports otherwise
	public const string Png  = "image/png";
	public const string Tiff = "image/tiff";
	public const string Webp = "image/webp";
#pragma warning restore CS1591

	internal static FileFormat ToFileFormat(string? mime, bool indexed = false) => mime switch {
		Bmp  => FileFormat.Bmp,
		Gif  => FileFormat.Gif,
		Png  => indexed ? FileFormat.Png8 : FileFormat.Png,
		Jpeg => FileFormat.Jpeg,
		Tiff => FileFormat.Tiff,
		_    => default
	};
}

/// <summary>Well-known file extensions for image formats.</summary>
public static class ImageFileExtensions
{
#pragma warning disable CS1591 // Missing XML comments
	public const string Avif = ".avif";
	public const string Bmp  = ".bmp";
	public const string Dds  = ".dds";
	public const string Gif  = ".gif";
	public const string Heic = ".heic";
	public const string Jpeg = ".jpeg";
	public const string Jxl  = ".jxl";
	public const string Jxr  = ".jxr";
	public const string Png  = ".png";
	public const string Tiff = ".tiff";
	public const string Webp = ".webp";
#pragma warning restore CS1591

	internal static string[] All = typeof(ImageFileExtensions)
		.GetFields(BindingFlags.Public | BindingFlags.Static)
		.Where(static f => f.FieldType == typeof(string))
		.Select(static f => (string)f.GetValue(null)!)
		.ToArray();
}

/// <summary>A pattern used to match <a href="https://en.wikipedia.org/wiki/List_of_file_signatures">magic bytes</a> in an image file header.</summary>
/// <param name="Offset">Number of bytes to skip from the start of the file before attempting pattern match.</param>
/// <param name="Pattern">A byte pattern to match at the given <paramref name="Offset" />.</param>
/// <param name="Mask">A mask to apply to image bytes (using binary <see langword="&amp;" />) before matching against the <paramref name="Pattern" />.</param>
/// <remarks>The total length described by (<paramref name="Offset" /> + <paramref name="Pattern" />.Length) should not be more than 16 bytes.</remarks>
public readonly record struct ContainerPattern(int Offset, byte[] Pattern, byte[] Mask);

/// <inheritdoc />
/// <param name="Name"><inheritdoc cref="IImageCodecInfo.Name" path="/summary/node()" /></param>
/// <param name="MimeTypes"><inheritdoc cref="IImageCodecInfo.MimeTypes" path="/summary/node()" /></param>
/// <param name="FileExtensions"><inheritdoc cref="IImageCodecInfo.FileExtensions" path="/summary/node()" /></param>
public abstract record class CodecInfo(
	string Name,
	IEnumerable<string> MimeTypes,
	IEnumerable<string> FileExtensions
) : IImageCodecInfo;

/// <inheritdoc />
/// <param name="Patterns"><inheritdoc cref="IImageDecoderInfo.Patterns" path="/summary/node()" /></param>
/// <param name="DefaultOptions"><inheritdoc cref="IImageDecoderInfo.DefaultOptions" path="/summary/node()" /></param>
/// <param name="Factory"><inheritdoc cref="IImageDecoderInfo.Factory" path="/summary/node()" /></param>
public sealed record class DecoderInfo(
	string Name,
	IEnumerable<string> MimeTypes,
	IEnumerable<string> FileExtensions,
	IEnumerable<ContainerPattern> Patterns,
	IDecoderOptions? DefaultOptions,
	Func<Stream, IDecoderOptions?, IImageContainer?> Factory
) : CodecInfo(Name, MimeTypes, FileExtensions), IImageDecoderInfo;

/// <inheritdoc />
/// <param name="PixelFormats"><inheritdoc cref="IImageEncoderInfo.PixelFormats" path="/summary/node()" /></param>
/// <param name="DefaultOptions"><inheritdoc cref="IImageEncoderInfo.DefaultOptions" path="/summary/node()" /></param>
/// <param name="Factory"><inheritdoc cref="IImageEncoderInfo.Factory" path="/summary/node()" /></param>
/// <param name="SupportsMultiFrame"><inheritdoc cref="IImageEncoderInfo.SupportsMultiFrame" path="/summary/node()" /></param>
/// <param name="SupportsAnimation"><inheritdoc cref="IImageEncoderInfo.SupportsAnimation" path="/summary/node()" /></param>
/// <param name="SupportsColorProfile"><inheritdoc cref="IImageEncoderInfo.SupportsColorProfile" path="/summary/node()" /></param>
public sealed record class EncoderInfo(
	string Name,
	IEnumerable<string> MimeTypes,
	IEnumerable<string> FileExtensions,
	IEnumerable<Guid> PixelFormats,
	IEncoderOptions? DefaultOptions,
	Func<Stream, IEncoderOptions?, IImageEncoder> Factory,
	bool SupportsMultiFrame,
	bool SupportsAnimation,
	bool SupportsColorProfile
) : CodecInfo(Name, MimeTypes, FileExtensions), IImageEncoderInfo;

internal sealed record class PlanarEncoderInfo(
	string Name,
	IEnumerable<string> MimeTypes,
	IEnumerable<string> FileExtensions,
	IEnumerable<Guid> PixelFormats,
	IEncoderOptions? DefaultOptions,
	Func<Stream, IEncoderOptions?, IImageEncoder> Factory,
	bool SupportsMultiFrame,
	bool SupportsAnimation,
	bool SupportsColorProfile,
	ChromaSubsampleMode[] SubsampleModes,
	ChromaPosition ChromaPosition,
	Matrix4x4 DefaultMatrix,
	bool SupportsCustomMatrix
) : CodecInfo(Name, MimeTypes, FileExtensions), IPlanarImageEncoderInfo;

/// <summary>Represents the set of configured codecs for the processing pipeline.</summary>
/// <remarks>Instances should not be retained or used outside of <see cref="CodecManager.Configure(Action{CodecCollection}?)" />.</remarks>
public sealed class CodecCollection : ICollection<IImageCodecInfo>, IReadOnlyCollection<IImageCodecInfo>
{
	private readonly List<IImageCodecInfo> codecs = [ ];
	private readonly Dictionary<string, IImageEncoderInfo> encoderMimeMap = new(StringComparer.OrdinalIgnoreCase);
	private readonly Dictionary<string, IImageEncoderInfo> encoderExtensionMap = new(StringComparer.OrdinalIgnoreCase);
	private DecoderPattern[] decoderPatterns = [ ];

	/// <inheritdoc />
	public int Count => codecs.Count;

	/// <inheritdoc />
	public bool Contains(IImageCodecInfo item) => codecs.Contains(item);

	/// <inheritdoc />
	public void Add(IImageCodecInfo item) => codecs.Add(item);

	/// <inheritdoc />
	public bool Remove(IImageCodecInfo item) => codecs.Remove(item);

	/// <inheritdoc />
	public void Clear() => codecs.Clear();

	/// <inheritdoc />
	public IEnumerator<IImageCodecInfo> GetEnumerator() => codecs.GetEnumerator();

	bool ICollection<IImageCodecInfo>.IsReadOnly => false;
	
	void ICollection<IImageCodecInfo>.CopyTo(IImageCodecInfo[] array, int arrayIndex) => codecs.CopyTo(array, arrayIndex);

	IEnumerator IEnumerable.GetEnumerator() => codecs.GetEnumerator();

	internal void AddRange(IEnumerable<IImageCodecInfo> items) => codecs.AddRange(items);

	internal IImageContainer GetDecoderForStream(Stream stm, IDecoderOptions? options = null)
	{
		const int siglen = sizeof(ulong) * 2;

		if ((stm.Length - stm.Position) < siglen)
			throw new InvalidDataException("The input is too small to be a valid image.");

		var patBuffer = (Span<byte>)stackalloc byte[siglen];
		stm.FillBuffer(patBuffer);
		stm.Seek(-siglen, SeekOrigin.Current);

		ref byte testval = ref MemoryMarshal.GetReference(patBuffer);
#if HWINTRINSICS
		if (Sse2.IsSupported)
		{
			var vtv = Unsafe.ReadUnaligned<Vector128<byte>>(ref testval);
			foreach (ref readonly var pat in decoderPatterns.AsSpan())
			{
				var vcv = Unsafe.ReadUnaligned<Vector128<byte>>(ref Unsafe.As<ulong, byte>(ref Unsafe.AsRef(in pat.p1)));
				var vcm = Unsafe.ReadUnaligned<Vector128<byte>>(ref Unsafe.As<ulong, byte>(ref Unsafe.AsRef(in pat.m1)));
				if (HWIntrinsics.IsMaskedZero(Sse2.Xor(vtv, vcv), vcm))
				{
					var dec = pat.dec.Factory(stm, options ?? pat.dec.DefaultOptions);
					if (dec is not null)
						return dec;
				}
			}
		}
		else
#endif
		{
			ulong tv0 = Unsafe.ReadUnaligned<ulong>(ref testval);
			ulong tv1 = Unsafe.ReadUnaligned<ulong>(ref Unsafe.Add(ref testval, sizeof(ulong)));
			foreach (ref readonly var pat in decoderPatterns.AsSpan())
			{
				if ((((tv0 ^ pat.p1) & pat.m1) | ((tv1 ^ pat.p2) & pat.m2)) == 0)
				{
					var dec = pat.dec.Factory(stm, options ?? pat.dec.DefaultOptions);
					if (dec is not null)
						return dec;
				}
			}
		}

		throw new InvalidDataException("Image format not supported.  Please ensure the input file is an image and that a codec capable of reading the image is registered.");
	}

	internal bool TryGetEncoderForFileExtension(string extension, [NotNullWhen(true)] out IImageEncoderInfo? info) => (info = encoderExtensionMap.GetValueOrDefault(extension)) is not null;

	internal bool TryGetEncoderForMimeType(string mimeType, [NotNullWhen(true)] out IImageEncoderInfo? info) => (info = encoderMimeMap.GetValueOrDefault(mimeType)) is not null;

	internal void ResetCaches()
	{
		decoderPatterns = codecs.OfType<IImageDecoderInfo>().SelectMany(static i => i.Patterns.Select(s => (Info: i, Pattern: s))).Select(static dec => {
			var pat = new DecoderPattern { dec = dec.Info };
			int offset = dec.Pattern.Offset.Clamp(0, sizeof(ulong) * 2);
			Unsafe.CopyBlockUnaligned(ref Unsafe.Add(ref Unsafe.As<ulong, byte>(ref pat.m1), offset), ref MemoryMarshal.GetReference(dec.Pattern.Mask.AsSpan()), (uint)(dec.Pattern.Mask?.Length ?? 0).Clamp(0, sizeof(ulong) * 2 - offset));
			Unsafe.CopyBlockUnaligned(ref Unsafe.Add(ref Unsafe.As<ulong, byte>(ref pat.p1), offset), ref MemoryMarshal.GetReference(dec.Pattern.Pattern.AsSpan()), (uint)(dec.Pattern.Pattern?.Length ?? 0).Clamp(0, sizeof(ulong) * 2 - offset));
			return pat;
		}).ToArray();

		encoderMimeMap.Clear();
		foreach (var enc in codecs.OfType<IImageEncoderInfo>().SelectMany(static i => i.MimeTypes.Select(s => (Info: i, MimeType: s))))
			encoderMimeMap.TryAdd(enc.MimeType, enc.Info);

		encoderExtensionMap.Clear();
		foreach (var enc in codecs.OfType<IImageEncoderInfo>().SelectMany(static i => i.FileExtensions.Select(s => (Info: i, Extension: s))))
		{
			string ext = enc.Extension;
			if (string.IsNullOrEmpty(ext))
				continue;

			if (ext[0] != '.')
				ext = string.Concat(".", ext);

			encoderExtensionMap.TryAdd(ext, enc.Info);
		}
	}

	[StructLayout(LayoutKind.Sequential)]
	private struct DecoderPattern { public ulong m1; public ulong m2; public ulong p1; public ulong p2; public IImageDecoderInfo dec; }
}

/// <summary>Manages registration of encoders, decoders, and image type mappings.</summary>
public static class CodecManager
{
	private static volatile CodecCollection? codecs;

	internal static readonly EncoderInfo FallbackEncoder = new(
		nameof(FallbackEncoder),
		Enumerable.Empty<string>(),
		Enumerable.Empty<string>(),
		Enumerable.Empty<Guid>(),
		null,
		(s, o) => throw new InvalidOperationException("No encoders are registered."),
		false, false, false
	);

	private static CodecCollection getCodecs()
	{
		if (codecs is null)
			Configure(default);

		return codecs;
	}

	/// <summary>Configure codecs for the application lifetime.</summary>
	/// <param name="configure">A callback that allows manipulation of the codec collection before it is registered with the pipeline.</param>
	/// <exception cref="InvalidOperationException"></exception>
	[MemberNotNull(nameof(codecs))]
	public static void Configure(Action<CodecCollection>? configure)
	{
		var cc = new CodecCollection();
		if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
			cc.UseWicCodecs(WicCodecPolicy.BuiltIn);

		if (configure is not null)
			configure(cc);

		if (cc.Count == 0)
			throw new InvalidOperationException($"No codecs are configured. You must add some codecs in a call to {nameof(CodecManager)}.{nameof(Configure)}.");

		cc.ResetCaches();
		codecs = cc;
	}

	internal static IImageContainer GetDecoderForStream(Stream stm, IDecoderOptions? opt = null) => getCodecs().GetDecoderForStream(stm, opt);

	internal static bool TryGetEncoderForFileExtension(string extension, [NotNullWhen(true)] out IImageEncoderInfo? info) => getCodecs().TryGetEncoderForFileExtension(extension, out info);

	internal static bool TryGetEncoderForMimeType(string mimeType, [NotNullWhen(true)] out IImageEncoderInfo? info) => getCodecs().TryGetEncoderForMimeType(mimeType, out info);

	internal static bool SupportsMimeType(this IImageEncoderInfo enc, string mime) => enc.MimeTypes.ContainsInsensitive(mime);

	internal static bool SupportsPixelFormat(this IImageEncoderInfo enc, Guid fmt) => enc.PixelFormats.Contains(fmt);

	internal static ChromaSubsampleMode GetClosestSubsampling(this IPlanarImageEncoderInfo enc, ChromaSubsampleMode sub)
	{
		var modes = enc.SubsampleModes;
		for (int i = 0; i < modes.Length; i++)
			if (modes[i] >= sub)
				return modes[i];

		for (int i = modes.Length - 1; i >= 0; i--)
			if (modes[i] < sub)
				return modes[i];

		return default;
	}

	internal static PixelFormat GetClosestPixelFormat(this IImageEncoderInfo enc, PixelFormat fmt)
	{
		if (enc.SupportsPixelFormat(fmt.FormatGuid))
			return fmt;

		if (fmt.AlphaRepresentation != PixelAlphaRepresentation.None)
		{
			if (enc.SupportsPixelFormat(PixelFormat.Bgra32.FormatGuid))
				return PixelFormat.Bgra32;
			else if (enc.SupportsPixelFormat(PixelFormat.Rgba32.FormatGuid))
				return PixelFormat.Rgba32;
		}
		if (fmt.ColorRepresentation == PixelColorRepresentation.Bgr || fmt == PixelFormat.Grey8)
		{
			if (enc.SupportsPixelFormat(PixelFormat.Bgr24.FormatGuid))
				return PixelFormat.Bgr24;
			else if (enc.SupportsPixelFormat(PixelFormat.Rgb24.FormatGuid))
				return PixelFormat.Rgb24;
			else if (enc.SupportsPixelFormat(PixelFormat.Bgra32.FormatGuid))
				return PixelFormat.Bgra32;
			else if (enc.SupportsPixelFormat(PixelFormat.Rgba32.FormatGuid))
				return PixelFormat.Rgba32;
		}
		else if (fmt == PixelFormat.Y8 || fmt == PixelFormat.Y8Video)
		{
			if (enc.SupportsPixelFormat(PixelFormat.Y8.FormatGuid))
				return PixelFormat.Y8;
			else if (enc.SupportsPixelFormat(PixelFormat.Y8Video.FormatGuid))
				return PixelFormat.Y8Video;
		}
		else if (fmt == PixelFormat.Cb8 || fmt == PixelFormat.Cb8Video)
		{
			if (enc.SupportsPixelFormat(PixelFormat.Cb8.FormatGuid))
				return PixelFormat.Cb8;
			else if (enc.SupportsPixelFormat(PixelFormat.Cb8Video.FormatGuid))
				return PixelFormat.Cb8Video;
		}
		else if (fmt == PixelFormat.Cr8 || fmt == PixelFormat.Cr8Video)
		{
			if (enc.SupportsPixelFormat(PixelFormat.Cr8.FormatGuid))
				return PixelFormat.Cr8;
			else if (enc.SupportsPixelFormat(PixelFormat.Cr8Video.FormatGuid))
				return PixelFormat.Cr8Video;
		}

		return fmt;
	}
}
