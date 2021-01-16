// Copyright © Clinton Ingram and Contributors.  Licensed under the MIT License.

using System;
using System.Drawing;
using System.Numerics;
using System.Runtime.CompilerServices;

#if HWINTRINSICS
using System.Runtime.Intrinsics;
using System.Runtime.Intrinsics.X86;
#endif

using static PhotoSauce.MagicScaler.MathUtil;

namespace PhotoSauce.MagicScaler.Transforms
{
	internal sealed class MatteTransform : ChainedPixelSource
	{
		private readonly uint matteB, matteG, matteR, matteA;
		private readonly uint matteValue32;
		private readonly ulong matteValue64;
		private readonly Vector4 vmatte;

		public override PixelFormat Format { get; }

		public MatteTransform(PixelSource source, Color color, bool allowFormatChange) : base(source)
		{
			Format = source.Format;

			if (Format.ColorRepresentation != PixelColorRepresentation.Bgr || Format.AlphaRepresentation == PixelAlphaRepresentation.None)
				throw new NotSupportedException("Pixel format not supported.  Must be BGRA");

			if (allowFormatChange && Format == PixelFormat.Pbgra128BppLinearFloat && !color.IsTransparent())
				Format = PixelFormat.Bgrx128BppLinearFloat;

			var igtq = LookupTables.SrgbInverseGammaUQ15;

			matteB =  igtq[color.B];
			matteG =  igtq[color.G];
			matteR =  igtq[color.R];
			matteA = Fix15(color.A);

			matteValue32 = (uint)color.ToArgb();
			matteValue64 = ((ulong)matteA << 48) | ((ulong)UnFix15(matteR * matteA) << 32) | ((ulong)UnFix15(matteG * matteA) << 16) | UnFix15(matteB * matteA);

			var igtf = LookupTables.SrgbInverseGamma;
			var atf = LookupTables.Alpha;

			float mr = igtf[color.R], mg = igtf[color.G], mb = igtf[color.B], maa = atf[color.A];
			vmatte = new Vector4(mb, mg, mr, 1f) * new Vector4(maa);
		}

		protected override unsafe void CopyPixelsInternal(in PixelArea prc, int cbStride, int cbBufferSize, IntPtr pbBuffer)
		{
			Profiler.PauseTiming();
			PrevSource.CopyPixels(prc, cbStride, cbBufferSize, pbBuffer);
			Profiler.ResumeTiming();

			if (Format == PixelFormat.Pbgra128BppLinearFloat || Format == PixelFormat.Bgrx128BppLinearFloat)
#if HWINTRINSICS
				if (Avx.IsSupported)
					applyMatteLinearAvx(prc, (float*)pbBuffer, cbStride / sizeof(float));
				else
#endif
					applyMatteLinearFloat(prc, (float*)pbBuffer, cbStride / sizeof(float));
			else if (Format == PixelFormat.Pbgra64BppLinearUQ15)
				applyMatteLinear(prc, (ushort*)pbBuffer, cbStride / sizeof(ushort));
			else if (Format == PixelFormat.Bgra32Bpp)
				applyMatteCompanded(prc, (byte*)pbBuffer, cbStride);
			else
				throw new NotSupportedException("Pixel format not supported.");
		}

#if HWINTRINSICS
		[MethodImpl(MethodImplOptions.AggressiveOptimization)]
		private unsafe void applyMatteLinearAvx(in PixelArea prc, float* pixels, int stride)
		{
			var vmt = vmatte;
			var vmat = Avx.BroadcastVector128ToVector256((float*)&vmt);
			var vone = Vector256.Create(1f);

			for (int y = 0; y < prc.Height; y++)
			{
				float* ip = pixels + y * stride;
				float* ipe = ip + prc.Width * 4;

				ipe -= Vector256<float>.Count;
				while (ip <= ipe)
				{
					var vi = Avx.LoadVector256(ip);
					var va = Avx.Shuffle(vi, vi, HWIntrinsics.ShuffleMaskAlpha);

					va = Avx.Subtract(vone, va);
					vi = HWIntrinsics.MultiplyAdd(vi, vmat, va);

					Avx.Store(ip, vi);
					ip += Vector256<float>.Count;
				}
				ipe += Vector256<float>.Count;

				while (ip < ipe)
				{
					var vi = Sse.LoadVector128(ip);
					var va = Sse.Shuffle(vi, vi, HWIntrinsics.ShuffleMaskAlpha);

					va = Sse.Subtract(vone.GetLower(), va);
					vi = HWIntrinsics.MultiplyAdd(vi, vmat.GetLower(), va);

					Sse.Store(ip, vi);
					ip += Vector128<float>.Count;
				}
			}
		}
#endif

		private unsafe void applyMatteLinearFloat(in PixelArea prc, float* pixels, int stride)
		{
			var vmat = vmatte;
			var vone = Vector4.One;

			for (int y = 0; y < prc.Height; y++)
			{
				float* ip = pixels + y * stride;
				float* ipe = ip + prc.Width * 4;

				while (ip < ipe)
				{
					var vi = Unsafe.ReadUnaligned<Vector4>(ip);
					var va = vone - new Vector4(vi.W);

					vi += vmat * va;

					Unsafe.WriteUnaligned(ip, vi);
					ip += 4;
				}
			}
		}

		private unsafe void applyMatteLinear(in PixelArea prc, ushort* pixels, int stride)
		{
			const ushort maxalpha = UQ15One;

			for (int y = 0; y < prc.Height; y++)
			{
				ushort* ip = pixels + y * stride;
				ushort* ipe = ip + prc.Width * 4;

				while (ip < ipe)
				{
					uint alpha = ip[3];
					if (alpha == 0)
					{
						*(ulong*)ip = matteValue64;
					}
					else if (alpha < maxalpha)
					{
						uint ia = alpha, ma = UnFix15(matteA * (UQ15One - ia));
						uint ib = ip[0];
						uint ig = ip[1];
						uint ir = ip[2];

						ib += UnFix15(matteB * ma);
						ig += UnFix15(matteG * ma);
						ir += UnFix15(matteR * ma);
						ia += ma;

						ip[0] = ClampToUQ15(ib);
						ip[1] = ClampToUQ15(ig);
						ip[2] = ClampToUQ15(ir);
						ip[3] = ClampToUQ15(ia);
					}
					ip += 4;
				}
			}
		}

		private unsafe void applyMatteCompanded(in PixelArea prc, byte* pixels, int stride)
		{
			const uint maxalpha = byte.MaxValue;

			fixed (ushort* igtstart = &LookupTables.SrgbInverseGammaUQ15[0])
			fixed (byte* gtstart = &LookupTables.SrgbGammaUQ15[0])
			{
				byte* gt = gtstart;
				ushort* igt = igtstart;

				for (int y = 0; y < prc.Height; y++)
				{
					byte* ip = pixels + y * stride;
					byte* ipe = ip + prc.Width * 4;

					while (ip < ipe)
					{
						byte alpha = ip[3];
						if (alpha == 0)
						{
							*(uint*)ip = matteValue32;
						}
						else if (alpha < maxalpha)
						{
							uint ia = FastFix15(alpha);
							uint ib = igt[(uint)ip[0]];
							uint ig = igt[(uint)ip[1]];
							uint ir = igt[(uint)ip[2]];

							uint ma = UnFix15(matteA * (UQ15One - ia));
							ib = UnFix15(ib * ia + matteB * ma);
							ig = UnFix15(ig * ia + matteG * ma);
							ir = UnFix15(ir * ia + matteR * ma);
							ia += ma;

							uint fa = UQ15One * UQ15One / ia;
							ib = UnFix15(ib * fa);
							ig = UnFix15(ig * fa);
							ir = UnFix15(ir * fa);

							ib = ClampToUQ15One(ib);
							ig = ClampToUQ15One(ig);
							ir = ClampToUQ15One(ir);

							ip[0] = gt[ib];
							ip[1] = gt[ig];
							ip[2] = gt[ir];
							ip[3] = UnFix15ToByte(ia * maxalpha);
						}
						ip += 4;
					}
				}
			}
		}

		public override string ToString() => nameof(MatteTransform);
	}
}
