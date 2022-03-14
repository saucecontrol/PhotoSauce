// Copyright © Clinton Ingram and Contributors.  Licensed under the MIT License.

using System;
using System.Buffers.Binary;
using System.Diagnostics.CodeAnalysis;

using TerraFX.Interop.Windows;

using PhotoSauce.Interop.Wic;

namespace PhotoSauce.MagicScaler
{
	internal unsafe class WicImageContainer : IImageContainer, IMetadataSource, IDisposable
	{
		public IWICBitmapDecoder* WicDecoder { get; private set; }

		public string? MimeType { get; }
		public IDecoderOptions? Options { get; }
		public int FrameCount { get; }
		public int FrameOffset { get; }

		public virtual IImageFrame GetFrame(int index)
		{
			if ((uint)(FrameOffset + index) >= (uint)(FrameOffset + FrameCount)) throw new IndexOutOfRangeException("Invalid frame index.");

			return new WicImageFrame(this, (uint)(FrameOffset + index));
		}

		protected WicImageContainer(IWICBitmapDecoder* dec, string? mime, IDecoderOptions? options)
		{
			WicDecoder = dec;
			MimeType = mime;
			Options = options;

			uint fcount;
			HRESULT.Check(dec->GetFrameCount(&fcount));
			if (options is IMultiFrameDecoderOptions opt)
				(FrameOffset, FrameCount) = opt.FrameRange.GetOffsetAndLength((int)fcount);
			else
				(FrameOffset, FrameCount) = (0, (int)fcount);
		}

		public static WicImageContainer Create(IWICBitmapDecoder* dec, string? mime, IDecoderOptions? options = null)
		{
			if (mime == ImageMimeTypes.Gif)
				return new WicGifContainer(dec, options);

			return new WicImageContainer(dec, mime, options);
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

		public WicGifContainer(IWICBitmapDecoder* dec, IDecoderOptions? options) : base(dec, ImageMimeTypes.Gif, options)
		{
			using var meta = default(ComPtr<IWICMetadataQueryReader>);
			HRESULT.Check(dec->GetMetadataQueryReader(meta.GetAddressOf()));

			int screenWidth = meta.Get()->GetValueOrDefault<ushort>(Wic.Metadata.Gif.LogicalScreenWidth);
			int screenHeight = meta.Get()->GetValueOrDefault<ushort>(Wic.Metadata.Gif.LogicalScreenHeight);

			int bgColor = 0;
			if (meta.Get()->GetValueOrDefault<bool>(Wic.Metadata.Gif.GlobalPaletteFlag))
			{
				using var pal = default(ComPtr<IWICPalette>);
				HRESULT.Check(Wic.Factory->CreatePalette(pal.GetAddressOf()));
				HRESULT.Check(dec->CopyPalette(pal));

				uint cc;
				HRESULT.Check(pal.Get()->GetColorCount(&cc));

				uint idx = meta.Get()->GetValueOrDefault<byte>(Wic.Metadata.Gif.BackgroundColorIndex);
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
			var appext = meta.Get()->GetValueOrDefault(Wic.Metadata.Gif.AppExtension, sbuff);
			if (appext.Length == 11 && (Netscape2_0.SequenceEqual(appext) || Animexts1_0.SequenceEqual(appext)))
			{
				var appdata = meta.Get()->GetValueOrDefault(Wic.Metadata.Gif.AppExtensionData, sbuff);
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