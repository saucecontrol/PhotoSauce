using System;
using System.Numerics;
using System.Runtime.InteropServices;
using System.Runtime.CompilerServices;

namespace PhotoSauce.MagicScaler.Transforms
{
	internal sealed class OrientationTransformInternal : ChainedPixelSource, IDisposable
	{
		private readonly Orientation orient;
		private readonly PixelBuffer? srcBuff;
		private readonly int bytesPerPixel;

		private ArraySegment<byte> lineBuff;

		public override int Width { get; }
		public override int Height { get; }

		public OrientationTransformInternal(PixelSource source, Orientation orientation) : base(source)
		{
			bytesPerPixel = source.Format.BytesPerPixel;
			if (!(bytesPerPixel == 1 || bytesPerPixel == 2 || bytesPerPixel == 3 || bytesPerPixel == 4 || bytesPerPixel == 16))
				throw new NotSupportedException("Pixel format not supported.");

			Width = source.Width;
			Height = source.Height;

			orient = orientation;
			if (orient.SwapsDimensions())
			{
				lineBuff = BufferPool.Rent(BufferStride);
				(Width, Height) = (Height, Width);
			}

			int bufferStride = MathUtil.PowerOfTwoCeiling(Width * bytesPerPixel, IntPtr.Size);
			if (orient.RequiresCache())
				srcBuff = new PixelBuffer(Height, bufferStride);
		}

		protected override void CopyPixelsInternal(in PixelArea prc, int cbStride, int cbBufferSize, IntPtr pbBuffer)
		{
			if (orient.RequiresCache())
				copyPixelsBuffered(prc, cbStride, cbBufferSize, pbBuffer);
			else
				copyPixelsDirect(prc, cbStride, cbBufferSize, pbBuffer);
		}

		protected override void Reset() => srcBuff?.Reset();

		unsafe private void copyPixelsDirect(in PixelArea prc, int cbStride, int cbBufferSize, IntPtr pbBuffer)
		{
			Profiler.PauseTiming();
			PrevSource.CopyPixels(prc, cbStride, cbBufferSize, pbBuffer);
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
			if (srcBuff is null) throw new ObjectDisposedException(nameof(OrientationTransformInternal));

			if (!srcBuff.ContainsLine(0))
			{
				int fl = 0, lc = Height;
				fixed (byte* bstart = srcBuff.PrepareLoad(ref fl, ref lc))
				{
					if (orient.SwapsDimensions())
						loadBufferTransposed(bstart);
					else
						loadBufferReversed(bstart);
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
			byte* pb = bstart + (Height - 1) * srcBuff!.Stride;

			for (int y = 0; y < Height; y++)
			{
				Profiler.PauseTiming();
				PrevSource.CopyPixels(new PixelArea(0, y, PrevSource.Width, 1), srcBuff.Stride, srcBuff.Stride, (IntPtr)pb);
				Profiler.ResumeTiming();

				if (orient == Orientation.Rotate180)
					flipLine(pb, PrevSource.Width * bytesPerPixel);

				pb -= srcBuff.Stride;
			}
		}

		unsafe private void loadBufferTransposed(byte* bstart)
		{
			byte* bp = bstart;
			int colStride = srcBuff!.Stride;
			int rowStride = bytesPerPixel;

			if (orient == Orientation.Transverse || orient == Orientation.Rotate270)
			{
				bp += (PrevSource.Width - 1) * colStride;
				colStride = -colStride;
			}

			if (orient == Orientation.Transverse || orient == Orientation.Rotate90)
			{
				bp += (PrevSource.Height - 1) * rowStride;
				rowStride = -rowStride;
			}

			int cb = PrevSource.Width * bytesPerPixel;

			fixed (byte* lp = lineBuff.AsSpan())
			{
				for (int y = 0; y < PrevSource.Height; y++)
				{
					Profiler.PauseTiming();
					PrevSource.CopyPixels(new PixelArea(0, y, PrevSource.Width, 1), cb, cb, (IntPtr)lp);
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

						pe -= sizeof(ushort);
						pp += sizeof(ushort);
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

						pe -= sizeof(uint);
						pp += sizeof(uint);
					}
					break;
				case 16:
					while (pp < pe)
					{
						Vector4 t0 = Unsafe.ReadUnaligned<Vector4>(pe);

						Unsafe.WriteUnaligned(pe, Unsafe.ReadUnaligned<Vector4>(pp));
						Unsafe.WriteUnaligned(pp, t0);

						pe -= sizeof(Vector4);
						pp += sizeof(Vector4);
					}
					break;
			}
		}

		public void Dispose()
		{
			srcBuff?.Dispose();

			BufferPool.Return(lineBuff);
			lineBuff = default;
		}

		public override string ToString() => nameof(OrientationTransform);
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
