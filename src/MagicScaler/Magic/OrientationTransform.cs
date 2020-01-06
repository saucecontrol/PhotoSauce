using System;
using System.Buffers;
using System.Numerics;
using System.Runtime.InteropServices;
using System.Runtime.CompilerServices;

namespace PhotoSauce.MagicScaler.Transforms
{
	internal class OrientationTransformInternal : PixelSource, IDisposable
	{
		private readonly Orientation orient;
		private readonly PixelBuffer? srcBuff;
		private readonly int bytesPerPixel;

		private byte[]? lineBuff;

		public OrientationTransformInternal(PixelSource source, Orientation orientation) : base(source)
		{
			bytesPerPixel = Format.BytesPerPixel;
			if (!(bytesPerPixel == 1 || bytesPerPixel == 2 || bytesPerPixel == 3 || bytesPerPixel == 4 || bytesPerPixel == 16))
				throw new NotSupportedException("Pixel format not supported.");

			orient = orientation;
			if (orient.SwapsDimensions())
			{
				lineBuff = ArrayPool<byte>.Shared.Rent(BufferStride);

				Width = Source.Height;
				Height = Source.Width;
				BufferStride = MathUtil.PowerOfTwoCeiling(Width * bytesPerPixel, IntPtr.Size);
			}

			if (orient.RequiresCache())
				srcBuff = new PixelBuffer(Height, BufferStride);
		}

		protected override void CopyPixelsInternal(in PixelArea prc, int cbStride, int cbBufferSize, IntPtr pbBuffer)
		{
			if (!orient.RequiresCache())
				copyPixelsDirect(prc, cbStride, cbBufferSize, pbBuffer);
			else
				copyPixelsBuffered(prc, cbStride, cbBufferSize, pbBuffer);
		}

		unsafe private void copyPixelsDirect(in PixelArea prc, int cbStride, int cbBufferSize, IntPtr pbBuffer)
		{
			Profiler.PauseTiming();
			Source.CopyPixels(prc, cbStride, cbBufferSize, pbBuffer);
			Profiler.ResumeTiming();

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

		unsafe private void copyPixelsBuffered(in PixelArea prc, int cbStride, int cbBufferSize, IntPtr pbBuffer)
		{
			if (!srcBuff!.ContainsLine(0))
			{
				int fl = 0, lc = Height;
				fixed (byte* bstart = srcBuff.PrepareLoad(ref fl, ref lc))
				{
					if (!orient.SwapsDimensions())
						loadBufferReversed(bstart);
					else
						loadBufferTransposed(bstart);
				}
			}

			for (int y = 0; y < prc.Height; y++)
			{
				int line = prc.Y + y;

				var lspan = srcBuff.PrepareRead(line, 1).Slice(prc.X * bytesPerPixel, prc.Width * bytesPerPixel);
				Unsafe.CopyBlockUnaligned(ref Unsafe.AsRef<byte>((byte*)pbBuffer + y * cbStride), ref MemoryMarshal.GetReference(lspan), (uint)lspan.Length);
			}
		}

		unsafe private void loadBufferReversed(byte* bstart)
		{
			byte* pb = bstart + (Height - 1) * BufferStride;

			for (int y = 0; y < Height; y++)
			{
				Profiler.PauseTiming();
				Source.CopyPixels(new PixelArea(0, y, Source.Width, 1), BufferStride, BufferStride, (IntPtr)pb);
				Profiler.ResumeTiming();

				if (orient == Orientation.Rotate180)
					flipLine(pb, Source.Width * bytesPerPixel);

				pb -= BufferStride;
			}
		}

		unsafe private void loadBufferTransposed(byte* bstart)
		{
			byte* bp = bstart;
			int colStride = BufferStride;
			int rowStride = bytesPerPixel;

			if (orient == Orientation.Transverse || orient == Orientation.Rotate270)
			{
				bp += (Source.Width - 1) * BufferStride;
				colStride = -colStride;
			}

			if (orient == Orientation.Transverse || orient == Orientation.Rotate90)
			{
				bp += (Source.Height - 1) * bytesPerPixel;
				rowStride = -rowStride;
			}

			int cb = Source.Width * bytesPerPixel;

			fixed (byte* lp = &lineBuff![0])
			{
				for (int y = 0; y < Source.Height; y++)
				{
					Profiler.PauseTiming();
					Source.CopyPixels(new PixelArea(0, y, Source.Width, 1), cb, cb, (IntPtr)lp);
					Profiler.ResumeTiming();

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
						case 3:
							while (ip < ipe)
							{
								*(ushort*)op = *(ushort*)ip;
								op[2] = ip[2];

								ip += 3;
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
					}
				}
			}
		}

		unsafe private void flipLine(byte* bp, int cb)
		{
			byte* pp = bp, pe = pp + cb - bytesPerPixel;

			switch (bytesPerPixel)
			{
				case 1:
					while (pp < pe)
					{
						byte t0 = *pe;
						*pe-- = *pp;
						*pp++ = t0;
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
				case 3:
					while (pp < pe)
					{
						ushort t0 = *(ushort*)pe;
						*(ushort*)pe = *(ushort*)pp;
						*(ushort*)pp = t0;

						byte t1 = pe[2];
						pe[2] = pp[2];
						pp[2] = t1;

						pe -= 3;
						pp += 3;
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
				case 16:
					while (pp < pe)
					{
						Vector4 t0 = Unsafe.ReadUnaligned<Vector4>(pe);

						Unsafe.WriteUnaligned(pe, Unsafe.ReadUnaligned<Vector4>(pp));
						Unsafe.WriteUnaligned(pp, t0);

						pe -= Unsafe.SizeOf<Vector4>();
						pp += Unsafe.SizeOf<Vector4>();
					}
					break;
			}
		}

		public void Dispose()
		{
			srcBuff?.Dispose();

			ArrayPool<byte>.Shared.Return(lineBuff ?? Array.Empty<byte>());
			lineBuff = null;
		}
	}

	/// <summary>Transforms an image by changing its column/row order according to an <see cref="Orientation" /> value.</summary>
	public sealed class OrientationTransform : PixelTransformInternalBase, IPixelTransformInternal
	{
		private readonly Orientation orientation;

		/// <summary>Creates a new transform with the specified <paramref name="orientation" /> value.</summary>
		/// <param name="orientation">The <see cref="Orientation" /> correction to apply to the image.</param>
		public OrientationTransform(Orientation orientation) => this.orientation = orientation;

		void IPixelTransformInternal.Init(PipelineContext ctx)
		{
			MagicTransforms.AddFlipRotator(ctx, orientation);

			Source = ctx.Source;
		}
	}
}
