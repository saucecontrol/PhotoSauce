using System;
using System.Buffers;
using System.Runtime.InteropServices;
using System.Runtime.CompilerServices;

namespace PhotoSauce.MagicScaler
{
	/// <summary>Represents orientation correction to be applied to an image.  The values in this enumeration match the values defined in the Exif specification.</summary>
	public enum Orientation
	{
		/// <summary>No orientation correction is required.</summary>
		Normal = 1,
		/// <summary>The image should be flipped along the X axis prior to display.</summary>
		FlipHorizontal = 2,
		/// <summary>The image should be rotated 180 degrees prior to display.</summary>
		Rotate180 = 3,
		/// <summary>The image should be flipped along the Y axis prior to display.</summary>
		FlipVertical = 4,
		/// <summary>The image should be flipped along the diagonal axis from top left to bottom right prior to display.</summary>
		Transpose = 5,
		/// <summary>The image should be rotated 90 degrees prior to display.</summary>
		Rotate90 = 6,
		/// <summary>The image should be flipped along the diagonal axis from bottom left to top right prior to display.</summary>
		Transverse = 7,
		/// <summary>The image should be rotated 270 degrees prior to display.</summary>
		Rotate270 = 8
	}

	internal class OrientationTransformInternal : PixelSource, IDisposable
	{
		private readonly Orientation orient;
		private readonly PixelArea srcArea;
		private readonly PixelBuffer srcBuff;
		private readonly IMemoryOwner<byte> lineBuff;
		private readonly int bytesPerPixel;

		public OrientationTransformInternal(PixelSource source, Orientation orientation, PixelArea crop = default) : base(source)
		{
			if (!(Format.BitsPerPixel == 8 || Format.BitsPerPixel == 16 || Format.BitsPerPixel == 24 || Format.BitsPerPixel == 32))
				throw new NotSupportedException("Pixel format not supported.");

			bytesPerPixel = Format.BitsPerPixel / 8;
			orient = orientation;
			srcArea = crop == default ? new PixelArea(0, 0, (int)Source.Width, (int)Source.Height) : crop;

			Width = orient.RequiresDimensionSwap() ? (uint)srcArea.Height : (uint)srcArea.Width;
			Height = orient.RequiresDimensionSwap() ? (uint)srcArea.Width : (uint)srcArea.Height;

			if (orient.RequiresDimensionSwap())
			{
				lineBuff = MemoryPool<byte>.Shared.Rent((int)BufferStride);
				BufferStride = (uint)MathUtil.PowerOf2Ceiling(MathUtil.DivCeiling((int)Width * Format.BitsPerPixel, 8), IntPtr.Size);
			}

			if (orient.RequiresCache())
				srcBuff = new PixelBuffer((int)Height, (int)BufferStride);
		}

		protected override void CopyPixelsInternal(in PixelArea prc, uint cbStride, uint cbBufferSize, IntPtr pbBuffer)
		{
			if (!orient.RequiresCache())
				copyPixelsDirect(prc, cbStride, cbBufferSize, pbBuffer);
			else
				copyPixelsBuffered(prc, cbStride, cbBufferSize, pbBuffer);
		}

		unsafe private void copyPixelsDirect(in PixelArea prc, uint cbStride, uint cbBufferSize, IntPtr pbBuffer)
		{
			int cx = orient == Orientation.Normal ? srcArea.X + prc.X : srcArea.Width - prc.Width - srcArea.X - prc.X;

			Timer.Stop();
			Source.CopyPixels(new PixelArea(cx, srcArea.Y + prc.Y, prc.Width, prc.Height), cbStride, cbBufferSize, pbBuffer);
			Timer.Start();

			if (orient == Orientation.FlipHorizontal)
			{
				int cb = prc.Width * bytesPerPixel;
				byte* pb = (byte*)pbBuffer;

				for (int y = 0; y < prc.Height; y++)
				{
					flipLine(pb, cb);
					pb += cbStride;
				}
			}
		}

		unsafe private void copyPixelsBuffered(in PixelArea prc, uint cbStride, uint cbBufferSize, IntPtr pbBuffer)
		{
			if (!srcBuff.ContainsLine(0))
			{
				int fl = 0, lc = (int)Height;
				fixed (byte* bstart = srcBuff.PrepareLoad(ref fl, ref lc))
				{
					if (!orient.RequiresDimensionSwap())
						loadBufferFlipped(bstart);
					else
						loadBufferRotated(bstart);
				}
			}

			for (int y = 0; y < prc.Height; y++)
			{
				int line = prc.Y + y;

				var lspan = srcBuff.PrepareRead(line, 1).Slice(prc.X * bytesPerPixel, prc.Width * bytesPerPixel);
				Unsafe.CopyBlockUnaligned(ref Unsafe.AsRef<byte>((byte*)pbBuffer + y * cbStride), ref MemoryMarshal.GetReference(lspan), (uint)lspan.Length);
			}
		}

		unsafe private void loadBufferFlipped(byte* bstart)
		{
			int cx = orient == Orientation.FlipVertical ? srcArea.X : (int)Source.Width - srcArea.Width - srcArea.X;
			int cy = (int)Source.Height - srcArea.Height - srcArea.Y;

			byte* pb = bstart + (Height - 1) * (uint)srcBuff.Stride;

			for (int y = 0; y < (int)Height; y++)
			{
				Timer.Stop();
				Source.CopyPixels(new PixelArea(cx, cy + y, srcArea.Width, 1), BufferStride, BufferStride, (IntPtr)pb);
				Timer.Start();

				if (orient == Orientation.Rotate180)
					flipLine(pb, srcArea.Width * bytesPerPixel);

				pb -= BufferStride;
			}
		}

		unsafe private void loadBufferRotated(byte* bstart)
		{
			byte* bp = bstart;
			int colStride = (int)BufferStride;
			int rowStride = bytesPerPixel;
			int cx = srcArea.X;
			int cy = srcArea.Y;

			switch (orient)
			{
				case Orientation.Rotate90:
					bp += ((uint)srcArea.Height - 1) * (uint)bytesPerPixel;
					rowStride = -rowStride;
					cy = (int)Source.Height - srcArea.Height - srcArea.Y;
					break;
				case Orientation.Rotate270:
					bp += ((uint)srcArea.Width - 1) * BufferStride;
					colStride = -colStride;
					cx = (int)Source.Width - srcArea.Width - srcArea.X;
					break;
				case Orientation.Transverse:
					bp += ((uint)srcArea.Width - 1) * BufferStride + ((uint)srcArea.Height - 1) * (uint)bytesPerPixel;
					colStride = -colStride;
					rowStride = -rowStride;
					cx = (int)Source.Width - srcArea.Width - srcArea.X;
					cy = (int)Source.Height - srcArea.Height - srcArea.Y;
					break;
			}

			uint cb = (uint)srcArea.Width * (uint)bytesPerPixel;

			fixed (byte* lp = lineBuff.Memory.Span)
			{
				for (int y = 0; y < srcArea.Height; y++)
				{
					Timer.Stop();
					Source.CopyPixels(new PixelArea(cx, cy + y, srcArea.Width, 1), cb, cb, (IntPtr)lp);
					Timer.Start();

					byte* ip = lp, ipe = lp + cb;
					byte* op = bp + y * rowStride;

					switch (bytesPerPixel)
					{
						case 1:
							while (ip < ipe)
							{
								*op = *ip;

								ip++;
								op += colStride;
							}
							break;
						case 2:
							while (ip < ipe)
							{
								*(ushort*)op = *(ushort*)ip;

								ip += 2;
								op += colStride;
							}
							break;
						case 4:
							while (ip < ipe)
							{
								*(uint*)op = *(uint*)ip;

								ip += 4;
								op += colStride;
							}
							break;
						default:
							while (ip < ipe)
							{
								*(ushort*)op = *(ushort*)ip;
								op[2] = ip[2];

								ip += 3;
								op += colStride;
							}
							break;
					}
				}
			}
		}

		unsafe private void flipLine(byte* bp, int cb)
		{
			byte* pp = bp, pe = pp + cb - (uint)bytesPerPixel;

			switch (bytesPerPixel)
			{
				case 1:
					while (pp < pe)
					{
						byte t0 = *pe;
						*pe = *pp;
						*pp = t0;

						pe--;
						pp++;
					}
					break;
				case 2:
					while (pp < pe)
					{
						ushort t0 = *(ushort*)pe;

						*(ushort*)pe = *(ushort*)pp;
						*(ushort*)pp = t0;

						pe -= 2;
						pp += 2;
					}
					break;
				case 4:
					while (pp < pe)
					{
						uint t0 = *(uint*)pe;

						*(uint*)pe = *(uint*)pp;
						*(uint*)pp = t0;

						pe -= 4;
						pp += 4;
					}
					break;
				default:
					while (pp < pe)
					{
						ushort t0 = *(ushort*)pe;
						byte t1 = pe[2];

						*(ushort*)pe = *(ushort*)pp;
						pe[2] = pp[2];
						*(ushort*)pp = t0;
						pp[2] = t1;

						pe -= 3;
						pp += 3;
					}
					break;
			}
		}

		public void Dispose()
		{
			srcBuff?.Dispose();
			lineBuff?.Dispose();
		}
	}

	/// <summary>Transforms an image by changing its column/row order according to an <see cref="Orientation" /> value.</summary>
	public sealed class OrientationTransform : PixelTransform, IPixelTransformInternal
	{
		private readonly Orientation orientation;

		/// <summary>Creates a new transform with the specified <paramref name="orientation" /> value.</summary>
		/// <param name="orientation">The <see cref="Orientation" /> correction to apply to the image.</param>
		public OrientationTransform(Orientation orientation) => this.orientation = orientation;

		void IPixelTransformInternal.Init(PipelineContext ctx)
		{
			MagicTransforms.AddRotator(ctx, orientation);

			Source = ctx.Source;
		}
	}
}
