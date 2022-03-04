// Copyright © Clinton Ingram and Contributors.  Licensed under the MIT License.

using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;

using TerraFX.Interop;
using static TerraFX.Interop.Windows;

using PhotoSauce.Interop.Wic;

namespace PhotoSauce.MagicScaler
{
	internal unsafe class WicImageContainer : IImageContainer, IMetadataSource, IDisposable
	{
		public static readonly Dictionary<Guid, FileFormat> FormatMap = new() {
			[GUID_ContainerFormatBmp] = FileFormat.Bmp,
			[GUID_ContainerFormatGif] = FileFormat.Gif,
			[GUID_ContainerFormatJpeg] = FileFormat.Jpeg,
			[GUID_ContainerFormatPng] = FileFormat.Png,
			[GUID_ContainerFormatTiff] = FileFormat.Tiff
		};

		public IWICBitmapDecoder* WicDecoder { get; private set; }

		public FileFormat ContainerFormat { get; }
		public IDecoderOptions? Options { get; }
		public int FrameCount { get; }
		public int FrameOffset { get; }

		public bool IsRawContainer {
			get {
				if (ContainerFormat != FileFormat.Unknown)
					return false;

				var guid = default(Guid);
				HRESULT.Check(WicDecoder->GetContainerFormat(&guid));
				return guid == GUID_ContainerFormatRaw || guid == GUID_ContainerFormatRaw2 || guid == GUID_ContainerFormatAdng;
			}
		}

		public virtual IImageFrame GetFrame(int index)
		{
			if ((uint)(FrameOffset + index) >= (uint)(FrameOffset + FrameCount)) throw new IndexOutOfRangeException("Invalid frame index.");

			return new WicImageFrame(this, (uint)(FrameOffset + index));
		}

		protected WicImageContainer(IWICBitmapDecoder* dec, FileFormat fmt, IDecoderOptions? options)
		{
			WicDecoder = dec;
			ContainerFormat = fmt;
			Options = options;

			uint fcount;
			HRESULT.Check(dec->GetFrameCount(&fcount));
			if (options is IMultiFrameDecoderOptions opt)
				(FrameOffset, FrameCount) = opt.FrameRange.GetOffsetAndLength((int)fcount);
			else
				(FrameOffset, FrameCount) = (0, (int)fcount);
		}

		public static WicImageContainer Create(IWICBitmapDecoder* dec, IDecoderOptions? options = null)
		{
			var guid = default(Guid);
			HRESULT.Check(dec->GetContainerFormat(&guid));

			var fmt = FormatMap.GetValueOrDefault(guid, FileFormat.Unknown);
			if (fmt == FileFormat.Gif)
				return new WicGifContainer(dec, options);

			return new WicImageContainer(dec, fmt, options);
		}

		public virtual bool TryGetMetadata<T>([NotNullWhen(true)] out T? metadata) where T : IMetadata
		{
			metadata = default;
			return false;
		}

		public virtual void Dispose()
		{
			Dispose(true);
			GC.SuppressFinalize(this);
		}

		protected virtual void Dispose(bool disposing)
		{
			if (WicDecoder is null)
				return;

			WicDecoder->Release();
			WicDecoder = null;
		}

		~WicImageContainer() => Dispose(false);
	}

	internal sealed unsafe class WicGifContainer : WicImageContainer
	{
		public static ReadOnlySpan<byte> Animexts1_0 => new[] {
			(byte)'A', (byte)'N', (byte)'I', (byte)'M', (byte)'E', (byte)'X', (byte)'T', (byte)'S', (byte)'1', (byte)'.', (byte)'0'
		};
		public static ReadOnlySpan<byte> Netscape2_0 => new[] {
			(byte)'N', (byte)'E', (byte)'T', (byte)'S', (byte)'C', (byte)'A', (byte)'P', (byte)'E', (byte)'2', (byte)'.', (byte)'0'
		};

		public readonly AnimationContainer AnimationMetadata;

		public bool IsAnimation => FrameOffset + FrameCount > 1;

		public WicGifContainer(IWICBitmapDecoder* dec, IDecoderOptions? options) : base(dec, FileFormat.Gif, options)
		{
			using var meta = default(ComPtr<IWICMetadataQueryReader>);
			HRESULT.Check(dec->GetMetadataQueryReader(meta.GetAddressOf()));

			int screenWidth = meta.GetValueOrDefault<ushort>(Wic.Metadata.Gif.LogicalScreenWidth);
			int screenHeight = meta.GetValueOrDefault<ushort>(Wic.Metadata.Gif.LogicalScreenHeight);

			int bgColor = 0;
			if (meta.GetValueOrDefault<bool>(Wic.Metadata.Gif.GlobalPaletteFlag))
			{
				using var pal = default(ComPtr<IWICPalette>);
				HRESULT.Check(Wic.Factory->CreatePalette(pal.GetAddressOf()));
				HRESULT.Check(dec->CopyPalette(pal));

				uint cc;
				HRESULT.Check(pal.Get()->GetColorCount(&cc));

				uint idx = meta.GetValueOrDefault<byte>(Wic.Metadata.Gif.BackgroundColorIndex);
				if (idx < cc)
				{
					using var buff = BufferPool.RentLocal<uint>((int)cc);
					fixed (uint* pbuff = buff)
					{
						HRESULT.Check(pal.Get()->GetColors(cc, pbuff, &cc));
						bgColor = (int)pbuff[idx];
					}
				}
			}

			int loopCount = 0;
			var sbuff = (Span<byte>)stackalloc byte[16];
			var appext = meta.GetValueOrDefault(Wic.Metadata.Gif.AppExtension, sbuff);
			if (appext.Length == 11 && (Netscape2_0.SequenceEqual(appext) || Animexts1_0.SequenceEqual(appext)))
			{
				var appdata = meta.GetValueOrDefault(Wic.Metadata.Gif.AppExtensionData, sbuff);
				if (appdata.Length >= 4 && appdata[0] >= 3 && appdata[1] == 1)
					loopCount = BinaryPrimitives.ReadUInt16LittleEndian(appdata.Slice(2));
			}

			AnimationMetadata = new(screenWidth, screenHeight, loopCount, bgColor, true);
		}

		public override IImageFrame GetFrame(int index)
		{
			if ((uint)(FrameOffset + index) >= (uint)(FrameOffset + FrameCount)) throw new IndexOutOfRangeException("Invalid frame index.");

			return new WicGifFrame(this, (uint)(FrameOffset + index));
		}

		public override bool TryGetMetadata<T>(out T metadata)
		{
			if (typeof(T) == typeof(AnimationContainer))
			{
				metadata = (T)(object)AnimationMetadata;
				return true;
			}

			return base.TryGetMetadata(out metadata!);
		}

		public void ReplayAnimation(PipelineContext ctx, int offset)
		{
			var anictx = ctx.AnimationContext ??= new();
			for (int i = -offset; i <= 0; i++)
			{
				var gifFrame = (WicGifFrame)GetFrame(i);
				if (gifFrame.AnimationMetadata.Disposal == FrameDisposalMethod.Preserve)
					anictx.UpdateFrameBuffer(gifFrame, AnimationMetadata, gifFrame.AnimationMetadata);

				anictx.LastDisposal = gifFrame.AnimationMetadata.Disposal;
			}
		}
	}
}