using System;
using System.Drawing;
using System.Numerics;
using System.Runtime.CompilerServices;

using PhotoSauce.Interop.Wic;
using static PhotoSauce.MagicScaler.MathUtil;

using VectorF = System.Numerics.Vector<float>;

namespace PhotoSauce.MagicScaler
{
	internal class MatteTransform : PixelSource
	{
		private readonly uint matteB, matteG, matteR, matteA;
		private readonly uint matteValue32;
		private readonly ulong matteValue64;
		private readonly VectorF vmatte;
		private readonly Vector<int> vmask0, vmask1;

		unsafe public MatteTransform(PixelSource source, Color color) : base(source)
		{
			if (Format.ColorRepresentation != PixelColorRepresentation.Bgr || Format.AlphaRepresentation == PixelAlphaRepresentation.None)
				throw new NotSupportedException("Pixel format not supported.  Must be BGRA");

			if (Format == PixelFormat.Pbgra128BppLinearFloat && color.A == byte.MaxValue)
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

			int* m0 = stackalloc int[] { -1, -1, -1, -1, 0, 0, 0, 0 };
			int* m1 = stackalloc int[] { -1, -1, -1, 0, -1, -1, -1, 0 };
			float* mat = stackalloc float[] { mb, mg, mr, 1f, mb, mg, mr, 1f };

			vmask0 = Unsafe.ReadUnaligned<Vector<int>>(m0);
			vmask1 = Unsafe.ReadUnaligned<Vector<int>>(m1);
			vmatte = Unsafe.ReadUnaligned<VectorF>(mat) * new VectorF(maa);
		}

		unsafe protected override void CopyPixelsInternal(in PixelArea prc, int cbStride, int cbBufferSize, IntPtr pbBuffer)
		{
			Profiler.PauseTiming();
			Source.CopyPixels(prc, cbStride, cbBufferSize, pbBuffer);
			Profiler.ResumeTiming();

			if (Format == PixelFormat.Pbgra128BppLinearFloat || Format == PixelFormat.Bgrx128BppLinearFloat)
				applyMatteLinearFloat(prc, (float*)pbBuffer, cbStride / sizeof(float));
			else if (Format == PixelFormat.Pbgra64BppLinearUQ15)
				applyMatteLinear(prc, (ushort*)pbBuffer, cbStride / sizeof(ushort));
			else if (Format.FormatGuid == Consts.GUID_WICPixelFormat32bppBGRA)
				applyMatteCompanded(prc, (byte*)pbBuffer, cbStride);
			else
				throw new NotSupportedException("Pixel format not supported.");
		}

		unsafe private void applyMatteLinearFloat(in PixelArea prc, float* pixels, int stride)
		{
			var v1 = VectorF.One;
			var vm0 = vmask0;
			var vm1 = vmask1;
			var vmat = vmatte;
			float fb = vmat[0], fg = vmat[1], fr = vmat[2], fa = vmat[3];

			for (int y = 0; y < prc.Height; y++)
			{
				float* ip = pixels + y * stride;
				float* ipe = ip + prc.Width * 4 - VectorF.Count;

				while (ip <= ipe)
				{
					var vi = Unsafe.ReadUnaligned<VectorF>(ip);
					float ia0 = vi[3], ia1 = VectorF.Count == 8 ? vi[7] : ia0;

					if (ia0 == 0 && ia1 == 0)
					{
						Unsafe.WriteUnaligned(ip, vmat);
					}
					else if (ia0 < 1f || ia1 < 1f)
					{
						var vpa = new VectorF(ia0);
						if (VectorF.Count == 8)
							vpa = Vector.ConditionalSelect(vm0, vpa, new VectorF(ia1));

						var vma = vmat * (v1 - vpa);

						var vr = vi + vma;
						var vo = Vector.ConditionalSelect(vm1, vr, vma + vpa);
						Unsafe.WriteUnaligned(ip, vo);
					}

					ip += VectorF.Count;
				}

				ipe += VectorF.Count;
				while (ip < ipe)
				{
					float ib = ip[0], ig = ip[1], ir = ip[2], ia = ip[3], ma = fa * (1f - ia);

					ib += fb * ma;
					ig += fg * ma;
					ir += fr * ma;
					ia += ma;

					ip[0] = ib;
					ip[1] = ig;
					ip[2] = ir;
					ip[3] = ia;

					ip += 4;
				}
			}
		}

		unsafe private void applyMatteLinear(in PixelArea prc, ushort* pixels, int stride)
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

		unsafe private void applyMatteCompanded(in PixelArea prc, byte* pixels, int stride)
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
							uint ia = Fix15(alpha);
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
	}
}
