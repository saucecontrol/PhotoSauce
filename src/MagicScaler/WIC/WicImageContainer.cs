// Copyright © Clinton Ingram and Contributors.  Licensed under the MIT License.

using System;
using System.Buffers.Binary;
using System.Diagnostics.CodeAnalysis;

using TerraFX.Interop.Windows;

using PhotoSauce.Interop.Wic;

namespace PhotoSauce.MagicScaler;

internal unsafe class WicImageContainer : IImageContainer
{
	public IWICBitmapDecoder* WicDecoder { get; private set; }

	public string? MimeType { get; }
	public IDecoderOptions? Options { get; }
	public int FrameCount { get; }
	protected int FrameOffset { get; }

	public virtual IImageFrame GetFrame(int index)
	{
		uint fidx = (uint)(FrameOffset + index);
		if (fidx >= (uint)(FrameOffset + FrameCount)) throw new ArgumentOutOfRangeException(nameof(index), "Invalid frame index.");

		if (MimeType == ImageMimeTypes.Jpeg && Options is not IPlanarDecoderOptions { AllowPlanar: false })
			return new WicPlanarFrame(this, fidx);

		return new WicImageFrame(this, fidx);
	}

	protected WicImageContainer(IWICBitmapDecoder* dec, string? mime, IDecoderOptions? options)
	{
		WicDecoder = dec;
		MimeType = mime;
		Options = options;

		uint fcount;
		HRESULT.Check(dec->GetFrameCount(&fcount));

		var range = options is IMultiFrameDecoderOptions mul ? mul.FrameRange : Range.All;
		(FrameOffset, FrameCount) = range.GetOffsetAndLengthNoThrow((int)fcount);
	}

	public static WicImageContainer Create(IWICBitmapDecoder* dec, string? mime, IDecoderOptions? options = null)
	{
		if (mime == ImageMimeTypes.Gif)
			return new WicGifContainer(dec, options);

		return new WicImageContainer(dec, mime, options);
	}

	protected virtual void Dispose(bool disposing)
	{
		if (WicDecoder is null)
			return;

		WicDecoder->Release();
		WicDecoder = null;

		if (disposing)
			GC.SuppressFinalize(this);
	}

	public void Dispose() => Dispose(true);

	~WicImageContainer()
	{
		ThrowHelper.ThrowIfFinalizerExceptionsEnabled(nameof(WicImageContainer));

		Dispose(false);
	}
}

internal sealed unsafe class WicGifContainer : WicImageContainer, IMetadataSource
{
	public static ReadOnlySpan<byte> Animexts1_0 => "ANIMEXTS1.0"u8;
	public static ReadOnlySpan<byte> Netscape2_0 => "NETSCAPE2.0"u8;

	public readonly AnimationContainer AnimationMetadata;

	public bool IsAnimation => FrameOffset + FrameCount > 1;

	public WicGifContainer(IWICBitmapDecoder* dec, IDecoderOptions? options) : base(dec, ImageMimeTypes.Gif, options)
	{
		using var meta = default(ComPtr<IWICMetadataQueryReader>);
		HRESULT.Check(dec->GetMetadataQueryReader(meta.GetAddressOf()));

		bool useBackground = options is IAnimationDecoderOptions { UseBackgroundColor: true };
		int screenWidth = meta.Get()->GetValueOrDefault<ushort>(Wic.Metadata.Gif.LogicalScreenWidth);
		int screenHeight = meta.Get()->GetValueOrDefault<ushort>(Wic.Metadata.Gif.LogicalScreenHeight);

		uint bgColor = default;
		if (meta.Get()->GetValueOrDefault<bool>(Wic.Metadata.Gif.GlobalPaletteFlag))
		{
			using var pal = default(ComPtr<IWICPalette>);
			HRESULT.Check(Wic.Factory->CreatePalette(pal.GetAddressOf()));
			HRESULT.Check(dec->CopyPalette(pal));

			uint cc;
			HRESULT.Check(pal.Get()->GetColorCount(&cc));

			uint idx = meta.Get()->GetValueOrDefault<byte>(Wic.Metadata.Gif.BackgroundColorIndex);
			if (useBackground && idx < cc)
			{
				using var buff = BufferPool.RentLocal<uint>((int)cc);
				fixed (uint* pbuff = buff)
				{
					HRESULT.Check(pal.Get()->GetColors(cc, pbuff, &cc));
					bgColor = pbuff[idx];
				}
			}
		}

		int loopCount = 1;
		var sbuff = (Span<byte>)stackalloc byte[16];
		var appext = meta.Get()->GetValueOrDefault(Wic.Metadata.Gif.AppExtension, sbuff);
		if (appext.SequenceEqual(Netscape2_0) || appext.SequenceEqual(Animexts1_0))
		{
			var appdata = meta.Get()->GetValueOrDefault(Wic.Metadata.Gif.AppExtensionData, sbuff);
			if (appdata.Length >= 4 && appdata[0] >= 3 && appdata[1] == 1)
				loopCount = BinaryPrimitives.ReadUInt16LittleEndian(appdata[2..]);
		}

		int par = meta.Get()->GetValueOrDefault<byte>(Wic.Metadata.Gif.PixelAspectRatio);
		float pixelAspect = par == default ? 1f : ((par + 15) / 64f);

		uint fcount;
		WicDecoder->GetFrameCount(&fcount);

		using var frame = default(ComPtr<IWICBitmapFrameDecode>);
		HRESULT.Check(WicDecoder->GetFrame(0, frame.GetAddressOf()));

		using var frmmeta = default(ComPtr<IWICMetadataQueryReader>);
		HRESULT.Check(frame.Get()->GetMetadataQueryReader(frmmeta.GetAddressOf()));

		if (frmmeta.Get()->GetValueOrDefault<bool>(Wic.Metadata.Gif.TransparencyFlag))
			bgColor = default;

		AnimationMetadata = new(screenWidth, screenHeight, (int)fcount, loopCount, (int)bgColor, pixelAspect, true);
	}

	public override IImageFrame GetFrame(int index)
	{
		if ((uint)(FrameOffset + index) >= (uint)(FrameOffset + FrameCount)) throw new ArgumentOutOfRangeException(nameof(index), "Invalid frame index.");

		return new WicGifFrame(this, (uint)(FrameOffset + index));
	}

	public bool TryGetMetadata<T>([NotNullWhen(true)] out T? metadata) where T : IMetadata
	{
		if (typeof(T) == typeof(AnimationContainer))
		{
			metadata = (T)(object)AnimationMetadata;
			return true;
		}

		metadata = default;
		return false;
	}
}