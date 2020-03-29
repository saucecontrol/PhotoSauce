using System;
using System.Buffers;
using System.Buffers.Binary;

using PhotoSauce.Interop.Wic;
using PhotoSauce.MagicScaler.Transforms;

namespace PhotoSauce.MagicScaler
{
	internal class WicImageContainer : IImageContainer
	{
		public IWICBitmapDecoder WicDecoder { get; }
		public FileFormat ContainerFormat { get; }
		public int FrameCount { get; }

		public bool IsRawContainer {
			get {
				if (ContainerFormat != FileFormat.Unknown)
					return false;

				var guid = WicDecoder.GetContainerFormat();
				return guid == Consts.GUID_ContainerFormatRaw || guid == Consts.GUID_ContainerFormatRaw2 || guid == Consts.GUID_ContainerFormatAdng;
			}
		}

		public virtual IImageFrame GetFrame(int index)
		{
			if ((uint)index >= (uint)FrameCount) throw new IndexOutOfRangeException("Frame index does not exist");

			return new WicImageFrame(this, (uint)index);
		}

		public WicImageContainer(IWICBitmapDecoder dec, WicPipelineContext ctx, FileFormat fmt)
		{
			WicDecoder = ctx.AddRef(dec);

			ContainerFormat = fmt;
			FrameCount = (int)dec.GetFrameCount();
		}

		public static WicImageContainer Create(IWICBitmapDecoder dec, PipelineContext ctx)
		{
			var fmt = WicImageDecoder.FormatMap.GetValueOrDefault(dec.GetContainerFormat(), FileFormat.Unknown);
			if (fmt == FileFormat.Gif)
				return ctx.AddDispose(new WicGifContainer(dec, ctx.WicContext));

			return new WicImageContainer(dec, ctx.WicContext, fmt);
		}
	}

	internal class GifAnimationContext : IDisposable
	{
		public FrameBufferSource? FrameBufferSource;
		public OverlayTransform? FrameOverlay;
		public GifDisposalMethod LastDisposal = GifDisposalMethod.RestoreBackground;
		public int LastFrame = -1;

		public void Dispose()
		{
			FrameBufferSource?.Dispose();
			FrameOverlay?.Dispose();
		}
	}

	internal class WicGifContainer : WicImageContainer, IDisposable
	{
		private static ReadOnlySpan<byte> animexts1_0 => new[] {
			(byte)'A', (byte)'N', (byte)'I', (byte)'M', (byte)'E', (byte)'X', (byte)'T', (byte)'S', (byte)'1', (byte)'.', (byte)'0'
		};
		private static ReadOnlySpan<byte> netscape2_0 => new[] {
			(byte)'N', (byte)'E', (byte)'T', (byte)'S', (byte)'C', (byte)'A', (byte)'P', (byte)'E', (byte)'2', (byte)'.', (byte)'0'
		};

		public readonly ushort LoopCount;
		public readonly ushort ScreenWidth;
		public readonly ushort ScreenHeight;
		public readonly uint BackgroundColor;

		public GifAnimationContext? AnimationContext { get; set; }

		public WicGifContainer(IWICBitmapDecoder dec, WicPipelineContext ctx) : base(dec, ctx, FileFormat.Gif)
		{
			using var wicmeta = ComHandle.Wrap(dec.GetMetadataQueryReader());
			var meta = wicmeta.ComObject;

			ScreenWidth = meta.GetValueOrDefault<ushort>(Wic.Metadata.Gif.LogicalScreenWidth);
			ScreenHeight = meta.GetValueOrDefault<ushort>(Wic.Metadata.Gif.LogicalScreenHeight);

			if (meta.GetValueOrDefault<bool>(Wic.Metadata.Gif.GlobalPaletteFlag))
			{
				using var wicpal = ComHandle.Wrap(Wic.Factory.CreatePalette());
				var pal = wicpal.ComObject;
				dec.CopyPalette(pal);

				uint pcc = pal.GetColorCount();
				uint idx = meta.GetValueOrDefault<byte>(Wic.Metadata.Gif.BackgroundColorIndex);
				if (idx < pcc)
				{
					var buff = ArrayPool<uint>.Shared.Rent((int)pcc);

					pal.GetColors(pcc, buff);
					BackgroundColor = buff[idx];

					ArrayPool<uint>.Shared.Return(buff);
				}
			}

			var appext = meta.GetValueOrDefault<byte[]>(Wic.Metadata.Gif.AppExtension).AsSpan();
			if (appext.Length == 11 && netscape2_0.SequenceEqual(appext) || animexts1_0.SequenceEqual(appext))
			{
				var appdata = meta.GetValueOrDefault<byte[]>(Wic.Metadata.Gif.AppExtensionData).AsSpan();
				if (appdata.Length >= 4 && appdata[0] >= 3 && appdata[1] == 1)
					LoopCount = BinaryPrimitives.ReadUInt16LittleEndian(appdata.Slice(2));
			}
		}

		public void Dispose() => AnimationContext?.Dispose();
	}
}