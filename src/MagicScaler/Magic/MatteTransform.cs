using System;
using System.Drawing;
using System.Numerics;
using System.Runtime.CompilerServices;

#if DRAWING_SHIM_COLOR
using System.Drawing.ColorShim;
#endif

using PhotoSauce.MagicScaler.Interop;
using static PhotoSauce.MagicScaler.MathUtil;

using VectorF = System.Numerics.Vector<float>;

namespace PhotoSauce.MagicScaler
{
	internal class MatteTransform : PixelSource
	{
		private readonly ushort maskB, maskG, maskR, maskA;
		private readonly uint maskValue32;
		private readonly ulong maskValue64;
		private readonly VectorF vmatte;
		private readonly Vector<int> vmask0, vmask1;

		unsafe public MatteTransform(PixelSource source, Color color) : base(source)
		{
			if (Format.ColorRepresentation != PixelColorRepresentation.Bgr || Format.AlphaRepresentation == PixelAlphaRepresentation.None)
				throw new NotSupportedException("Pixel format not supported.  Must be BGRA");

			var igtq = LookupTables.SrgbInverseGammaUQ15;
			var atq = LookupTables.AlphaUQ15;

			maskB = igtq[color.B];
			maskG = igtq[color.G];
			maskR = igtq[color.R];
			maskA = atq[color.A];

			maskValue32 = (uint)color.ToArgb();
			maskValue64 = ((ulong)maskA << 48) | ((ulong)UnFix15(maskR * maskA) << 32) | ((ulong)UnFix15(maskG * maskA) << 16) | (ushort)UnFix15(maskB * maskA);

			var igtf = LookupTables.SrgbInverseGammaFloat;
			var atf = LookupTables.AlphaFloat;

			float mr = igtf[color.R], mg = igtf[color.G], mb = igtf[color.B], maa = atf[color.A];

			int* m0 = stackalloc int[] { -1, -1, -1, -1, 0, 0, 0, 0 };
			int* m1 = stackalloc int[] { -1, -1, -1, 0, -1, -1, -1, 0 };
			float* mat = stackalloc float[] { mb, mg, mr, 1f, mb, mg, mr, 1f };

			vmask0 = Unsafe.Read<Vector<int>>(m0);
			vmask1 = Unsafe.Read<Vector<int>>(m1);
			vmatte = Unsafe.Read<VectorF>(mat) * new VectorF(maa);
		}

		unsafe protected override void CopyPixelsInternal(WICRect prc, uint cbStride, uint cbBufferSize, IntPtr pbBuffer)
		{
			Timer.Stop();
			Source.CopyPixels(prc, cbStride, cbBufferSize, pbBuffer);
			Timer.Start();

			if (Format == PixelFormat.Pbgra128BppLinearFloat)
				applyMatteLinearFloat(prc, (float*)pbBuffer, (int)(cbStride / sizeof(float)));
			else if (Format == PixelFormat.Pbgra64BppLinearUQ15)
				applyMatteLinear(prc, (ushort*)pbBuffer, (int)(cbStride / sizeof(ushort)));
			else if (Format.FormatGuid == Consts.GUID_WICPixelFormat32bppBGRA)
				applyMatteCompanded(prc, (byte*)pbBuffer, (int)cbStride);
			else
				throw new NotSupportedException("Pixel format not supported.");
		}

		unsafe private void applyMatteLinearFloat(WICRect prc, float* pixels, int stride)
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
					var vi = Unsafe.Read<VectorF>(ip);
					float ia0 = vi[3], ia1 = VectorF.Count == 8 ? vi[7] : ia0;

					if (ia0 == 0 && ia1 == 0)
					{
						Unsafe.Write(ip, vmat);
					}
					else if (ia0 < 1f || ia1 < 1f)
					{
						var vpa = new VectorF(ia0);
						if (VectorF.Count == 8)
							vpa = Vector.ConditionalSelect(vm0, vpa, new VectorF(ia1));

						var vma = vmat * (v1 - vpa);

						var vr = vi + vma;
						var vo = Vector.ConditionalSelect(vm1, vr, vma + vpa);
						Unsafe.Write(ip, vo);
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

		unsafe private void applyMatteLinear(WICRect prc, ushort* pixels, int stride)
		{
			const ushort maxalpha = UQ15One;

			for (int y = 0; y < prc.Height; y++)
			{
				ushort* ip = pixels + y * stride;
				ushort* ipe = ip + prc.Width * 4;

				while (ip < ipe)
				{
					int alpha = ip[3];
					if (alpha == 0)
					{
						*(ulong*)ip = maskValue64;
					}
					else if (alpha < maxalpha)
					{
						int ia = alpha, ma = UnFix15(maskA * (UQ15One - ia));
						int ib = ip[0];
						int ig = ip[1];
						int ir = ip[2];

						ib += UnFix15(maskB * ma);
						ig += UnFix15(maskG * ma);
						ir += UnFix15(maskR * ma);
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

		unsafe private void applyMatteCompanded(WICRect prc, byte* pixels, int stride)
		{
			const byte maxalpha = byte.MaxValue;

			fixed (ushort* igtstart = &LookupTables.SrgbInverseGammaUQ15[0], atstart = &LookupTables.AlphaUQ15[0])
			fixed (byte* gtstart = &LookupTables.SrgbGamma[0])
			{
				byte* gt = gtstart;
				ushort* igt = igtstart, at = atstart;

				for (int y = 0; y < prc.Height; y++)
				{
					byte* ip = pixels + y * stride;
					byte* ipe = ip + prc.Width * 4;

					while (ip < ipe)
					{
						int alpha = ip[3];
						if (alpha == 0)
						{
							*(uint*)ip = maskValue32;
						}
						else if (alpha < maxalpha)
						{
							int ia =  at[alpha];
							int ib = igt[ip[0]];
							int ig = igt[ip[1]];
							int ir = igt[ip[2]];

							int ma = UnFix15(maskA * (UQ15One - ia));
							ib = UnFix15(ib * ia + maskB * ma);
							ig = UnFix15(ig * ia + maskG * ma);
							ir = UnFix15(ir * ia + maskR * ma);
							ia += ma;

							int fa = (UQ15One << 15) / ia;
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
