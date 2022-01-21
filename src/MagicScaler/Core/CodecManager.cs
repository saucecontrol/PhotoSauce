// Copyright © Clinton Ingram and Contributors.  Licensed under the MIT License.

using System;
using System.IO;
using System.Linq;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Runtime.CompilerServices;
using System.Diagnostics.CodeAnalysis;

#if HWINTRINSICS
using System.Runtime.Intrinsics;
using System.Runtime.Intrinsics.X86;
#endif

namespace PhotoSauce.MagicScaler
{
	internal static class KnownMimeTypes
	{
		public const string Avif = "image/avif";
		public const string Bmp = "image/bmp";
		public const string Dds = "image/vnd.ms-dds";
		public const string Gif = "image/gif";
		public const string Heif = "image/heif";
		public const string Jpeg = "image/jpeg";
		public const string Jxl = "image/jxl";
		public const string Jxr = "image/vnd.ms-photo"; // actual IANA assignment is image/jxr, but WIC reports otherwise
		public const string Png = "image/png";
		public const string Tiff = "image/tiff";
		public const string Webp = "image/webp";
	}

	internal static class KnownFileExtensions
	{
		public const string Avif = ".avif";
		public const string Bmp = ".bmp";
		public const string Dds = ".dds";
		public const string Gif = ".gif";
		public const string Heif = ".heif";
		public const string Jpeg = ".jpg";
		public const string Jxl = ".jxl";
		public const string Jxr = ".jxr";
		public const string Png = ".png";
		public const string Tiff = ".tif";
		public const string Webp = ".webp";
	}

	/// <summary>A pattern used to match <a href="https://en.wikipedia.org/wiki/List_of_file_signatures">magic bytes</a> in an image file header.</summary>
	/// <param name="Offset">Number of bytes to skip from the start of the file before attempting pattern match.</param>
	/// <param name="Pattern">A byte pattern to match at the given <paramref name="Offset" />.</param>
	/// <param name="Mask">A mask to apply to image bytes (using binary <see langword="&amp;" />) before matching against the <paramref name="Pattern"/>.</param>
	/// <remarks>The total length described by (<paramref name="Offset" /> + <paramref name="Pattern" />.Length) should not be more than 16 bytes.</remarks>
	public readonly record struct ContainerPattern(int Offset, byte[] Pattern, byte[] Mask);

	/// <inheritdoc />
	/// <param name="Name"><inheritdoc cref="IImageCodecInfo.Name" path="/summary/node()" /></param>
	/// <param name="MimeTypes"><inheritdoc cref="IImageCodecInfo.MimeTypes" path="/summary/node()" /></param>
	/// <param name="FileExtensions"><inheritdoc cref="IImageCodecInfo.FileExtensions" path="/summary/node()" /></param>
	/// <param name="SupportsTransparency"><inheritdoc cref="IImageCodecInfo.SupportsTransparency" path="/summary/node()" /></param>
	/// <param name="SupportsMultiFrame"><inheritdoc cref="IImageCodecInfo.SupportsMultiFrame" path="/summary/node()" /></param>
	/// <param name="SupportsAnimation"><inheritdoc cref="IImageCodecInfo.SupportsAnimation" path="/summary/node()" /></param>
	public abstract record class CodecInfo(
		string Name,
		IEnumerable<string> MimeTypes,
		IEnumerable<string> FileExtensions,
		bool SupportsTransparency,
		bool SupportsMultiFrame,
		bool SupportsAnimation
	) : IImageCodecInfo;

	/// <inheritdoc />
	/// <param name="Name"><inheritdoc cref="IImageCodecInfo.Name" path="/summary/node()" /></param>
	/// <param name="MimeTypes"><inheritdoc cref="IImageCodecInfo.MimeTypes" path="/summary/node()" /></param>
	/// <param name="FileExtensions"><inheritdoc cref="IImageCodecInfo.FileExtensions" path="/summary/node()" /></param>
	/// <param name="Patterns"><inheritdoc cref="IImageDecoderInfo.Patterns" path="/summary/node()" /></param>
	/// <param name="DefaultOptions"><inheritdoc cref="IImageDecoderInfo.DefaultOptions" path="/summary/node()" /></param>
	/// <param name="Factory"><inheritdoc cref="IImageDecoderInfo.Factory" path="/summary/node()" /></param>
	/// <param name="SupportsTransparency"><inheritdoc cref="IImageCodecInfo.SupportsTransparency" path="/summary/node()" /></param>
	/// <param name="SupportsMultiFrame"><inheritdoc cref="IImageCodecInfo.SupportsMultiFrame" path="/summary/node()" /></param>
	/// <param name="SupportsAnimation"><inheritdoc cref="IImageCodecInfo.SupportsAnimation" path="/summary/node()" /></param>
	public sealed record class DecoderInfo(
		string Name,
		IEnumerable<string> MimeTypes,
		IEnumerable<string> FileExtensions,
		IEnumerable<ContainerPattern> Patterns,
		IDecoderOptions? DefaultOptions,
		Func<Stream, IDecoderOptions?, IImageContainer?> Factory,
		bool SupportsTransparency,
		bool SupportsMultiFrame,
		bool SupportsAnimation
	) : CodecInfo(Name, MimeTypes, FileExtensions, SupportsTransparency, SupportsMultiFrame, SupportsAnimation), IImageDecoderInfo;

	/// <inheritdoc />
	/// <param name="Name"><inheritdoc cref="IImageCodecInfo.Name" path="/summary/node()" /></param>
	/// <param name="MimeTypes"><inheritdoc cref="IImageCodecInfo.MimeTypes" path="/summary/node()" /></param>
	/// <param name="FileExtensions"><inheritdoc cref="IImageCodecInfo.FileExtensions" path="/summary/node()" /></param>
	/// <param name="DefaultOptions"><inheritdoc cref="IImageEncoderInfo.DefaultOptions" path="/summary/node()" /></param>
	/// <param name="Factory"><inheritdoc cref="IImageEncoderInfo.Factory" path="/summary/node()" /></param>
	/// <param name="SupportsTransparency"><inheritdoc cref="IImageCodecInfo.SupportsTransparency" path="/summary/node()" /></param>
	/// <param name="SupportsMultiFrame"><inheritdoc cref="IImageCodecInfo.SupportsMultiFrame" path="/summary/node()" /></param>
	/// <param name="SupportsAnimation"><inheritdoc cref="IImageCodecInfo.SupportsAnimation" path="/summary/node()" /></param>
	/// <param name="SupportsColorProfile"><inheritdoc cref="IImageEncoderInfo.SupportsColorProfile" path="/summary/node()" /></param>
	public sealed record class EncoderInfo(
		string Name,
		IEnumerable<string> MimeTypes,
		IEnumerable<string> FileExtensions,
		IEncoderOptions? DefaultOptions,
		Func<Stream, IEncoderOptions?, IImageEncoder> Factory,
		bool SupportsTransparency,
		bool SupportsMultiFrame,
		bool SupportsAnimation,
		bool SupportsColorProfile
	) : CodecInfo(Name, MimeTypes, FileExtensions, SupportsTransparency, SupportsMultiFrame, SupportsAnimation), IImageEncoderInfo;

	/// <summary>Represents the set of configured codecs for the processing pipeline.</summary>
	/// <remarks>Instances should not be retained or used outside of <see cref="CodecManager.Configure(Action{CodecCollection}?)"/>.</remarks>
	public sealed class CodecCollection : ICollection<IImageCodecInfo>, IReadOnlyCollection<IImageCodecInfo>
	{
		private readonly List<IImageCodecInfo> codecs = new();
		private readonly List<DecoderPattern> decoderPatternMap = new();
		private readonly Dictionary<string, IImageEncoderInfo> encoderMimeMap = new();
		private readonly Dictionary<string, IImageEncoderInfo> encoderExtensionMap = new();

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
			if ((stm.Length - stm.Position) < sizeof(ulong) * 2)
				throw new InvalidDataException("The given data is too small to be a valid image.");

			int rem = sizeof(ulong) * 2;
#if BUILTIN_SPAN
			using var patBuffer = BufferPool.RentLocal<byte>(rem);
			while (rem > 0)
				rem -= stm.Read(patBuffer.Span.Slice(patBuffer.Length - rem));
#else
			using var patBuffer = BufferPool.RentLocalArray<byte>(rem);
			while (rem > 0)
				rem -= stm.Read(patBuffer.Array, patBuffer.Length - rem, rem);
#endif

			stm.Seek(-patBuffer.Length, SeekOrigin.Current);
			ref byte testval = ref MemoryMarshal.GetReference(patBuffer.Span);

#if HWINTRINSICS
			if (Sse2.IsSupported)
			{
				var vtv = Unsafe.ReadUnaligned<Vector128<byte>>(ref testval);
				foreach (var pat in decoderPatternMap)
				{
					var vcv = Unsafe.ReadUnaligned<Vector128<byte>>(ref Unsafe.As<ulong, byte>(ref Unsafe.AsRef(in pat.p1)));
					var vcm = Unsafe.ReadUnaligned<Vector128<byte>>(ref Unsafe.As<ulong, byte>(ref Unsafe.AsRef(in pat.m1)));
					if (Sse2.MoveMask(Sse2.CompareEqual(Sse2.And(vtv, vcm), vcv)) == ushort.MaxValue)
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
				foreach (var pat in decoderPatternMap)
				{
					if ((((tv0 & pat.m1) ^ pat.p1) | ((tv1 & pat.m2) ^ pat.p2)) == 0)
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
			decoderPatternMap.Clear();
			foreach (var dec in codecs.OfType<IImageDecoderInfo>().SelectMany(i => i.Patterns.Select(s => (Info: i, Pattern: s))))
			{
				var pat = new DecoderPattern { dec = dec.Info };
				int offset = dec.Pattern.Offset.Clamp(0, sizeof(ulong) * 2);
				Unsafe.CopyBlockUnaligned(ref Unsafe.Add(ref Unsafe.As<ulong, byte>(ref pat.m1), offset), ref MemoryMarshal.GetReference(dec.Pattern.Mask.AsSpan()), (uint)(dec.Pattern.Mask?.Length ?? 0).Clamp(0, sizeof(ulong) * 2 - offset));
				Unsafe.CopyBlockUnaligned(ref Unsafe.Add(ref Unsafe.As<ulong, byte>(ref pat.p1), offset), ref MemoryMarshal.GetReference(dec.Pattern.Pattern.AsSpan()), (uint)(dec.Pattern.Pattern?.Length ?? 0).Clamp(0, sizeof(ulong) * 2 - offset));
				decoderPatternMap.Add(pat);
			}

			encoderMimeMap.Clear();
			foreach (var enc in codecs.OfType<IImageEncoderInfo>().SelectMany(i => i.MimeTypes.Select(s => (Info: i, MimeType: s))))
			{
				if (!encoderMimeMap.ContainsKey(enc.MimeType))
					encoderMimeMap.Add(enc.MimeType, enc.Info);
			}

			encoderExtensionMap.Clear();
			foreach (var enc in codecs.OfType<IImageEncoderInfo>().SelectMany(i => i.FileExtensions.Select(s => (Info: i, Extension: s))))
			{
				string ext = enc.Extension;
				if (string.IsNullOrEmpty(ext))
					continue;

				if (ext[0] != '.')
					ext = string.Concat(".", ext);

				if (!encoderExtensionMap.ContainsKey(ext))
					encoderExtensionMap.Add(ext, enc.Info);
			}
		}

#pragma warning disable CS0649 // fields are never initialized
		private struct DecoderPattern { public ulong m1; public ulong m2; public ulong p1; public ulong p2; public IImageDecoderInfo dec; }
#pragma warning restore CS0649
	}

	/// <summary>Manages registration of encoders, decoders, and image type mappings.</summary>
	public static class CodecManager
	{
		private static volatile CodecCollection? codecs;

		internal static readonly EncoderInfo FallbackEncoder = new(
			nameof(FallbackEncoder),
			new[] { string.Empty },
			new[] { string.Empty },
			null,
			(s, o) => throw new NotSupportedException("No encoders are registered."),
			false, false, false, false
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

		internal static IImageContainer GetDecoderForStream(Stream stm) => getCodecs().GetDecoderForStream(stm);

		internal static bool TryGetEncoderForFileExtension(string extension, [NotNullWhen(true)] out IImageEncoderInfo? info) => getCodecs().TryGetEncoderForFileExtension(extension, out info);

		internal static bool TryGetEncoderForMimeType(string mimeType, [NotNullWhen(true)] out IImageEncoderInfo? info) => getCodecs().TryGetEncoderForMimeType(mimeType, out info);
	}
}
