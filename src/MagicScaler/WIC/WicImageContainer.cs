// Copyright © Clinton Ingram and Contributors.  Licensed under the MIT License.

using System;
using System.Buffers.Binary;
using System.Runtime.InteropServices;

using TerraFX.Interop;
using static TerraFX.Interop.Windows;

using PhotoSauce.Interop.Wic;
using PhotoSauce.MagicScaler.Transforms;

namespace PhotoSauce.MagicScaler
{
	internal unsafe class WicImageContainer : IImageContainer, IDisposable
	{
		private readonly SafeHandle? handle;

		public IWICBitmapDecoder* WicDecoder { get; private set; }

		public FileFormat ContainerFormat { get; }
		public int FrameCount { get; }

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
			if ((uint)index >= (uint)FrameCount) throw new IndexOutOfRangeException("Frame index does not exist");

			return new WicImageFrame(this, (uint)index);
		}

		public WicImageContainer(IWICBitmapDecoder* dec, SafeHandle? src, FileFormat fmt)
		{
			WicDecoder = dec;
			handle = src;

			uint fcount;
			HRESULT.Check(dec->GetFrameCount(&fcount));
			FrameCount = (int)fcount;
			ContainerFormat = fmt;
		}

		public static WicImageContainer Create(IWICBitmapDecoder* dec, SafeHandle? src = null)
		{
			var guid = default(Guid);
			HRESULT.Check(dec->GetContainerFormat(&guid));

			var fmt = WicImageDecoder.FormatMap.GetValueOrDefault(guid, FileFormat.Unknown);
			if (fmt == FileFormat.Gif)
				return new WicGifContainer(dec, src);

			return new WicImageContainer(dec, src, fmt);
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

			if (disposing)
				handle?.Dispose();

			WicDecoder->Release();
			WicDecoder = null;
		}

		~WicImageContainer() => Dispose(false);
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

	internal unsafe class WicGifContainer : WicImageContainer
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

		public WicGifContainer(IWICBitmapDecoder* dec, SafeHandle? src) : base(dec, src, FileFormat.Gif)
		{
			using var meta = default(ComPtr<IWICMetadataQueryReader>);
			HRESULT.Check(dec->GetMetadataQueryReader(meta.GetAddressOf()));

			ScreenWidth = meta.GetValueOrDefault<ushort>(Wic.Metadata.Gif.LogicalScreenWidth);
			ScreenHeight = meta.GetValueOrDefault<ushort>(Wic.Metadata.Gif.LogicalScreenHeight);

			if (meta.GetValueOrDefault<bool>(Wic.Metadata.Gif.GlobalPaletteFlag))
			{
				using var pal = default(ComPtr<IWICPalette>);
				HRESULT.Check(Wic.Factory->CreatePalette(pal.GetAddressOf()));
				HRESULT.Check(dec->CopyPalette(pal));

				uint pcc;
				HRESULT.Check(pal.Get()->GetColorCount(&pcc));

				uint idx = meta.GetValueOrDefault<byte>(Wic.Metadata.Gif.BackgroundColorIndex);
				if (idx < pcc)
				{
					using var buff = new PoolBuffer<uint>((int)pcc);
					fixed (uint* pbuff = buff.Span)
					{
						HRESULT.Check(pal.Get()->GetColors(pcc, pbuff, &pcc));
						BackgroundColor = pbuff[idx];
					}
				}
			}

			var sbuff = (Span<byte>)stackalloc byte[16];
			var appext = meta.GetValueOrDefault(Wic.Metadata.Gif.AppExtension, sbuff);
			if (appext.Length == 11 && netscape2_0.SequenceEqual(appext) || animexts1_0.SequenceEqual(appext))
			{
				var appdata = meta.GetValueOrDefault(Wic.Metadata.Gif.AppExtensionData, sbuff);
				if (appdata.Length >= 4 && appdata[0] >= 3 && appdata[1] == 1)
					LoopCount = BinaryPrimitives.ReadUInt16LittleEndian(appdata.Slice(2));
			}
		}

		protected override void Dispose(bool disposing)
		{
			if (disposing)
				AnimationContext?.Dispose();

			base.Dispose(disposing);
		}

		~WicGifContainer() => Dispose(false);
	}
}